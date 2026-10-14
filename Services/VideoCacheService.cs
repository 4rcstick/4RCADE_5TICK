using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // Persistent record of which preview videos have been probed as playable (or not), so the launcher
    // doesn't re-probe the whole library every launch. Stored as JSON in the config folder.
    // Entries are keyed by path relative to the videos folder (drive letters change on a portable install)
    // and carry the file's size and modified time, so a replaced or edited file is treated as unknown
    // until it is probed again. Bumping CurrentRulesVersion discards the whole cache (use it if the
    // definition of "playable" ever changes).
    public class VideoCacheService
    {
        private const int CurrentRulesVersion = 1;

        private class CacheFile
        {
            public int RulesVersion { get; set; }
            public Dictionary<string, VideoCacheEntry> Entries { get; set; } = new();
        }

        private class VideoCacheEntry
        {
            public long Size { get; set; }
            public long ModifiedUtcTicks { get; set; }
            public bool Playable { get; set; }
        }

        private readonly ConfigurationSettings _settings;
        private readonly object _lock = new object();
        private Dictionary<string, VideoCacheEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
        private bool _dirty;

        public VideoCacheService(ConfigurationSettings settings)
        {
            _settings = settings;
        }

        // Loads the cache from disk. A missing, corrupt, or old-rules-version file just starts empty.
        public void Load()
        {
            lock (_lock)
            {
                _entries = new Dictionary<string, VideoCacheEntry>(StringComparer.OrdinalIgnoreCase);
                _dirty = false;

                try
                {
                    string path = _settings.GetVideoCachePath();
                    if (!File.Exists(path)) return;

                    var file = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(path));
                    if (file == null || file.RulesVersion != CurrentRulesVersion || file.Entries == null) return;

                    foreach (var pair in file.Entries)
                    {
                        _entries[pair.Key] = pair.Value;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"VideoCache: load failed, starting empty: {ex.Message}");
                    _entries = new Dictionary<string, VideoCacheEntry>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        // Returns true/false if the file has a current cache entry, or null if unknown (never probed,
        // or the file's size or modified time no longer matches what was probed).
        public bool? TryGetStatus(string videoFolder, string filePath)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists) return null;

                string key = MakeKey(videoFolder, filePath);
                lock (_lock)
                {
                    if (_entries.TryGetValue(key, out var entry)
                        && entry.Size == info.Length
                        && entry.ModifiedUtcTicks == info.LastWriteTimeUtc.Ticks)
                    {
                        return entry.Playable;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoCache: lookup failed for {filePath}: {ex.Message}");
            }

            return null;
        }

        // Records a probe result for the file as it exists on disk right now.
        public void Set(string videoFolder, string filePath, bool playable)
        {
            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists) return;

                string key = MakeKey(videoFolder, filePath);
                lock (_lock)
                {
                    _entries[key] = new VideoCacheEntry
                    {
                        Size = info.Length,
                        ModifiedUtcTicks = info.LastWriteTimeUtc.Ticks,
                        Playable = playable
                    };
                    _dirty = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoCache: set failed for {filePath}: {ex.Message}");
            }
        }

        public void Remove(string videoFolder, string filePath)
        {
            string key = MakeKey(videoFolder, filePath);
            lock (_lock)
            {
                if (_entries.Remove(key)) _dirty = true;
            }
        }

        // Drops entries whose files no longer exist (deleted or moved to the archive folder).
        public void PruneMissing(string videoFolder)
        {
            lock (_lock)
            {
                var stale = new List<string>();
                foreach (string key in _entries.Keys)
                {
                    if (!File.Exists(Path.Combine(videoFolder, key))) stale.Add(key);
                }

                foreach (string key in stale) _entries.Remove(key);
                if (stale.Count > 0) _dirty = true;
            }
        }

        // Writes the cache to a temp file and renames it over the real one, so a crash or a closed
        // launcher mid-save can't leave a half-written cache. Does nothing if nothing changed.
        public void Save()
        {
            try
            {
                string json;
                lock (_lock)
                {
                    if (!_dirty) return;

                    var file = new CacheFile
                    {
                        RulesVersion = CurrentRulesVersion,
                        Entries = new Dictionary<string, VideoCacheEntry>(_entries, StringComparer.OrdinalIgnoreCase)
                    };
                    json = JsonSerializer.Serialize(file);
                    _dirty = false;
                }

                string path = _settings.GetVideoCachePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                string tempPath = path + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, path, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VideoCache: save failed: {ex.Message}");
                lock (_lock) { _dirty = true; }
            }
        }

        private static string MakeKey(string videoFolder, string filePath)
        {
            return Path.GetRelativePath(videoFolder, filePath).Replace('\\', '/');
        }
    }
}