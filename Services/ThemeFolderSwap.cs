using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace ArcadeStick.Services
{
    // A theme load that stopped. The message is written for the user and says what state things were left in.
    public class ThemeLoadException : Exception
    {
        public ThemeLoadException(string message) : base(message) { }
    }

    // [SECTION: Theme Folder Swap]
    // The disk side of a safe theme load: a theme is extracted into Themes\_staging first, and only replaces its
    // Themes\Extracted\<name> folder through two folder renames, so a locked folder or a bad zip leaves the working copy
    // whole. Nothing lists folders in Themes (the theme lists read *.zip files only), so _staging never shows up as a theme.
    public static class ThemeFolderSwap
    {
        public const string StagingFolderName = "_staging";

        private const string LoadingMarker = "loading";
        private const string OldMarker = "old";
        private const string DeletedMarker = "deleted";
        private const int MoveAttempts = 5;
        private const int MoveRetryDelayMs = 200;

        // Only names this class creates: <theme>.loading-<32 hex>, <theme>.old-<32 hex> or <theme>.deleted-<32 hex>
        private static readonly Regex StagingNamePattern = new(
            $@"^(?<theme>.+)\.(?<kind>{LoadingMarker}|{OldMarker}|{DeletedMarker})-[0-9a-f]{{32}}$",
            RegexOptions.CultureInvariant);

        // Returns an unused path in the staging folder for a theme's new extraction, creating the staging folder if needed
        public static string NewStagingPath(string stagingRoot, string themeName)
        {
            string path = ThemePaths.StagingFolder(stagingRoot, themeName, LoadingMarker);
            Directory.CreateDirectory(stagingRoot);
            return path;
        }

        // Returns an unused path in the staging folder for a theme's working folder while it is being deleted
        public static string NewDeletedPath(string stagingRoot, string themeName)
        {
            string path = ThemePaths.StagingFolder(stagingRoot, themeName, DeletedMarker);
            Directory.CreateDirectory(stagingRoot);
            return path;
        }

        // Deletes leftovers from earlier loads and deletes: only direct children of the staging folder with this class's exact names.
        // An .old folder is kept while its theme has no Extracted folder, since a failed rollback left it as the only working copy.
        public static void CleanStaleFolders(string stagingRoot, string extractedRoot)
        {
            try
            {
                if (!Directory.Exists(stagingRoot)) return;

                foreach (string folder in Directory.GetDirectories(stagingRoot))
                {
                    Match match = StagingNamePattern.Match(Path.GetFileName(folder));
                    if (!match.Success) continue;

                    bool isOld = match.Groups["kind"].Value == OldMarker;
                    if (isOld && !Directory.Exists(Path.Combine(extractedRoot, match.Groups["theme"].Value))) continue;

                    TryDeleteFolder(folder);
                }
            }
            catch
            {
            }
        }

        // Puts the staged folder in place as the live folder. The live folder is renamed aside first, which Windows refuses
        // while anything inside it is locked, so that failure changes nothing. If the second rename fails the live folder is
        // renamed back. The old copy is deleted only after the swap, and a failed delete is left for CleanStaleFolders.
        public static void Swap(string stagedDir, string liveDir, string stagingRoot, string themeName)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(liveDir) ?? throw new InvalidOperationException($"Invalid theme path: {liveDir}"));

            string? asideDir = null;
            if (Directory.Exists(liveDir))
            {
                asideDir = ThemePaths.StagingFolder(stagingRoot, themeName, OldMarker);
                try
                {
                    MoveFolder(liveDir, asideDir);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new ThemeLoadException(
                        $"Couldn't load '{themeName}'. Another program (for example an image editor like Photoshop) may be using that theme's folder. Close it and try again. Nothing was changed.\n\nDetails: {ex.Message}");
                }
            }

            try
            {
                MoveFolder(stagedDir, liveDir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (asideDir == null)
                {
                    throw new ThemeLoadException($"Couldn't load '{themeName}'. Its folder could not be created. Nothing was changed.\n\nDetails: {ex.Message}");
                }

                try
                {
                    MoveFolder(asideDir, liveDir);
                }
                catch (Exception rollbackEx) when (rollbackEx is IOException or UnauthorizedAccessException)
                {
                    ThemeLog.Write("SwapRollback", themeName, "-", "-", "failed", rollbackEx.Message);
                    throw new ThemeLoadException(
                        $"Couldn't load '{themeName}', and its working folder could not be put back. The theme's zip is untouched.\n\nTo recover by hand, rename this folder:\n{asideDir}\n\nback to:\n{liveDir}\n\nDetails: {ex.Message}\n{rollbackEx.Message}");
                }

                ThemeLog.Write("SwapRollback", themeName, "-", "-", "ok", ex.Message);
                throw new ThemeLoadException($"Couldn't load '{themeName}'. The new copy could not be put in place, so the old one was put back. Nothing was changed.\n\nDetails: {ex.Message}");
            }

            if (asideDir != null) TryDeleteFolder(asideDir);
        }

        // Deletes a folder, ignoring any failure
        public static void TryDeleteFolder(string folder)
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
            catch
            {
            }
        }

        // Renames a folder, retrying briefly because a video player or virus scanner can hold a file for a moment after release
        public static void MoveFolder(string source, string destination)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    Directory.Move(source, destination);
                    return;
                }
                catch (Exception ex) when (attempt < MoveAttempts && ex is IOException or UnauthorizedAccessException)
                {
                    Thread.Sleep(MoveRetryDelayMs);
                }
            }
        }
    }
    // [END SECTION: Theme Folder Swap]
}
