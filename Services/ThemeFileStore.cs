using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace ArcadeStick.Services
{
    // A theme save or delete that stopped. The message is written for the user and says what state things were left in.
    public class ThemeFileException : Exception
    {
        public ThemeFileException(string message) : base(message) { }
    }

    // [SECTION: Theme File Store]
    // The disk side of a safe save and delete: nothing is replaced or removed until its replacement is confirmed good. Helper
    // files stay out of the theme lists, which read only *.zip directly in Themes: <name>.zip.tmp-<guid>, <name>.zip.bak and
    // the Themes\Deleted folder.
    public static class ThemeFileStore
    {
        public const string DeletedFolderName = "Deleted";

        private const int ReplaceAttempts = 5;
        private const int ReplaceRetryDelayMs = 200;
        private static readonly TimeSpan StaleTempAge = TimeSpan.FromHours(1);

        // Only names ThemePaths.TempZip creates
        private static readonly Regex TempZipPattern = new(@"^.+\.zip\.tmp-[0-9a-f]{32}$", RegexOptions.CultureInvariant);

        // Deletes this feature's own temp zips older than an hour, directly in the given folder only. Ignores errors.
        public static void CleanStaleTempZips(string themesDir)
        {
            try
            {
                if (!Directory.Exists(themesDir)) return;

                foreach (string file in Directory.GetFiles(themesDir))
                {
                    if (!TempZipPattern.IsMatch(Path.GetFileName(file))) continue;
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < StaleTempAge) continue;

                    try { File.Delete(file); } catch { }
                }
            }
            catch
            {
            }
        }

        // Throws unless the zip opens, its theme.json parses and it holds every file in the staging folder at the same size
        public static void VerifyZip(string zipPath, string stagingDir)
        {
            using var zip = ZipFile.OpenRead(zipPath);

            var themeEntry = zip.GetEntry("theme.json") ?? throw new InvalidDataException("The new zip has no theme.json.");
            using (var themeStream = themeEntry.Open())
            using (JsonDocument.Parse(themeStream))
            {
            }

            foreach (string file in Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories))
            {
                string entryName = Path.GetRelativePath(stagingDir, file).Replace(Path.DirectorySeparatorChar, '/');
                var entry = zip.GetEntry(entryName) ?? throw new InvalidDataException($"The new zip is missing {entryName}.");
                if (entry.Length != new FileInfo(file).Length) throw new InvalidDataException($"The new zip's copy of {entryName} is the wrong size.");
            }
        }

        // Puts the verified temp zip in place. An existing zip becomes the backup (replacing the previous one). On a
        // failure the old zip stays or is put back; if even that fails the message says how to recover by hand.
        public static void ReplaceZip(string tempZip, string themeFile, string backup)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(themeFile)) File.Replace(tempZip, themeFile, backup, ignoreMetadataErrors: true);
                    else File.Move(tempZip, themeFile);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Windows can stop a replace after it has moved the old zip to the backup name
                    if (!File.Exists(themeFile) && File.Exists(backup))
                    {
                        try
                        {
                            File.Move(backup, themeFile);
                        }
                        catch (Exception restoreEx) when (restoreEx is IOException or UnauthorizedAccessException)
                        {
                            throw new ThemeFileException($"Couldn't save the theme, and the previous version could not be put back. To recover by hand, rename this file:\n{backup}\n\nback to:\n{themeFile}\n\nDetails: {restoreEx.Message}");
                        }
                    }

                    if (attempt >= ReplaceAttempts) throw;
                    Thread.Sleep(ReplaceRetryDelayMs);
                }
            }
        }

        // Removes a theme recoverably: the working folder is renamed aside (refused while anything in it is locked), the zip
        // moves to Themes\Deleted, and only then is the renamed folder deleted. A failure leaves or puts back both.
        public static void DeleteTheme(string themeFile, string extractDir, string stagingRoot, string deletedDir, string themeName)
        {
            // Worked out first: a refused name must stop this before the folder moves
            string? deletedZip = File.Exists(themeFile) ? NewDeletedZipPath(deletedDir, themeName) : null;

            string lockedMessage = $"Couldn't delete '{themeName}'. Another program (for example an image editor, or an open File Explorer window) may be using its folder. Close it and try again. Nothing was deleted.";

            string? asideDir = null;
            if (Directory.Exists(extractDir))
            {
                asideDir = ThemeFolderSwap.NewDeletedPath(stagingRoot, themeName);
                try
                {
                    ThemeFolderSwap.MoveFolder(extractDir, asideDir);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new ThemeFileException($"{lockedMessage}\n\nDetails: {ex.Message}");
                }
            }

            try
            {
                if (deletedZip != null)
                {
                    Directory.CreateDirectory(deletedDir);
                    File.Move(themeFile, deletedZip);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (asideDir != null)
                {
                    try
                    {
                        ThemeFolderSwap.MoveFolder(asideDir, extractDir);
                    }
                    catch (Exception rollbackEx) when (rollbackEx is IOException or UnauthorizedAccessException)
                    {
                        ThemeLog.Write("SwapRollback", themeName, "-", "-", "failed", rollbackEx.Message);
                        throw new ThemeFileException($"Couldn't delete '{themeName}', and its working folder could not be put back. The theme's zip was not deleted.\n\nTo recover by hand, rename this folder:\n{asideDir}\n\nback to:\n{extractDir}\n\nDetails: {ex.Message}\n{rollbackEx.Message}");
                    }
                }

                throw new ThemeFileException($"Couldn't delete '{themeName}'. Another program may be using its zip. Close it and try again. Nothing was deleted.\n\nDetails: {ex.Message}");
            }

            if (asideDir != null) ThemeFolderSwap.TryDeleteFolder(asideDir);
        }

        // Returns an unused name for a deleted theme's zip, so deleting a theme of the same name twice keeps both
        private static string NewDeletedZipPath(string deletedDir, string themeName)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string path = ThemePaths.DeletedZip(deletedDir, themeName, stamp, 0);
            for (int counter = 1; File.Exists(path); counter++)
            {
                path = ThemePaths.DeletedZip(deletedDir, themeName, stamp, counter);
            }
            return path;
        }
    }
    // [END SECTION: Theme File Store]
}
