using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ArcadeStick.Native;

namespace ArcadeStick.Playback;

internal enum PlayerState { Stopped, Opening, Playing, Pausing, Paused, Seeking, Stopping, Error, Closed }

internal sealed record PlayerStateChanged(PlayerState State, string? SourcePath, string? Error, bool IsReady);

/// <summary>UI-thread owner; presents timestamped frames on WPF's composition cadence.</summary>
internal sealed class VideoPresenter
{
    // Notifications are delivered on the owning Dispatcher after control locks release.
    public event EventHandler<PlayerStateChanged>? StateChanged;
    private readonly DispatcherTimer notifications;
    private PlayerStateChanged? lastNotification;
    private readonly FFmpegApi api;
    private readonly Image surface;
    private readonly SemaphoreSlim changes = new(1, 1);
    private readonly Stopwatch clock = new();
    private VideoSession? session;
    private VideoFrame? pending;
    private WriteableBitmap? bitmap;
    private Task stopping = Task.CompletedTask;
    private int requestVersion;
    private bool disposed;
    private bool desiredPaused, clockInitialized, playAudio;
    private VideoInfo? retainedInfo;
    private PlayerState state = PlayerState.Stopped;
    private double sessionStart;
    public PlayerState State => disposed ? PlayerState.Closed : Error is not null ? PlayerState.Error : state;
    public string? SourcePath { get; private set; }
    public double? SeekTarget { get; private set; }
    public double? ActualResumedPosition { get; private set; }
    public double Position => state == PlayerState.Stopped ? 0 :
        Duration > 0 ? Math.Clamp((AudioState?.Clock ?? (clockInitialized ? clockOrigin + clock.Elapsed.TotalSeconds : sessionStart)) % Duration, 0, Duration) : SourceSeconds;
    public double Duration => Info?.Duration ?? 0;
    private double clockOrigin;
    private TimeSpan lastRenderingTime = TimeSpan.MinValue;
    private double totalLateness;
    private double previousPresentedAt;

    private long targetSizePacked = ((long)640 << 32) | 480u;

    /// <summary>Decode box in pixels (the video layer's size). Thread-safe; the decoder reads it per frame and rebuilds its scaler when it changes.</summary>
    public void SetTargetSize(int width, int height)
    {
        if (width < 1 || height < 1) return;
        Volatile.Write(ref targetSizePacked, ((long)width << 32) | (uint)height);
    }

    private (int Width, int Height) ReadTargetSize()
    {
        long packed = Volatile.Read(ref targetSizePacked);
        return ((int)(packed >> 32), (int)(packed & 0xFFFFFFFF));
    }

    public AudioSettings AudioSettings { get; } = new();
    public AudioInfo? AudioInfo => session?.Audio?.Info;
    public AudioOutputState? AudioState => session?.Audio?.State;
    public long AudioDecodedSamples => session?.Audio?.DecodedSamples ?? 0;
    public long AudioTimestampFallbacks => session?.Audio?.TimestampFallbacks ?? 0;
    public long AudioPaddedSamples => session?.Audio?.PaddedSamples ?? 0;
    public long AudioTrimmedSamples => session?.Audio?.TrimmedSamples ?? 0;
    public double VideoTimestamp { get; private set; }
    public double AvOffsetMs => AudioState is { Started: true } state ? (VideoTimestamp - state.Clock) * 1000 : 0;
    public double LastPresentationOffsetMs { get; private set; }
    public double LoopDuration => session?.LoopDuration ?? 0;
    public VideoInfo? Info => session?.Info ?? retainedInfo;
    public string? Error => session?.Error;
    public long SeekFallbacks => session?.SeekFallbacks ?? 0;
    public long DecodedFrames => session?.DecodedFrames ?? 0;
    public long TimestampFallbacks => session?.TimestampFallbacks ?? 0;
    public long RenderedFrames { get; private set; }
    private long presentationDrops;
    public long DroppedFrames => presentationDrops + (session?.SkippedLateFrames ?? 0);
    public int CompletedLoops { get; private set; }
    public double SourceSeconds { get; private set; }
    public double ElapsedSeconds => clock.Elapsed.TotalSeconds;
    public double RenderedFps => ElapsedSeconds > 0 ? RenderedFrames / ElapsedSeconds : 0;
    public double MeanLatenessMs => RenderedFrames > 0 ? totalLateness / RenderedFrames : 0;
    public double MaxLatenessMs { get; private set; }
    public double MaxLoopGapMs { get; private set; }
    public long RenderingCallbacks { get; private set; }
    public long QueueEmptyCallbacks { get; private set; }
    public double RenderingCallbackFps => ElapsedSeconds > 0 ? RenderingCallbacks / ElapsedSeconds : 0;

