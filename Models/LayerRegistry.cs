using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcadeStick.Models
{
    // One layer's geometry + visibility for a single resolution. Z is the draw order (higher = on top).
    public class LayerLayoutEntry
    {
        public string Id { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int Z { get; set; }
        public bool Visible { get; set; } = true;

        // Name is for custom image layers only (Id starts with LayerRegistry.CustomIdPrefix); built-in layers
        // leave it empty. ImagePath is used by custom image layers and by built-in layers that show a file
        // (Random Game Display). It follows the same convention as the theme asset paths: relative to the app
        // folder when the file is inside it, otherwise absolute.
        public string Name { get; set; } = "";
        public string ImagePath { get; set; } = "";
    }

    // All layer entries for one resolution. A theme file can hold several of these, keyed by Width/Height.
    public class ThemeLayout
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<LayerLayoutEntry> Layers { get; set; } = new();
    }

    // Static description of a layer: identity, display name, and its default rect/Z at the 1280x720 base.
    // IsPositionable=false means the layer is a fill (no rect editing); CanHide=false locks it visible.
    public class LayerDefinition
    {
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public double X { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
        public int Z { get; init; }
        public bool IsPositionable { get; init; } = true;
        public bool CanHide { get; init; } = true;

        // Non-empty = this layer is a part of a group (see LayerRegistry.AllGroups). Parts of one group
        // share a single Z and appear as one row in the Advanced tab's layer list.
        public string GroupId { get; init; } = "";

        // True = this built-in layer shows a file (image, gif/APNG or mp4) chosen on its edit page, stored in
        // the entry's ImagePath. Used by Random Game Display.
        public bool HasFile { get; init; } = false;
    }

    public record ResolutionPreset(int Width, int Height)
    {
        public override string ToString() => $"{Width} x {Height}";
    }

    // A group of layers shown as one row in the Advanced tab's layer list. PartIds are in the order the
    // parts appear on the group's edit page. All parts share the group's Z.
    public record LayerGroupDefinition(string Id, string DisplayName, string[] PartIds);

    // [SECTION: Layer Registry]
    // Single source of truth for the layer list. Adding a layer = adding one entry to AllLayers; the
    // Advanced tab list and default-layout generation both read from here. Registry order is the
    // tie-breaker when two layers share a Z value.
    // Base defaults below are derived from the current 1280x720 main window layout. Other presets are
    // proportionally scaled copies of these (starting points only - nothing scales at runtime).
    public static class LayerRegistry
    {
        public const int BaseWidth = 1280;
        public const int BaseHeight = 720;
        public const int DefaultWidth = 1280;
        public const int DefaultHeight = 720;

        // Layer Ids (used as keys in theme files - don't rename once released)
        public const string MainBackground = "MainBackground";
        public const string SearchLabel = "SearchLabel";
        public const string SearchBox = "SearchBox";
        public const string RandomButton = "RandomButton";
        public const string SortButton = "SortButton";
        public const string OptionsButton = "OptionsButton";
        public const string HamburgerButton = "HamburgerButton";
        public const string GamesList = "GamesList";
        public const string GamesListBg = "GamesListBg";
        public const string MarqueePanel = "MarqueePanel";
        public const string MediaPanel = "MediaPanel";
        public const string SplashImage = "SplashImage";
        public const string VideoPreview = "VideoPreview";
        public const string Footer = "Footer";
        public const string VideoConvertPrompt = "VideoConvertPrompt";
        public const string RandomGameDisplay = "RandomGameDisplay";
        public const string RandomGameDisplaySplash = "RandomGameDisplaySplash";
        public const string RandomGameGroup = "RandomGameGroup";

        // Group Ids (a group is one row in the Advanced tab's layer list - see AllGroups)
        public const string SearchAndButtonsGroup = "SearchAndButtons";
        public const string GameInfoGroup = "GameInfoGroup";

        // Parts of the Game Info group (the old single GameInfo and ImagePreviews layers are being replaced by these)
        public const string GameTitle = "GameTitle";
        public const string PublisherBlock = "PublisherBlock";
        public const string GameInfoNav = "GameInfoNav";
        public const string GameInfoBody = "GameInfoBody";
        public const string ImagePreviewsNav = "ImagePreviewsNav";
        public const string ImagePreviewsBody = "ImagePreviewsBody";

        // Each group's parts share one Z and are edited on one page, in the order listed here.
        public static readonly IReadOnlyList<LayerGroupDefinition> AllGroups = new List<LayerGroupDefinition>
        {
            new(SearchAndButtonsGroup, "Search & Buttons", new[] { SearchLabel, SearchBox, RandomButton, SortButton, OptionsButton, HamburgerButton }),
            new(GameInfoGroup, "Game Info", new[] { GameTitle, PublisherBlock, GameInfoNav, GameInfoBody, ImagePreviewsNav, ImagePreviewsBody }),
            new(RandomGameGroup, "Random Game Display", new[] { RandomGameDisplay, RandomGameDisplaySplash })
        };

        // Custom image layers: Ids look like "Custom_<guid>". At most 10 per resolution.
        public const string CustomIdPrefix = "Custom_";
        public const int MaxCustomLayersPerResolution = 10;
        public static bool IsCustomId(string id) => id.StartsWith(CustomIdPrefix, StringComparison.Ordinal);

        public static readonly IReadOnlyList<ResolutionPreset> ResolutionPresets = new List<ResolutionPreset>
        {
            new(1280, 720),
            new(1600, 900),
            new(1920, 1080),
            new(1024, 768),
            new(800, 600)
        };

        public static readonly IReadOnlyList<LayerDefinition> AllLayers = new List<LayerDefinition>
        {
            new() { Id = MainBackground, DisplayName = "Main Window Background", X = 0, Y = 0, Width = 1280, Height = 720, Z = 0, IsPositionable = false, CanHide = false },

            new() { Id = GamesListBg,    DisplayName = "Games List BG",     X = 20,  Y = 60,  Width = 353, Height = 640, Z = 50 },
            new() { Id = MediaPanel,     DisplayName = "Media Panel",       X = 393, Y = 177, Width = 867, Height = 523, Z = 30 },

            new() { Id = GamesList,      DisplayName = "Games List",        X = 25,  Y = 70,  Width = 343, Height = 625, Z = 51, CanHide = false },
            new() { Id = MarqueePanel,   DisplayName = "Marquee Panel",     X = 393, Y = 20,  Width = 867, Height = 137, Z = 40 },

            new() { Id = SplashImage,    DisplayName = "Splash Image",      X = 393, Y = 177, Width = 867, Height = 523, Z = 31 },

            new() { Id = VideoPreview,   DisplayName = "Video Preview",     X = 414, Y = 198, Width = 328, Height = 248, Z = 33 },
            

            new() { Id = GameTitle,        DisplayName = "Game Title",          X = 765, Y = 198, Width = 490, Height = 45,  Z = 34, GroupId = GameInfoGroup },
            new() { Id = PublisherBlock,   DisplayName = "Publisher Block",     X = 765, Y = 248, Width = 490, Height = 90,  Z = 34, GroupId = GameInfoGroup },
            new() { Id = GameInfoNav,      DisplayName = "Game Info Nav",       X = 765, Y = 348, Width = 490, Height = 40,  Z = 34, GroupId = GameInfoGroup },
            new() { Id = GameInfoBody,     DisplayName = "Game Info",           X = 765, Y = 398, Width = 490, Height = 298, Z = 34, GroupId = GameInfoGroup },
            new() { Id = ImagePreviewsNav, DisplayName = "Image Previews Nav",  X = 414, Y = 456, Width = 328, Height = 20,  Z = 34, GroupId = GameInfoGroup },
            new() { Id = ImagePreviewsBody,DisplayName = "Image Previews",      X = 414, Y = 486, Width = 328, Height = 198, Z = 34, GroupId = GameInfoGroup },

            new() { Id = SearchLabel,    DisplayName = "Search Label",      X = 20,  Y = 20,  Width = 67,  Height = 30, Z = 20, GroupId = SearchAndButtonsGroup },
            new() { Id = SearchBox,      DisplayName = "Search Box",        X = 87,  Y = 20,  Width = 142, Height = 30, Z = 20, GroupId = SearchAndButtonsGroup },
            new() { Id = RandomButton,   DisplayName = "Random Game Button",X = 235, Y = 20,  Width = 30,  Height = 30, Z = 20, GroupId = SearchAndButtonsGroup },
            new() { Id = SortButton,     DisplayName = "Sort/Filter Button",X = 271, Y = 20,  Width = 30,  Height = 30, Z = 20, GroupId = SearchAndButtonsGroup },
            new() { Id = OptionsButton,  DisplayName = "Options Button",    X = 307, Y = 20,  Width = 30,  Height = 30, Z = 20, GroupId = SearchAndButtonsGroup },
            new() { Id = HamburgerButton,DisplayName = "Hamburger Button",  X = 343, Y = 20,  Width = 30,  Height = 30, Z = 20, GroupId = SearchAndButtonsGroup },

            new() { Id = RandomGameDisplay,       DisplayName = "When a game is selected", X = 414, Y = 198, Width = 328, Height = 248, Z = 35, GroupId = RandomGameGroup },
            new() { Id = RandomGameDisplaySplash, DisplayName = "When on the splash page", X = 393, Y = 177, Width = 867, Height = 523, Z = 35, GroupId = RandomGameGroup },
            new() { Id = VideoConvertPrompt, DisplayName = "Video Convert Prompt", X = 393, Y = 701, Width = 450, Height = 18, Z = 11 },
            new() { Id = Footer,         DisplayName = "Footer",            X = 20,  Y = 700, Width = 1240,Height = 20, Z = 10 },
        };

        public static LayerDefinition? GetDefinition(string id) => AllLayers.FirstOrDefault(l => l.Id == id);

        // Builds a full default layout for a resolution: base rects scaled by width/height ratio, rounded
        // to whole pixels. At 1280x720 this returns the base rects unchanged.
        public static ThemeLayout BuildDefaultLayout(int width, int height)
        {
            double sx = (double)width / BaseWidth;
            double sy = (double)height / BaseHeight;

            var layout = new ThemeLayout { Width = width, Height = height };
            foreach (var def in AllLayers)
            {
                if (!def.IsPositionable)
                {
                    layout.Layers.Add(new LayerLayoutEntry { Id = def.Id, X = 0, Y = 0, Width = width, Height = height, Z = def.Z, Visible = true });
                    continue;
                }

                layout.Layers.Add(new LayerLayoutEntry
                {
                    Id = def.Id,
                    X = Math.Round(def.X * sx),
                    Y = Math.Round(def.Y * sy),
                    Width = Math.Round(def.Width * sx),
                    Height = Math.Round(def.Height * sy),
                    Z = def.Z,
                    Visible = true
                });
            }
            return layout;
        }

        // Returns a complete layout for the resolution: uses the saved layout's entries where present and
        // fills any missing layer (old theme, newly added layer) from defaults. A null saved layout yields
        // the full default layout. Locked-visible layers are forced visible.
        public static ThemeLayout ResolveLayout(ThemeLayout? saved, int width, int height)
        {
            var defaults = BuildDefaultLayout(width, height);
            if (saved == null) return defaults;

            var result = new ThemeLayout { Width = width, Height = height };
            foreach (var defEntry in defaults.Layers)
            {
                var savedEntry = saved.Layers.FirstOrDefault(e => e.Id == defEntry.Id);
                var entry = savedEntry ?? defEntry;

                var def = GetDefinition(entry.Id);
                if (def != null && !def.CanHide) entry.Visible = true;

                result.Layers.Add(entry);
            }

            // Custom image layers aren't in the registry, so carry them through from the saved layout
            foreach (var customEntry in saved.Layers.Where(e => IsCustomId(e.Id)))
            {
                result.Layers.Add(customEntry);
            }
            return result;
        }
    }
    // [END SECTION: Layer Registry]
}