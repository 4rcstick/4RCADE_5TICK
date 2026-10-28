using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using ArcadeStick.Native;
using static ArcadeStick.Native.FFmpegApi;
using static ArcadeStick.Native.FFmpegLayout;

namespace ArcadeStick.Playback;

/// <summary>One worker owns every native decode resource. Only pooled BGRA frames cross to WPF.</summary>
internal sealed class VideoSession
{
    private sealed class SeekPrerollException : Exception;
    private long seekFallbacks;
    public long SeekFallbacks => Interlocked.Read(ref seekFallbacks);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Channel<VideoFrame> frames = Channel.CreateBounded<VideoFrame>(new BoundedChannelOptions(4)
    { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly FFmpegApi api;
    private readonly double startSeconds;
    private int pauseRequested;
    public bool Paused { get => Volatile.Read(ref pauseRequested) != 0; set => Volatile.Write(ref pauseRequested, value ? 1 : 0); }
    private readonly Task worker;
    private static int activeWorkers;
    public static int ActiveWorkers => Volatile.Read(ref activeWorkers);
    private VideoInfo? info;
    private string? error;
    private long decodedFrames;
    private long timestampFallbacks;
    private long skippedLateFrames;
    public long SkippedLateFrames => Interlocked.Read(ref skippedLateFrames);
    private Task? stopTask;
    private AudioSession? audio;
    public AudioSession? Audio => Volatile.Read(ref audio);
    public bool HasAudio { get; private set; }
    public double LoopDuration { get; private set; }
    public void Fail(Exception exception)
    {
        Volatile.Write(ref error, $"{System.IO.Path.GetFileName(Path)}: {exception.Message}");
        cancellation.Cancel();
    }

    public string Path { get; }
    public VideoInfo? Info => Volatile.Read(ref info);
    public string? Error => Volatile.Read(ref error);
    public long DecodedFrames => Interlocked.Read(ref decodedFrames);
    public long TimestampFallbacks => Interlocked.Read(ref timestampFallbacks);
    public bool IsCompleted => worker.IsCompleted;
    public bool TryRead(out VideoFrame? frame) => frames.Reader.TryRead(out frame);

    private readonly Func<(int Width, int Height)> targetSize;
    private readonly bool playAudio;

    // playAudio false never opens an AudioSession or WASAPI: the presenter's stopwatch is the clock, as for files with no audio stream.
    public VideoSession(FFmpegApi api, string path, AudioSettings settings, Func<(int Width, int Height)> targetSize, double startSeconds = 0, bool paused = false, bool playAudio = false)
    {
        this.api = api;
        this.targetSize = targetSize;
        this.startSeconds = startSeconds;
        this.playAudio = playAudio;
        Paused = paused;
        Path = System.IO.Path.GetFullPath(path);
        worker = Task.Factory.StartNew(() =>
        {
            Interlocked.Increment(ref activeWorkers);
            try { Decode(settings, cancellation.Token); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception exception)
            {
                Volatile.Write(ref error, $"{System.IO.Path.GetFileName(Path)}: {exception.Message}");
            }
            finally { frames.Writer.TryComplete(); Interlocked.Decrement(ref activeWorkers); }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    // Called only by the UI owner. Cancellation also interrupts native I/O and a full frame queue.
    public Task StopAsync() => stopTask ??= StopCoreAsync();
    private async Task StopCoreAsync()
    {
        cancellation.Cancel();
        await worker.ConfigureAwait(false);
        while (frames.Reader.TryRead(out VideoFrame? frame)) frame.Dispose();
        cancellation.Dispose();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int Interrupt(void* opaque) => Volatile.Read(ref *(int*)opaque);

    private unsafe void Decode(AudioSettings settings, CancellationToken token)
    {
        if (!File.Exists(Path)) throw new FileNotFoundException("Local video file does not exist.", Path);
        VideoFrame? seekPreview = null;
        nint format = 0, codec = 0, packet = 0, frame = 0, scaler = 0, options = 0;
        nint interruptFlag = (nint)NativeMemory.AllocZeroed(sizeof(int));
        if (interruptFlag == 0) throw new OutOfMemoryException();
        using CancellationTokenRegistration registration = token.Register(() => Marshal.WriteInt32(interruptFlag, 1));
        try
        {
            format = Require(api.AllocFormat(), "format context");
            Write(format, AVFormatContext_interrupt_callback + AVIOInterruptCB_callback,
                (nint)(delegate* unmanaged[Cdecl]<void*, int>)&Interrupt);
            Write(format, AVFormatContext_interrupt_callback + AVIOInterruptCB_opaque, interruptFlag);
            byte[] filename = Encoding.UTF8.GetBytes(Path + '\0');
            fixed (byte* key = "protocol_whitelist\0"u8)
            fixed (byte* value = "file\0"u8)
                api.Check(api.SetDictionary(&options, key, value, 0), "Restrict input to local files");
            nint openedFormat = format;
            int opened;
            fixed (byte* path = filename)
                opened = api.OpenInput(&openedFormat, path, 0, &options);
            format = openedFormat; // FFmpeg can replace or free this pointer even on failure.
            api.Check(opened, "Open input");
            api.FreeDictionary(&options);
            api.Check(api.FindStreamInfo(format, 0), "Read stream information");
            token.ThrowIfCancellationRequested();
            nint decoder = 0;
            int streamIndex = api.FindBestStream(format, VideoMediaType, -1, -1, &decoder, 0);
            api.Check(streamIndex, "Find video stream/decoder");
            nint stream = ((nint*)Read<nint>(format, AVFormatContext_streams))[streamIndex];
            nint parameters = Read<nint>(stream, AVStream_codecpar);
            Rational timeBase = Read<Rational>(stream, AVStream_time_base);
            if (timeBase.Value <= 0) throw new InvalidDataException("Video stream has an invalid time base.");
            double fps = api.GuessFrameRate(format, stream, 0).Value;
            double fallbackDuration = fps is > 0 and <= 1000 ? 1 / fps : 1.0 / 30;
            long start = Read<long>(stream, AVStream_start_time);
            if (start == long.MinValue) start = 0;
            long streamDuration = Read<long>(stream, AVStream_duration);
            double duration = streamDuration > 0 ? streamDuration * timeBase.Value :
                Math.Max(0, Read<long>(format, AVFormatContext_duration) / 1_000_000.0);
            nint audioDecoder = 0;
            int audioIndex = playAudio ? api.FindBestStream(format, AudioMediaType, -1, streamIndex, &audioDecoder, 0) : StreamNotFound;
            if (audioIndex < 0 && audioIndex != StreamNotFound) api.Check(audioIndex, "Find audio stream/decoder");
            HasAudio = audioIndex >= 0;
            long formatStart = Read<long>(format, AVFormatContext_start_time);
            double mediaOrigin = formatStart != long.MinValue ? formatStart / 1_000_000.0 : start * timeBase.Value;
            if (HasAudio)
            {
                duration = Read<long>(format, AVFormatContext_duration) / 1_000_000.0;
                if (!double.IsFinite(duration) || duration <= 0)
                    throw new NotSupportedException("Synchronized looping requires a known local-file duration.");
                duration = Math.Round(duration * WasapiOutput.Rate) / WasapiOutput.Rate;
                LoopDuration = duration;
                Volatile.Write(ref audio, new AudioSession(api, Path, audioIndex, mediaOrigin, duration, settings, token, Fail, startSeconds, () => Paused));
            }
            string codecName = Text(api.CodecName(Read<int>(parameters, AVCodecParameters_codec_id)));
            codec = Require(api.AllocCodec(decoder), "codec context");
            api.Check(api.CopyParameters(codec, parameters), "Copy codec parameters");
            Write(codec, AVCodecContext_pkt_timebase, timeBase);
            api.Check(api.OpenCodec(codec, decoder, 0), "Open video decoder");
            packet = Require(api.AllocPacket(), "packet");
            frame = Require(api.AllocFrame(), "frame");

            if (startSeconds > 0)
            {
                api.Check(api.SeekFrame(format, streamIndex,
                    (long)Math.Floor((mediaOrigin + startSeconds) / timeBase.Value), SeekBackward), "Seek video");
                api.FlushCodec(codec);
            }
            int loop = 0;
            bool seekPending = startSeconds > 0;
            bool linearPreroll = false;
            double loopOffset = 0;
            double previousTime = -1, previousDuration = fallbackDuration;

            int framesInLoop = 0;
            (int, int, int, int, int, int, int) scalerKey = default;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int result = api.ReadFrame(format, packet);
                if (result == EndOfFile)
                {
                    SendAndDrain(0); // Drain delayed/reordered frames before rewinding.
                    if (seekPreview is not null)
                    {
                        frames.Writer.WriteAsync(seekPreview, token).AsTask().GetAwaiter().GetResult();
                        seekPreview = null;
                        seekPending = false;
                    }
                    if (framesInLoop == 0) throw new InvalidDataException("The file produced no video frames.");
                    loopOffset += HasAudio ? duration : previousTime + previousDuration;
                    api.Check(api.SeekFrame(format, streamIndex, start, SeekBackward), "Rewind for looping");
                    api.FlushCodec(codec);
                    previousTime = -1;
                    previousDuration = fallbackDuration;
                    framesInLoop = 0;
                    loop++;
                    continue;
                }
                api.Check(result, "Read packet");
                try
                {
                    if (Read<int>(packet, AVPacket_stream_index) == streamIndex)
                        SendAndDrain(packet);
                    // The independent audio worker demuxes its own stream, avoiding cross-stream queue deadlock.
                }
                catch (SeekPrerollException)
                {
                    // Some containers mark non-random-access H.264 packets as keyframes.
                    // Never display concealed preroll: retry from the known initial decoder state.
                    api.Check(api.SeekFrame(format, streamIndex, start, SeekBackward), "Retry seek from beginning");
                    api.FlushCodec(codec);
                    seekPreview?.Dispose();
                    seekPreview = null;
                    previousTime = -1;
                    previousDuration = fallbackDuration;
                    framesInLoop = 0;
                    linearPreroll = true;
                    Interlocked.Increment(ref seekFallbacks);
                }
                finally { api.UnrefPacket(packet); }
            }

            void SendAndDrain(nint input)
            {
                int sent = api.SendPacket(codec, input);
                if (sent == Again)
                {
                    Drain();
                    sent = api.SendPacket(codec, input); // Retry the same packet; never discard on EAGAIN.
                }
                api.Check(sent, "Send video packet");
                Drain();
            }

            void Drain()
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    int received = api.ReceiveFrame(codec, frame);
                    if (received is Again or EndOfFile) return;
                    api.Check(received, "Decode video frame");
                    try { PublishFrame(); }
                    finally { api.UnrefFrame(frame); }
                }
            }

            void PublishFrame()
            {
                if (seekPending && (Read<int>(frame, AVFrame_decode_error_flags) != 0 ||
                    (Read<int>(frame, AVFrame_flags) & CorruptFrame) != 0))
                {
                    if (!linearPreroll) throw new SeekPrerollException();
                    throw new InvalidDataException("Video seek preroll remains corrupt when decoded from the beginning.");
                }
                long timestamp = Read<long>(frame, AVFrame_best_effort_timestamp);
                double sourceTime = timestamp != long.MinValue
                    ? timestamp * timeBase.Value - mediaOrigin : previousTime + previousDuration;
                if (HasAudio && (sourceTime < -0.001 || sourceTime >= duration)) return;
                if (sourceTime < 0) sourceTime = 0;
                if (timestamp == long.MinValue || sourceTime <= previousTime)
                {
                    sourceTime = previousTime < 0 ? 0 : previousTime + previousDuration;
                    Interlocked.Increment(ref timestampFallbacks);
                }
                long frameDuration = Read<long>(frame, AVFrame_duration);
                double seconds = frameDuration > 0 ? frameDuration * timeBase.Value : fallbackDuration;
                // Audio continues when WPF is occluded or stalls. Drop expired decoded frames
                // before conversion so a bounded video queue cannot accumulate permanent A/V lag.
                if (Audio?.State is { Started: true } audioClock && loopOffset + sourceTime + seconds < audioClock.Clock)
                {
                    previousTime = sourceTime;
                    previousDuration = seconds;
                    framesInLoop++;
                    Interlocked.Increment(ref skippedLateFrames);
                    Interlocked.Increment(ref decodedFrames);
                    return;
                }
                int width = Read<int>(frame, AVFrame_width), height = Read<int>(frame, AVFrame_height);
                int pixelFormat = Read<int>(frame, AVFrame_format);
                if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
                    throw new InvalidDataException($"Unsupported frame dimensions: {width}×{height}.");
                double sar = api.GuessAspectRatio(format, stream, frame).Value;
                if (!double.IsFinite(sar) || sar <= 0) sar = 1;
                double aspect = width * sar / height;

                var (requestedWidth, requestedHeight) = targetSize();
                int boxWidth = Math.Clamp(requestedWidth, 16, 4096);
                int boxHeight = Math.Clamp(requestedHeight, 16, 4096);
                double boxAspect = (double)boxWidth / boxHeight;
                int outputWidth = aspect >= boxAspect ? boxWidth : Math.Max(1, (int)Math.Round(boxHeight * aspect));
                int outputHeight = aspect >= boxAspect ? Math.Max(1, (int)Math.Round(boxWidth / aspect)) : boxHeight;

                int range = Read<int>(frame, AVFrame_color_range);
                int colorSpace = Read<int>(frame, AVFrame_colorspace);
                var key = (width, height, pixelFormat, outputWidth, outputHeight, range, colorSpace);
                if (scaler == 0 || scalerKey != key)
                {
                    if (scaler != 0) { api.FreeScaler(scaler); scaler = 0; }
                    scaler = Require(api.CreateScaler(width, height, pixelFormat, outputWidth, outputHeight,
                    Bgra, Bicubic, 0, 0, 0), "BGRA conversion context");
                    int matrix = colorSpace switch { 1 => 1, 4 => 4, 7 => 7, 9 or 10 => 9, _ => 5 };
                    int* coefficients = api.ColorCoefficients(matrix);
                    api.Check(api.SetColorspace(scaler, coefficients, range == FullRange ? 1 : 0,
                        coefficients, 1, 0, 1 << 16, 1 << 16), "Set video color conversion");
                    scalerKey = key;
                    Volatile.Write(ref info, new VideoInfo(Path, codecName, Text(api.PixelFormatName(pixelFormat)),
                        width, height, fps, duration, sar));
                }
                var output = new VideoFrame(outputWidth, outputHeight, loopOffset + (seekPending ? Math.Max(startSeconds, sourceTime) : sourceTime), sourceTime, seconds, loop);
                try
                {
                    fixed (byte* pixels = output.Pixels)
                    {
                        byte** planes = stackalloc byte*[4] { pixels, null, null, null };
                        int* strides = stackalloc int[4] { output.Stride, 0, 0, 0 };
                        int rows = api.Scale(scaler, (byte**)((byte*)frame + AVFrame_data),
                            (int*)((byte*)frame + AVFrame_linesize), 0, height, planes, strides);
                        api.Check(rows, "Convert video frame");
                        if (rows != outputHeight) throw new InvalidDataException("Incomplete BGRA frame conversion.");
                    }
                    if (seekPending && sourceTime < startSeconds)
                    {
                        seekPreview?.Dispose();
                        seekPreview = output;
                    }
                    else
                    {
                        if (seekPreview is not null)
                        {
                            if (sourceTime <= startSeconds + 0.000001) seekPreview.Dispose();
                            else frames.Writer.WriteAsync(seekPreview, token).AsTask().GetAwaiter().GetResult();
                            seekPreview = null;
                        }
                        seekPending = false;
                        frames.Writer.WriteAsync(output, token).AsTask().GetAwaiter().GetResult();
                    }
                    Interlocked.Increment(ref decodedFrames);
                }
                catch { output.Dispose(); throw; }
                previousTime = sourceTime;
                previousDuration = seconds;
                framesInLoop++;
            }
        }
        finally
        {
            cancellation.Cancel();
            seekPreview?.Dispose();
            Audio?.Join();
            if (scaler != 0) api.FreeScaler(scaler);
            nint releaseFrame = frame, releaseCodec = codec, releaseFormat = format;
            if (releaseFrame != 0) api.FreeFrame(&releaseFrame);
            if (packet != 0) api.FreePacket(&packet);
            if (releaseCodec != 0) api.FreeCodec(&releaseCodec);
            if (releaseFormat != 0) api.CloseInput(&releaseFormat);
            if (options != 0) api.FreeDictionary(&options);
            registration.Dispose(); // Wait for any cancellation callback before releasing its native flag.
            NativeMemory.Free((void*)interruptFlag);
        }
    }

    private static nint Require(nint value, string resource) => value != 0 ? value :
        throw new OutOfMemoryException($"FFmpeg could not allocate {resource}.");
}
