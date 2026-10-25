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

        // Generic text styling for layers that show one block of text (Search Label, Search Box, the shared
        // main window button style on the Random Game Button entry; the Game Info title, publisher, body and nav
        // entries). Same rules as above:
        // null on every other layer, left out of the theme file when null, filled from defaults where used.
        // TextAlign: "Left", "Center" or "Right".
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? TextFontSize { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? TextColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? TextFontFamily { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? TextBold { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? TextAlign { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? HoverColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? HoverBgColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? HoverBorderColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? TextShadowEnabled { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? TextShadowColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? TextShadowBlur { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? TextShadowDepth { get; set; }

        // Which images the Marquee layer's shadow (TextShadow* above) applies to: "Both", "Logo" (the idle logo
        // only) or "Marquees" (the game's marquee and the spin frames only). MarqueePanel entry only; unknown or
        // blank reads as "Both".
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? ShadowTarget { get; set; }

        // Moves only the drawn text up (negative) or down (positive) in pixels, -10 to 10; the layer's fill,
        // size and position stay put. Lets the user re-center text when a font's ascent/descent shifts it.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? TextNudgeY { get; set; }

        // Game Info Body entry only: the gap in pixels between the body text and the right edge of its scroll area (the
        // scrollbar when it shows), 0 to 100, on the Game Info, Trivia, Tips and Specs & Credits pages. Same rules as above.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? TextRightMargin { get; set; }

        // Random Game Display only (its first part, RandomGameDisplay): the spin card's message. An empty message means
        // no text; null fills from the default. TextWrap (whether the text wraps at the box width) is used by that part
        // and by the Marquee layer's no-art banner. Same rules as above.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? TextMessage { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? TextWrap { get; set; }

        // Marquee layer only (MarqueePanel): the no-art banner title's own drop shadow. Its TextShadow* fields are
        // already the shared Marquee image shadow. Same rules as above.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? BannerShadowEnabled { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? BannerShadowColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? BannerShadowBlur { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? BannerShadowDepth { get; set; }

        // Marquee layer only (MarqueePanel): the no-art banner title's left and right inset in the card's 400x100
        // reference box units (it scales with the card), 0 to 100. Used only when the title is on one line (TextWrap off).
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? BannerTextMargin { get; set; }

        // Game Info section headers (the Game Info Body entry only; its body text uses the generic Text* fields
        // above). A second block of text styling because the generic fields only fit one block per entry.
        // Same rules: null on every other layer, left out of the theme file when null, filled from defaults.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? SectionHeaderFontSize { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? SectionHeaderColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? SectionHeaderFontFamily { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? SectionHeaderBold { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? SectionHeaderShadowEnabled { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? SectionHeaderShadowColorHex { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? SectionHeaderShadowBlur { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public int? SectionHeaderShadowDepth { get; set; }

        // Main Window Background layer only: true = no image or video, the solid Background Color shows instead
        // (ImagePath holds the wallpaper; empty means the built-in default image). Null on every other layer.
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public bool? ImageDisabled { get; set; }

        // [SECTION: Style Copy]
        // Copies every styling field (everything except Id, rect, Z, Visible, Name and ImagePath) from source.
        // The one place entry styling is copied: CloneLayouts, the resolution-switch copy and Reset Layer all
        // use it, so a new style field only has to be added here.
        public void CopyStyleFrom(LayerLayoutEntry source)
        {
            FolderFontSize = source.FolderFontSize;
            FolderColorHex = source.FolderColorHex;
            FolderSelectedColorHex = source.FolderSelectedColorHex;
            FolderSelectedBgColorHex = source.FolderSelectedBgColorHex;
            FolderFontFamily = source.FolderFontFamily;
            FolderBold = source.FolderBold;
            ArrowColorHex = source.ArrowColorHex;
            ArrowNudgeY = source.ArrowNudgeY;
            GameFontSize = source.GameFontSize;
            GameColorHex = source.GameColorHex;
            GameSelectedColorHex = source.GameSelectedColorHex;
            GameSelectedBgColorHex = source.GameSelectedBgColorHex;
            GameHoverColorHex = source.GameHoverColorHex;
            GameFontFamily = source.GameFontFamily;
            GameBold = source.GameBold;
            FolderShadowEnabled = source.FolderShadowEnabled;
            FolderShadowColorHex = source.FolderShadowColorHex;
            FolderShadowBlur = source.FolderShadowBlur;
            FolderShadowDepth = source.FolderShadowDepth;
            GameShadowEnabled = source.GameShadowEnabled;
            GameShadowColorHex = source.GameShadowColorHex;
            GameShadowBlur = source.GameShadowBlur;
            GameShadowDepth = source.GameShadowDepth;
            FolderSelectedShadowEnabled = source.FolderSelectedShadowEnabled;
            FolderSelectedShadowColorHex = source.FolderSelectedShadowColorHex;
            FolderSelectedShadowBlur = source.FolderSelectedShadowBlur;
            FolderSelectedShadowDepth = source.FolderSelectedShadowDepth;
            GameSelectedShadowEnabled = source.GameSelectedShadowEnabled;
            GameSelectedShadowColorHex = source.GameSelectedShadowColorHex;
            GameSelectedShadowBlur = source.GameSelectedShadowBlur;
            GameSelectedShadowDepth = source.GameSelectedShadowDepth;
            VisibilityMode = source.VisibilityMode;
            BgColorHex = source.BgColorHex;
            BorderSize = source.BorderSize;
            BorderColorHex = source.BorderColorHex;
            CornerRadius = source.CornerRadius;
            FileOpacity = source.FileOpacity;
            TextFontSize = source.TextFontSize;
            TextColorHex = source.TextColorHex;
            TextFontFamily = source.TextFontFamily;
            TextBold = source.TextBold;
            TextAlign = source.TextAlign;
            HoverColorHex = source.HoverColorHex;
            HoverBgColorHex = source.HoverBgColorHex;
            HoverBorderColorHex = source.HoverBorderColorHex;
            TextShadowEnabled = source.TextShadowEnabled;
            TextShadowColorHex = source.TextShadowColorHex;
            TextShadowBlur = source.TextShadowBlur;
            TextShadowDepth = source.TextShadowDepth;
            ShadowTarget = source.ShadowTarget;
            TextNudgeY = source.TextNudgeY;
            TextRightMargin = source.TextRightMargin;
            TextMessage = source.TextMessage;
            TextWrap = source.TextWrap;
            BannerShadowEnabled = source.BannerShadowEnabled;
            BannerShadowColorHex = source.BannerShadowColorHex;
            BannerShadowBlur = source.BannerShadowBlur;
            BannerShadowDepth = source.BannerShadowDepth;
            BannerTextMargin = source.BannerTextMargin;
            SectionHeaderFontSize = source.SectionHeaderFontSize;
            SectionHeaderColorHex = source.SectionHeaderColorHex;
            SectionHeaderFontFamily = source.SectionHeaderFontFamily;
            SectionHeaderBold = source.SectionHeaderBold;
            SectionHeaderShadowEnabled = source.SectionHeaderShadowEnabled;
            SectionHeaderShadowColorHex = source.SectionHeaderShadowColorHex;
            SectionHeaderShadowBlur = source.SectionHeaderShadowBlur;
            SectionHeaderShadowDepth = source.SectionHeaderShadowDepth;
            ImageDisabled = source.ImageDisabled;
        }
        // [END SECTION: Style Copy]
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
    }

    public record ResolutionPreset(int Width, int Height)
    {
        public override string ToString() => $"{Width} x {Height}";
    }

    // A group of layers shown as one row in the Advanced tab's layer list. PartIds are in the order the
    // parts appear on the group's edit page. All parts share the group's Z. AllowGroupMove offers the
    // "Move all parts together" checkbox on the group's Position tab; AllowRatioLock gives each part its own
    // aspect-ratio lock between its Width and Height boxes.
    public record LayerGroupDefinition(string Id, string DisplayName, string[] PartIds, bool AllowGroupMove = false, bool AllowRatioLock = false);

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

        // Does the border-ring rule check: true for the layers whose rect is the content area with the border drawn outside it
        public static bool HasOutsideBorder(string id) => HasBorderTab(id) || id == RandomGameDisplay || id == RandomGameDisplaySplash;

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

        // Main Window Background: the image (or the built-in default) shows unless the user disables it
        public const bool DefaultImageDisabled = false;

        // Main Window Background color (the former flat BackgroundColor default). Also the app's popup and dialog
        // background: the Theme Builder picker popups, Sorting and Manage Playlists windows, main window popups and
        // the no-art banner all follow it.
        public const string DefaultMainBackgroundColorHex = "#FF1C1C1E";

        // Video Preview background (the former flat VideoBgColorHex default): the fill behind and around the video
        public const string DefaultVideoBgColorHex = "#FF000000";

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
            new(SearchAndButtonsGroup, "Search & Buttons", new[] { SearchLabel, SearchBox, RandomButton, SortButton, OptionsButton, HamburgerButton }, AllowGroupMove: true),
            new(GameInfoGroup, "Game Info", new[] { GameTitle, PublisherBlock, GameInfoNav, GameInfoBody, ImagePreviewsNav, ImagePreviewsBody }, AllowGroupMove: true),
            new(RandomGameGroup, "Random Game Display", new[] { RandomGameDisplay, RandomGameDisplaySplash }, AllowRatioLock: true)
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
                    // Fill layers (the Main Window Background): image on by default, empty path = built-in image, and the
                    // background color (BgColorHex) under it
                    layout.Layers.Add(new LayerLayoutEntry { Id = def.Id, X = 0, Y = 0, Width = width, Height = height, Z = def.Z, Visible = true, ImageDisabled = DefaultImageDisabled, BgColorHex = DefaultMainBackgroundColorHex });
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

                // Splash Image and Video Preview have a Border tab too: size 0 (no border) with the gray color ready.
                // Video Preview also has a Background tab: the fill behind and around the video (letterbox bars).
                if (def.Id == SplashImage || def.Id == VideoPreview)
                {
                    entry.BorderSize = 0;
                    entry.BorderColorHex = DefaultShapeBorderColorHex;
                    entry.CornerRadius = DefaultShapeCornerRadius;
                    if (def.Id == VideoPreview) entry.BgColorHex = DefaultVideoBgColorHex;
                }

                // Search & Buttons styling: label, box and the shared button style (on the Random Game Button)
                FillSearchButtonsStyle(entry);

                // Game Info styling: title, publisher block, headers + body, and the two navs
                FillGameInfoStyle(entry);

                // Image shadow: the Marquee layer and the Image Previews
                FillImageShadowStyle(entry);

                // Random Game Display: the spin card's text, background and border, and each state's max text size
                FillRandomGameDisplayStyle(entry);

                // Marquee no-art banner: the title card's font, colors, wrap, border and text shadow
                FillMarqueeBannerStyle(entry);

                layout.Layers.Add(entry);
            }
            return layout;
        }

        // Fills any missing Search & Buttons style value from defaults. Label values live on the SearchLabel
        // entry, box values on SearchBox, and the style shared by all four buttons on RandomButton. Other
        // entries are left untouched; saved values are kept.
        private static void FillSearchButtonsStyle(LayerLayoutEntry entry)
        {
            if (entry.Id == SearchLabel)
            {
                entry.TextFontSize ??= DefaultSearchLabelFontSize;
                if (string.IsNullOrWhiteSpace(entry.TextColorHex)) entry.TextColorHex = DefaultSearchLabelColorHex;
                if (string.IsNullOrWhiteSpace(entry.BgColorHex)) entry.BgColorHex = DefaultSearchLabelBgColorHex;
                if (string.IsNullOrWhiteSpace(entry.TextFontFamily)) entry.TextFontFamily = DefaultFontFamily;
                entry.TextBold ??= DefaultSearchLabelBold;
                if (string.IsNullOrWhiteSpace(entry.TextAlign)) entry.TextAlign = DefaultSearchLabelAlign;
                entry.TextShadowEnabled ??= DefaultShadowEnabled;
                if (string.IsNullOrWhiteSpace(entry.TextShadowColorHex)) entry.TextShadowColorHex = DefaultShadowColorHex;
                entry.TextShadowBlur ??= DefaultShadowBlur;
                entry.TextShadowDepth ??= DefaultShadowDepth;
                entry.TextNudgeY ??= DefaultSearchLabelNudgeY;
            }
            else if (entry.Id == SearchBox)
            {
                entry.TextFontSize ??= DefaultSearchBoxFontSize;
                if (string.IsNullOrWhiteSpace(entry.TextColorHex)) entry.TextColorHex = DefaultSearchBoxColorHex;
                if (string.IsNullOrWhiteSpace(entry.BgColorHex)) entry.BgColorHex = DefaultSearchBoxBgColorHex;
            }
            else if (entry.Id == RandomButton)
            {
                entry.TextFontSize ??= DefaultButtonFontSize;
                if (string.IsNullOrWhiteSpace(entry.TextColorHex)) entry.TextColorHex = DefaultButtonColorHex;
                if (string.IsNullOrWhiteSpace(entry.BgColorHex)) entry.BgColorHex = DefaultButtonBgColorHex;
                if (string.IsNullOrWhiteSpace(entry.HoverColorHex)) entry.HoverColorHex = DefaultButtonHoverColorHex;
                if (string.IsNullOrWhiteSpace(entry.HoverBgColorHex)) entry.HoverBgColorHex = DefaultButtonHoverBgColorHex;
                entry.BorderSize ??= DefaultButtonBorderSize;
                if (string.IsNullOrWhiteSpace(entry.BorderColorHex)) entry.BorderColorHex = DefaultButtonBorderColorHex;
                entry.CornerRadius ??= DefaultButtonCornerRadius;
                if (string.IsNullOrWhiteSpace(entry.HoverBorderColorHex)) entry.HoverBorderColorHex = DefaultButtonHoverBorderColorHex;
            }
        }

        // Fills any missing Game Info style value from defaults. Title text on GameTitle, publisher text on
        // PublisherBlock, body text (Text*, its right margin included) and section headers (SectionHeader*) on GameInfoBody, and each nav's
        // size/color/shadow on its own nav entry. Other entries are left untouched; saved values are kept.
        private static void FillGameInfoStyle(LayerLayoutEntry entry)
        {
            if (entry.Id == GameTitle)
            {
                if (string.IsNullOrWhiteSpace(entry.TextFontFamily)) entry.TextFontFamily = DefaultFontFamily;
                entry.TextBold ??= DefaultTitleBold;
                entry.TextFontSize ??= DefaultTitleFontSize;
                if (string.IsNullOrWhiteSpace(entry.TextColorHex)) entry.TextColorHex = DefaultGameInfoTextColorHex;
                entry.TextShadowEnabled ??= DefaultTitleShadowEnabled;
                if (string.IsNullOrWhiteSpace(entry.TextShadowColorHex)) entry.TextShadowColorHex = DefaultTitleShadowColorHex;
                entry.TextShadowBlur ??= DefaultTitleShadowBlur;
                entry.TextShadowDepth ??= DefaultTitleShadowDepth;
            }
            else if (entry.Id == PublisherBlock)
            {
                if (string.IsNullOrWhiteSpace(entry.TextFontFamily)) entry.TextFontFamily = DefaultFontFamily;
                entry.TextBold ??= DefaultPublisherBold;
                entry.TextFontSize ??= DefaultPublisherFontSize;
                if (string.IsNullOrWhiteSpace(entry.TextAlign)) entry.TextAlign = DefaultPublisherAlign;
                if (string.IsNullOrWhiteSpace(entry.TextColorHex)) entry.TextColorHex = DefaultGameInfoTextColorHex;
                FillTextShadowDefaults(entry);
            }
            else if (entry.Id == GameInfoBody)
            {
                if (string.IsNullOrWhiteSpace(entry.SectionHeaderFontFamily)) entry.SectionHeaderFontFamily = DefaultFontFamily;
                entry.SectionHeaderBold ??= DefaultInfoHeaderBold;
                entry.SectionHeaderFontSize ??= DefaultInfoHeaderFontSize;
                if (string.IsNullOrWhiteSpace(entry.SectionHeaderColorHex)) entry.SectionHeaderColorHex = DefaultGameInfoTextColorHex;
                entry.SectionHeaderShadowEnabled ??= DefaultShadowEnabled;
                if (string.IsNullOrWhiteSpace(entry.SectionHeaderShadowColorHex)) entry.SectionHeaderShadowColorHex = DefaultShadowColorHex;
                entry.SectionHeaderShadowBlur ??= DefaultShadowBlur;
                entry.SectionHeaderShadowDepth ??= DefaultShadowDepth;

                if (string.IsNullOrWhiteSpace(entry.TextFontFamily)) entry.TextFontFamily = DefaultInfoBodyFontFamily;
                entry.TextBold ??= DefaultInfoBodyBold;
                entry.TextFontSize ??= DefaultInfoBodyFontSize;
                if (string.IsNullOrWhiteSpace(entry.TextColorHex)) entry.TextColorHex = DefaultGameInfoTextColorHex;
                entry.TextRightMargin ??= DefaultInfoBodyRightMargin;
                FillTextShadowDefaults(entry);
            }
            else if (entry.Id == GameInfoNav || entry.Id == ImagePreviewsNav)
            {
                entry.TextFontSize ??= DefaultNavSize;
                if (string.IsNullOrWhiteSpace(entry.TextColorHex)) entry.TextColorHex = DefaultGameInfoTextColorHex;
                FillTextShadowDefaults(entry);
            }
        }

        // Fills the image shadow (generic TextShadow* fields, off by default) on the Marquee layer (logo, marquee
        // image and spin frames) and the Image Previews, plus the Marquee shadow's target (unknown or blank becomes
        // "Both"). Other entries are left untouched; saved values are kept.
        private static void FillImageShadowStyle(LayerLayoutEntry entry)
        {
            if (entry.Id == MarqueePanel || entry.Id == ImagePreviewsBody) FillTextShadowDefaults(entry);
            if (entry.Id == MarqueePanel && !IsShadowTarget(entry.ShadowTarget)) entry.ShadowTarget = DefaultShadowTarget;
        }

        // Fills the Random Game Display spin card's style from defaults. The text style shared by both states (message,
        // wrap, font, bold, color, alignment, shadow) lives on RandomGameDisplay; each part keeps its own max text size
        // (TextFontSize), background color and border. An empty saved message is kept (it means no text); saved values are kept.
        private static void FillRandomGameDisplayStyle(LayerLayoutEntry entry)
        {
            if (entry.Id == RandomGameDisplay)
            {
                entry.TextMessage ??= DefaultRandomDisplayMessage;
                entry.TextWrap ??= DefaultRandomDisplayWrap;
                if (string.IsNullOrWhiteSpace(entry.TextFontFamily)) entry.TextFontFamily = DefaultFontFamily;
                entry.TextBold ??= DefaultRandomDisplayBold;
                if (string.IsNullOrWhiteSpace(entry.TextColorHex)) entry.TextColorHex = DefaultRandomDisplayColorHex;
                if (string.IsNullOrWhiteSpace(entry.TextAlign)) entry.TextAlign = DefaultRandomDisplayAlign;
                FillTextShadowDefaults(entry);
                entry.TextFontSize ??= DefaultRandomDisplayFontSize;
                FillRandomDisplayCardDefaults(entry);
            }
            else if (entry.Id == RandomGameDisplaySplash)
            {
                entry.TextFontSize ??= DefaultRandomDisplaySplashFontSize;
                FillRandomDisplayCardDefaults(entry);
            }
        }

        // Fills the Marquee layer's no-art banner style (the idle banner and the spin's no-marquee card share it) on the
        // MarqueePanel entry: font, bold, max size, text color, wrap, one-line text margin, background, border and the
        // title's own shadow.
        // Saved values are kept, a saved false included.
        private static void FillMarqueeBannerStyle(LayerLayoutEntry entry)
        {
            if (entry.Id != MarqueePanel) return;

            if (string.IsNullOrWhiteSpace(entry.TextFontFamily)) entry.TextFontFamily = DefaultFontFamily;
            entry.TextBold ??= DefaultBannerBold;
            entry.TextFontSize ??= DefaultBannerFontSize;
            if (string.IsNullOrWhiteSpace(entry.TextColorHex)) entry.TextColorHex = DefaultBannerColorHex;
            entry.TextWrap ??= DefaultBannerWrap;
            if (string.IsNullOrWhiteSpace(entry.BgColorHex)) entry.BgColorHex = DefaultBannerBgColorHex;
            if (string.IsNullOrWhiteSpace(entry.BorderColorHex)) entry.BorderColorHex = DefaultBannerBorderColorHex;
            entry.BorderSize ??= DefaultBannerBorderSize;
            entry.CornerRadius ??= DefaultBannerCornerRadius;
            entry.BannerShadowEnabled ??= DefaultShadowEnabled;
            if (string.IsNullOrWhiteSpace(entry.BannerShadowColorHex)) entry.BannerShadowColorHex = DefaultShadowColorHex;
            entry.BannerShadowBlur ??= DefaultShadowBlur;
            entry.BannerShadowDepth ??= DefaultShadowDepth;
            entry.BannerTextMargin ??= DefaultBannerTextMargin;
        }

        // Fills one Random Game Display state's background color and border (black, unbordered, square; saved values kept)
        private static void FillRandomDisplayCardDefaults(LayerLayoutEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.BgColorHex)) entry.BgColorHex = DefaultRandomDisplayBgColorHex;
            entry.BorderSize ??= 0;
            if (string.IsNullOrWhiteSpace(entry.BorderColorHex)) entry.BorderColorHex = DefaultShapeBorderColorHex;
            entry.CornerRadius ??= DefaultShapeCornerRadius;
        }

        // Does the Marquee shadow target check: true for "Both", "Logo" or "Marquees"
        public static bool IsShadowTarget(string? target) =>
            target == ShadowTargetBoth || target == ShadowTargetLogo || target == ShadowTargetMarquees;

        // Fills the generic TextShadow* fields with the standard off-by-default shadow (saved values kept)
        private static void FillTextShadowDefaults(LayerLayoutEntry entry)
        {
            entry.TextShadowEnabled ??= DefaultShadowEnabled;
            if (string.IsNullOrWhiteSpace(entry.TextShadowColorHex)) entry.TextShadowColorHex = DefaultShadowColorHex;
            entry.TextShadowBlur ??= DefaultShadowBlur;
            entry.TextShadowDepth ??= DefaultShadowDepth;
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

                // Splash Image and Video Preview: border values (and the Video Preview background) fill in from defaults when missing
                if (entry.Id == SplashImage || entry.Id == VideoPreview)
                {
                    entry.BorderSize ??= 0;
                    if (string.IsNullOrWhiteSpace(entry.BorderColorHex)) entry.BorderColorHex = DefaultShapeBorderColorHex;
                    entry.CornerRadius ??= DefaultShapeCornerRadius;
                    if (entry.Id == VideoPreview && string.IsNullOrWhiteSpace(entry.BgColorHex)) entry.BgColorHex = DefaultVideoBgColorHex;
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

                // Search & Buttons: fill any missing label, box or shared button style value
                FillSearchButtonsStyle(entry);

                // Game Info: fill any missing title, publisher, header, body or nav style value
                FillGameInfoStyle(entry);

                // Image shadow: fill any missing Marquee layer or Image Previews shadow value
                FillImageShadowStyle(entry);

                // Random Game Display: fill any missing card style value or per-state max text size
                FillRandomGameDisplayStyle(entry);

                // Marquee no-art banner: fill any missing banner style value
                FillMarqueeBannerStyle(entry);

                // Main Window Background: the Disable flag and the background color fill in when missing (saved values kept)
                if (entry.Id == MainBackground)
                {
                    entry.ImageDisabled ??= DefaultImageDisabled;
                    if (string.IsNullOrWhiteSpace(entry.BgColorHex)) entry.BgColorHex = DefaultMainBackgroundColorHex;
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

        // Search & Buttons styling defaults (the former flat SearchLabel*, SearchBox* and MainWinBtn* defaults)
        public const int DefaultSearchLabelFontSize = 13;
        public const string DefaultSearchLabelColorHex = "#FF00D4FF";
        public const string DefaultSearchLabelBgColorHex = "#001C1C1E";
        public const bool DefaultSearchLabelBold = false;
        public const string DefaultSearchLabelAlign = "Right";
        public const int DefaultSearchLabelNudgeY = 0;
        public const int MinTextNudgeY = -10;
        public const int MaxTextNudgeY = 10;
        public const int DefaultSearchBoxFontSize = 13;
        public const string DefaultSearchBoxColorHex = "#FFF2F2F7";
        public const string DefaultSearchBoxBgColorHex = "#FF1C1C1E";
        public const int DefaultButtonFontSize = 13;
        public const string DefaultButtonColorHex = "#FF00D4FF";
        public const string DefaultButtonBgColorHex = "#FF1C1C1E";
        public const string DefaultButtonHoverColorHex = "#FF00D4FF";
        public const string DefaultButtonHoverBgColorHex = "#FF2C2C2E";
        public const int DefaultButtonBorderSize = 1;
        public const string DefaultButtonBorderColorHex = "#FF00D4FF";
        public const int DefaultButtonCornerRadius = 0;
        public const string DefaultButtonHoverBorderColorHex = "#FF00D4FF";

        // Game Info styling defaults (the former flat GameNameHeader*, PubYearRating*, InfoHeader*, InfoBody* and
        // NavIcons* defaults). The title keeps its old built-in shadow (black at 60% alpha, blur 4, depth 2) on.
        public const string DefaultGameInfoTextColorHex = "#FFFFFFFF";
        public const int DefaultTitleFontSize = 32;
        public const bool DefaultTitleBold = true;
        public const bool DefaultTitleShadowEnabled = true;
        public const string DefaultTitleShadowColorHex = "#99000000";
        public const int DefaultTitleShadowBlur = 4;
        public const int DefaultTitleShadowDepth = 2;
        public const int DefaultPublisherFontSize = 13;
        public const bool DefaultPublisherBold = false;
        public const string DefaultPublisherAlign = "Right"; // the old hard-coded look

        // Marquee shadow target (LayerLayoutEntry.ShadowTarget): which marquee images the shadow applies to
        public const string ShadowTargetBoth = "Both";
        public const string ShadowTargetLogo = "Logo";
        public const string ShadowTargetMarquees = "Marquees";
        public const string DefaultShadowTarget = ShadowTargetBoth; // the Part 1 look: logo, marquee and spin frames
        public const int DefaultInfoHeaderFontSize = 20;
        public const bool DefaultInfoHeaderBold = true;
        public const string DefaultInfoBodyFontFamily = "Georgia";
        public const int DefaultInfoBodyFontSize = 16;
        public const bool DefaultInfoBodyBold = false;
        public const int DefaultInfoBodyRightMargin = 20; // the old hard-coded gap
        public const int MinInfoBodyRightMargin = 0;
        public const int MaxInfoBodyRightMargin = 100;
        public const int DefaultNavSize = 9;
        public const int MinNavSize = 4;
        public const int MaxNavSize = 40;

        // Random Game Display spin card defaults (the old hard-coded look: bold Segoe UI in the folder cyan on black,
        // square and unbordered). The max sizes match what the old fill-the-box text came to at the default rects.
        public const string DefaultRandomDisplayMessage = "Rolling the Dice!";
        public const int MaxRandomDisplayMessageLength = 60;
        public const bool DefaultRandomDisplayWrap = false;
        public const bool DefaultRandomDisplayBold = true;
        public const string DefaultRandomDisplayColorHex = "#FF00D4FF";
        public const string DefaultRandomDisplayAlign = "Center";
        public const string DefaultRandomDisplayBgColorHex = "#FF000000";
        public const int DefaultRandomDisplayFontSize = 38;
        public const int DefaultRandomDisplaySplashFontSize = 100;
        public const int MinRandomDisplayFontSize = 6;
        public const int MaxRandomDisplayFontSize = 200;

        // Marquee no-art banner defaults (the old look as fixed constants: the old Global Header and Main Window
        // Background defaults, a 2px border, square). The size is the max inside the card's 400x100 reference box.
        public const int DefaultBannerFontSize = 30;
        public const bool DefaultBannerBold = true;
        public const string DefaultBannerColorHex = "#FFEF44C3";
        public const bool DefaultBannerWrap = true;
        public const string DefaultBannerBgColorHex = "#FF1C1C1E";
        public const string DefaultBannerBorderColorHex = "#FFEF44C3";
        public const int DefaultBannerBorderSize = 2;
        public const int DefaultBannerCornerRadius = 0;
        public const int MinBannerFontSize = 6;
        public const int MaxBannerFontSize = 200;
        public const int DefaultBannerTextMargin = 5;
        public const int MinBannerTextMargin = 0;
        public const int MaxBannerTextMargin = 100;

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