using ArcadeStick.Models;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace ArcadeStick.Services
{
    // [SECTION: Theme Thumbnail Store]
    // Disk side of the theme thumbnail: a captured image stays in memory (ConfigurationSettings.PendingThemeThumbnail)
    // until Save Adjustments or Save Changes writes it to Themes/Thumbnails/<guid>/theme-thumbnail.png. Not under
    // Themes/Extracted, which LoadTheme deletes. The folder's GUID is the theme's only unique ID.
    public static class ThemeThumbnailStore
    {
        public const string FileName = "theme-thumbnail.png";
        private const string FolderName = "Thumbnails";

        // Writes the pending capture (if any) and points the live ThumbnailPath at it. Retakes overwrite the same file:
        // the GUID comes from the current ThumbnailPath's folder when it has one, otherwise a new one is made.
        // The pending image is kept; the caller clears it once its whole save has succeeded.
        public static void WritePending(ConfigurationSettings settings)
        {
            if (settings.PendingThemeThumbnail is not BitmapSource pending) return;

            string folder = Path.Combine(settings.GetArcadeStickFilesPath(), "Themes", FolderName, GetGuidFolderName(settings.Info.ThumbnailPath));
            Directory.CreateDirectory(folder);
            string filePath = Path.Combine(folder, FileName);

            WritePng(pending, filePath);

            settings.Info.ThumbnailPath = Path.GetRelativePath(settings.BaseDirectory, filePath);
        }

        // Writes an image as a png file. Save Changes uses it to bundle a pending capture before anything live changes.
        public static void WritePng(BitmapSource image, string filePath)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(stream);
        }

        // Returns the GUID folder name of a stored thumbnail path, or a new GUID if the path is not in the Thumbnails folder
        private static string GetGuidFolderName(string storedPath)
        {
            if (!string.IsNullOrWhiteSpace(storedPath))
            {
                var guidFolder = Directory.GetParent(storedPath);
                if (guidFolder != null && guidFolder.Parent?.Name == FolderName && Guid.TryParse(guidFolder.Name, out _))
                {
                    return guidFolder.Name;
                }
            }
            return Guid.NewGuid().ToString("N");
        }

        // Deletes every GUID folder directly inside the Thumbnails folder except the ones the live info block and the
        // Theme Builder's restore point use. Call only after a successful save has moved the restore point. Fails
        // silently, folder by folder: a locked file must never break a save, and the next save tries again.
        public static void RemoveUnusedFolders(ConfigurationSettings settings, ThemeInfo restorePoint)
        {
            try
            {
                string root = Path.Combine(settings.GetArcadeStickFilesPath(), "Themes", FolderName);
                if (!Directory.Exists(root)) return;

                string liveFolder = GetFolderName(settings.Info.ThumbnailPath);
                string restoreFolder = GetFolderName(restorePoint.ThumbnailPath);

                foreach (string folder in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(folder);
                    if (!Guid.TryParse(name, out _)) continue;
                    if (string.Equals(name, liveFolder, StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(name, restoreFolder, StringComparison.OrdinalIgnoreCase)) continue;

                    try { Directory.Delete(folder, recursive: true); } catch { }
                }
            }
            catch
            {
            }
        }

        // Returns the name of the folder holding a stored thumbnail path; empty when blank. A path into Themes/Extracted
        // gives "assets", which never matches a GUID folder.
        private static string GetFolderName(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return string.Empty;
            return Path.GetFileName(Path.GetDirectoryName(storedPath)) ?? string.Empty;
        }

        // Resolves a stored thumbnail path to an absolute path; empty when blank
        public static string ResolvePath(ConfigurationSettings settings, string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return string.Empty;
            return Path.IsPathRooted(storedPath) ? storedPath : Path.Combine(settings.BaseDirectory, storedPath);
        }
    }
    // [END SECTION: Theme Thumbnail Store]
}
