// 🏗️ START: LIGHTWEIGHT THEME SETTINGS MODEL FOR DTO SERIALIZATION
using System;
using System.Collections.Generic;

namespace ArcadeStick.Models
{
    public class ThemeSettings
    {
        // File format version for theme.json. Bump when the structure changes in a way old code can't read.
        public int Version { get; set; } = 1;

        // Per-resolution layer layouts (position, size, z-order, visibility). Empty for older themes -
        // LayerRegistry.ResolveLayout fills in defaults for any resolution or layer that isn't saved here.
        public List<ThemeLayout> Layouts { get; set; } = new();

        // Resolution the theme was saved at. Loading the theme switches the window to this size.
        // 0 means "not saved" (older themes), in which case the current resolution is kept.
        public int ActiveWidth { get; set; } = 0;
        public int ActiveHeight { get; set; } = 0;
        public string BackgroundColor { get; set; } = "#FF1C1C1E";
        public string VideoBoxBg { get; set; } = "#FF000000";
        public string OptionsBg { get; set; } = "";
        public string BorderColorFramework { get; set; } = "#FF3A3A3C";
        public string ScrollTrackColor { get; set; } = "#FF000000";
        public string ScrollTrackHoverColor { get; set; } = "#FF1C1C1E";
        public string ScrollThumbColor { get; set; } = "#FF2C2C2E";
        public string ScrollThumbHoverColor { get; set; } = "#FF3A3A3C";
        public string ScrollThumbDragColor { get; set; } = "#FF00D4FF";
        public string MainWindowWallpaper { get; set; } = "";
        public bool DisableMainBgImage { get; set; } = false;
        public string MediaWindowWallpaper { get; set; } = "";
        public string ThemeBootSplash { get; set; } = "";
        public string ThemeMissingPreview { get; set; } = "";
        public string FolderSelectedColorHex { get; set; } = "#FF00D4FF";
        public string FolderSelectedBgColorHex { get; set; } = "#FF2C2C2E";
        public int TabFontSize { get; set; } = 12;
        public string TabColorHex { get; set; } = "#FFA0A0A2";
        public string TabBgColorHex { get; set; } = "Transparent";
        public string TabActiveColorHex { get; set; } = "#000000";
        public string TabActiveBgColorHex { get; set; } = "#FF00D4FF";
        public string TabHoverColorHex { get; set; } = "#FF00D4FF";
        public int HeaderFontSize { get; set; } = 14;
        public string HeaderColorHex { get; set; } = "#FFFFCC00";
        public int SubHeaderFontSize { get; set; } = 13;
        public string SubHeaderColorHex { get; set; } = "#FF00D4FF";
        public int StandardFontSize { get; set; } = 12;
        public string StandardColorHex { get; set; } = "#FFF2F2F7";
        public int InputFontSize { get; set; } = 13;
        public string InputColorHex { get; set; } = "#FFFFFFFF";
        public string SeparatorColorHex { get; set; } = "#FF2C2C2E";
        public double BorderWidthValue { get; set; } = 1;
        public double BorderCurveValue { get; set; } = 0;

        public int GameNameHeaderFontSize { get; set; } = 32;
        public string GameNameHeaderColorHex { get; set; } = "#FFFFFFFF";
        public int PubYearRatingFontSize { get; set; } = 13;
        public string PubYearRatingColorHex { get; set; } = "#FFFFFFFF";
        public int InfoHeaderFontSize { get; set; } = 20;
        public string InfoHeaderColorHex { get; set; } = "#FFFFFFFF";
        public int InfoBodyFontSize { get; set; } = 16;
        public string InfoBodyColorHex { get; set; } = "#FFFFFFFF";
        public int CreditsHeaderFontSize { get; set; } = 20;
        public string CreditsHeaderColorHex { get; set; } = "#FFFFFFFF";
        public int CreditsBodyFontSize { get; set; } = 16;
        public string CreditsBodyColorHex { get; set; } = "#FFFFFFFF";
        public string VideoBgColorHex { get; set; } = "#FF000000";
        public double VideoBorderSize { get; set; } = 0;
        public string VideoBorderColorHex { get; set; } = "#FF3A3A3C";
        public double VideoBorderRadius { get; set; } = 0;
        public string NavIconsColorHex { get; set; } = "#FFFFFFFF";
        public double NavIconsSize { get; set; } = 9;
        public double PreviewBorderSize { get; set; } = 1;
        public string PreviewBorderColorHex { get; set; } = "#FF3A3A3C";
        public double PreviewBorderRadius { get; set; } = 0;
        public string ContextMenuFontColorHex { get; set; } = "#FFF2F2F7";
        public string ContextMenuIconColorHex { get; set; } = "#FF00D4FF";
        public string ContextMenuBgColorHex { get; set; } = "#FF1C1C1E";
        public string ContextMenuHoverColorHex { get; set; } = "#FFFFFFFF";
        public string ContextMenuHoverBgColorHex { get; set; } = "#FF2C2C2E";
        public int SubTextFontSize { get; set; } = 12;
        public string SubTextColorHex { get; set; } = "#FFA0A0A2";
        public string OptionsMenuBgColorHex { get; set; } = "#FF1C1C1E";
        public double OptionsMenuBorderSize { get; set; } = 1;
        public string OptionsMenuBorderColorHex { get; set; } = "#FF3A3A3C";
        public double OptionsMenuBorderRadius { get; set; } = 0;
        public string TabColorHoverHex { get; set; } = "#FF00D4FF";
        public string TabBgColorHoverHex { get; set; } = "#FF2C2C2E";
        public int OptionsBtnFontSize { get; set; } = 13;
        public string OptionsBtnColorHoverHex { get; set; } = "#FF00D4FF";
        public double OptionsBtnBorderSize { get; set; } = 1;
        public double OptionsBtnBorderRadius { get; set; } = 0;
        public string BtnBgColorHex { get; set; } = "#FF1C1C1E";
        public string BtnBorderColorHex { get; set; } = "#FF00D4FF";
        public string BtnTextColorNormalHex { get; set; } = "#FF00D4FF";
        public string BtnBgColorHoverHex { get; set; } = "#FF2C2C2E";
    }
}
// 🏗️ END: LIGHTWEIGHT THEME SETTINGS MODEL FOR DTO SERIALIZATION