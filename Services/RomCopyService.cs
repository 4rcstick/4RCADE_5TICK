using System;
using System.IO;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    public class RomCopyService
    {
        private readonly ConfigurationSettings _settings;

        // [SECTION: Lifecycle & Dependency Injection]
        public RomCopyService(ConfigurationSettings settings)
        {
            _settings = settings;
        }
        // [END SECTION: Lifecycle & Dependency Injection]

        // [SECTION: Active ROMs Root Resolver]
        // Mirrors InitializeDatabaseAsync's storage_options.cfg override check exactly, so the copy
        // source always matches whatever roms path the currently-loaded GamesCollection was actually
        // scanned from - not just the compiled default.
        private string GetActiveRomsPath()
        {
            string activeRomsPath = Path.Combine(_settings.GetMamePath(), _settings.RomsSubFolder);
            string storageFile = Path.Combine(_settings.GetConfigPath(), _settings.StorageOptionsFile);

            if (File.Exists(storageFile))
            {
                foreach (string line in File.ReadAllLines(storageFile))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("roms_path=", StringComparison.OrdinalIgnoreCase))
                    {
                        string rawPath = trimmed.Substring("roms_path=".Length).Trim();
                        activeRomsPath = Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(_settings.BaseDirectory, rawPath);
                    }
                }
            }

            return activeRomsPath;
        }
        // [END SECTION: Active ROMs Root Resolver]

        // [SECTION: ROM Copy-To-Disk Operation]
        // Resolves the source zip via GameItem.FolderPath (same "roms" sentinel meaning "ungrouped,
        // lives directly in the roms root" that UpdateLiveTreeDisplay already uses), then copies it to
        // the user-configured destination, overwriting silently if it already exists there. Returns a
        // (Success, Message) pair for the caller to show in the copy-status tooltip.
        public (bool Success, string Message) CopyRomToDestination(GameItem game)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.RomName))
            {
                return (false, "No game selected.");
            }

            if (string.IsNullOrWhiteSpace(_settings.RomCopyDestinationPath))
            {
                return (false, "No destination set (Shift+Ctrl+X).");
            }

            try
            {
                string activeRomsPath = GetActiveRomsPath();

                string folderPath = (game.FolderPath ?? string.Empty).Trim();
                bool isUngrouped = string.Equals(folderPath, "roms", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(folderPath);
                string sourceFolder = isUngrouped ? activeRomsPath : Path.Combine(activeRomsPath, folderPath);

                string sourceFile = Path.Combine(sourceFolder, $"{game.RomName}.zip");

                if (!File.Exists(sourceFile))
                {
                    return (false, $"ROM file not found: {game.RomName}.zip");
                }

                Directory.CreateDirectory(_settings.RomCopyDestinationPath);

                string destinationFile = Path.Combine(_settings.RomCopyDestinationPath, $"{game.RomName}.zip");
                File.Copy(sourceFile, destinationFile, overwrite: true);

                return (true, $"Copied '{game.RomName}.zip' to destination.");
            }
            catch (Exception ex)
            {
                return (false, $"Copy failed: {ex.Message}");
            }
        }
        // [END SECTION: ROM Copy-To-Disk Operation]

        // [SECTION: ROM Move-To-Disk Operation]
        // Identical source resolution and destination handling as CopyRomToDestination above - same
        // "roms" sentinel check, same silent-overwrite behavior - but relocates the file instead of
        // duplicating it. Mirrors that method deliberately rather than sharing a private helper, since
        // the two are simple enough that a shared helper would add more indirection than it saves.
        public (bool Success, string Message) MoveRomToDestination(GameItem game)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.RomName))
            {
                return (false, "No game selected.");
            }

            if (string.IsNullOrWhiteSpace(_settings.RomCopyDestinationPath))
            {
                return (false, "No destination set (Shift+Ctrl+V).");
            }

            try
            {
                string activeRomsPath = GetActiveRomsPath();

                string folderPath = (game.FolderPath ?? string.Empty).Trim();
                bool isUngrouped = string.Equals(folderPath, "roms", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(folderPath);
                string sourceFolder = isUngrouped ? activeRomsPath : Path.Combine(activeRomsPath, folderPath);

                string sourceFile = Path.Combine(sourceFolder, $"{game.RomName}.zip");

                if (!File.Exists(sourceFile))
                {
                    return (false, $"ROM file not found: {game.RomName}.zip");
                }

                Directory.CreateDirectory(_settings.RomCopyDestinationPath);

                string destinationFile = Path.Combine(_settings.RomCopyDestinationPath, $"{game.RomName}.zip");
                File.Move(sourceFile, destinationFile, overwrite: true);

                return (true, $"Moved '{game.RomName}.zip' to destination.");
            }
            catch (Exception ex)
            {
                return (false, $"Move failed: {ex.Message}");
            }
        }
        // [END SECTION: ROM Move-To-Disk Operation]
    }
}