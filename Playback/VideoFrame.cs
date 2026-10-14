using System.Buffers;

namespace ArcadeStick.Playback;

internal sealed class VideoFrame(int width, int height, double presentationSeconds,
    double sourceSeconds, double durationSeconds, int loop) : IDisposable
{
    private byte[]? pixels = ArrayPool<byte>.Shared.Rent(checked(width * height * 4));
    public byte[] Pixels => pixels ?? throw new ObjectDisposedException(nameof(VideoFrame));
    public int Width { get; } = width;
    public int Height { get; } = height;
    public int Stride => Width * 4;
    public double PresentationSeconds { get; } = presentationSeconds;
    public double SourceSeconds { get; } = sourceSeconds;
    public double DurationSeconds { get; } = durationSeconds;
    public int Loop { get; } = loop;
    public void Dispose()
    {
        byte[]? buffer = Interlocked.Exchange(ref pixels, null);
        if (buffer is not null) ArrayPool<byte>.Shared.Return(buffer);
    }
}

internal sealed record VideoInfo(string Path, string Codec, string PixelFormat, int Width,
    int Height, double FrameRate, double Duration, double SampleAspectRatio);
