using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArcadeStick.Services
{
    // A theme name refused as a path. The operation that caught it logs a PathRefused line with the short Reason.
    public class ThemePathException : ThemeFileException
    {
        public string Reason { get; }

        public ThemePathException(string message, string reason) : base(message)
        {
            Reason = reason;
        }
    }

    // [SECTION: Theme Paths]
    // The only place a theme name becomes a path. Check 1 (TryValidateNewName) holds a name the user types to strict rules
    // before anything happens. Check 2 runs inside every path below for every name, including existing ones, which Check 1
    // must not lock out: it refuses a path that would leave its folder or that Windows would silently change.
    public static class ThemePaths
    {
        private const string ExtractedFolderName = "Extracted";
        private const int MaxNameLength = 50;
        private const string ExplicitlyInvalidChars = "\\/:*?\"<>|";

        // Windows treats these as devices, with or without an extension
        private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"
        };

        // Check 1: returns true when a typed name may be used for a new theme, otherwise false with a short reason for the log
        public static bool TryValidateNewName(string name, out string reason)
        {
            string trimmed = name.Trim();
            string beforeFirstDot = trimmed.Split('.')[0].Trim();

            if (trimmed.Length == 0) reason = "empty";
            else if (trimmed.Length > MaxNameLength) reason = $"longer than {MaxNameLength} characters";
            else if (HasInvalidChar(trimmed)) reason = "contains an invalid character";
            else if (trimmed.Contains("..")) reason = "contains ..";
            else if (trimmed.StartsWith('.')) reason = "starts with a dot";
            else if (trimmed.EndsWith('.')) reason = "ends with a dot";
            else if (ReservedNames.Contains(beforeFirstDot)) reason = "reserved Windows name";
            else reason = "";

            return reason.Length == 0;
        }

        // Returns the theme's zip: Themes\<name>.zip
        public static string Zip(string themesDir, string name) => Checked(Path.Combine(themesDir, $"{name}.zip"), themesDir, name);

        // Returns the theme's working folder: Themes\Extracted\<name>
        public static string WorkingFolder(string themesDir, string name)
        {
            string extractedRoot = Path.Combine(themesDir, ExtractedFolderName);
            return Checked(Path.Combine(extractedRoot, name), extractedRoot, name);
        }

        // Returns a new temp zip path beside the theme's zip (same drive, so the swap is a rename): Themes\<name>.zip.tmp-<guid>
        public static string TempZip(string themesDir, string name) => Checked(Path.Combine(themesDir, $"{name}.zip.tmp-{Guid.NewGuid():N}"), themesDir, name);

        // Returns the one backup kept of a theme's previous zip: Themes\<name>.zip.bak
        public static string Backup(string themesDir, string name) => Checked(Path.Combine(themesDir, $"{name}.zip.bak"), themesDir, name);

        // Returns a deleted copy's path: Themes\Deleted\<name>_<stamp>.zip, with _<counter> added when counter is above 0
        public static string DeletedZip(string deletedDir, string name, string stamp, int counter)
        {
            string suffix = counter > 0 ? $"_{counter}" : "";
            return Checked(Path.Combine(deletedDir, $"{name}_{stamp}{suffix}.zip"), deletedDir, name);
        }

        // Returns a new staging folder path: Themes\_staging\<name>.<marker>-<guid>
        public static string StagingFolder(string stagingRoot, string name, string marker) => Checked(Path.Combine(stagingRoot, $"{name}.{marker}-{Guid.NewGuid():N}"), stagingRoot, name);

        // Check 2: returns the path unchanged when it sits directly inside its folder under the name as given, otherwise throws
        private static string Checked(string path, string folder, string name)
        {
            string? problem = FindNameProblem(name) ?? FindPathProblem(path, folder);
            if (problem == null) return path;

            throw new ThemePathException($"Couldn't use the theme name '{name}': it {problem}. Nothing was changed.", problem);
        }

        // Returns what is wrong with a name itself, whatever the path: no real file can have these
        private static string? FindNameProblem(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "is empty";
            if (HasInvalidChar(name)) return "contains an invalid character";
            if (name.EndsWith('.') || name.EndsWith(' ')) return "ends with a dot or a space, which Windows would remove";
            return null;
        }

        // Returns what is wrong with a resolved path: it must sit directly inside the folder and keep its file name
        private static string? FindPathProblem(string path, string folder)
        {
            string fullPath = Path.GetFullPath(path);
            string fullFolder = WithTrailingSeparator(Path.GetFullPath(folder));
            string parent = WithTrailingSeparator(Path.GetDirectoryName(fullPath) ?? "");

            if (!fullPath.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(parent, fullFolder, StringComparison.OrdinalIgnoreCase))
            {
                return "would resolve outside its folder";
            }

            if (!string.Equals(Path.GetFileName(fullPath), Path.GetFileName(path), StringComparison.OrdinalIgnoreCase))
            {
                return "would be changed by Windows";
            }

            return null;
        }

        // Adds a trailing separator only if it is missing (a drive root already has one)
        private static string WithTrailingSeparator(string path) =>
            path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

        // True when a name holds a character Windows does not allow in a file name, or a control character
        private static bool HasInvalidChar(string name) =>
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.IndexOfAny(ExplicitlyInvalidChars.ToCharArray()) >= 0
            || name.Any(char.IsControl);
    }
    // [END SECTION: Theme Paths]
}
