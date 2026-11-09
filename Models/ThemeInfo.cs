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
        // Where the theme is offered (a ThemeShowIn value). Null or unknown reads as Both; readers go through ThemeShowIn.Normalize.
        public string ShowIn { get; set; } = ThemeShowIn.Both;

        // Does an independent copy, for the restore point and the theme file's own rewritten copy
        public ThemeInfo Clone() => (ThemeInfo)MemberwiseClone();
    }
    // [END SECTION: Theme Info Block]

    // [SECTION: Theme Show In Values]
    // The stored values of ThemeInfo.ShowIn (text, like VisibilityMode, so a new option never shifts the others). The Theme
    // Builder's Load Theme combo is never filtered by it; the hamburger menu and, later, the Theme Browser are.
    public static class ThemeShowIn
    {
        public const string Both = "Both";
        public const string BrowserOnly = "BrowserOnly";
        public const string MenuOnly = "MenuOnly";
        public const string Neither = "Neither";

        // Does the read rule: one of the four values exactly (case-sensitive), otherwise Both
        public static string Normalize(string? value) =>
            value is Both or BrowserOnly or MenuOnly or Neither ? value : Both;

        // Does the hamburger menu check: true for Both and MenuOnly
        public static bool ShowsInHamburgerMenu(string? value) => Normalize(value) is Both or MenuOnly;

        // Does the Theme Browser check: true for Both and BrowserOnly (ThemeCatalogReader filters the catalog with it)
        public static bool ShowsInThemeBrowser(string? value) => Normalize(value) is Both or BrowserOnly;
    }
    // [END SECTION: Theme Show In Values]

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
