using System;

namespace ArcadeStick.Models
{
    // [SECTION: Theme Info Block]
    // The theme's descriptive metadata, kept as one block on both ThemeSettings (theme.json) and ConfigurationSettings
    // (settings.json), so the Theme Browser can later read just this block. Text may be empty; null ModifiedUtc means
    // never saved with Save Changes. ThumbnailPath is relative to BaseDirectory (assets/... inside a theme file).
    public class ThemeInfo
    {
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime? ModifiedUtc { get; set; }
        public string ThumbnailPath { get; set; } = "";

        // Does an independent copy, for the restore point and the theme file's own rewritten copy
        public ThemeInfo Clone() => (ThemeInfo)MemberwiseClone();
    }
    // [END SECTION: Theme Info Block]

    // [SECTION: Theme Info Fallback Text]
    // The text the theme info display controls show when a value is blank. Edit it only here.
    public static class ThemeInfoFallbacks
    {
        public const string NoImage = "No Image";
        public const string NoDescription = "No description";
        public const string NotSavedYet = "Not saved yet";
    }
    // [END SECTION: Theme Info Fallback Text]
}
