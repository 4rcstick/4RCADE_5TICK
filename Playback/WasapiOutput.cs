using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ArcadeStick.Playback;

internal sealed class AudioSettings
{
    private double volume = 0.7;
    private int muted;
    public double Volume { get => Volatile.Read(ref volume); set => Volatile.Write(ref volume, Math.Clamp(value, 0, 1)); }
    public bool Muted { get => Volatile.Read(ref muted) != 0; set => Volatile.Write(ref muted, value ? 1 : 0); }
    public double Gain => Muted ? 0 : Volume;
}

internal sealed record AudioOutputState(double Clock, double BufferedSeconds, long Underruns,
    long SubmittedSamples, double InputRms, double OutputRms, double AppliedGain, bool Started,
    double SampledAt = 0, double DeviceLatency = 0, bool Paused = false);

/// <summary>Small WASAPI shared-mode PCM sink. All COM calls and releases stay on the audio worker.
/// UI reads immutable clock snapshots, interpolated using the device's correlated QPC timestamp.</summary>
internal sealed unsafe class WasapiOutput : IDisposable
{
    internal const int Rate = 48000, Channels = 2, BlockFrames = 480;
    private readonly AudioSettings settings;
    private readonly Func<bool> pauseRequested;
    private bool everStarted, counted;
    private static int activeOutputs;
    public static int ActiveOutputs => Volatile.Read(ref activeOutputs);
    private nint enumerator, endpoint, client, render, clock;
    private bool comInitialized, initialized, started;
    private uint capacity;
    private ulong frequency;
    private int startRequested;
    private long submitted;
    private double inputRms, outputRms, appliedGain, latency;
    private AudioOutputState snapshot = new(0, 0, 0, 0, 0, 0, 0, false);
    public static double QpcSeconds => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort Tag, Channels;
        public uint Rate, BytesPerSecond;
        public ushort BlockAlign, Bits, Extra;
    }

    public WasapiOutput(AudioSettings settings, Func<bool> pauseRequested)
    {
        this.settings = settings;
        this.pauseRequested = pauseRequested;
        try
        {
            Check(CoInitializeEx(0, 0), "Initialize audio COM apartment");
            comInitialized = true;
            Guid clsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
            Guid iid = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
            Check(CoCreateInstance(ref clsid, 0, 1, ref iid, out enumerator), "Create audio device enumerator");
            nint value;
            Check(((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)Method(enumerator, 4))
                (enumerator, 0, 1, &value), "Get default multimedia output");
            endpoint = value;
            iid = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, nint, nint*, int>)Method(endpoint, 3))
                (endpoint, &iid, 23, 0, &value), "Activate WASAPI output");
            client = value;
            var format = new WaveFormat { Tag = 1, Channels = Channels, Rate = Rate,
                BytesPerSecond = Rate * 4, BlockAlign = 4, Bits = 16 };
            // Shared mode, automatic engine format conversion, high quality SRC, no persisted session settings.
            Check(((delegate* unmanaged[Stdcall]<nint, int, uint, long, long, WaveFormat*, nint, int>)Method(client, 3))
                (client, 0, 0x88080000, 2_000_000, 0, &format, 0), "Initialize 48 kHz stereo WASAPI output");
            initialized = true;
            uint size;
            Check(((delegate* unmanaged[Stdcall]<nint, uint*, int>)Method(client, 4))(client, &size), "Get PCM buffer size");
            capacity = size;
            long ticks;
            Check(((delegate* unmanaged[Stdcall]<nint, long*, int>)Method(client, 5))(client, &ticks), "Get output latency");
            latency = ticks / 10_000_000.0;
            render = Service(new Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"));
            clock = Service(new Guid("CD63314F-3FBA-4A1B-812C-EF96358728E7"));
            ulong rate;
            Check(((delegate* unmanaged[Stdcall]<nint, ulong*, int>)Method(clock, 3))(clock, &rate), "Get audio clock frequency");
            frequency = rate;
            if (rate == 0) throw new InvalidOperationException("Audio clock has zero frequency.");
            Interlocked.Increment(ref activeOutputs);
            counted = true;
        }
        catch { Dispose(); throw; }
    }

    public AudioOutputState State
    {
        get
        {
            var state = Volatile.Read(ref snapshot);
            double estimate = state.Started ? state.Clock + Math.Max(0, QpcSeconds - state.SampledAt) : state.Clock;
            // Never run video beyond PCM actually submitted, including during decoder stalls.
            estimate = Math.Min(estimate, state.SubmittedSamples / (double)Rate);
            return state with { Clock = estimate, BufferedSeconds = Math.Max(0, state.SubmittedSamples / (double)Rate - estimate) };
        }
    }
    public bool TryStart()
    {
        Volatile.Write(ref startRequested, 1);
        return Volatile.Read(ref snapshot).Started;
    }

    public void Write(ReadOnlySpan<short> pcm, CancellationToken token)
    {
        while (!pcm.IsEmpty)
        {
            token.ThrowIfCancellationRequested();
            bool paused = pauseRequested();
            if (paused && started)
            {
                Check(((delegate* unmanaged[Stdcall]<nint, int>)Method(client, 11))(client), "Pause WASAPI output");
                started = false;
            }
            uint padding;
            Check(((delegate* unmanaged[Stdcall]<nint, uint*, int>)Method(client, 6))(client, &padding), "Read PCM buffer padding");
            if (!paused && !started && Volatile.Read(ref startRequested) != 0 && padding >= Math.Min(capacity, Rate / 10))
            {
                Check(((delegate* unmanaged[Stdcall]<nint, int>)Method(client, 10))(client), "Start WASAPI output");
                started = everStarted = true;
            }
            // Never silently let a device clock outrun missing media. A genuine underrun fails the session;
            // source selection can reopen it. No unbounded clock correction or permanent lip-sync shift.
            if (started && padding == 0)
            {
                var last = Volatile.Read(ref snapshot);
                Volatile.Write(ref snapshot, last with { Underruns = last.Underruns + 1 });
                throw new InvalidOperationException("Audio buffer underrun; playback stopped to preserve A/V sync. Re-select the source.");
            }
            PublishClock(padding);
            int frames = (int)Math.Min(Math.Min(BlockFrames, capacity - padding), (uint)(pcm.Length / Channels));
            if (frames == 0)
            {
                if (token.WaitHandle.WaitOne(2)) token.ThrowIfCancellationRequested();
                continue;
            }
            byte* buffer;
            Check(((delegate* unmanaged[Stdcall]<nint, uint, byte**, int>)Method(render, 3))(render, (uint)frames, &buffer), "Acquire PCM buffer");
            double gain = settings.Gain, inputEnergy = 0, outputEnergy = 0;
            Span<short> target = new(buffer, frames * Channels);
            for (int i = 0; i < target.Length; i++)
            {
                short sample = pcm[i];
                target[i] = (short)Math.Round(sample * gain);
                inputEnergy += (double)sample * sample;
                outputEnergy += (double)target[i] * target[i];
            }
            Check(((delegate* unmanaged[Stdcall]<nint, uint, uint, int>)Method(render, 4))
                (render, (uint)frames, gain == 0 ? 2u : 0u), "Release PCM buffer");
            inputRms = Math.Sqrt(inputEnergy / target.Length) / 32768;
            outputRms = Math.Sqrt(outputEnergy / target.Length) / 32768;
            appliedGain = gain;
            submitted += frames;
            pcm = pcm[(frames * Channels)..];
        }
    }

    private void PublishClock(uint padding)
    {
        ulong position = 0, qpc = 0;
        if (everStarted)
            Check(((delegate* unmanaged[Stdcall]<nint, ulong*, ulong*, int>)Method(clock, 4))(clock, &position, &qpc), "Read audio device clock");
        // Before the first nonzero device reading, do not extrapolate through startup latency.
        bool advancing = started && position > 0;
        Volatile.Write(ref snapshot, new AudioOutputState(position / (double)frequency, padding / (double)Rate,
            0, submitted, inputRms, outputRms, appliedGain, advancing,
            advancing ? qpc / 10_000_000.0 : QpcSeconds, latency, pauseRequested() && !started));
    }
    private nint Service(Guid iid)
    {
        nint value;
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Method(client, 14))(client, &iid, &value), "Get WASAPI service");
        return value;
    }
    private static nint Method(nint instance, int slot) => (*(nint**)instance)[slot];
    private static void Release(ref nint instance)
    {
        if (instance == 0) return;
        ((delegate* unmanaged[Stdcall]<nint, uint>)Method(instance, 2))(instance);
        instance = 0;
    }
    public void Dispose()
    {
        int stopped = 0, reset = 0;
        if (client != 0 && initialized)
        {
            if (started) stopped = ((delegate* unmanaged[Stdcall]<nint, int>)Method(client, 11))(client);
            reset = ((delegate* unmanaged[Stdcall]<nint, int>)Method(client, 12))(client);
        }
        var last = State;
        Volatile.Write(ref snapshot, last with { Started = false, BufferedSeconds = 0 });
        Release(ref clock); Release(ref render); Release(ref client); Release(ref endpoint); Release(ref enumerator);
        if (comInitialized) { CoUninitialize(); comInitialized = false; }
        if (counted) { Interlocked.Decrement(ref activeOutputs); counted = false; }
        // Release all COM references even if the device was unplugged while stopping.
        if (stopped < 0) Check(stopped, "Stop WASAPI output");
        if (reset < 0) Check(reset, "Reset WASAPI output");
    }
    private static void Check(int result, string operation)
    {
        if (result < 0) throw new COMException($"{operation}: {Marshal.GetExceptionForHR(result)?.Message}", result);
    }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(ref Guid clsid, nint outer, uint context, ref Guid iid, out nint instance);
}

