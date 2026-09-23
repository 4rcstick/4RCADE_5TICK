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
            // Themes live one level up from config, as their own top-level folder in
            // 4rcade5tick_files - .zip rather than .cfg, since a saved theme now bundles the JSON config
            // alongside copies of every referenced asset file (wallpaper, boot splash, etc.), not just
            // the raw settings data.
            return Path.Combine(GetArcadeStickFilesPath(), "Themes", $"{themeName}.zip");
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

        public string OptionsBg { get; set; } = "#FF00000";
        public string BorderColorFramework { get; set; } = "#FF3A3A3C";
        public string MarqueeBorderColorHex { get; set; } = "#FF3A3A3C";
        public double MarqueeBorderWidthValue { get; set; } = 1;

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

        public int SearchBoxFontSize { get; set; } = 13;
        public string SearchBoxColorHex { get; set; } = "#FFF2F2F7";
        public string SearchBoxBgColorHex { get; set; } = "#FF1C1C1E";

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

        public int SearchLabelFontSize { get; set; } = 13;
        public string SearchLabelColorHex { get; set; } = "#FF00D4FF";
        public string SearchLabelBgColorHex { get; set; } = "#001C1C1E";

        public int MainWinBtnFontSize { get; set; } = 13;
        public string MainWinBtnColorHex { get; set; } = "#FF00D4FF";
        public string MainWinBtnBgColorHex { get; set; } = "#FF1C1C1E";
        public string MainWinBtnColorHoverHex { get; set; } = "#FF00D4FF";
        public string MainWinBtnBgColorHoverHex { get; set; } = "#FF2C2C2E";
        public double MainWinBtnBorderSize { get; set; } = 1;
        public string MainWinBtnBorderColorHex { get; set; } = "#FF00D4FF";
        public double MainWinBtnCornerRadius { get; set; } = 0;

        public double GamesBorderSize { get; set; } = 1;
        public string GamesBorderColorHex { get; set; } = "#FF3A3A3C";
        public double GamesBorderCornerRadius { get; set; } = 0;

        public string ContextMenuFontColorHex { get; set; } = "#FFF2F2F7";
        public string ContextMenuIconColorHex { get; set; } = "#FF00D4FF";
        public string ContextMenuBgColorHex { get; set; } = "#FF1C1C1E";
        public string ContextMenuHoverColorHex { get; set; } = "#FFFFFFFF";
        public string ContextMenuHoverBgColorHex { get; set; } = "#FF2C2C2E";

        public double MarqueeBorderRadius { get; set; } = 0;

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
        // 🏁 START: ROM COPY-TO-DISK DESTINATION (DEV TOOL)
        // =========================================================================
        // Absolute path on the host machine - deliberately NOT relative like every other path in this
        // file, since Ctrl+V's copy destination points outside the USB stick entirely (e.g. a folder on
        // the host's hard drive). Empty by default; Shift+Ctrl+X opens the overlay to set it.
        public string RomCopyDestinationPath { get; set; } = "";
        // =========================================================================
        // 🛑 END: ROM COPY-TO-DISK DESTINATION (DEV TOOL)
        // =========================================================================

        // =========================================================================
        // 🏁 START: ARCADE DATABASE (ADB) ARTWORK SCRAPER SETTINGS
        // =========================================================================
        public bool ScraperEnabled { get; set; } = false;

        // Trigger toggles - independent of each other and of ScraperEnabled's category selection.
        public bool ScraperFetchOnLaunch { get; set; } = true;
        public bool ScraperFetchOnContextMenu { get; set; } = true;

        // Category checkboxes - which asset types QUERY_MAME_MEDIA results get downloaded for.
        public bool ScraperFetchMarquees { get; set; } = true;
        public bool ScraperFetchFlyers { get; set; } = true;
        public bool ScraperFetchTitlescreens { get; set; } = true;
        public bool ScraperFetchSnaps { get; set; } = true;
        public bool ScraperFetchCabinets { get; set; } = true;
        public bool ScraperFetchVideos { get; set; } = false;

        // false (default) = skip files that already exist locally rather than overwriting them -
        // consistent with the "fill in only what's missing" trigger model.
        public bool ScraperOverwriteExisting { get; set; } = false;
        // =========================================================================
        // 🛑 END: ARCADE DATABASE (ADB) ARTWORK SCRAPER SETTINGS
        // =========================================================================

        // =========================================================================
        // 🏁 START: CORE CACHE TRACKING FILES CONFIGURATION RECORDS
        // =========================================================================
        public string MameExeName { get; set; } = "mame.exe";

        // Virtual category auto-sort toggle - when true, the game tree renders the sort_database.ini
        // generated category tree instead of the custom folder structure. Underlying custom folder data
        // is never destroyed by this toggle. Driven by the Sorting window's "Enable Auto Sorting" checkbox.
        // Defaults to true so a fresh install (especially a large, unsorted single-folder ROM dump)
        // renders into manageable category buckets immediately rather than one giant flat folder -
        // sidesteps the known WPF container-generation freeze risk on very large single buckets.
        public bool IsCategorySortEnabled { get; set; } = true;

        // Independent of IsCategorySortEnabled - this controls whether the filter checkboxes/slider below
        // have any effect at all, and applies regardless of which folder structure (custom or catver) is
        // active. Off by default; all filter controls in the Sorting window grey out until this is checked.
        public bool IsSortFilteringEnabled { get; set; } = false;

        // Sorting window filter state - persisted so selections survive a relaunch. Converted into
        // VirtualCategorySortService.SortFilterOptions' HashSets at tree-build time. Empty list means no
        // restriction (matches the checkbox-row "checking nothing behaves like checking everything"
        // semantics already established in IsVariantEligible).
        public List<string> SortEnabledRegions { get; set; } = new();
        public List<int> SortEnabledPlayerCounts { get; set; } = new();
        public List<string> SortEnabledGenres { get; set; } = new();
        public bool SortIncludeRevisions { get; set; } = false;
        public int SortMinimumRating { get; set; } = 0;

        // Upper bound of the rating range filter, paired with SortMinimumRating above. Defaults to 100
        // (no ceiling restriction) so a fresh install behaves the same as before this field existed.
        public int SortMaximumRating { get; set; } = 100;

        // Year range filter, same span-the-full-range-by-default pattern as Rating above.
        public int SortMinimumYear { get; set; } = 1970;
        public int SortMaximumYear { get; set; } = 2026;

        // Single-select manufacturer filter. Empty string (default) means no restriction.
        public string SortSelectedManufacturer { get; set; } = string.Empty;
        public string FavoritesListFile { get; set; } = "favorites.cfg";
        public string PlayHistoryFile { get; set; } = "play_history.cfg";
        // Each holds just a single hex color line - same shape as a custom folder's own .cfg, minus the
        // ROM membership lines, since these two folders' membership is computed from play_history.cfg
        // instead of a manually-curated list.
        public string RecentlyPlayedColorFile { get; set; } = "recently_played_color.cfg";
        public string MostPlayedColorFile { get; set; } = "most_played_color.cfg";
        public string FolderOrderFile { get; set; } = "folder_order.cfg";
        public string VirtualOrderFile { get; set; } = "virtual_order.cfg";
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

        // Resolves a single asset-category subfolder (e.g. "marquees") against the consolidated
        // media/ folder at MAME root. Mirrors the cleaning logic previously duplicated across five
        // local ResolvePath functions in MainViewModel - strips a leading ".\", trims stray leading
        // slashes, and respects an already-rooted/absolute path exactly as those did. Used by both
        // MainViewModel's media resolution and ArtworkScraperService's download targets, so a user's
        // customized Asset Paths tab entries are honored everywhere automatically.
        public string GetMediaCategoryPath(string categorySubfolder)
        {
            if (string.IsNullOrWhiteSpace(categorySubfolder)) return string.Empty;

            string cleanPath = categorySubfolder.Replace(@".\", "").TrimStart('\\', '/');
            if (Path.IsPathRooted(cleanPath)) return cleanPath;

            return Path.Combine(GetMamePath(), MediaSubFolder, cleanPath);
        }

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
            this.MarqueeBorderColorHex = source.MarqueeBorderColorHex;
            this.MarqueeBorderWidthValue = source.MarqueeBorderWidthValue;
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
            this.RecentlyPlayedColorFile = source.RecentlyPlayedColorFile;
            this.MostPlayedColorFile = source.MostPlayedColorFile;
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
            this.SearchBoxFontSize = source.SearchBoxFontSize;
            this.SearchBoxColorHex = source.SearchBoxColorHex;
            this.SearchBoxBgColorHex = source.SearchBoxBgColorHex;

            this.GameNameHeaderFontSize = source.GameNameHeaderFontSize;
            this.GameNameHeaderColorHex = source.GameNameHeaderColorHex;
            this.PubYearRatingFontSize = source.PubYearRatingFontSize;
            this.PubYearRatingColorHex = source.PubYearRatingColorHex;
            this.InfoHeaderFontSize = source.InfoHeaderFontSize;
            this.InfoHeaderColorHex = source.InfoHeaderColorHex;
            this.InfoBodyFontSize = source.InfoBodyFontSize;
            this.InfoBodyColorHex = source.InfoBodyColorHex;
            this.CreditsHeaderFontSize = source.CreditsHeaderFontSize;
            this.CreditsHeaderColorHex = source.CreditsHeaderColorHex;
            this.CreditsBodyFontSize = source.CreditsBodyFontSize;
            this.CreditsBodyColorHex = source.CreditsBodyColorHex;
            this.VideoBgColorHex = source.VideoBgColorHex;
            this.VideoBorderSize = source.VideoBorderSize;
            this.VideoBorderColorHex = source.VideoBorderColorHex;
            this.VideoBorderRadius = source.VideoBorderRadius;
            this.NavIconsColorHex = source.NavIconsColorHex;
            this.NavIconsSize = source.NavIconsSize;
            this.PreviewBorderSize = source.PreviewBorderSize;
            this.PreviewBorderColorHex = source.PreviewBorderColorHex;
            this.PreviewBorderRadius = source.PreviewBorderRadius;

            this.SearchLabelFontSize = source.SearchLabelFontSize;
            this.SearchLabelColorHex = source.SearchLabelColorHex;
            this.SearchLabelBgColorHex = source.SearchLabelBgColorHex;

            this.MainWinBtnFontSize = source.MainWinBtnFontSize;
            this.MainWinBtnColorHex = source.MainWinBtnColorHex;
            this.MainWinBtnBgColorHex = source.MainWinBtnBgColorHex;
            this.MainWinBtnColorHoverHex = source.MainWinBtnColorHoverHex;
            this.MainWinBtnBgColorHoverHex = source.MainWinBtnBgColorHoverHex;
            this.MainWinBtnBorderSize = source.MainWinBtnBorderSize;
            this.MainWinBtnBorderColorHex = source.MainWinBtnBorderColorHex;
            this.MainWinBtnCornerRadius = source.MainWinBtnCornerRadius;

            this.GamesBorderSize = source.GamesBorderSize;
            this.GamesBorderColorHex = source.GamesBorderColorHex;
            this.GamesBorderCornerRadius = source.GamesBorderCornerRadius;

            this.ContextMenuFontColorHex = source.ContextMenuFontColorHex;
            this.ContextMenuIconColorHex = source.ContextMenuIconColorHex;
            this.ContextMenuBgColorHex = source.ContextMenuBgColorHex;
            this.ContextMenuHoverColorHex = source.ContextMenuHoverColorHex;
            this.ContextMenuHoverBgColorHex = source.ContextMenuHoverBgColorHex;

            this.MarqueeBorderRadius = source.MarqueeBorderRadius;

            this.SubTextFontSize = source.SubTextFontSize;
            this.SubTextColorHex = source.SubTextColorHex;
            this.OptionsMenuBgColorHex = source.OptionsMenuBgColorHex;
            this.OptionsMenuBorderSize = source.OptionsMenuBorderSize;
            this.OptionsMenuBorderColorHex = source.OptionsMenuBorderColorHex;
            this.OptionsMenuBorderRadius = source.OptionsMenuBorderRadius;
            this.TabColorHoverHex = source.TabColorHoverHex;
            this.TabBgColorHoverHex = source.TabBgColorHoverHex;
            this.OptionsBtnFontSize = source.OptionsBtnFontSize;
            this.OptionsBtnColorHoverHex = source.OptionsBtnColorHoverHex;
            this.OptionsBtnBorderSize = source.OptionsBtnBorderSize;
            this.OptionsBtnBorderRadius = source.OptionsBtnBorderRadius;

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