    public VideoPresenter(FFmpegApi api, Image surface)
    {
        this.api = api;
        this.surface = surface;
        CompositionTarget.Rendering += Render;
        notifications = new DispatcherTimer(TimeSpan.FromMilliseconds(20), DispatcherPriority.Background,
            (_, _) => PublishState(), surface.Dispatcher);
    }

    // All public control methods are called on the WPF dispatcher. Replace operations are
    // serialized and superseded requests cannot publish an older source/seek afterwards.
    // withAudio opens the audio session (WASAPI, audio clock); off, the stopwatch clocks the video. Restart and seek keep the choice.
    public Task PlayAsync(string path, bool withAudio = false)
    {
        SourcePath = System.IO.Path.GetFullPath(path);
        playAudio = withAudio;
        desiredPaused = false;
        return ReplaceAsync(SourcePath, 0, false);
    }

    public Task PlayAsync() => State is PlayerState.Paused or PlayerState.Pausing || (desiredPaused && state == PlayerState.Seeking)
        ? ResumeAsync() : State is PlayerState.Playing or PlayerState.Opening or PlayerState.Seeking ? Task.CompletedTask : RestartAsync();

    public Task RestartAsync()
    {
        if (SourcePath is null || disposed) return Task.CompletedTask;
        desiredPaused = false;
        return ReplaceAsync(SourcePath, 0, true);
    }

