// 🏗️ START: LIGHTWEIGHT THEME SETTINGS MODEL FOR DTO SERIALIZATION
using System;

namespace ArcadeStick.Models
{
    public class ThemeSettings
    {
        public string BackgroundColor { get; set; } = "#FF1C1C1E";
        public string FileListBg { get; set; } = "#FF000000";
        public string MarqueeBoxBg { get; set; } = "#FF000000";
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
        public string GamesListWallpaper { get; set; } = "";
        public string MarqueeWindowWallpaper { get; set; } = "";
        public string MediaWindowWallpaper { get; set; } = "";
        public string ThemeLogo { get; set; } = "";
        public string ThemeBootSplash { get; set; } = "";
        public string ThemeMissingPreview { get; set; } = "";

        public int FolderFontSize { get; set; } = 14;
        public string FolderColorHex { get; set; } = "#FFF2F2F7";
        public string FolderSelectedColorHex { get; set; } = "#FF00D4FF";
        public string FolderSelectedBgColorHex { get; set; } = "#FF2C2C2E";
        public int GameFontSize { get; set; } = 13;
        public string GameColorHex { get; set; } = "#FFA0A0A2";
        public string GameHoverColorHex { get; set; } = "#FFFFFFFF";
        public string GameSelectedColorHex { get; set; } = "#FF00D4FF";
        public string GameSelectedBgColorHex { get; set; } = "#FF2C2C2E";
        public string FavoritesColorHex { get; set; } = "#FFFFCC00";

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

        public string BtnBgColorHex { get; set; } = "#FF1C1C1E";
        public string BtnBorderColorHex { get; set; } = "#FF00D4FF";
        public string BtnTextColorNormalHex { get; set; } = "#FF00D4FF";
        public string BtnBgColorHoverHex { get; set; } = "#FF2C2C2E";
    }
}
// 🏗️ END: LIGHTWEIGHT THEME SETTINGS MODEL FOR DTO SERIALIZATION