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

        // Flat theme values. Defaults must equal the ConfigurationSettings initializers (the one source of defaults),
        // so a theme saved without a key loads the same value Restore Defaults gives.
        // GlobalThemeResources also reuses this class as the Theme Builder's close-revert snapshot of the Global values.
        public string OptionsBg { get; set; } = "#FF000000";
        public string BorderColorFramework { get; set; } = "#FF3A3A3C";
        public string ScrollTrackColor { get; set; } = "#FF000000";
        public string ScrollTrackHoverColor { get; set; } = "#FF1C1C1E";
        public string ScrollThumbColor { get; set; } = "#FF2C2C2E";
        public string ScrollThumbHoverColor { get; set; } = "#FF3A3A3C";
        public string ScrollThumbDragColor { get; set; } = "#FF00D4FF";
        public int TabFontSize { get; set; } = 12;
        public string TabColorHex { get; set; } = "#FFA0A0A2";
        public string TabBgColorHex { get; set; } = "Transparent";
        public string TabActiveColorHex { get; set; } = "#000000";
        public string TabActiveBgColorHex { get; set; } = "#FF00D4FF";
        public string TabHoverColorHex { get; set; } = "#FF00D4FF";
        public int HeaderFontSize { get; set; } = 14;
        public string HeaderColorHex { get; set; } = "#FFEF44C3";
        public int SubHeaderFontSize { get; set; } = 13;
        public string SubHeaderColorHex { get; set; } = "#FF00D4FF";
        public int StandardFontSize { get; set; } = 13;
        public string StandardColorHex { get; set; } = "#FFF2F2F7";
        public string SeparatorColorHex { get; set; } = "#FF2C2C2E";
        public double BorderWidthValue { get; set; } = 1;
        public double BorderCurveValue { get; set; } = 0;

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
        public string BtnBgColorHoverHex { get; set; } = "#FF422a3c";
    }
}
// 🏗️ END: LIGHTWEIGHT THEME SETTINGS MODEL FOR DTO SERIALIZATION