    public Task SeekAsync(double seconds)
    {
        if (!double.IsFinite(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (SourcePath is null || disposed || Duration <= 0 || Info?.Path != SourcePath) return Task.CompletedTask;
        if (State == PlayerState.Stopped) desiredPaused = true;
        double target = Math.Round(Math.Clamp(seconds, 0, Math.Max(0, Duration - 1.0 / WasapiOutput.Rate)) * WasapiOutput.Rate) / WasapiOutput.Rate;
        return ReplaceAsync(SourcePath, target, true);
    }

    private async Task ReplaceAsync(string path, double target, bool seek)
    {
        int request = ++requestVersion;
        lastNotification = null; // A superseded queued event must not suppress the new request's notification.
        await changes.WaitAsync();
        try
        {
            if (disposed || request != requestVersion) return;
            state = seek ? PlayerState.Seeking : PlayerState.Opening;
            await StopCurrentAsync();
            if (disposed || request != requestVersion) return;
            if (retainedInfo?.Path != path) retainedInfo = null;
            RenderedFrames = presentationDrops = CompletedLoops = 0;
            RenderingCallbacks = QueueEmptyCallbacks = 0;
            SourceSeconds = totalLateness = MaxLatenessMs = MaxLoopGapMs = previousPresentedAt = 0;
            lastRenderingTime = TimeSpan.MinValue;
            VideoTimestamp = LastPresentationOffsetMs = 0;
            SeekTarget = seek ? target : null;
            ActualResumedPosition = null;
            sessionStart = target;
            session = new VideoSession(api, path, AudioSettings, ReadTargetSize, target, desiredPaused, playAudio);
        }
        finally { changes.Release(); PublishState(); }
    }

    public async Task PauseAsync()
    {
        if (disposed) return;
        desiredPaused = true;
        await changes.WaitAsync();
        try
        {
            if (session is null || disposed || Error is not null) return;
            session.Paused = true;
            state = PlayerState.Pausing;
            // Opening/paused-seek may not have an endpoint yet. Rendering acknowledges
            // that pause after prebuffer/preview; never block a source change on I/O.
            if (AudioState is not null)
            {
                var deadline = Stopwatch.StartNew();
                while (session.Audio?.State is { Paused: false } && Error is null)
                {
                    if (deadline.Elapsed.TotalSeconds > 3) { session.Fail(new TimeoutException("Audio pause was not acknowledged.")); break; }
                    await Task.Delay(5);
                }
            }
            clock.Stop();
            if (RenderedFrames > 0 && Error is null) state = PlayerState.Paused;
        }
        finally { changes.Release(); PublishState(); }
    }

    public async Task ResumeAsync()
    {
        if (disposed) return;
        desiredPaused = false;
        await changes.WaitAsync();
        try
        {
            if (session is null || disposed || Error is not null) return;
            session.Paused = false;
            state = PlayerState.Opening;
            surface.InvalidateVisual();
        }
        finally { changes.Release(); PublishState(); }
    }

    public async Task StopAsync()
    {
        ++requestVersion;
        await changes.WaitAsync();
        try
        {
            state = PlayerState.Stopping;
            await StopCurrentAsync();
            SourceSeconds = sessionStart = 0;
            state = PlayerState.Stopped;
        }
        finally { changes.Release(); PublishState(); }
    }

    private async Task StopCurrentAsync()
    {
        VideoSession? old = session;
        retainedInfo = session?.Info ?? retainedInfo;
        session = null;
        pending?.Dispose();
        pending = null;
        surface.Source = null;
        bitmap = null;
        clock.Reset();
        clockInitialized = false;
        if (old is not null)
        {
            stopping = old.StopAsync();
            await stopping;
        }
    }

    public async Task CloseAsync()
    {
        disposed = true;
        notifications.Stop();
        CompositionTarget.Rendering -= Render;
        await StopAsync();
    }

    // Application-exit fallback. Worker cleanup never needs the UI dispatcher.
    public void ShutdownNow()
    {
        disposed = true;
        notifications.Stop();
        ++requestVersion;
        CompositionTarget.Rendering -= Render;
        pending?.Dispose();
        pending = null;
        session?.StopAsync().GetAwaiter().GetResult();
        stopping.GetAwaiter().GetResult();
        session = null;
        PublishState();
    }

    private void PublishState()
    {
        var value = new PlayerStateChanged(State, SourcePath, Error,
            State is PlayerState.Playing or PlayerState.Paused && RenderedFrames > 0);
        if (value == lastNotification) return;
        lastNotification = value;
        // Detect worker failures even when WPF composition is throttled/occluded.
        if (value.State == PlayerState.Error)
        {
            pending?.Dispose(); pending = null; bitmap = null; surface.Source = null;
        }
        int version = requestVersion;
        _ = surface.Dispatcher.BeginInvoke(() =>
        {
            if (version == requestVersion) StateChanged?.Invoke(this, value);
        });
    }

    private void Render(object? sender, EventArgs args)
    {
        try { RenderCore(args); }
        catch (Exception exception) { session?.Fail(exception); }
    }

    private void RenderCore(EventArgs args)
    {
        if (session is null || disposed || session.Error is not null) return;
        if (desiredPaused && RenderedFrames > 0)
        {
            if (!session.HasAudio || AudioState is { Paused: true }) state = PlayerState.Paused;
            return;
        }
        if (args is RenderingEventArgs rendering)
        {
            if (rendering.RenderingTime == lastRenderingTime) return;
            lastRenderingTime = rendering.RenderingTime;
        }
        RenderingCallbacks++;
        if (pending is null && !session.TryRead(out pending)) { QueueEmptyCallbacks++; return; }
        bool preview = RenderedFrames == 0;
        if (!desiredPaused && !preview && session.HasAudio && session.Audio?.TryStart() != true) { surface.InvalidateVisual(); return; }
        if (!clockInitialized)
        {
            clockOrigin = sessionStart;
            clockInitialized = true;
        }
        if (!desiredPaused && !preview) clock.Start();
        double now = desiredPaused || preview ? pending!.PresentationSeconds :
            session.HasAudio ? session.Audio!.State!.Clock : clockOrigin + clock.Elapsed.TotalSeconds;
        // The stopwatch keeps running while composition is stopped (minimized window) but the decoder waits on the full
        // queue, and nothing drops late frames on this path. Rebase onto the next frame instead of fast-forwarding the backlog.
        if (!desiredPaused && !preview && !session.HasAudio && pending!.PresentationSeconds < now - 0.5)
        {
            clockOrigin = pending.PresentationSeconds;
            clock.Restart();
            now = clockOrigin;
        }
        VideoFrame? display = null;
        while (pending is not null && pending.PresentationSeconds <= now + 0.001)
        {
            if (display is not null) { display.Dispose(); presentationDrops++; }
            display = pending;
            session.TryRead(out pending);
            if (desiredPaused || preview) break;
        }
        if (display is null)
        {
            // A 30fps source often needs an unchanged 60Hz composition tick. Without
            // invalidation WPF can skip that tick, causing the next frame to miss its
            // deadline even though it is already queued. Reuse the existing bitmap.
            surface.InvalidateVisual();
            return;
        }
        using (display)
        {
            if (bitmap is null || bitmap.PixelWidth != display.Width || bitmap.PixelHeight != display.Height)
            {
                bitmap = new WriteableBitmap(display.Width, display.Height, 96, 96, PixelFormats.Bgra32, null);
                surface.Source = bitmap;
            }
            bitmap.WritePixels(new Int32Rect(0, 0, display.Width, display.Height), display.Pixels, display.Stride, 0);
            double late = Math.Max(0, now - display.PresentationSeconds) * 1000;
            totalLateness += late;
            MaxLatenessMs = Math.Max(MaxLatenessMs, late);
            if (display.Loop != CompletedLoops && RenderedFrames > 0)
                MaxLoopGapMs = Math.Max(MaxLoopGapMs, (now - previousPresentedAt) * 1000);
            previousPresentedAt = now;
            CompletedLoops = display.Loop;
            SourceSeconds = display.SourceSeconds;
            VideoTimestamp = display.PresentationSeconds;
            LastPresentationOffsetMs = (display.PresentationSeconds - now) * 1000;
            RenderedFrames++;
            ActualResumedPosition ??= display.SourceSeconds;
            state = desiredPaused ? PlayerState.Paused : preview ? PlayerState.Opening : PlayerState.Playing;
        }
    }
}


