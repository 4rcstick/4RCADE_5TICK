using System.IO;
using System.Runtime.InteropServices;

namespace ArcadeStick.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct Rational(int Numerator, int Denominator)
{
    public double Value => Denominator == 0 ? 0 : (double)Numerator / Denominator;
}

/// <summary>Minimal direct bindings to the locally built FFmpeg snapshot. Contexts remain opaque.</summary>
internal unsafe sealed class FFmpegApi
{
    public readonly delegate* unmanaged[Cdecl]<nint> AllocFormat;
    public readonly delegate* unmanaged[Cdecl]<nint*, byte*, nint, nint*, int> OpenInput;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, int> FindStreamInfo;
    public readonly delegate* unmanaged[Cdecl]<nint, int, int, int, nint*, int, int> FindBestStream;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, int> ReadFrame;
    public readonly delegate* unmanaged[Cdecl]<nint, int, long, int, int> SeekFrame;
    public readonly delegate* unmanaged[Cdecl]<nint*, void> CloseInput;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, nint, Rational> GuessFrameRate;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, nint, Rational> GuessAspectRatio;
    public readonly delegate* unmanaged[Cdecl]<nint, nint> AllocCodec;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, int> CopyParameters;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, nint, int> OpenCodec;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, int> SendPacket;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, int> ReceiveFrame;
    public readonly delegate* unmanaged[Cdecl]<nint, void> FlushCodec;
    public readonly delegate* unmanaged[Cdecl]<nint*, void> FreeCodec;
    public readonly delegate* unmanaged[Cdecl]<nint> AllocPacket;
    public readonly delegate* unmanaged[Cdecl]<nint, void> UnrefPacket;
    public readonly delegate* unmanaged[Cdecl]<nint*, void> FreePacket;
    public readonly delegate* unmanaged[Cdecl]<nint> AllocFrame;
    public readonly delegate* unmanaged[Cdecl]<nint, void> UnrefFrame;
    public readonly delegate* unmanaged[Cdecl]<nint*, void> FreeFrame;
    public readonly delegate* unmanaged[Cdecl]<int, byte*> CodecName;
    public readonly delegate* unmanaged[Cdecl]<int, byte*> PixelFormatName;
    public readonly delegate* unmanaged[Cdecl]<int, byte*, nuint, int> ErrorText;
    public readonly delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int, nint, nint, nint, nint> CreateScaler;
    public readonly delegate* unmanaged[Cdecl]<nint, byte**, int*, int, int, byte**, int*, int> Scale;
    public readonly delegate* unmanaged[Cdecl]<nint, void> FreeScaler;
    public readonly delegate* unmanaged[Cdecl]<int, int*> ColorCoefficients;
    public readonly delegate* unmanaged[Cdecl]<nint, int*, int, int*, int, int, int, int, int> SetColorspace;
    public readonly delegate* unmanaged[Cdecl]<nint*, byte*, byte*, int, int> SetDictionary;
    public readonly delegate* unmanaged[Cdecl]<nint*, void> FreeDictionary;

    public readonly delegate* unmanaged[Cdecl]<nint, int, void> DefaultChannelLayout;
    public readonly delegate* unmanaged[Cdecl]<nint, void> UninitChannelLayout;
    public readonly delegate* unmanaged[Cdecl]<nint*, nint, int, int, nint, int, int, int, nint, int> AllocateResampler;
    public readonly delegate* unmanaged[Cdecl]<nint, int> InitResampler;
    public readonly delegate* unmanaged[Cdecl]<nint, byte**, int, byte**, int, int> ConvertAudio;
    public readonly delegate* unmanaged[Cdecl]<nint, int, int> AudioCapacity;
    public readonly delegate* unmanaged[Cdecl]<nint, long, long> AudioDelay;
    public readonly delegate* unmanaged[Cdecl]<nint*, void> FreeResampler;

