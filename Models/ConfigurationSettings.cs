// 🏗️ START: EXPANDED THEME BUILDER CONFIGURATION MODEL PAYLOAD
using System;
using System.IO;
using System.Collections.Generic;

namespace ArcadeStick.Models
{
    public class ConfigurationSettings
    {
        // 🏗️ INFRASTRUCTURE ANCHOR
        public string BaseDirectory { get; set; }

        // 🏗️ Add this method to your ConfigurationSettings class
        public string GetThemePath(string themeName)
        {
            return Path.Combine(GetConfigPath(), "Themes", $"{themeName}.cfg");
        }

        public ConfigurationSettings()
        {
            // 🏗️ Check for boot.cfg to override the working directory
            string bootFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boot.cfg");

            if (File.Exists(bootFile))
            {
                string customPath = File.ReadAllText(bootFile).Trim();
                if (Directory.Exists(customPath))
                {
                    BaseDirectory = customPath;
                }
                else
                {
                    // Fallback if path in boot.cfg is invalid
                    BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                }
            }
            else
            {
                // Fallback to local directory if boot.cfg is missing
                BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        // =========================================================================
        // 🏁 START: THEME PALETTE HEX BRUSH PROPERTIES
        // =========================================================================
        public string BackgroundColor { get; set; } = "#FF1C1C1E";
        public string FileListBg { get; set; } = "#FF000000";
        public string MarqueeBoxBg { get; set; } = "#FF000000";
        public string VideoBoxBg { get; set; } = "#FF000000";
        public string VideoBoxRecordBg { get; set; } = "#4A0000";
        public string TextColor { get; set; } = "#F2F2F7";
        public string MainFoldersColor { get; set; } = "#00D4FF";
        public string VirtualListsColor { get; set; } = "#FFD60A";
        public string FavoritesColor { get; set; } = "#EFBF04";

        public string OptionsBg { get; set; } = "";
        public string BorderColorFramework { get; set; } = "#FF3A3A3C";

        public string ScrollTrackColor { get; set; } = "#FF000000";
        public string ScrollTrackHoverColor { get; set; } = "#FF1C1C1E";
        public string ScrollThumbColor { get; set; } = "#FF2C2C2E";
        public string ScrollThumbHoverColor { get; set; } = "#FF3A3A3C";
        public string ScrollThumbDragColor { get; set; } = "#FF00D4FF";
        // =========================================================================
        // 🛑 END: THEME PALETTE HEX BRUSH PROPERTIES
        // =========================================================================

        // =========================================================================
        // 🏁 START: CUSTOM VISUAL ENVIRONMENT WALLPAPER REFERENCES
        // =========================================================================
        public string MainWindowWallpaper { get; set; } = "";
        public bool DisableMainBgImage { get; set; } = false;
        public string GamesListWallpaper { get; set; } = "";
        public string MarqueeWindowWallpaper { get; set; } = "";
        public string MediaWindowWallpaper { get; set; } = "";

        public string ThemeLogo { get; set; } = "";
        public string ThemeBootSplash { get; set; } = "";
        public string ThemeMissingPreview { get; set; } = "";
        public string ActiveThemeName { get; set; } = "";
        // =========================================================================
        // 🛑 END: CUSTOM VISUAL ENVIRONMENT WALLPAPER REFERENCES
        // =========================================================================

        // =========================================================================
        // 🏁 START: SECTION 4 TYPOGRAPHY AND SUB-INTERFACE SPECIFICATION RULES
        // =========================================================================
        public int FolderFontSize { get; set; } = 17;
        public string FolderColorHex { get; set; } = "#FF00D4FF";
        public string FolderSelectedColorHex { get; set; } = "#FF000000";
        public string FolderSelectedBgColorHex { get; set; } = "#FF00D4FF";
        public int GameFontSize { get; set; } = 15;
        public string GameColorHex { get; set; } = "#FFA0A0A2";
        public string GameHoverColorHex { get; set; } = "#FFEF44C3";
        public string GameSelectedColorHex { get; set; } = "#FF000000";
        public string GameSelectedBgColorHex { get; set; } = "#FF00D4FF";
        public string FavoritesColorHex { get; set; } = "#FFFFCC00";
        public string ArrowColorHex { get; set; } = "#FFA0A0A2";
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
        public int InputFontSize { get; set; } = 13;
        public string InputColorHex { get; set; } = "#FFFFFFFF";
        public string SeparatorColorHex { get; set; } = "#FF2C2C2E";
        public double BorderWidthValue { get; set; } = 1;
        public double BorderCurveValue { get; set; } = 0;
        // =========================================================================
        // 🛑 END: SECTION 4 TYPOGRAPHY AND SUB-INTERFACE SPECIFICATION RULES
        // =========================================================================

        // =========================================================================
        // 🏁 START: SECTION 5 INTERACTIVE UI BUTTON COMPONENT KEYS
        // =========================================================================
        public string BtnBgColorHex { get; set; } = "#FF1C1C1E";
        public string BtnBorderColorHex { get; set; } = "#FF00D4FF";
        public string BtnTextColorNormalHex { get; set; } = "#FF00D4FF";
        public string BtnBgColorHoverHex { get; set; } = "#FF422a3c";
        // =========================================================================
        // 🛑 END: SECTION 5 INTERACTIVE UI BUTTON COMPONENT KEYS
        // =========================================================================

        // =========================================================================
        // 🏗️ START: HOST MACHINE FILE SYSTEM DIRECTORY STRUCTURE
        // =========================================================================
        public string ArcadeStickFilesFolder { get; set; } = "4rcade5tick_files";
        public string RomsSubFolder { get; set; } = "roms";
        public string MediaSubFolder { get; set; } = "media";
        public string ConfigSubFolder { get; set; } = "config";
        public string PlaylistsSubFolder { get; set; } = "gamelists";
        public string ChdPath { get; set; } = "chd";
        public string BiosPath { get; set; } = "bios";

        public string MarqueesPath { get; set; } = "marquees";
        public string VideosPath { get; set; } = "videos";
        public string FlyersPath { get; set; } = "flyers";
        public string ScreenshotsPath { get; set; } = "snap";
        public string TitlescreensPath { get; set; } = "titles";
        public string CabinetsPath { get; set; } = "cabinets";
        // =========================================================================
        // 🛑 END: HOST MACHINE FILE SYSTEM DIRECTORY STRUCTURE
        // =========================================================================

        // =========================================================================
        // 🏁 START: CORE CACHE TRACKING FILES CONFIGURATION RECORDS
        // =========================================================================
        public string MameExeName { get; set; } = "mame.exe";
        public string MameCacheFile { get; set; } = "mame_cache.txt";
        public string FavoritesListFile { get; set; } = "favorites.cfg";
        public string FolderOrderFile { get; set; } = "folder_order.cfg";
        public string MediaOrderFile { get; set; } = "media_order.cfg";
        public string MouseSupportFile { get; set; } = "mouse_support.cfg";
        public string LaunchOptionsFile { get; set; } = "launch_options.cfg";
        public string InputOptionsFile { get; set; } = "input_options.cfg";
        public string StorageOptionsFile { get; set; } = "storage_options.cfg";
        public List<string> MediaPriorityOrder { get; set; } = new() { "videos", "flyers", "screenshots", "titlescreens", "cabinets" };

        public bool EnableGamepadPolling { get; set; } = true;
        public bool AllowBackgroundInput { get; set; } = false;
        public int DirectInputDeviceId { get; set; } = 0;
        public int JoystickDeadzonePercentage { get; set; } = 15;

        // Joystick Delay defaults
        public int JoystickInitialDelayMs { get; set; } = 500;
        public int JoystickRepeatDelayMs { get; set; } = 75;
        public string NavigationMode { get; set; } = "D-Pad & Analog";
        // =========================================================================
        // 🛑 END: CORE CACHE TRACKING FILES CONFIGURATION RECORDS
        // =========================================================================

        // =========================================================================
        // 🏗️ START: SYSTEM LOGIC WORKSPACE LOCATION RESOLVERS & OPERATIONS
        // =========================================================================
        public string GetMamePath() => BaseDirectory;
        public string GetArcadeStickFilesPath() => Path.Combine(BaseDirectory, ArcadeStickFilesFolder);
        public string GetConfigPath() => Path.Combine(GetArcadeStickFilesPath(), ConfigSubFolder);

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Windows.Media.ImageSource? BmacButtonAsset
        {
            get
            {
                try
                {
                    string path = Path.Combine(GetArcadeStickFilesPath(), "assets", "bmac.png");
                    if (!File.Exists(path)) return null;

                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    byte[] fileBytes = File.ReadAllBytes(path);
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(fileBytes);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
            }
        }
        public void ApplyThemeSettings(ConfigurationSettings source)
        {
            if (source == null) return;

            this.MainWindowWallpaper = source.MainWindowWallpaper;
            this.DisableMainBgImage = source.DisableMainBgImage;
            this.GamesListWallpaper = source.GamesListWallpaper;
            this.MarqueeWindowWallpaper = source.MarqueeWindowWallpaper;
            this.MediaWindowWallpaper = source.MediaWindowWallpaper;
            this.ThemeLogo = source.ThemeLogo;
            this.ThemeBootSplash = source.ThemeBootSplash;
            this.ThemeMissingPreview = source.ThemeMissingPreview;
            this.ActiveThemeName = source.ActiveThemeName;

            this.BackgroundColor = source.BackgroundColor;
            this.FileListBg = source.FileListBg;
            this.MarqueeBoxBg = source.MarqueeBoxBg;
            this.VideoBoxBg = source.VideoBoxBg;
            this.OptionsBg = source.OptionsBg;
            this.BorderColorFramework = source.BorderColorFramework;
            this.ScrollTrackColor = source.ScrollTrackColor;
            this.ScrollTrackHoverColor = source.ScrollTrackHoverColor;
            this.ScrollThumbColor = source.ScrollThumbColor;
            this.ScrollThumbHoverColor = source.ScrollThumbHoverColor;
            this.ScrollThumbDragColor = source.ScrollThumbDragColor;

            this.FolderFontSize = source.FolderFontSize;
            this.FolderColorHex = source.FolderColorHex;
            this.FolderSelectedColorHex = source.FolderSelectedColorHex;
            this.FolderSelectedBgColorHex = source.FolderSelectedBgColorHex;
            this.GameFontSize = source.GameFontSize;
            this.GameColorHex = source.GameColorHex;
            this.GameHoverColorHex = source.GameHoverColorHex;
            this.GameSelectedColorHex = source.GameSelectedColorHex;
            this.GameSelectedBgColorHex = source.GameSelectedBgColorHex;
            this.FavoritesColorHex = source.FavoritesColorHex;
            this.ArrowColorHex = source.ArrowColorHex;

            this.TabFontSize = source.TabFontSize;
            this.TabColorHex = source.TabColorHex;
            this.TabColorHex = source.TabColorHex;
            this.TabBgColorHex = source.TabBgColorHex;
            this.TabActiveColorHex = source.TabActiveColorHex;
            this.TabActiveBgColorHex = source.TabActiveBgColorHex;
            this.TabHoverColorHex = source.TabHoverColorHex;
            this.HeaderFontSize = source.HeaderFontSize;
            this.HeaderColorHex = source.HeaderColorHex;
            this.SubHeaderFontSize = source.SubHeaderFontSize;
            this.SubHeaderColorHex = source.SubHeaderColorHex;
            this.StandardFontSize = source.StandardFontSize;
            this.StandardColorHex = source.StandardColorHex;
            this.InputFontSize = source.InputFontSize;
            this.InputColorHex = source.InputColorHex;
            this.SeparatorColorHex = source.SeparatorColorHex;
            this.BorderWidthValue = source.BorderWidthValue;
            this.BorderCurveValue = source.BorderCurveValue;

            this.BtnBgColorHex = source.BtnBgColorHex;
            this.BtnBorderColorHex = source.BtnBorderColorHex;
            this.BtnTextColorNormalHex = source.BtnTextColorNormalHex;
            this.BtnBgColorHoverHex = source.BtnBgColorHoverHex;
        }
    
        // =========================================================================
        // 🛑 END: SYSTEM LOGIC WORKSPACE LOCATION RESOLVERS & OPERATIONS
        // =========================================================================
    }
}
// 🏗️ END: EXPANDED THEME BUILDER CONFIGURATION MODEL PAYLOAD