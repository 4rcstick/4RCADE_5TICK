using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // One usable template: its menu label and the file it came from
    public record ThemeTemplate(string Name, string FilePath);

    // A template that cannot be used. Reason is the short text for the log.
    public class ThemeTemplateException : Exception
    {
        public string Reason { get; }

        public ThemeTemplateException(string reason) : base(reason)
        {
            Reason = reason;
        }
    }

    // [SECTION: Theme Templates]
    // Read-only starting points for new themes: *.json files shaped like a theme's theme.json, directly in the Templates
    // folder. This class only reads them. It never creates the folder and never writes, renames or deletes a template.
    public static class ThemeTemplateStore
    {
        public const string FolderName = "Templates";
        private const long MaxFileBytes = 2 * 1024 * 1024;
        private const int PreferredWidth = 1280;
        private const int PreferredHeight = 720;

        // What the last check of each file found (null Problem = valid), so reopening the menu only checks file details
        private static readonly Dictionary<string, (DateTime WriteTimeUtc, long Length, string? Problem)> CheckedFiles = new(StringComparer.OrdinalIgnoreCase);
        // Log lines already written this session, so opening the menu repeatedly does not repeat them
        private static readonly HashSet<string> LoggedLines = new(StringComparer.OrdinalIgnoreCase);

        // Returns the valid templates sorted by name, logging each skipped file once. A missing folder or a bad file only shortens the list.
        public static List<ThemeTemplate> ListValid(string templatesDir, string loadedTheme)
        {
            var templates = new List<ThemeTemplate>();
            try
            {
                if (!Directory.Exists(templatesDir)) return templates;

                foreach (string file in Directory.GetFiles(templatesDir, "*.json"))
                {
                    string? problem = FindProblem(file, loadedTheme);
                    if (problem == null)
                    {
                        templates.Add(new ThemeTemplate(Path.GetFileNameWithoutExtension(file), file));
                    }
                    else
                    {
                        LogOnce("TemplateSkipped", Path.GetFileName(file), loadedTheme, "skipped", problem);
                    }
                }
            }
            catch (Exception ex)
            {
                LogOnce("TemplateSkipped", FolderName, loadedTheme, "skipped", ex.Message);
            }

            templates.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
            return templates;
        }

        // Re-reads and checks a template and returns the theme.json text for a new theme made from it: every layout, the
        // active size from ChooseActiveSize and a blank Info block. Throws ThemeTemplateException, having written nothing.
        public static string BuildThemeJson(string templatePath, string loadedTheme)
        {
            ThemeSettings theme = ReadAndCheck(templatePath, loadedTheme);

            (theme.ActiveWidth, theme.ActiveHeight) = ChooseActiveSize(theme.Layouts.Select(layout => (layout.Width, layout.Height)));
            theme.Info = new ThemeInfo();

            return JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
        }

        // Picks the active size: 1280x720 if present, otherwise the height closest to 720, ties going to the smaller size
        public static (int Width, int Height) ChooseActiveSize(IEnumerable<(int Width, int Height)> sizes)
        {
            var list = sizes.ToList();
            if (list.Contains((PreferredWidth, PreferredHeight))) return (PreferredWidth, PreferredHeight);

            return list.OrderBy(size => Math.Abs(size.Height - PreferredHeight))
                .ThenBy(size => size.Height)
                .ThenBy(size => size.Width)
                .First();
        }

        // Returns why a file cannot be used, or null when it can, reusing the last answer while its date and length are unchanged
        private static string? FindProblem(string file, string loadedTheme)
        {
            DateTime writeTimeUtc;
            long length;
            try
            {
                var info = new FileInfo(file);
                writeTimeUtc = info.LastWriteTimeUtc;
                length = info.Length;
            }
            catch (Exception ex)
            {
                return $"Can't be read: {ex.Message}";
            }

            if (CheckedFiles.TryGetValue(file, out var known) && known.WriteTimeUtc == writeTimeUtc && known.Length == length)
            {
                return known.Problem;
            }

            string? problem = null;
            try
            {
                ReadAndCheck(file, loadedTheme);
            }
            catch (ThemeTemplateException ex)
            {
                problem = ex.Reason;
            }

            CheckedFiles[file] = (writeTimeUtc, length, problem);
            return problem;
        }

        // Reads a template with the same default options LoadTheme uses and returns it, or throws with the reason it cannot be used
        private static ThemeSettings ReadAndCheck(string file, string loadedTheme)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (!ThemePaths.TryValidateNewName(name, out string nameProblem)) throw new ThemeTemplateException($"Bad file name: {nameProblem}.");

            ThemeSettings? theme;
            try
            {
                if (new FileInfo(file).Length > MaxFileBytes) throw new ThemeTemplateException($"Over the {MaxFileBytes / (1024 * 1024)} MB limit.");

                theme = JsonSerializer.Deserialize<ThemeSettings>(File.ReadAllText(file));
            }
            catch (Exception ex) when (ex is not ThemeTemplateException)
            {
                throw new ThemeTemplateException($"Can't be read as theme JSON: {ex.Message}");
            }

            if (theme?.Layouts == null || theme.Layouts.Count == 0) throw new ThemeTemplateException("It has no layouts.");
            if (theme.Layouts.Any(layout => layout == null || layout.Layers == null)) throw new ThemeTemplateException("A layout has no layer list.");
            if (theme.Layouts.Any(layout => layout.Width <= 0 || layout.Height <= 0)) throw new ThemeTemplateException("A layout has a width or height of 0 or less.");
            if (theme.Layouts.Any(layout => layout.Layers.Any(entry => entry == null))) throw new ThemeTemplateException("A layout has an empty layer.");

            LogAssetWarnings(theme, Path.GetFileName(file), loadedTheme);
            return theme;
        }

        // Logs each image path set in a template once. They are allowed: a path into the zip finds no file, any other stays as stored.
        private static void LogAssetWarnings(ThemeSettings theme, string fileName, string loadedTheme)
        {
            foreach (string path in theme.Layouts.SelectMany(layout => layout.Layers).Select(entry => entry.ImagePath).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                bool insideZip = path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("assets\\", StringComparison.OrdinalIgnoreCase);
                string reason = insideZip ? "points into the zip, which has no assets" : "kept as stored, outside the theme until a Save Changes bundles it";
                LogOnce("TemplateWarning", fileName, loadedTheme, "warning", $"{path}: {reason}");
            }
        }

        // Writes a log line unless the same action, file and detail was already logged this session
        private static void LogOnce(string action, string fileName, string loadedTheme, string result, string detail)
        {
            if (LoggedLines.Add($"{action}|{fileName}|{detail}"))
            {
                ThemeLog.Write(action, fileName, "-", loadedTheme, result, detail);
            }
        }
    }
    // [END SECTION: Theme Templates]
}
