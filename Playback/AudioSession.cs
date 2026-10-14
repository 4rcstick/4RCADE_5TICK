using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ArcadeStick.Native;
using static ArcadeStick.Native.FFmpegApi;
using static ArcadeStick.Native.FFmpegLayout;

namespace ArcadeStick.Playback;

internal sealed record AudioInfo(string Codec, int SampleRate, int Channels);

/// <summary>Independent local-file demux/decode avoids cross-stream backpressure deadlocks.
/// The device receives a continuous timestamp-aligned PCM timeline across loops.</summary>
internal sealed class AudioSession
{
    private readonly Task worker;
    private static int activeWorkers;
    public static int ActiveWorkers => Volatile.Read(ref activeWorkers);
    private readonly double startSeconds;
    private WasapiOutput? output;
    private AudioInfo? info;
    private long decodedSamples, timestampFallbacks, paddedSamples, trimmedSamples;
    public AudioInfo? Info => Volatile.Read(ref info);
    public AudioOutputState? State
    {
        get { var state = Volatile.Read(ref output)?.State; return state is null ? null : state with { Clock = state.Clock + startSeconds }; }
    }
    public long DecodedSamples => Interlocked.Read(ref decodedSamples);
    public long TimestampFallbacks => Interlocked.Read(ref timestampFallbacks);
    public long PaddedSamples => Interlocked.Read(ref paddedSamples);
    public long TrimmedSamples => Interlocked.Read(ref trimmedSamples);
    public bool TryStart() => Volatile.Read(ref output)?.TryStart() == true;
    public void Join() => worker.GetAwaiter().GetResult();

