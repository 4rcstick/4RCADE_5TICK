using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace ArcadeStick.Models
{
    // [SECTION: Theme Catalog Entry]
    // One valid theme in the Theme Browser catalog, read from its saved zip only (never from unsaved working state).
    // Immutable and shared by the catalog cache: an unchanged zip returns the same instance on every refresh.
    public sealed class ThemeCatalogEntry
    {
        // The zip file name without .zip (the theme's identity and display name)
        public string Name { get; init; } = "";
        public string ZipPath { get; init; } = "";
        // Trimmed text from Info; empty when not set
        public string Author { get; init; } = "";
        public string Description { get; init; } = "";
        // Info.ShowIn, normalized through ThemeShowIn.Normalize
        public string ShowIn { get; init; } = ThemeShowIn.Both;
        // Every size in Layouts, de-duplicated and ordered by width then height; empty for an older theme with no layouts
        public IReadOnlyList<(int Width, int Height)> Sizes { get; init; } = Array.Empty<(int Width, int Height)>();
        // The thumbnail decoded at tile size and frozen; null when the zip has no usable thumbnail
        public BitmapSource? Tile { get; init; }
        // The cache key parts (with the path)
        public DateTime LastWriteUtc { get; init; }
        public long Length { get; init; }
    }
    // [END SECTION: Theme Catalog Entry]
}
