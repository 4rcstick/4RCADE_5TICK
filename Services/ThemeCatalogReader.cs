using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows.Media.Imaging;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // What one catalog build did, for the Browser's temporary diagnostics line
    public sealed class ThemeCatalogStats
    {
        public int Read { get; set; }
        public int FromCache { get; set; }
        public int Skipped { get; set; }
        public int Hidden { get; set; }
    }

    // [SECTION: Theme Catalog Reader]
    // Builds the Theme Browser catalog from the *.zip files directly in the Themes folder, reading each zip's theme.json
    // (or legacy theme.cfg) and its bundled thumbnail without extracting anything. Saved-zip data only: never
    // Themes\Extracted or Themes\Thumbnails. Every zip is opened through one short-lived ZipFile.OpenRead and closed before
    // any decoding, because an open zip blocks Save's File.Replace and Delete's File.Move. Results are cached per
    // (path, write time, length), invalid zips included, and an invalid zip is logged once per file state (BrowserSkipped).
    // Runs on the UI thread. Never creates, changes or deletes a file.
    public static class ThemeCatalogReader
    {
        private const string LogAction = "BrowserSkipped";

        // One cached read: a valid entry, or the reason the zip was skipped
        private sealed class CachedRead
        {
            public DateTime WriteUtc { get; init; }
            public long Length { get; init; }
            public ThemeCatalogEntry? Entry { get; init; }
            public string? Problem { get; init; }
        }

        // The last read of each zip, keyed by full path (any case)
        private static readonly Dictionary<string, CachedRead> Cache = new(StringComparer.OrdinalIgnoreCase);
        // Skip lines already logged this session (path, write time and length), so refreshing never repeats them
        private static readonly HashSet<string> LoggedSkips = new(StringComparer.OrdinalIgnoreCase);

        // Returns the themes the Browser shows (valid, and Both or BrowserOnly for Show Theme In), sorted by name (ordinal,
        // ignore case), with the read statistics. A missing Themes folder gives an empty list. Never throws.
        public static List<ThemeCatalogEntry> BuildCatalog(ConfigurationSettings settings, out ThemeCatalogStats stats)
        {
            stats = new ThemeCatalogStats();
            var visible = new List<ThemeCatalogEntry>();

            string[] files;
            try
            {
                string themesDir = Path.Combine(settings.GetArcadeStickFilesPath(), "Themes");
                files = Directory.Exists(themesDir) ? Directory.GetFiles(themesDir, "*.zip") : Array.Empty<string>();
            }
            catch
            {
                files = Array.Empty<string>();
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                seen.Add(file);

                DateTime writeUtc;
                long length;
                try
                {
                    var info = new FileInfo(file);
                    writeUtc = info.LastWriteTimeUtc;
                    length = info.Length;
                }
                catch (Exception ex)
                {
                    stats.Skipped++;
                    LogSkipOnce(settings, file, $"{file}|unreadable", $"Can't be read: {ex.Message}");
                    continue;
                }

                if (Cache.TryGetValue(file, out var cached) && cached.WriteUtc == writeUtc && cached.Length == length)
                {
                    stats.FromCache++;
                }
                else
                {
                    stats.Read++;
                    var entry = ReadZip(file, writeUtc, length, out string? problem);
                    cached = new CachedRead { WriteUtc = writeUtc, Length = length, Entry = entry, Problem = problem };
                    Cache[file] = cached;
                }

                if (cached.Entry == null)
                {
                    stats.Skipped++;
                    LogSkipOnce(settings, file, $"{file}|{writeUtc.Ticks}|{length}", cached.Problem ?? "Unknown problem.");
                    continue;
                }

                if (!ThemeShowIn.ShowsInThemeBrowser(cached.Entry.ShowIn))
                {
                    stats.Hidden++;
                    continue;
                }

                visible.Add(cached.Entry);
            }

            // Forget zips that are gone, so the cache never grows past the folder
            foreach (string gone in Cache.Keys.Where(key => !seen.Contains(key)).ToList())
            {
                Cache.Remove(gone);
            }

            visible.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
            return visible;
        }

        // Does the read of one zip: the theme file's Info and layout sizes, and the thumbnail's bytes, all inside one using
        // that closes the zip before the tile is decoded. Returns null with the reason when the zip cannot be used.
        private static ThemeCatalogEntry? ReadZip(string zipPath, DateTime writeUtc, long length, out string? problem)
        {
            problem = null;
            string author = "";
            string description = "";
            string showIn = ThemeShowIn.Both;
            var sizes = new List<(int Width, int Height)>();
            byte[]? thumbnailBytes = null;

            try
            {
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    var themeEntry = FindEntry(zip, "theme.json") ?? FindEntry(zip, "theme.cfg");
                    if (themeEntry == null)
                    {
                        problem = "No theme.json or theme.cfg.";
                        return null;
                    }

                    string? storedThumbnail = null;
                    using (var themeStream = themeEntry.Open())
                    using (var document = JsonDocument.Parse(themeStream))
                    {
                        // Property names are PascalCase and case-sensitive, as the app writes them and LoadTheme reads them
                        var root = document.RootElement;
                        if (root.ValueKind != JsonValueKind.Object)
                        {
                            problem = "The theme file is not a JSON object.";
                            return null;
                        }

                        if (root.TryGetProperty("Info", out JsonElement info) && info.ValueKind == JsonValueKind.Object)
                        {
                            author = ReadString(info, "Author")?.Trim() ?? "";
                            description = ReadString(info, "Description")?.Trim() ?? "";
                            storedThumbnail = ReadString(info, "ThumbnailPath");
                            showIn = ThemeShowIn.Normalize(ReadString(info, "ShowIn"));
                        }

                        if (root.TryGetProperty("Layouts", out JsonElement layouts) && layouts.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement layout in layouts.EnumerateArray())
                            {
                                if (layout.ValueKind != JsonValueKind.Object) continue;
                                if (!layout.TryGetProperty("Width", out JsonElement w) || !w.TryGetInt32(out int width)) continue;
                                if (!layout.TryGetProperty("Height", out JsonElement h) || !h.TryGetInt32(out int height)) continue;
                                if (width > 0 && height > 0) sizes.Add((width, height));
                            }
                        }
                    }

                    // A thumbnail failure only costs the tile; the theme stays valid
                    var thumbnailEntry = FindThumbnailEntry(zip, storedThumbnail);
                    if (thumbnailEntry != null)
                    {
                        try
                        {
                            using var thumbnailStream = thumbnailEntry.Open();
                            using var buffer = new MemoryStream();
                            thumbnailStream.CopyTo(buffer);
                            thumbnailBytes = buffer.ToArray();
                        }
                        catch
                        {
                            thumbnailBytes = null;
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                problem = $"The theme file isn't valid JSON: {ex.Message}";
                return null;
            }
            catch (Exception ex)
            {
                problem = $"The zip can't be read: {ex.Message}";
                return null;
            }

            return new ThemeCatalogEntry
            {
                Name = Path.GetFileNameWithoutExtension(zipPath),
                ZipPath = zipPath,
                Author = author,
                Description = description,
                ShowIn = showIn,
                Sizes = sizes.Distinct().OrderBy(s => s.Width).ThenBy(s => s.Height).ToList(),
                Tile = thumbnailBytes != null ? DecodeTile(thumbnailBytes) : null,
                LastWriteUtc = writeUtc,
                Length = length
            };
        }

        // Returns a string property's value, or null when it is missing or not a string
        private static string? ReadString(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

        // Returns the entry with this name, matched in any case and with either slash, or null
        private static ZipArchiveEntry? FindEntry(ZipArchive zip, string name) =>
            zip.Entries.FirstOrDefault(entry => entry.FullName.Replace('\\', '/').Equals(name, StringComparison.OrdinalIgnoreCase));

        // Does the thumbnail lookup the way LoadTheme does: the stored path when it points into the zip (assets/ or assets\),
        // otherwise, or when that entry is missing, assets/theme-thumbnail.png. A rooted path or one with ".." segments is
        // never followed.
        private static ZipArchiveEntry? FindThumbnailEntry(ZipArchive zip, string? storedPath)
        {
            string? entryName = ToZipAssetPath(storedPath);
            return (entryName != null ? FindEntry(zip, entryName) : null)
                ?? FindEntry(zip, $"assets/{ThemeThumbnailStore.FileName}");
        }

        // Returns a stored path as a zip entry name (forward slashes) when it is a safe path inside the zip, otherwise null
        private static string? ToZipAssetPath(string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return null;

            bool insideZip = storedPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
                || storedPath.StartsWith("assets\\", StringComparison.OrdinalIgnoreCase);
            if (!insideZip || Path.IsPathRooted(storedPath)) return null;

            string entryName = storedPath.Replace('\\', '/');
            if (entryName.Split('/').Any(segment => segment == "..")) return null;
            return entryName;
        }

        // Does the tile decode from memory: reads the pixel size first, then decodes to fit inside the tile decode box with
        // one DecodePixel value (aspect kept, never upscaled), frozen. Null when the image can't be decoded.
        private static BitmapSource? DecodeTile(byte[] bytes)
        {
            try
            {
                int sourceWidth;
                int sourceHeight;
                using (var probe = new MemoryStream(bytes))
                {
                    var decoder = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    sourceWidth = decoder.Frames[0].PixelWidth;
                    sourceHeight = decoder.Frames[0].PixelHeight;
                }
                if (sourceWidth <= 0 || sourceHeight <= 0) return null;

                double widthFit = (double)ThemeThumbnailStore.BrowserTileDecodeMaxWidth / sourceWidth;
                double heightFit = (double)ThemeThumbnailStore.BrowserTileDecodeMaxHeight / sourceHeight;
                double scale = Math.Min(1.0, Math.Min(widthFit, heightFit));

                var bitmap = new BitmapImage();
                using var stream = new MemoryStream(bytes);
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (scale < 1.0)
                {
                    if (widthFit <= heightFit) bitmap.DecodePixelWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
                    else bitmap.DecodePixelHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
                }
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        // Writes one BrowserSkipped line per file state per session: the zip's file name, the active theme and the reason
        private static void LogSkipOnce(ConfigurationSettings settings, string zipPath, string key, string reason)
        {
            if (LoggedSkips.Add(key))
            {
                ThemeLog.Write(LogAction, Path.GetFileName(zipPath), "-", settings.ActiveThemeName, "skipped", reason);
            }
        }
    }
    // [END SECTION: Theme Catalog Reader]
}
