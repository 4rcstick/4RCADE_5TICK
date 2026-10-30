using System.Diagnostics;
using System.Windows.Controls;
using ArcadeStick.Native;

namespace ArcadeStick.Playback;

/// <summary>Owns the FFmpeg runtime and the preview presenter. All calls must come from the WPF dispatcher thread.
/// If the runtime fails to load, every call is a quiet no-op and LoadError says why.</summary>
internal sealed class PreviewPlayerHost
{
    private FFmpegRuntime? runtime;
    private VideoPresenter? presenter;
    private bool closed;

    public string? LoadError { get; }
    public bool IsAvailable => presenter is not null && !closed;

    // The presenter's state changes (Error carries the error text and source path), raised on the dispatcher.
    // Events from a superseded Play / Restart / Stop are already filtered out by the presenter.
    public event EventHandler<PlayerStateChanged>? StateChanged;

    public PreviewPlayerHost(string ffmpegDirectory, Image surface)
    {
        try
        {
            runtime = FFmpegRuntime.Load(ffmpegDirectory);
            var api = new FFmpegApi(runtime);
            presenter = new VideoPresenter(api, surface);
            presenter.StateChanged += (_, change) => StateChanged?.Invoke(this, change);
        }
        catch (Exception exception)
        {
            LoadError = exception.Message;
            Debug.WriteLine($"PreviewPlayer: runtime failed to load: {exception.Message}");
            presenter = null;
            runtime?.Dispose();
            runtime = null;
        }
    }

    // Decode box in pixels (the Video Preview layer's size).
    public void SetTargetSize(int width, int height) => presenter?.SetTargetSize(width, height);

    // withAudio off (the default) opens no audio session at all; on plays the clip's sound on the audio clock.
    public void Play(string path, bool withAudio = false) => _ = RunAsync(p => p.PlayAsync(path, withAudio));
    // Reopens the current source from the start with the same audio choice.
    public void Restart() => _ = RunAsync(p => p.RestartAsync());
    public void Stop() => _ = RunAsync(p => p.StopAsync());
    public void Pause() => _ = RunAsync(p => p.PauseAsync());
    public void Resume() => _ = RunAsync(p => p.ResumeAsync());

    // Commands are latest-request-wins inside the presenter, so firing them without awaiting is safe.
    private async Task RunAsync(Func<VideoPresenter, Task> action)
    {
        if (closed || presenter is null) return;
        try { await action(presenter); }
        catch (Exception exception) { Debug.WriteLine($"PreviewPlayer: command failed: {exception.Message}"); }
    }

    // Normal shutdown: workers are joined before the native modules are unloaded.
    public async Task CloseAsync()
    {
        if (closed) return;
        closed = true;
        VideoPresenter? current = presenter;
        try { if (current is not null) await current.CloseAsync(); }
        catch (Exception exception) { Debug.WriteLine($"PreviewPlayer: close failed: {exception.Message}"); }
        Release();
    }

    // Application-exit fallback, safe to call after CloseAsync.
    public void ShutdownNow()
    {
        if (closed && presenter is null) return;
        closed = true;
        try { presenter?.ShutdownNow(); }
        catch (Exception exception) { Debug.WriteLine($"PreviewPlayer: shutdown failed: {exception.Message}"); }
        Release();
    }

    private void Release()
    {
        presenter = null;
        runtime?.Dispose();
        runtime = null;
    }
}