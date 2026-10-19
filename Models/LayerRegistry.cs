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

        // Games List styling, stored per resolution on the layer's entry. Null on every other layer and
        // left out of the theme file when null. LayerRegistry fills the Games List entry's values from
        // defaults when a saved entry doesn't have them yet.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? FolderFontSize { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? FolderColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? FolderSelectedColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? FolderSelectedBgColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? FolderFontFamily { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? FolderBold { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? ArrowColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? ArrowNudgeY { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? GameFontSize { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? GameColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? GameSelectedColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? GameSelectedBgColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? GameHoverColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? GameFontFamily { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? GameBold { get; set; }

        // Drop shadow on the folder text and on the game title text (and the mouse dot). Same rules as above:
        // null on every other layer, left out of the theme file when null, filled from defaults on the Games List.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? FolderShadowEnabled { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? FolderShadowColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? FolderShadowBlur { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? FolderShadowDepth { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? GameShadowEnabled { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? GameShadowColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? GameShadowBlur { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? GameShadowDepth { get; set; }

        // Drop shadow on the text of the selected row (folders and game titles). Same rules as above:
        // null on every other layer, left out of the theme file when null, filled from defaults on the Games List.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? FolderSelectedShadowEnabled { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? FolderSelectedShadowColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? FolderSelectedShadowBlur { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? FolderSelectedShadowDepth { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? GameSelectedShadowEnabled { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? GameSelectedShadowColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? GameSelectedShadowBlur { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? GameSelectedShadowDepth { get; set; }

        // Shape layer properties (Shape Layer 01 to 04). Null on every other layer and left out of the theme
        // file when null; LayerRegistry fills them from defaults on the shape layers.
        // VisibilityMode: "Always", "GameSelected", "NoGameSelected" or "Never".
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? VisibilityMode { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? BgColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? BorderSize { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? BorderColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? CornerRadius { get; set; }

        // Opacity of the layer's image or video, 0 to 100 percent (the fill and border keep their own alpha).
        // Null on every other layer and left out of the theme file when null.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? FileOpacity { get; set; }
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
        public const string RandomGameDisplay = "RandomGameDisplay";
        public const string RandomGameDisplaySplash = "RandomGameDisplaySplash";
        public const string RandomGameGroup = "RandomGameGroup";

        // Shape layers: four identical background/border layers (image or video, color with alpha, border).
        public const string ShapeLayer01 = "ShapeLayer01";
        public const string ShapeLayer02 = "ShapeLayer02";
        public const string ShapeLayer03 = "ShapeLayer03";
        public const string ShapeLayer04 = "ShapeLayer04";
        public static readonly string[] AllShapeIds = { ShapeLayer01, ShapeLayer02, ShapeLayer03, ShapeLayer04 };
        public static bool IsShapeId(string id) => AllShapeIds.Contains(id);

        // Layers that have a Border tab (size, color, corner radius stored on the entry): the shape layers,
        // plus the Splash Image and Video Preview layers.
        public static bool HasBorderTab(string id) => IsShapeId(id) || IsCustomId(id) || id == SplashImage || id == VideoPreview;

        // Layers drawn as a bordered box with a fill and an optional image or video: the shape layers and the
        // custom layers. They share one renderer, one tab set and the visibility dropdown.
        public static bool IsBoxId(string id) => IsShapeId(id) || IsCustomId(id);

        // Where a newly added custom layer starts (the layer then resizes itself when a file is chosen)
        public const double NewLayerX = 20;
        public const double NewLayerY = 20;
        public const double NewLayerWidth = 200;
        public const double NewLayerHeight = 200;

        // Visibility modes (stored as text in the entry's VisibilityMode). Never hides the layer entirely.
        public const string VisibilityAlways = "Always";
        public const string VisibilityGameSelected = "GameSelected";
        public const string VisibilityNoGameSelected = "NoGameSelected";
        public const string VisibilityNever = "Never";

        // Shape layer defaults: a black fill with a 1px gray border, the look of the old Games List BG and
        // Media Panel. Shape Layer 03 (under the Marquee) starts transparent with no border, and Shape Layer 04
        // (the spare) starts hidden. These per-layer differences are applied where the defaults are built.
        public const string DefaultVisibilityMode = VisibilityAlways;
        public const string DefaultShapeBgColorHex = "#FF000000";
        public const int DefaultShapeBorderSize = 1;
        public const string DefaultShapeBorderColorHex = "#FF3A3A3C";
        public const int DefaultShapeCornerRadius = 0;
        public const int DefaultFileOpacity = 100;

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

            new() { Id = ShapeLayer01,   DisplayName = "Shape Layer 01",    X = 20,  Y = 60,  Width = 353, Height = 640, Z = 4 },
            new() { Id = ShapeLayer02,   DisplayName = "Shape Layer 02",    X = 393, Y = 177, Width = 867, Height = 523, Z = 3 },
            new() { Id = ShapeLayer03,   DisplayName = "Shape Layer 03",    X = 393, Y = 20,  Width = 867, Height = 137, Z = 2 },
            new() { Id = ShapeLayer04,   DisplayName = "Shape Layer 04",    X = 393, Y = 177, Width = 867, Height = 523, Z = 1 },

            new() { Id = GamesList,      DisplayName = "Games List",        X = 25,  Y = 70,  Width = 343, Height = 625, Z = 51, CanHide = false },
                        new() { Id = MarqueePanel,   DisplayName = "Marquee",           X = 393, Y = 20,  Width = 867, Height = 137, Z = 40 },

            new() { Id = SplashImage,    DisplayName = "Splash Image",      X = 393, Y = 177, Width = 867, Height = 523, Z = 31 },

            new() { Id = VideoPreview,   DisplayName = "Video Preview",     X = 414, Y = 198, Width = 328, Height = 248, Z = 33 },
            

            new() { Id = GameTitle,        DisplayName = "Game Title",          X = 765, Y = 198, Width = 490, Height = 45,  Z = 34, GroupId = GameInfoGroup },
            new() { Id = PublisherBlock,   DisplayName = "Publisher Block",     X = 745, Y = 248, Width = 490, Height = 90,  Z = 34, GroupId = GameInfoGroup },
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

                var entry = new LayerLayoutEntry
                {
                    Id = def.Id,
                    X = Math.Round(def.X * sx),
                    Y = Math.Round(def.Y * sy),
                    Width = Math.Round(def.Width * sx),
                    Height = Math.Round(def.Height * sy),
                    Z = def.Z,
                    Visible = true
                };

                // Games List styling defaults (today's compiled defaults)
                if (def.Id == GamesList)
                {
                    entry.FolderFontSize = DefaultFolderFontSize;
                    entry.FolderColorHex = DefaultFolderColorHex;
                    entry.FolderSelectedColorHex = DefaultFolderSelectedColorHex;
                    entry.FolderSelectedBgColorHex = DefaultFolderSelectedBgColorHex;
                    entry.FolderFontFamily = DefaultFontFamily;
                    entry.FolderBold = DefaultFolderBold;
                    entry.ArrowColorHex = DefaultArrowColorHex;
                    entry.ArrowNudgeY = DefaultArrowNudgeY;
                    entry.GameFontSize = DefaultGameFontSize;
                    entry.GameColorHex = DefaultGameColorHex;
                    entry.GameSelectedColorHex = DefaultGameSelectedColorHex;
                    entry.GameSelectedBgColorHex = DefaultGameSelectedBgColorHex;
                    entry.GameHoverColorHex = DefaultGameHoverColorHex;
                    entry.GameFontFamily = DefaultFontFamily;
                    entry.GameBold = DefaultGameBold;
                    entry.FolderShadowEnabled = DefaultShadowEnabled;
                    entry.FolderShadowColorHex = DefaultShadowColorHex;
                    entry.FolderShadowBlur = DefaultShadowBlur;
                    entry.FolderShadowDepth = DefaultShadowDepth;
                    entry.GameShadowEnabled = DefaultShadowEnabled;
                    entry.GameShadowColorHex = DefaultShadowColorHex;
                    entry.GameShadowBlur = DefaultShadowBlur;
                    entry.GameShadowDepth = DefaultShadowDepth;
                    entry.FolderSelectedShadowEnabled = DefaultShadowEnabled;
                    entry.FolderSelectedShadowColorHex = DefaultShadowColorHex;
                    entry.FolderSelectedShadowBlur = DefaultShadowBlur;
                    entry.FolderSelectedShadowDepth = DefaultShadowDepth;
                    entry.GameSelectedShadowEnabled = DefaultShadowEnabled;
                    entry.GameSelectedShadowColorHex = DefaultShadowColorHex;
                    entry.GameSelectedShadowBlur = DefaultShadowBlur;
                    entry.GameSelectedShadowDepth = DefaultShadowDepth;
                }

                // Shape layer defaults: Shape Layer 03 (under the Marquee) is transparent with no border, and
                // Shape Layer 04 (the spare) is hidden with the Never mode. The others use the standard look.
                if (IsShapeId(def.Id))
                {
                    bool underMarquee = def.Id == ShapeLayer03;
                    entry.VisibilityMode = def.Id == ShapeLayer04 ? VisibilityNever : DefaultVisibilityMode;
                    entry.BgColorHex = underMarquee ? "#00000000" : DefaultShapeBgColorHex;
                    entry.BorderSize = underMarquee ? 0 : DefaultShapeBorderSize;
                    entry.BorderColorHex = DefaultShapeBorderColorHex;
                    entry.CornerRadius = DefaultShapeCornerRadius;
                }

                // Splash Image and Video Preview have a Border tab too: size 0 (no border) with the gray color ready
                if (def.Id == SplashImage || def.Id == VideoPreview)
                {
                    entry.BorderSize = 0;
                    entry.BorderColorHex = DefaultShapeBorderColorHex;
                    entry.CornerRadius = DefaultShapeCornerRadius;
                }

                layout.Layers.Add(entry);
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

                // Older saved Games List entries have no style values yet: fill them from defaults
                if (entry.Id == GamesList)
                {
                    entry.FolderFontSize ??= DefaultFolderFontSize;
                    if (string.IsNullOrWhiteSpace(entry.FolderColorHex)) entry.FolderColorHex = DefaultFolderColorHex;
                    if (string.IsNullOrWhiteSpace(entry.FolderSelectedColorHex)) entry.FolderSelectedColorHex = DefaultFolderSelectedColorHex;
                    if (string.IsNullOrWhiteSpace(entry.FolderSelectedBgColorHex)) entry.FolderSelectedBgColorHex = DefaultFolderSelectedBgColorHex;
                    if (string.IsNullOrWhiteSpace(entry.FolderFontFamily)) entry.FolderFontFamily = DefaultFontFamily;
                    entry.FolderBold ??= DefaultFolderBold;
                    if (string.IsNullOrWhiteSpace(entry.ArrowColorHex)) entry.ArrowColorHex = DefaultArrowColorHex;
                    entry.ArrowNudgeY ??= DefaultArrowNudgeY;
                    entry.GameFontSize ??= DefaultGameFontSize;
                    if (string.IsNullOrWhiteSpace(entry.GameColorHex)) entry.GameColorHex = DefaultGameColorHex;
                    if (string.IsNullOrWhiteSpace(entry.GameSelectedColorHex)) entry.GameSelectedColorHex = DefaultGameSelectedColorHex;
                    if (string.IsNullOrWhiteSpace(entry.GameSelectedBgColorHex)) entry.GameSelectedBgColorHex = DefaultGameSelectedBgColorHex;
                    if (string.IsNullOrWhiteSpace(entry.GameHoverColorHex)) entry.GameHoverColorHex = DefaultGameHoverColorHex;
                    if (string.IsNullOrWhiteSpace(entry.GameFontFamily)) entry.GameFontFamily = DefaultFontFamily;
                    entry.GameBold ??= DefaultGameBold;
                    entry.FolderShadowEnabled ??= DefaultShadowEnabled;
                    if (string.IsNullOrWhiteSpace(entry.FolderShadowColorHex)) entry.FolderShadowColorHex = DefaultShadowColorHex;
                    entry.FolderShadowBlur ??= DefaultShadowBlur;
                    entry.FolderShadowDepth ??= DefaultShadowDepth;
                    entry.GameShadowEnabled ??= DefaultShadowEnabled;
                    if (string.IsNullOrWhiteSpace(entry.GameShadowColorHex)) entry.GameShadowColorHex = DefaultShadowColorHex;
                    entry.GameShadowBlur ??= DefaultShadowBlur;
                    entry.GameShadowDepth ??= DefaultShadowDepth;
                    entry.FolderSelectedShadowEnabled ??= DefaultShadowEnabled;
                    if (string.IsNullOrWhiteSpace(entry.FolderSelectedShadowColorHex)) entry.FolderSelectedShadowColorHex = DefaultShadowColorHex;
                    entry.FolderSelectedShadowBlur ??= DefaultShadowBlur;
                    entry.FolderSelectedShadowDepth ??= DefaultShadowDepth;
                    entry.GameSelectedShadowEnabled ??= DefaultShadowEnabled;
                    if (string.IsNullOrWhiteSpace(entry.GameSelectedShadowColorHex)) entry.GameSelectedShadowColorHex = DefaultShadowColorHex;
                    entry.GameSelectedShadowBlur ??= DefaultShadowBlur;
                    entry.GameSelectedShadowDepth ??= DefaultShadowDepth;
                }

                // Splash Image and Video Preview: border values fill in from defaults when missing
                if (entry.Id == SplashImage || entry.Id == VideoPreview)
                {
                    entry.BorderSize ??= 0;
                    if (string.IsNullOrWhiteSpace(entry.BorderColorHex)) entry.BorderColorHex = DefaultShapeBorderColorHex;
                    entry.CornerRadius ??= DefaultShapeCornerRadius;
                }

                // Shape layers: fill any missing value from the matching default entry
                if (IsShapeId(entry.Id))
                {
                    if (string.IsNullOrWhiteSpace(entry.VisibilityMode)) entry.VisibilityMode = defEntry.VisibilityMode ?? DefaultVisibilityMode;
                    if (string.IsNullOrWhiteSpace(entry.BgColorHex)) entry.BgColorHex = defEntry.BgColorHex ?? DefaultShapeBgColorHex;
                    entry.BorderSize ??= defEntry.BorderSize ?? DefaultShapeBorderSize;
                    if (string.IsNullOrWhiteSpace(entry.BorderColorHex)) entry.BorderColorHex = defEntry.BorderColorHex ?? DefaultShapeBorderColorHex;
                    entry.CornerRadius ??= defEntry.CornerRadius ?? DefaultShapeCornerRadius;
                    entry.FileOpacity ??= DefaultFileOpacity;
                }

                result.Layers.Add(entry);
            }

            // Custom image layers aren't in the registry, so carry them through from the saved layout
            foreach (var customEntry in saved.Layers.Where(e => IsCustomId(e.Id)))
            {
                // Custom layers use the shape layer properties: fill any missing value from the shape defaults
                if (string.IsNullOrWhiteSpace(customEntry.VisibilityMode)) customEntry.VisibilityMode = DefaultVisibilityMode;
                if (string.IsNullOrWhiteSpace(customEntry.BgColorHex)) customEntry.BgColorHex = DefaultShapeBgColorHex;
                customEntry.BorderSize ??= DefaultShapeBorderSize;
                if (string.IsNullOrWhiteSpace(customEntry.BorderColorHex)) customEntry.BorderColorHex = DefaultShapeBorderColorHex;
                customEntry.CornerRadius ??= DefaultShapeCornerRadius;
                customEntry.FileOpacity ??= DefaultFileOpacity;

                result.Layers.Add(customEntry);
            }
            return result;
        }

        // Games List styling defaults (today's compiled defaults)
        public const int DefaultFolderFontSize = 17;
        public const string DefaultFolderColorHex = "#FF00D4FF";
        public const string DefaultFolderSelectedColorHex = "#FF000000";
        public const string DefaultFolderSelectedBgColorHex = "#FF00D4FF";
        public const string DefaultFontFamily = "Segoe UI";
        public const bool DefaultFolderBold = true;
        public const string DefaultArrowColorHex = "#FFA0A0A2";
        public const int DefaultArrowNudgeY = 0;
        public const int DefaultGameFontSize = 15;
        public const string DefaultGameColorHex = "#FFA0A0A2";
        public const string DefaultGameSelectedColorHex = "#FF000000";
        public const string DefaultGameSelectedBgColorHex = "#FF00D4FF";
        public const string DefaultGameHoverColorHex = "#FFEF44C3";
        public const bool DefaultGameBold = true;

        // Drop shadow defaults: off. The color, blur and depth are the starting values when someone turns it on
        // (the old built-in look: black, soft, one pixel down and to the right).
        public const bool DefaultShadowEnabled = false;
        public const string DefaultShadowColorHex = "#FF000000";
        public const int DefaultShadowBlur = 3;
        public const int DefaultShadowDepth = 1;

        // Windows core fonts offered in the Advanced tab's font dropdown (all ship with Windows 10 and 11).
        // Segoe UI is also the fallback when a theme has no saved family.
        public static readonly IReadOnlyList<string> AvailableFonts = new List<string>
        {
            "Segoe UI", "Arial", "Bahnschrift",
            "Georgia", "Times New Roman", "Palatino Linotype",
            "Impact", "Arial Black", "Segoe UI Black",
            "Consolas", "Courier New"
        };
    }
    // [END SECTION: Layer Registry]
}