using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    public class CacheScannerService
    {
        private readonly ConfigurationSettings _settings;

        // Bumped whenever the cache schema gains/changes fields. A mismatch forces regeneration even
        // when ROM-set drift detection sees no change, so existing users' stale-format caches don't
        // silently miss new fields. v2 adds Year and Manufacturer.
        private const int CurrentSchemaVersion = 2;

        // [SECTION: Lifecycle & Dependency Injection]
        // Stores the shared ConfigurationSettings instance used to locate the MAME cache file.
        public CacheScannerService(ConfigurationSettings settings)
        {
            _settings = settings;
        }
        // [END SECTION: Lifecycle & Dependency Injection]

        // JSON-serializable cache DTOs - internal to this service, since nothing else touches the file
        // format directly; consumers only ever see the resulting GameItem map.
        private class MameCacheFile
        {
            public int SchemaVersion { get; set; }
            public Dictionary<string, MameCacheEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private class MameCacheEntry
        {
            public string Description { get; set; } = string.Empty;
            public string CloneOf { get; set; } = string.Empty;
            public int Players { get; set; }
            public string Year { get; set; } = string.Empty;
            public string Manufacturer { get; set; } = string.Empty;
        }

        // Resolves mame_cache.json's path alongside sort_database.ini, under the app's own config
        // directory rather than the MAME install folder - this is our generated artifact, not MAME's.
        private string GetCacheFilePath()
        {
            return Path.Combine(_settings.GetConfigPath(), "database", "mame_cache.json");
        }

        // [SECTION: Asynchronous MAME Cache Parser Engine]
        // Reads mame_cache.json to build a RomName -> GameItem map, filtered against discoveredZipNames
        // so only physically present ROMs are included. Falls back to a bare-bones map (rom name as
        // title) if the cache file doesn't exist yet or fails a schema-version check.
        public async Task<Dictionary<string, GameItem>> ParseCacheFileAsync(HashSet<string> discoveredZipNames, Dictionary<string, string> folderMap)
        {
            var databaseMap = new Dictionary<string, GameItem>(StringComparer.OrdinalIgnoreCase);
            string cachePath = GetCacheFilePath();

            MameCacheFile? cacheFile = null;
            if (File.Exists(cachePath))
            {
                try
                {
                    using var fileStream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    cacheFile = await JsonSerializer.DeserializeAsync<MameCacheFile>(fileStream);
                }
                catch
                {
                    cacheFile = null;
                }
            }

            if (cacheFile == null || cacheFile.SchemaVersion != CurrentSchemaVersion)
            {
                // Fallback mechanism to populate map from discovered physical assets if cache is missing
                // or from an outdated schema version - caller's drift-detection path is responsible for
                // triggering GenerateCacheFileAsync() in this scenario.
                foreach (var shortName in discoveredZipNames)
                {
                    folderMap.TryGetValue(shortName, out string? assignedFolder);
                    databaseMap[shortName] = new GameItem
                    {
                        RomName = shortName,
                        FullTitle = shortName.ToUpper(),
                        FolderPath = assignedFolder ?? "roms"
                    };
                }
                return databaseMap;
            }

            // Regex pattern to target any parenthetical blocks along with their leading whitespace.
            // Greedy .* (not [^)]*) so nested parens - e.g. "(bootleg (V2.0))" - match through to the
            // LAST closing paren instead of stopping at the first one and leaving a trailing ")" behind.
            var parentheticalMatcher = new Regex(@"\s*\(.*\)", RegexOptions.Compiled);

            foreach (var kvp in cacheFile.Entries)
            {
                string shortName = kvp.Key.ToLowerInvariant();

                // Enforce intersection check to ensure listing only includes verified physical zip archives
                if (!discoveredZipNames.Contains(shortName))
                    continue;

                folderMap.TryGetValue(shortName, out string? assignedFolder);

                string rawTitle = kvp.Value.Description;
                string rawParenthetical = string.Join(" ", parentheticalMatcher.Matches(rawTitle).Select(m => m.Value.Trim()));
                string cleanTitle = parentheticalMatcher.Replace(rawTitle, "").Trim();

                databaseMap[shortName] = new GameItem
                {
                    RomName = shortName,
                    FullTitle = cleanTitle,
                    FolderPath = assignedFolder ?? "roms",
                    RawParentheticalInfo = rawParenthetical,
                    CloneOf = kvp.Value.CloneOf,
                    Players = kvp.Value.Players,
                    Year = kvp.Value.Year,
                    Manufacturer = kvp.Value.Manufacturer
                };
            }

            return databaseMap;
        }
        // [END SECTION: Asynchronous MAME Cache Parser Engine]

        // [SECTION: Unfiltered RomName -> Parent Map]
        // Reads mame_cache.json WITHOUT filtering against discoveredZipNames - used by HistoryXmlService's
        // stub-resolution lineage index, which needs to know a ROM's true MAME parent even when that
        // parent (or a sibling regional release) isn't physically present in the user's romset. The
        // regular ParseCacheFileAsync intentionally filters to owned ROMs for GamesCollection/the tree
        // view; this is a separate, narrower read for exactly this one cross-referencing need.
        public async Task<Dictionary<string, string>> GetFullRomToParentMapAsync()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string cachePath = GetCacheFilePath();

            if (!File.Exists(cachePath)) return result;

            MameCacheFile? cacheFile;
            try
            {
                using var fileStream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                cacheFile = await JsonSerializer.DeserializeAsync<MameCacheFile>(fileStream);
            }
            catch
            {
                return result;
            }

            if (cacheFile == null || cacheFile.SchemaVersion != CurrentSchemaVersion) return result;

            foreach (var kvp in cacheFile.Entries)
            {
                string romName = kvp.Key;
                string parent = string.IsNullOrEmpty(kvp.Value.CloneOf) ? romName : kvp.Value.CloneOf;
                result[romName] = parent;
            }

            return result;
        }
        // [END SECTION: Unfiltered RomName -> Parent Map]

        // [SECTION: MAME Cache File Generator]
        // Invokes mame.exe -listxml and stream-parses the XML output directly (rather than buffering the
        // full output into a string first - listxml's output is far larger than listfull's) into a
        // reduced MameCacheFile, then writes that as mame_cache.json. Only the fields this app currently
        // uses are extracted; -listxml carries several more, still reserved for later.
        public async Task GenerateCacheFileAsync()
        {
            string mamePath = _settings.GetMamePath();
            string exePath = Path.Combine(mamePath, "mame.exe");
            string cachePath = GetCacheFilePath();

            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "-listxml",
                WorkingDirectory = mamePath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var cacheFile = new MameCacheFile { SchemaVersion = CurrentSchemaVersion };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    return;
                }

                using (var xmlReader = XmlReader.Create(process.StandardOutput, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Ignore }))
                {
                    while (await xmlReader.ReadAsync())
                    {
                        if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "machine")
                        {
                            string romName = xmlReader.GetAttribute("name") ?? string.Empty;
                            string cloneOf = xmlReader.GetAttribute("cloneof") ?? string.Empty;
                            if (string.IsNullOrEmpty(romName)) continue;

                            string description = string.Empty;
                            int players = 0;
                            string year = string.Empty;
                            string manufacturer = string.Empty;

                            using (var machineReader = xmlReader.ReadSubtree())
                            {
                                while (await machineReader.ReadAsync())
                                {
                                    if (machineReader.NodeType == XmlNodeType.Element && machineReader.Name == "description")
                                    {
                                        description = await machineReader.ReadElementContentAsStringAsync();
                                    }
                                    else if (machineReader.NodeType == XmlNodeType.Element && machineReader.Name == "input")
                                    {
                                        string playersAttr = machineReader.GetAttribute("players") ?? "0";
                                        int.TryParse(playersAttr, out players);
                                    }
                                    else if (machineReader.NodeType == XmlNodeType.Element && machineReader.Name == "year")
                                    {
                                        year = await machineReader.ReadElementContentAsStringAsync();
                                    }
                                    else if (machineReader.NodeType == XmlNodeType.Element && machineReader.Name == "manufacturer")
                                    {
                                        manufacturer = await machineReader.ReadElementContentAsStringAsync();
                                    }
                                }
                            }

                            cacheFile.Entries[romName] = new MameCacheEntry
                            {
                                Description = description,
                                CloneOf = cloneOf,
                                Players = players,
                                Year = year,
                                Manufacturer = manufacturer
                            };
                        }
                    }
                }

                await process.WaitForExitAsync();
            }

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            using (var outStream = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(outStream, cacheFile, jsonOptions);
            }
        }
        // [END SECTION: MAME Cache File Generator]
    }
}