    public FFmpegApi(FFmpegRuntime runtime)
    {
        foreach (var (name, expected) in new[] {
            ("avcodec", FFmpegLayout.AvcodecVersion), ("avformat", FFmpegLayout.AvformatVersion),
            ("avutil", FFmpegLayout.AvutilVersion), ("swscale", FFmpegLayout.SwscaleVersion), ("swresample", FFmpegLayout.SwresampleVersion) })
        {
            uint actual = ((delegate* unmanaged[Cdecl]<uint>)runtime.Export(name, name + "_version"))();
            if (actual != expected)
                throw new NotSupportedException($"{name} ABI does not match the generated bindings ({actual} != {expected}). " +
                    "Restore the documented DLL set; regenerate and revalidate bindings when upgrading FFmpeg.");
        }
        DefaultChannelLayout = (delegate* unmanaged[Cdecl]<nint, int, void>)runtime.Export("avutil", "av_channel_layout_default");
        UninitChannelLayout = (delegate* unmanaged[Cdecl]<nint, void>)runtime.Export("avutil", "av_channel_layout_uninit");
        AllocateResampler = (delegate* unmanaged[Cdecl]<nint*, nint, int, int, nint, int, int, int, nint, int>)runtime.Export("swresample", "swr_alloc_set_opts2");
        InitResampler = (delegate* unmanaged[Cdecl]<nint, int>)runtime.Export("swresample", "swr_init");
        ConvertAudio = (delegate* unmanaged[Cdecl]<nint, byte**, int, byte**, int, int>)runtime.Export("swresample", "swr_convert");
        AudioCapacity = (delegate* unmanaged[Cdecl]<nint, int, int>)runtime.Export("swresample", "swr_get_out_samples");
        AudioDelay = (delegate* unmanaged[Cdecl]<nint, long, long>)runtime.Export("swresample", "swr_get_delay");
        FreeResampler = (delegate* unmanaged[Cdecl]<nint*, void>)runtime.Export("swresample", "swr_free");
        AllocFormat = (delegate* unmanaged[Cdecl]<nint>)runtime.Export("avformat", "avformat_alloc_context");
        OpenInput = (delegate* unmanaged[Cdecl]<nint*, byte*, nint, nint*, int>)runtime.Export("avformat", "avformat_open_input");
        FindStreamInfo = (delegate* unmanaged[Cdecl]<nint, nint, int>)runtime.Export("avformat", "avformat_find_stream_info");
        FindBestStream = (delegate* unmanaged[Cdecl]<nint, int, int, int, nint*, int, int>)runtime.Export("avformat", "av_find_best_stream");
        ReadFrame = (delegate* unmanaged[Cdecl]<nint, nint, int>)runtime.Export("avformat", "av_read_frame");
        SeekFrame = (delegate* unmanaged[Cdecl]<nint, int, long, int, int>)runtime.Export("avformat", "av_seek_frame");
        CloseInput = (delegate* unmanaged[Cdecl]<nint*, void>)runtime.Export("avformat", "avformat_close_input");
        GuessFrameRate = (delegate* unmanaged[Cdecl]<nint, nint, nint, Rational>)runtime.Export("avformat", "av_guess_frame_rate");
        GuessAspectRatio = (delegate* unmanaged[Cdecl]<nint, nint, nint, Rational>)runtime.Export("avformat", "av_guess_sample_aspect_ratio");
        AllocCodec = (delegate* unmanaged[Cdecl]<nint, nint>)runtime.Export("avcodec", "avcodec_alloc_context3");
        CopyParameters = (delegate* unmanaged[Cdecl]<nint, nint, int>)runtime.Export("avcodec", "avcodec_parameters_to_context");
        OpenCodec = (delegate* unmanaged[Cdecl]<nint, nint, nint, int>)runtime.Export("avcodec", "avcodec_open2");
        SendPacket = (delegate* unmanaged[Cdecl]<nint, nint, int>)runtime.Export("avcodec", "avcodec_send_packet");
        ReceiveFrame = (delegate* unmanaged[Cdecl]<nint, nint, int>)runtime.Export("avcodec", "avcodec_receive_frame");
        FlushCodec = (delegate* unmanaged[Cdecl]<nint, void>)runtime.Export("avcodec", "avcodec_flush_buffers");
        FreeCodec = (delegate* unmanaged[Cdecl]<nint*, void>)runtime.Export("avcodec", "avcodec_free_context");
        AllocPacket = (delegate* unmanaged[Cdecl]<nint>)runtime.Export("avcodec", "av_packet_alloc");
        UnrefPacket = (delegate* unmanaged[Cdecl]<nint, void>)runtime.Export("avcodec", "av_packet_unref");
        FreePacket = (delegate* unmanaged[Cdecl]<nint*, void>)runtime.Export("avcodec", "av_packet_free");
        AllocFrame = (delegate* unmanaged[Cdecl]<nint>)runtime.Export("avutil", "av_frame_alloc");
        UnrefFrame = (delegate* unmanaged[Cdecl]<nint, void>)runtime.Export("avutil", "av_frame_unref");
        FreeFrame = (delegate* unmanaged[Cdecl]<nint*, void>)runtime.Export("avutil", "av_frame_free");
        CodecName = (delegate* unmanaged[Cdecl]<int, byte*>)runtime.Export("avcodec", "avcodec_get_name");
        PixelFormatName = (delegate* unmanaged[Cdecl]<int, byte*>)runtime.Export("avutil", "av_get_pix_fmt_name");
        ErrorText = (delegate* unmanaged[Cdecl]<int, byte*, nuint, int>)runtime.Export("avutil", "av_strerror");
        CreateScaler = (delegate* unmanaged[Cdecl]<int, int, int, int, int, int, int, nint, nint, nint, nint>)runtime.Export("swscale", "sws_getContext");
        Scale = (delegate* unmanaged[Cdecl]<nint, byte**, int*, int, int, byte**, int*, int>)runtime.Export("swscale", "sws_scale");
        FreeScaler = (delegate* unmanaged[Cdecl]<nint, void>)runtime.Export("swscale", "sws_freeContext");
        ColorCoefficients = (delegate* unmanaged[Cdecl]<int, int*>)runtime.Export("swscale", "sws_getCoefficients");
        SetColorspace = (delegate* unmanaged[Cdecl]<nint, int*, int, int*, int, int, int, int, int>)runtime.Export("swscale", "sws_setColorspaceDetails");
        SetDictionary = (delegate* unmanaged[Cdecl]<nint*, byte*, byte*, int, int>)runtime.Export("avutil", "av_dict_set");
        FreeDictionary = (delegate* unmanaged[Cdecl]<nint*, void>)runtime.Export("avutil", "av_dict_free");
    }

    public void Check(int result, string operation)
    {
        if (result >= 0) return;
        byte* text = stackalloc byte[256];
        ErrorText(result, text, 256);
        throw new IOException($"{operation}: {Marshal.PtrToStringUTF8((nint)text)} ({result}).");
    }
    public static T Read<T>(nint pointer, int offset) where T : unmanaged => *(T*)((byte*)pointer + offset);
    public static void Write<T>(nint pointer, int offset, T value) where T : unmanaged => *(T*)((byte*)pointer + offset) = value;
    public static string Text(byte* text) => Marshal.PtrToStringUTF8((nint)text) ?? "unknown";
}
