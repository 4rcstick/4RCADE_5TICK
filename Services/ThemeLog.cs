using System;
using System.IO;
using System.Threading.Tasks;

namespace ArcadeStick.Services
{
    // [SECTION: Theme Log]
    // Appends one line per theme load, save or delete to 4rcade5tick_files/logs/theme.log, beside preview.log. Writing happens
    // in order on a background task, so it never slows the UI, and every failure is swallowed, so it never breaks the action.
    public static class ThemeLog
    {
        private const string FileName = "theme.log";
        private const long MaxLogBytes = 1024 * 1024;
        private const int MaxDetailLength = 1000;

        private static readonly object QueueLock = new();
        private static Task _lastWrite = Task.CompletedTask;
        private static string? _folder;

        // Sets the folder the log is written to. Nothing is logged before this runs.
        public static void Configure(string logsFolder) => _folder = logsFolder;

        // Queues one line: timestamp, action, theme, dropdown value, loaded theme, result and a short detail
        public static void Write(string action, string themeName, string dropdown, string loadedTheme, string result, string detail = "")
        {
            try
            {
                string? folder = _folder;
                if (folder == null) return;

                string shortDetail = detail.Replace("\r", " ").Replace("\n", " ");
                if (shortDetail.Length > MaxDetailLength) shortDetail = shortDetail.Substring(0, MaxDetailLength) + "...";

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {action} | theme={themeName} | dropdown={dropdown} | loaded={loadedTheme} | {result} | {shortDetail}{Environment.NewLine}";

                lock (QueueLock)
                {
                    _lastWrite = _lastWrite.ContinueWith(_ => AppendLine(folder, line), TaskScheduler.Default);
                }
            }
            catch
            {
            }
        }

        // Appends the line, first moving a full log to theme.log.1 (one old file is kept)
        private static void AppendLine(string folder, string line)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, FileName);

                if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
                {
                    File.Move(path, path + ".1", overwrite: true);
                }

                File.AppendAllText(path, line);
            }
            catch
            {
            }
        }
    }
    // [END SECTION: Theme Log]
}
