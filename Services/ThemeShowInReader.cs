using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using ArcadeStick.Models;

namespace ArcadeStick.Services
{
    // [SECTION: Theme Show In Reader]
    // Reads a saved theme's Show Theme In value straight from its zip (only the theme file entry, nothing extracted), for
    // the lists that filter themes by it. Only Save Changes writes the zip, so an unsaved change is not seen here.
    public static class ThemeShowInReader
    {
        // Does the read: theme.json (or theme.cfg in older themes, names matched in any case), then Info.ShowIn, normalized.
        // Any failure (a damaged or locked zip, no theme file, bad JSON) gives Both, because the theme must still be listed.
        // It never throws and never writes.
        public static string Read(string zipPath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(zipPath);
                var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals("theme.json", StringComparison.OrdinalIgnoreCase))
                    ?? zip.Entries.FirstOrDefault(e => e.FullName.Equals("theme.cfg", StringComparison.OrdinalIgnoreCase));
                if (entry == null) return ThemeShowIn.Both;

                using var stream = entry.Open();
                using var document = JsonDocument.Parse(stream);

                // Property names are PascalCase and case-sensitive, as the app writes them
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("Info", out JsonElement info)
                    && info.ValueKind == JsonValueKind.Object
                    && info.TryGetProperty("ShowIn", out JsonElement showIn)
                    && showIn.ValueKind == JsonValueKind.String)
                {
                    return ThemeShowIn.Normalize(showIn.GetString());
                }

                return ThemeShowIn.Both;
            }
            catch
            {
                return ThemeShowIn.Both;
            }
        }
    }
    // [END SECTION: Theme Show In Reader]
}