    public AudioSession(FFmpegApi api, string path, int selectedStream, double origin, double duration, AudioSettings settings,
        CancellationToken token, Action<Exception> fail, double startSeconds, Func<bool> paused)
    {
        this.startSeconds = startSeconds;
        worker = Task.Factory.StartNew(() =>
        {
            Interlocked.Increment(ref activeWorkers);
            try { Decode(api, path, selectedStream, origin, duration, settings, token, paused); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception) { fail(exception); }
            finally { Interlocked.Decrement(ref activeWorkers); }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int Interrupt(void* opaque) => Volatile.Read(ref *(int*)opaque);

    private unsafe void Decode(FFmpegApi api, string path, int selectedStream, double origin, double duration,
        AudioSettings settings, CancellationToken token, Func<bool> paused)
    {
        nint format = 0, codec = 0, packet = 0, frame = 0, resampler = 0, options = 0;
        nint flag = (nint)NativeMemory.AllocZeroed(sizeof(int));
        if (flag == 0) throw new OutOfMemoryException();
        using var registration = token.Register(() => Marshal.WriteInt32(flag, 1));
        WasapiOutput? sink = null;
        try
        {
            format = Require(api.AllocFormat());
            Write(format, AVFormatContext_interrupt_callback + AVIOInterruptCB_callback,
                (nint)(delegate* unmanaged[Cdecl]<void*, int>)&Interrupt);
            Write(format, AVFormatContext_interrupt_callback + AVIOInterruptCB_opaque, flag);
            fixed (byte* key = "protocol_whitelist\0"u8)
            fixed (byte* value = "file\0"u8)
                api.Check(api.SetDictionary(&options, key, value, 0), "Restrict audio to local files");
            nint opened = format;
            int result;
            fixed (byte* name = Encoding.UTF8.GetBytes(path + '\0')) result = api.OpenInput(&opened, name, 0, &options);
            format = opened;
            api.Check(result, "Open audio input");
            api.FreeDictionary(&options);
            api.Check(api.FindStreamInfo(format, 0), "Read audio stream information");
            nint decoder = 0;
            int index = api.FindBestStream(format, AudioMediaType, selectedStream, -1, &decoder, 0);
            api.Check(index, "Find audio decoder");
            nint stream = ((nint*)Read<nint>(format, AVFormatContext_streams))[index];
            nint parameters = Read<nint>(stream, AVStream_codecpar);
            var timeBase = Read<Rational>(stream, AVStream_time_base);
            if (timeBase.Value <= 0) throw new InvalidDataException("Invalid audio time base.");
            long start = Read<long>(stream, AVStream_start_time);
            if (start == long.MinValue) start = (long)Math.Round(origin / timeBase.Value);
            string codecName = Text(api.CodecName(Read<int>(parameters, AVCodecParameters_codec_id)));
            codec = Require(api.AllocCodec(decoder));
            api.Check(api.CopyParameters(codec, parameters), "Copy audio parameters");
            Write(codec, AVCodecContext_pkt_timebase, timeBase);
            api.Check(api.OpenCodec(codec, decoder, 0), "Open audio decoder");
            packet = Require(api.AllocPacket());
            frame = Require(api.AllocFrame());
            if (startSeconds > 0)
            {
                long seek = (long)Math.Floor((origin + Math.Max(0, startSeconds - 0.25)) / timeBase.Value);
                api.Check(api.SeekFrame(format, index, seek, SeekBackward), "Seek audio");
                api.FlushCodec(codec);
            }
            sink = new WasapiOutput(settings, paused);
            Volatile.Write(ref output, sink);
            long loopSamples = checked((long)Math.Round(duration * WasapiOutput.Rate));
            long cursor = (long)Math.Round(startSeconds * WasapiOutput.Rate), loop = 0;
            int inputRate = 0, inputFormat = 0, inputChannels = 0;
            long framesInLoop = 0;
            double nextTimestamp = 0;
            short[] silence = new short[WasapiOutput.BlockFrames * 2];
            while (true)
            {
                token.ThrowIfCancellationRequested();
                result = api.ReadFrame(format, packet);
                if (result == EndOfFile)
                {
                    Send(0);
                    if (framesInLoop == 0) throw new InvalidDataException("Audio stream produced no samples.");
                    while (resampler != 0 && Convert(0, 0, cursor) > 0) { }
                    Pad((loop + 1) * loopSamples);
                    api.Check(api.SeekFrame(format, index, start, SeekBackward), "Rewind audio");
                    api.FlushCodec(codec);
                    nint old = resampler;
                    api.FreeResampler(&old);
                    resampler = 0;
                    loop++;
                    framesInLoop = 0;
                    nextTimestamp = 0;
                    continue;
                }
                api.Check(result, "Read audio packet");
                try { if (Read<int>(packet, AVPacket_stream_index) == index) Send(packet); }
                finally { api.UnrefPacket(packet); }
            }

            void Send(nint input)
            {
                int sent = api.SendPacket(codec, input);
                if (sent == Again) { Drain(); sent = api.SendPacket(codec, input); }
                api.Check(sent, "Send audio packet");
                Drain();
            }
            void Drain()
            {
                byte* layout = stackalloc byte[ChannelLayoutSize];
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    int received = api.ReceiveFrame(codec, frame);
                    if (received is Again or EndOfFile) return;
                    api.Check(received, "Decode audio frame");
                    try
                    {
                        int rate = Read<int>(frame, AVFrame_sample_rate);
                        int sampleFormat = Read<int>(frame, AVFrame_format);
                        int channels = Read<int>(frame, AVFrame_ch_layout + AVChannelLayout_nb_channels);
                        int count = Read<int>(frame, AVFrame_nb_samples);
                        if (rate <= 0 || channels <= 0 || count <= 0) throw new InvalidDataException("Invalid decoded audio format.");
                        if (resampler != 0 && (rate != inputRate || sampleFormat != inputFormat || channels != inputChannels))
                            throw new NotSupportedException("Midstream audio format changes require reopening the source.");
                        if (resampler == 0)
                        {

                            new Span<byte>(layout, ChannelLayoutSize).Clear();
                            api.DefaultChannelLayout((nint)layout, 2);
                            try
                            {
                                nint allocated = 0;
                                api.Check(api.AllocateResampler(&allocated, (nint)layout, Signed16, WasapiOutput.Rate,
                                    frame + AVFrame_ch_layout, sampleFormat, rate, 0, 0), "Allocate PCM resampler");
                                resampler = allocated;
                                api.Check(api.InitResampler(resampler), "Initialize PCM resampler");
                            }
                            finally { api.UninitChannelLayout((nint)layout); }
                            inputRate = rate; inputFormat = sampleFormat; inputChannels = channels;
                            Volatile.Write(ref info, new AudioInfo(codecName, rate, channels));
                        }
                        long pts = Read<long>(frame, AVFrame_best_effort_timestamp);
                        double timestamp = pts == long.MinValue ? nextTimestamp : pts * timeBase.Value - origin;
                        if (pts == long.MinValue) Interlocked.Increment(ref timestampFallbacks);
                        long target = loop * loopSamples + (long)Math.Round(timestamp * WasapiOutput.Rate)
                            - api.AudioDelay(resampler, WasapiOutput.Rate);
                        Convert(Read<nint>(frame, AVFrame_extended_data), count, target);
                        nextTimestamp = timestamp + count / (double)rate;
                        Interlocked.Add(ref decodedSamples, count);
                        framesInLoop++;
                    }
                    finally { api.UnrefFrame(frame); }
                }
            }
            int Convert(nint input, int count, long target)
            {
                int capacity = api.AudioCapacity(resampler, count);
                api.Check(capacity, "Size resampled PCM");
                if (capacity > WasapiOutput.Rate * 10) throw new InvalidDataException("Unreasonably large audio frame.");
                short[] pcm = new short[Math.Max(1, capacity) * 2];
                int samples;
                fixed (short* buffer = pcm)
                {
                    byte* plane = (byte*)buffer;
                    samples = api.ConvertAudio(resampler, &plane, capacity, (byte**)input, count);
                }
                api.Check(samples, "Resample PCM audio");
                long end = (loop + 1) * loopSamples;
                // Coarse container time bases (e.g. Matroska milliseconds) round audio PTS.
                // Preserve sample continuity within one input timestamp tick.
                if (Math.Abs(target - cursor) <= Math.Max(2, Math.Ceiling(timeBase.Value * WasapiOutput.Rate) + 2)) target = cursor;
                Pad(Math.Min(end, Math.Max(cursor, target)));
                int skip = (int)Math.Min(samples, Math.Max(0, cursor - target));
                int take = (int)Math.Min(samples - skip, Math.Max(0, end - cursor));
                if (take > 0) { sink.Write(pcm.AsSpan(skip * 2, take * 2), token); cursor += take; }
                Interlocked.Add(ref trimmedSamples, samples - take);
                return samples;
            }
            void Pad(long target)
            {
                if (target - cursor > WasapiOutput.Rate * 3600L) throw new InvalidDataException("Audio timestamp gap exceeds one hour.");
                while (cursor < target)
                {
                    int count = (int)Math.Min(WasapiOutput.BlockFrames, target - cursor);
                    sink.Write(silence.AsSpan(0, count * 2), token);
                    cursor += count;
                    Interlocked.Add(ref paddedSamples, count);
                }
            }
        }
        finally
        {
            // Free decode resources even if an audio driver reports a shutdown failure.
            try { sink?.Dispose(); }
            finally
            {
                nint releaseResampler = resampler, releaseFrame = frame, releaseCodec = codec, releaseFormat = format;
                if (releaseResampler != 0) api.FreeResampler(&releaseResampler);
                if (releaseFrame != 0) api.FreeFrame(&releaseFrame);
                if (packet != 0) api.FreePacket(&packet);
                if (releaseCodec != 0) api.FreeCodec(&releaseCodec);
                if (releaseFormat != 0) api.CloseInput(&releaseFormat);
                if (options != 0) api.FreeDictionary(&options);
                registration.Dispose();
                NativeMemory.Free((void*)flag);
            }
        }
    }
    private static nint Require(nint value) => value != 0 ? value : throw new OutOfMemoryException("FFmpeg audio allocation failed.");
}
