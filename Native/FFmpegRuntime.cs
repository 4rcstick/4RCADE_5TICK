using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ArcadeStick.Native;

/// <summary>Owns the local modules; all playback workers must stop before these are unloaded.</summary>
internal sealed class FFmpegRuntime : IDisposable
{
    private readonly List<LibraryHandle> libraries = [];
    private readonly Dictionary<string, LibraryHandle> namedLibraries = [];
    internal IntPtr Export(string library, string symbol) =>
        NativeLibrary.GetExport(namedLibraries[library].DangerousGetHandle(), symbol);
    public string Report { get; private set; } = string.Empty;

    public static FFmpegRuntime Load(string directory)
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("The FFmpeg runtime requires an x64 process.");

        directory = Path.GetFullPath(directory);
        var runtime = new FFmpegRuntime();
        var report = new StringBuilder()
            .AppendLine("FFmpeg runtime: READY")
            .AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}")
            .AppendLine($"Runtime directory: {directory}")
            .AppendLine();
        try
        {
            // Dependency order; release in reverse order. FFmpeg modules and their
            // dependencies resolve only from the DLL's own directory and System32.
            foreach (string name in new[] { "avutil", "swresample", "swscale", "avcodec", "avformat" })
            {
                string[] candidates = Directory.GetFiles(directory, $"{name}-*.dll");
                if (candidates.Length != 1)
                    throw new FileNotFoundException(
                        $"Expected exactly one {name}-*.dll in '{directory}', found {candidates.Length}. " +
                        "Copy one consistent MABS shared build into the ffmpeg folder and rebuild.");

                string path = candidates[0];
                const uint loadLibrarySearchDllLoadDir = 0x00000100;
                const uint loadLibrarySearchSystem32 = 0x00000800;
                LibraryHandle handle = LoadLibraryExW(path, IntPtr.Zero,
                    loadLibrarySearchDllLoadDir | loadLibrarySearchSystem32);
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    throw new Win32Exception(error, $"Cannot load '{path}': " +
                        $"{new Win32Exception(error).Message}. Check x64 architecture and dependent DLLs.");
                }
                runtime.libraries.Add(handle);
                runtime.namedLibraries.Add(name, handle);

                uint version = GetFunction<VersionFunction>(handle, $"{name}_version")();
                uint major = version >> 16;
                string fileMajor = Path.GetFileNameWithoutExtension(path)[(name.Length + 1)..];
                if (!uint.TryParse(fileMajor, NumberStyles.None, CultureInfo.InvariantCulture, out uint expected)
                    || expected != major)
                    throw new InvalidDataException($"{path}: filename and reported ABI major {major} disagree.");

                string license = ReadString(handle, $"{name}_license");
                report.AppendLine($"{Path.GetFileName(path)}: {major}.{(version >> 8) & 255}.{version & 255}")
                    .AppendLine($"  License reported by library: {license}");
                if (name == "avutil")
                    report.AppendLine($"  FFmpeg version: {ReadString(handle, "av_version_info")}");
                if (name == "avcodec")
                    report.AppendLine().AppendLine("Build configuration:")
                        .AppendLine(ReadString(handle, "avcodec_configuration")).AppendLine();
            }
            report.AppendLine().AppendLine("All five native version APIs were called successfully.")
                .AppendLine("Runtime probe completed without opening media.");
            runtime.Report = report.ToString();
            return runtime;
        }
        catch
        {
            runtime.Dispose();
            throw;
        }
    }

    private static T GetFunction<T>(LibraryHandle handle, string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(handle.DangerousGetHandle(), name));

    private static string ReadString(LibraryHandle handle, string name) =>
        Marshal.PtrToStringUTF8(GetFunction<StringFunction>(handle, name)()) ?? "(not reported)";

    public void Dispose()
    {
        for (int index = libraries.Count - 1; index >= 0; index--)
            libraries[index].Dispose();
        libraries.Clear();
        namedLibraries.Clear();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint VersionFunction();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr StringFunction();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern LibraryHandle LoadLibraryExW(string path, IntPtr file, uint flags);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);

    private sealed class LibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public LibraryHandle() : base(ownsHandle: true) { }
        protected override bool ReleaseHandle() => FreeLibrary(handle);
    }
}
