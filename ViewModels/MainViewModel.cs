// [SECTION: File Overrides] - MainViewModel layered preview binding extensions
using ArcadeStick.Models;
using ArcadeStick.Services;
using ArcadeStick.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ArcadeStick.ViewModels
{
    // Controls how folder/game text is emphasized against busy backgrounds.
    // None = plain text, DropShadow = soft shadow, Stroke = solid outline.
    public enum TextEmphasisMode
    {
        None,
        DropShadow,
        Stroke
    }
    // Used by MoveFolderInOrder (context menu "Move" submenu) - shared between MainViewModel and
    // MainWindow_xaml.cs.
    public enum MoveDirection
    {
        Top,
        Up,
        Down,
        Bottom
    }

    // Result of one "Play Random Game" spin - FillerFrames play in sequence during the swap-loop
    // animation, then OvershootFrame plays as the partial-peek beat, then the animation snaps to
    // WinnerFrame. Winner is locked in before any frame in this sequence is generated.
    public class RandomizerSpinResult
    {
        public List<GameItem> FillerFrames { get; set; } = new();
        public GameItem OvershootFrame { get; set; } = null!;
        public GameItem WinnerFrame { get; set; } = null!;
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CacheScannerService _cacheService;
        private readonly ProcessLaunchService _launchService;
        private readonly ArtworkScraperService _scraperService;

        // In-flight guard for the context-menu "Get Artwork" trigger - prevents rapid-fire clicking
        // from stacking parallel requests for the same romname. Not used by the launch trigger, since
        // a game can only be launched once at a time anyway.
        private readonly HashSet<string> _scraperInFlightRoms = new(StringComparer.OrdinalIgnoreCase);
        private readonly VirtualCategorySortService _sortService;
        private readonly HistoryXmlService _historyService;
        private readonly GameInfoService _wikiService;

        // [SECTION: Play Random Game]
        // Shared instance reused across spins rather than "new Random()" per spin - avoids the classic
        // clock-tick-seed pitfall if spins were ever triggered in rapid succession.
        private readonly Random _randomizerRng = new Random();

        // Tracks the immediately preceding spin's winner so it can be excluded from the very next
        // spin's pool only (one-spin cooldown, not a permanent exclusion) - null before the first spin.
        private string? _lastRandomizerWinnerRomName;
        // [END SECTION: Play Random Game]

        private string _searchText = string.Empty;
        private DispatcherTimer? _searchDebounceTimer;
        private DispatcherTimer? _previewDebounceTimer;
        private GameItem? _selectedGame;
        private BitmapImage? _marqueeImage;
        private string _videoSourcePath = string.Empty;
        private bool _isDevMode;
        private bool _isCategorySortEnabled;

        public List<string> PreviewPriorityOrder { get; private set; } = new List<string>();

        // Converts a hex string or WPF named color into a brush without throwing. Bare hex digits with
        // no "#" (e.g. "1C1C1E") are prepended with "#" before conversion, matching the normalization
        // ThemesTabControl.xaml.cs's FormatHexCode already does for the Theme Builder UI. Falls back to
        // Brushes.Gray on null/empty input or any conversion failure so a malformed saved value can't
        // throw and break the caller.
        internal static SolidColorBrush SafeConvertToBrush(string hexOrColorName)
        {
            if (string.IsNullOrEmpty(hexOrColorName)) return Brushes.Gray;

            string input = hexOrColorName;
            if (!input.StartsWith("#") && !IsKnownColorName(input))
            {
                input = "#" + input;
            }

            try
            {
                return (SolidColorBrush)new BrushConverter().ConvertFrom(input)!;
            }
            catch
            {
                return Brushes.Gray;
            }
        }

        // Checks whether a string matches a recognized WPF named color (e.g. "Red", "Transparent")
        private static bool IsKnownColorName(string input)
        {
            return typeof(Colors).GetProperty(input, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase) != null;
        }

        // [SECTION: Constructor & Settings Load]
        // Creates ConfigurationSettings, loads settings.json (theme + path overrides) if present via
        // ApplyThemeSettings, wires up the cache/launch services, initializes collections/commands, and
        // loads favorites/mouse-support/preview-order from disk before doing an initial theme refresh.
        public MainViewModel()
        {
            Configuration = new ConfigurationSettings();
            string configFilePath = Path.Combine(Configuration.GetArcadeStickFilesPath(), "settings.json");

            if (File.Exists(configFilePath))
            {
                try
                {
                    string jsonString = File.ReadAllText(configFilePath);
                    var loadedSettings = System.Text.Json.JsonSerializer.Deserialize<ConfigurationSettings>(jsonString);

                    if (loadedSettings != null)
                    {
                        Configuration.ApplyThemeSettings(loadedSettings);

                        Configuration.ChdPath = loadedSettings.ChdPath;
                        Configuration.RomsSubFolder = loadedSettings.RomsSubFolder;
                        Configuration.BiosPath = loadedSettings.BiosPath;
                        Configuration.MarqueesPath = loadedSettings.MarqueesPath;
                        Configuration.VideosPath = loadedSettings.VideosPath;
                        Configuration.FlyersPath = loadedSettings.FlyersPath;
                        Configuration.ScreenshotsPath = loadedSettings.ScreenshotsPath;
                        Configuration.TitlescreensPath = loadedSettings.TitlescreensPath;
                        Configuration.CabinetsPath = loadedSettings.CabinetsPath;
                        Configuration.IsCategorySortEnabled = loadedSettings.IsCategorySortEnabled;
                        Configuration.IsSortFilteringEnabled = loadedSettings.IsSortFilteringEnabled;
                        Configuration.SortEnabledRegions = loadedSettings.SortEnabledRegions;
                        Configuration.SortEnabledPlayerCounts = loadedSettings.SortEnabledPlayerCounts;
                        Configuration.SortEnabledGenres = loadedSettings.SortEnabledGenres;
                        Configuration.SortIncludeRevisions = loadedSettings.SortIncludeRevisions;
                        Configuration.SortMinimumRating = loadedSettings.SortMinimumRating;
                        Configuration.SortMaximumRating = loadedSettings.SortMaximumRating;
                        Configuration.SortMinimumYear = loadedSettings.SortMinimumYear;
                        Configuration.SortMaximumYear = loadedSettings.SortMaximumYear;
                        Configuration.SortSelectedManufacturer = loadedSettings.SortSelectedManufacturer;

                        Configuration.ScraperEnabled = loadedSettings.ScraperEnabled;
                        Configuration.ScraperFetchOnLaunch = loadedSettings.ScraperFetchOnLaunch;
                        Configuration.ScraperFetchOnContextMenu = loadedSettings.ScraperFetchOnContextMenu;
                        Configuration.ScraperFetchMarquees = loadedSettings.ScraperFetchMarquees;
                        Configuration.ScraperFetchFlyers = loadedSettings.ScraperFetchFlyers;
                        Configuration.ScraperFetchTitlescreens = loadedSettings.ScraperFetchTitlescreens;
                        Configuration.ScraperFetchSnaps = loadedSettings.ScraperFetchSnaps;
                        Configuration.ScraperFetchCabinets = loadedSettings.ScraperFetchCabinets;
                        Configuration.ScraperFetchVideos = loadedSettings.ScraperFetchVideos;
                        Configuration.ScraperOverwriteExisting = loadedSettings.ScraperOverwriteExisting;

                        Configuration.RomCopyDestinationPath = loadedSettings.RomCopyDestinationPath;
                    }
                }
                catch
                {
                }
            }

            _cacheService = new CacheScannerService(Configuration);
            _launchService = new ProcessLaunchService(Configuration);
            _scraperService = new ArtworkScraperService(Configuration);
            _sortService = new VirtualCategorySortService(Configuration);
            _historyService = new HistoryXmlService(Configuration);
            _wikiService = new GameInfoService(Configuration);

            // Sync the initial toggle state from loaded settings directly into the backing field,
            // bypassing the IsCategorySortEnabled setter (which persists to disk and rebuilds the tree -
            // neither is valid yet at this point in construction).
            _isCategorySortEnabled = Configuration.IsCategorySortEnabled;

            GamesCollection = new ObservableCollection<GameItem>();
            TreeNodesCollection = new ObservableCollection<TreeCategoryNode>();

            RefreshCacheCommand = new RelayCommand(async _ => await InitializeDatabaseAsync());
            LaunchGameCommand = new RelayCommand(async param => await ExecuteLaunchAsync(param));

            LoadFavoritesFromDisk();
            LoadMouseSupportFromDisk();
            LoadPreviewOrderConfig();

            // Fresh install (settings.json didn't exist above) or any other case where no theme is
            // active - extract and apply the bundled default.zip through the same pipeline a user-saved
            // theme goes through. Swallows failure silently: a missing/corrupt default.zip just leaves
            // Configuration at its compiled defaults (loose default_marquee.png/no_preview.png fallbacks
            // still cover the logo/missing-preview art; the boot splash panel is simply blank).
            if (string.IsNullOrWhiteSpace(Configuration.ActiveThemeName))
            {
                try { LoadTheme("default"); } catch { }
            }

            RefreshThemeBindings();
        }
        // [END SECTION: Constructor & Settings Load]

        // [SECTION: Preview Order Config]
        // Loads (or creates with defaults) preview_order.cfg, which controls the priority order media
        // types are checked in when resolving what to show in the main media panel (see UpdateActiveMediaPreviews).
        private void LoadPreviewOrderConfig()
        {
            string configDir = Configuration.GetConfigPath();
            string configFile = Path.Combine(configDir, "preview_order.cfg");

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            // Row 1's preview slot only ever resolves video/screenshot/titlescreen - flyers and cabinets
            // are shown exclusively in the media panel rework's Row 3 (Flyer/Cabinet/Staff/Technical
            // 4-state slot) now, so they're excluded here to avoid the same image appearing redundantly
            // in both places. Existing configs saved before this change are filtered on load too, so a
            // stale preview_order.cfg from beta.3 doesn't keep resolving flyers/cabinets into Row 1.
            if (!File.Exists(configFile))
            {
                string[] defaultOrder = { "videos", "screenshots", "titlescreens" };
                File.WriteAllLines(configFile, defaultOrder);
                PreviewPriorityOrder = new List<string>(defaultOrder);
            }
            else
            {
                PreviewPriorityOrder = File.ReadAllLines(configFile)
                                          .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                                          .Select(line => line.Trim().ToLower())
                                          .Where(category => category != "flyers" && category != "cabinets")
                                          .ToList();
            }
        }
        // [END SECTION: Preview Order Config]

        // [SECTION: Core Properties, Collections & Commands]
        public ConfigurationSettings Configuration { get; }
        public ObservableCollection<GameItem> GamesCollection { get; }
        public ObservableCollection<TreeCategoryNode> TreeNodesCollection { get; }

        // True for the duration of UpdateLiveTreeDisplay's body. TreeNodesCollection.Clear() mid-rebuild
        // destroys the previously-selected row's container, which fires a transient null through
        // GameTree_SelectedItemChanged - not a genuine user deselection. MainWindow_xaml.cs checks this
        // flag to skip that transient null, since letting it through nulls SelectedGame/VideoSourcePath,
        // which stops the preview player and (via IsGameSelected) starts the boot splash player almost
        // simultaneously - a native LibVLC/D3D11 collision that hangs the UI thread.
        public bool IsRebuildingTree { get; private set; }
        public RangeObservableCollection<object> FlatVisibleRows { get; } = new RangeObservableCollection<object>();
        public HashSet<string> FavoriteRoms { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> MouseSupportRoms { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public ICommand RefreshCacheCommand { get; }
        public ICommand LaunchGameCommand { get; }
        // [END SECTION: Core Properties, Collections & Commands]

        // [SECTION: Search & Dev Mode]
        // Dev mode toggles ROM name prefixing in the tree (see UpdateLiveTreeDisplay -> GetFormattedTitle).
        public bool IsDevMode
        {
            get => _isDevMode;
            set
            {
                if (_isDevMode != value)
                {
                    _isDevMode = value;
                    OnPropertyChanged();
                    UpdateLiveTreeDisplay();
                }
            }
        }

        // Virtual category auto-sort toggle. Persists immediately to settings.json (mirroring
        // OptionsWindow.PersistSettingsToDisk's serialization) since this checkbox lives on the main
        // window rather than behind the Options Save button, then rebuilds the tree.
        public bool IsCategorySortEnabled
        {
            get => _isCategorySortEnabled;
            set
            {
                if (_isCategorySortEnabled != value)
                {
                    _isCategorySortEnabled = value;
                    Configuration.IsCategorySortEnabled = value;
                    OnPropertyChanged();
                    PersistConfigurationToDisk();
                    UpdateLiveTreeDisplay();
                }
            }
        }

        // Serializes the full ConfigurationSettings object to settings.json - same approach as
        // OptionsWindow.PersistSettingsToDisk, duplicated here since several main-window-level controls
        // (category sort toggle, Sorting window, RomCopyPathWindow) save independently of the Options
        // window's Save button. Public so those callers can persist without routing through a command.
        public void PersistConfigurationToDisk()
        {
            try
            {
                string configFilePath = Path.Combine(Configuration.GetArcadeStickFilesPath(), "settings.json");
                var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string jsonString = System.Text.Json.JsonSerializer.Serialize(Configuration, jsonOptions);
                File.WriteAllText(configFilePath, jsonString);
            }
            catch
            {
            }
        }

        // [SECTION: Context Menu Text Size]
        // Multiplicative nudge (not a flat +/- N) so FolderFontSize and GameFontSize scale by the same
        // ratio every time, preserving whatever visual hierarchy the active theme set between them
        // regardless of how many times this gets clicked. Clamped to a sane range so repeated clicks
        // can't collapse text to unreadable or runaway sizes. Persists immediately, same as
        // IsCategorySortEnabled's setter above - this lives in a context menu, not behind Options' Save.
        public void NudgeFontSize(double percentChange)
        {
            Configuration.FolderFontSize = (int)Math.Round(Math.Clamp(Configuration.FolderFontSize * (1 + percentChange), 6, 72));
            Configuration.GameFontSize = (int)Math.Round(Math.Clamp(Configuration.GameFontSize * (1 + percentChange), 6, 72));

            RefreshThemeBindings();
            RefreshFolderColorsLive();
            RefreshGameColorsLive();
            PersistConfigurationToDisk();
        }

        // Reverts both font sizes (and, as a side effect of LoadTheme, everything else theme-related)
        // back to the currently active theme's saved values - re-persists afterward so settings.json
        // stays in sync with what's now displayed.
        public void ResetFontSizeToSavedTheme()
        {
            if (string.IsNullOrWhiteSpace(Configuration.ActiveThemeName)) return;

            LoadTheme(Configuration.ActiveThemeName);
            PersistConfigurationToDisk();
        }
        // [END SECTION: Context Menu Text Size]

        // Single entry point for the Sorting window's Apply button - writes every filter field into
        // Configuration, persists once, and rebuilds the tree once, rather than each field triggering
        // its own save/rebuild the way the old single-checkbox setter did.
        public void ApplySortingWindowSettings(bool categorySortEnabled, bool filteringEnabled, List<string> enabledRegions, List<int> enabledPlayerCounts, List<string> enabledGenres, bool includeRevisions, int minimumRating, int maximumRating, int minimumYear, int maximumYear, string selectedManufacturer)
        {
            _isCategorySortEnabled = categorySortEnabled;
            Configuration.IsCategorySortEnabled = categorySortEnabled;
            Configuration.IsSortFilteringEnabled = filteringEnabled;
            Configuration.SortEnabledRegions = enabledRegions;
            Configuration.SortEnabledPlayerCounts = enabledPlayerCounts;
            Configuration.SortEnabledGenres = enabledGenres;
            Configuration.SortIncludeRevisions = includeRevisions;
            Configuration.SortMinimumRating = minimumRating;
            Configuration.SortMaximumRating = maximumRating;
            Configuration.SortMinimumYear = minimumYear;
            Configuration.SortMaximumYear = maximumYear;
            Configuration.SortSelectedManufacturer = selectedManufacturer;

            OnPropertyChanged(nameof(IsCategorySortEnabled));
            PersistConfigurationToDisk();
            UpdateLiveTreeDisplay();
        }

        // Debounces search input 400ms before rebuilding the tree, so the tree isn't rebuilt on every keystroke
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();

                    _searchDebounceTimer?.Stop();
                    _searchDebounceTimer ??= new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(400)
                    };
                    _searchDebounceTimer.Tick -= SearchDebounceTimer_Tick;
                    _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
                    _searchDebounceTimer.Start();
                }
            }
        }

        private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _searchDebounceTimer?.Stop();
            UpdateLiveTreeDisplay();
        }
        // [END SECTION: Search & Dev Mode]

        // SelectedGame drives the whole media preview pipeline. HasActiveMedia is resolved immediately
        // (cheap File.Exists check) so the placeholder-vs-content decision is never stale - this fixes a
        // bug where selecting a folder (SelectedGame = null) then quickly selecting a game briefly showed
        // the "no preview" placeholder before the debounced video load caught up. UpdateActiveMediaPreviews
        // itself is still debounced (~150ms) since it does the expensive work (marquee bitmap read + video/
        // image load), and rapid gamepad scrolling through titles was triggering that on every single
        // intermediate step, causing navigation to stutter. Only the final selection once scrolling
        // pauses actually loads media.
        public GameItem? SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (_selectedGame != value)
                {
                    _selectedGame = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsGameSelected));

                    // Play-stats footer text - a plain synchronous string lookup, completely independent
                    // of the video/preview pipeline below. Deliberately its own separate line, not folded
                    // into any of the media-loading logic that follows.
                    SelectedGamePlayStatsText = ResolvePlayStatsText(value);

                    // Column Two's Publisher/Year/Rating row (media panel rework) - cheap dictionary
                    // lookup, resolved immediately like HasActiveMedia below rather than debounced, since
                    // there's no I/O involved. Null means no sort_database.ini entry for this ROM/family.
                    DisplayRating = value != null ? _sortService.GetDisplayRating(value) : null;

                    // history.xml-derived text for Column One Row 3 (Staff/Technical) and Column Two
                    // (Game Info/Trivia/Tips & Tricks) - extracted into RefreshHistoryPanelsForSelectedGame
                    // so ReloadHistoryOverrides can re-run the same lookup on-demand (hotkey-triggered)
                    // without duplicating this logic.
                    RefreshHistoryPanelsForSelectedGame(value);
                    HasActiveMedia = ResolveHasActiveMediaImmediate();
                    PreviewImage = null;
                    VideoSourcePath = string.Empty;

                    _previewDebounceTimer?.Stop();
                    _previewDebounceTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(600)
                    };
                    _previewDebounceTimer.Tick += (s, e) =>
                    {
                        _previewDebounceTimer.Stop();
                        UpdateActiveMediaPreviews();
                    };
                    _previewDebounceTimer.Start();
                }
            }
        }

        // Fast, synchronous existence check only - mirrors the folder-walking logic in UpdateActiveMediaPreviews
        // but skips the expensive bitmap/video loading. Keeps HasActiveMedia accurate immediately on selection
        // change instead of waiting on the debounce timer, so the placeholder never flashes based on stale state.
        private string _selectedGamePlayStatsText = string.Empty;
        // Drives the footer play-stats pill. Empty string collapses the pill entirely (bound via a
        // DataTrigger in XAML) - populated whenever the selected game has recorded play history.
        public string SelectedGamePlayStatsText
        {
            get => _selectedGamePlayStatsText;
            private set
            {
                if (_selectedGamePlayStatsText != value)
                {
                    _selectedGamePlayStatsText = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _filteredGameCountText = string.Empty;
        // Drives the footer games-count pill. Reflects the total filtered ROM pool (same eligibility
        // set both structure modes render from), not a per-folder or per-tree-leaf count - recalculated
        // once per UpdateLiveTreeDisplay pass, i.e. only on Apply Filter, not live per-keystroke/toggle.
        public string FilteredGameCountText
        {
            get => _filteredGameCountText;
            private set
            {
                if (_filteredGameCountText != value)
                {
                    _filteredGameCountText = value;
                    OnPropertyChanged();
                }
            }
        }

        // Looks up a game's play history entry (if any) and formats it as "N plays · Xh Ym". Global -
        // works for any game with history, regardless of which folder it's currently being viewed from.
        private string ResolvePlayStatsText(GameItem? game)
        {
            if (game == null) return string.Empty;

            var entries = ReadPlayHistoryEntries();
            var match = entries.FirstOrDefault(e => e.RomName.Equals(game.RomName, StringComparison.OrdinalIgnoreCase));
            if (match.RomName == null || match.PlayCount <= 0) return string.Empty;

            var totalTime = TimeSpan.FromSeconds(match.TotalSeconds);
            string timeText = totalTime.TotalHours >= 1
                ? $"{(int)totalTime.TotalHours}h {totalTime.Minutes}m"
                : $"{totalTime.Minutes}m";
            // Play count - Play time text
            return $"Play Count: {match.PlayCount} \u00b7 Play Time: {timeText}";
        }

        private bool ResolveHasActiveMediaImmediate()
        {
            if (SelectedGame == null) return false;

            foreach (var category in PreviewPriorityOrder)
            {
                string targetFolder = string.Empty;
                string[] extensions = { ".png", ".jpg" };

                switch (category)
                {
                    case "videos":
                        targetFolder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.VideosPath) ? "videos" : Configuration.VideosPath);
                        extensions = new[] { ".mp4", ".avi" };
                        break;
                    case "flyers":
                        targetFolder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.FlyersPath) ? "flyers" : Configuration.FlyersPath);
                        break;
                    case "screenshots":
                    case "snapshots":
                    case "gameplay":
                        targetFolder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.ScreenshotsPath) ? "snap" : Configuration.ScreenshotsPath);
                        break;
                    case "titlescreens":
                        targetFolder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.TitlescreensPath) ? "titles" : Configuration.TitlescreensPath);
                        break;
                    case "cabinets":
                        targetFolder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.CabinetsPath) ? "cabinets" : Configuration.CabinetsPath);
                        break;
                    case "marquees":
                        targetFolder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.MarqueesPath) ? "marquees" : Configuration.MarqueesPath);
                        break;
                    default:
                        continue;
                }

                if (TryResolveMediaFile(targetFolder, SelectedGame.RomName, SelectedGame.CloneOf, extensions) != null)
                {
                    return true;
                }
            }

            return false;
        }

        public BitmapImage? MarqueeImage
        {
            get => _marqueeImage;
            set
            {
                if (_marqueeImage != value)
                {
                    _marqueeImage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasMarqueeImage));
                }
            }
        }

        public bool HasMarqueeImage => MarqueeImage != null;

        public string VideoSourcePath
        {
            get => _videoSourcePath;
            set
            {
                if (_videoSourcePath != value)
                {
                    _videoSourcePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsGameSelected => SelectedGame != null;

        private bool _isOptionsWindowOpen;
        public bool IsOptionsWindowOpen
        {
            get => _isOptionsWindowOpen;
            set
            {
                if (_isOptionsWindowOpen != value)
                {
                    _isOptionsWindowOpen = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _hasActiveMedia = true;
        public bool HasActiveMedia
        {
            get => _hasActiveMedia;
            set
            {
                if (_hasActiveMedia != value)
                {
                    _hasActiveMedia = value;
                    OnPropertyChanged();
                }
            }
        }
        // [END SECTION: Selection & Media State Properties]

        // [SECTION: Theme Property Forwarders]
        // DANGER - SYNC POINT 1 of 3: these are pass-through bindings from ConfigurationSettings to XAML.
        // Any new theme property added to ConfigurationSettings needs a forwarder here too, or XAML bindings
        // referencing "Theme*" names will silently fail. This list must also be raised in RefreshThemeBindings()
        // below (sync point 2) - a property added here but missing there won't update live after Theme Builder saves.
        public string ThemeMainColor => Configuration.BackgroundColor;
        public string ThemeGamesColor => Configuration.FileListBg;
        public string ThemeMarqueeColor => Configuration.MarqueeBoxBg;
        public string ThemeMediaColor => Configuration.VideoBoxBg;
        public string ThemeBorderColor => Configuration.BorderColorFramework;
        public double ThemeBorderWidth => Configuration.BorderWidthValue;
        public double ThemeBorderCurve => Configuration.BorderCurveValue;
        public string ThemeMarqueeBorderColor => Configuration.MarqueeBorderColorHex;
        public double ThemeMarqueeBorderWidth => Configuration.MarqueeBorderWidthValue;
        public string ThemeSeparatorColor => Configuration.SeparatorColorHex;
        public string ThemeScrollTrackColor => Configuration.ScrollTrackColor;
        public string ThemeScrollTrackHoverColor => Configuration.ScrollTrackHoverColor;
        public string ThemeScrollThumbColor => Configuration.ScrollThumbColor;
        public string ThemeScrollThumbHoverColor => Configuration.ScrollThumbHoverColor;
        public string ThemeScrollThumbDragColor => Configuration.ScrollThumbDragColor;
        public int ThemeFolderFontSize => Configuration.FolderFontSize;
        public int ThemeGameFontSize => Configuration.GameFontSize;
        public string ThemeGameColor => Configuration.GameColorHex;
        public string ThemeFolderSelectedColor => Configuration.FolderSelectedColorHex;
        public string ThemeFolderSelectedBgColor => Configuration.FolderSelectedBgColorHex;
        public string ThemeGameHoverColor => Configuration.GameHoverColorHex;
        public string ThemeGameSelectedColor => Configuration.GameSelectedColorHex;
        public string ThemeGameSelectedBgColor => Configuration.GameSelectedBgColorHex;
        public string ThemeArrowColor => Configuration.ArrowColorHex;
        public string ThemeFolderColor => Configuration.FolderColorHex;

        // Temporary hardcoded test switch for folder/game text emphasis (drop shadow vs stroke).
        // Will be replaced by a real theme setting + checkbox once we decide which look to keep.
        public TextEmphasisMode TextEmphasisMode => TextEmphasisMode.DropShadow;

        public int ThemeSearchBoxFontSize => Configuration.SearchBoxFontSize;
        public string ThemeSearchBoxColor => Configuration.SearchBoxColorHex;
        public string ThemeSearchBoxBgColor => Configuration.SearchBoxBgColorHex;
        public int ThemeGameNameHeaderFontSize => Configuration.GameNameHeaderFontSize;
        public string ThemeGameNameHeaderColor => Configuration.GameNameHeaderColorHex;
        public int ThemePubYearRatingFontSize => Configuration.PubYearRatingFontSize;
        public string ThemePubYearRatingColor => Configuration.PubYearRatingColorHex;
        public int ThemeInfoHeaderFontSize => Configuration.InfoHeaderFontSize;
        public string ThemeInfoHeaderColor => Configuration.InfoHeaderColorHex;
        public int ThemeInfoBodyFontSize => Configuration.InfoBodyFontSize;
        public string ThemeInfoBodyColor => Configuration.InfoBodyColorHex;
        public int ThemeCreditsHeaderFontSize => Configuration.CreditsHeaderFontSize;
        public string ThemeCreditsHeaderColor => Configuration.CreditsHeaderColorHex;
        public int ThemeCreditsBodyFontSize => Configuration.CreditsBodyFontSize;
        public string ThemeCreditsBodyColor => Configuration.CreditsBodyColorHex;
        public string ThemeVideoBgColor => Configuration.VideoBgColorHex;
        public double ThemeVideoBorderSize => Configuration.VideoBorderSize;
        public string ThemeVideoBorderColor => Configuration.VideoBorderColorHex;
        public double ThemeVideoBorderRadius => Configuration.VideoBorderRadius;
        public string ThemeNavIconsColor => Configuration.NavIconsColorHex;
        public double ThemeNavIconsSize => Configuration.NavIconsSize;
        public double ThemePreviewBorderSize => Configuration.PreviewBorderSize;
        public string ThemePreviewBorderColor => Configuration.PreviewBorderColorHex;
        public double ThemePreviewBorderRadius => Configuration.PreviewBorderRadius;
        public int ThemeSearchLabelFontSize => Configuration.SearchLabelFontSize;
        public string ThemeSearchLabelColor => Configuration.SearchLabelColorHex;
        public string ThemeSearchLabelBgColor => Configuration.SearchLabelBgColorHex;
        public int ThemeMainWinBtnFontSize => Configuration.MainWinBtnFontSize;
        public string ThemeMainWinBtnColor => Configuration.MainWinBtnColorHex;
        public string ThemeMainWinBtnBgColor => Configuration.MainWinBtnBgColorHex;
        public string ThemeMainWinBtnColorHover => Configuration.MainWinBtnColorHoverHex;
        public string ThemeMainWinBtnBgColorHover => Configuration.MainWinBtnBgColorHoverHex;
        public double ThemeMainWinBtnBorderSize => Configuration.MainWinBtnBorderSize;
        public string ThemeMainWinBtnBorderColor => Configuration.MainWinBtnBorderColorHex;
        public double ThemeMainWinBtnCornerRadius => Configuration.MainWinBtnCornerRadius;
        public double ThemeGamesBorderSize => Configuration.GamesBorderSize;
        public string ThemeGamesBorderColor => Configuration.GamesBorderColorHex;
        public double ThemeGamesBorderCornerRadius => Configuration.GamesBorderCornerRadius;
        public string ThemeContextMenuFontColor => Configuration.ContextMenuFontColorHex;
        public string ThemeContextMenuIconColor => Configuration.ContextMenuIconColorHex;
        public string ThemeContextMenuBgColor => Configuration.ContextMenuBgColorHex;
        public string ThemeContextMenuHoverColor => Configuration.ContextMenuHoverColorHex;
        public string ThemeContextMenuHoverBgColor => Configuration.ContextMenuHoverBgColorHex;
        public double ThemeMarqueeBorderRadius => Configuration.MarqueeBorderRadius;
        public int ThemeSubTextFontSize => Configuration.SubTextFontSize;
        public string ThemeSubTextColor => Configuration.SubTextColorHex;
        public string ThemeOptionsMenuBgColor => Configuration.OptionsMenuBgColorHex;
        public double ThemeOptionsMenuBorderSize => Configuration.OptionsMenuBorderSize;
        public string ThemeOptionsMenuBorderColor => Configuration.OptionsMenuBorderColorHex;
        public double ThemeOptionsMenuBorderRadius => Configuration.OptionsMenuBorderRadius;
        public string ThemeTabColorHover => Configuration.TabColorHoverHex;
        public string ThemeTabBgColorHover => Configuration.TabBgColorHoverHex;
        public int ThemeOptionsBtnFontSize => Configuration.OptionsBtnFontSize;
        public string ThemeOptionsBtnColorHover => Configuration.OptionsBtnColorHoverHex;
        public double ThemeOptionsBtnBorderSize => Configuration.OptionsBtnBorderSize;
        public double ThemeOptionsBtnBorderRadius => Configuration.OptionsBtnBorderRadius;
        public string ThemeTabColor => Configuration.TabColorHex;
        public string ThemeTabBgColor => Configuration.TabBgColorHex;
        public string ThemeTabActiveColor => Configuration.TabActiveColorHex;
        public string ThemeTabActiveBgColor => Configuration.TabActiveBgColorHex;
        public string ThemeStandardColor => Configuration.StandardColorHex;
        public string ThemeHeaderColor => Configuration.HeaderColorHex;
        public string ThemeSubHeaderColor => Configuration.SubHeaderColorHex;
        // [END SECTION: Theme Property Forwarders]

        // [SECTION: Theme Wallpaper & Asset Loading]
        // Resolves theme-configured image paths to ImageSource, falling back to bundled defaults in
        // 4rcade5tick_files/assets when a theme doesn't specify its own asset.
        public ImageSource? ThemeMainWallpaper => Configuration.DisableMainBgImage
            ? null
            : LoadThemeImage(
                !string.IsNullOrWhiteSpace(Configuration.MainWindowWallpaper)
                    ? Configuration.MainWindowWallpaper
                    : Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets", "default_background.png"));
        public ImageSource? ThemeGamesWallpaper => LoadThemeImage(Configuration.GamesListWallpaper);

        public ImageSource? ThemeMarqueeWallpaper => LoadThemeImage(Configuration.MarqueeWindowWallpaper);

        public ImageSource? ThemeMediaWallpaper => LoadThemeImage(Configuration.MediaWindowWallpaper);

        public ImageSource? ThemeLogoAsset => LoadThemeImage(
            !string.IsNullOrWhiteSpace(Configuration.ThemeLogo)
                ? Configuration.ThemeLogo
                : Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets", "default_marquee.png"));

        // Resolves the boot splash to the custom theme path if set, else the bundled default - preferring
        // an mp4 companion file over the png when falling back to the default asset
        private string ResolveBootSplashPath()
        {
            if (!string.IsNullOrWhiteSpace(Configuration.ThemeBootSplash))
            {
                string splashPath = Configuration.ThemeBootSplash;
                if (!Path.IsPathRooted(splashPath))
                {
                    splashPath = Path.GetFullPath(Path.Combine(Configuration.BaseDirectory, splashPath));
                }
                return splashPath;
            }

            string assetsDir = Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets");
            string defaultVideoPath = Path.Combine(assetsDir, "default_mediabg.mp4");

            return File.Exists(defaultVideoPath)
                ? defaultVideoPath
                : Path.Combine(assetsDir, "default_mediabg.png");
        }

        // True when the resolved boot splash asset is an mp4 file
        public bool IsBootSplashVideo => Path.GetExtension(ResolveBootSplashPath()).Equals(".mp4", StringComparison.OrdinalIgnoreCase);

        // File path for the LibVLC VideoView when the boot splash resolves to an mp4; null when it's a png
        public string? ThemeBootSplashVideoPath => IsBootSplashVideo ? ResolveBootSplashPath() : null;

        // Bitmap for the boot splash Image/ImageBrush when it resolves to a png; null when it's an mp4
        public ImageSource? ThemeBootSplashAsset => IsBootSplashVideo ? null : LoadThemeImage(ResolveBootSplashPath());

        public ImageSource? ThemeMissingPreviewAsset => LoadThemeImage(
            !string.IsNullOrWhiteSpace(Configuration.ThemeMissingPreview)
                ? Configuration.ThemeMissingPreview
                : Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets", "no_preview.png"));

        // Resolves a possibly-relative theme asset path to an absolute path and loads it as a frozen BitmapImage
        private ImageSource? LoadThemeImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            try
            {
                string cleanPath = path;

                if (!Path.IsPathRooted(cleanPath))
                {
                    if (cleanPath.StartsWith(@".\"))
                    {
                        cleanPath = cleanPath.Substring(2);
                    }

                    cleanPath = Path.GetFullPath(Path.Combine(Configuration.BaseDirectory, cleanPath));
                }

                if (!File.Exists(cleanPath)) return null;

                var bitmap = new BitmapImage();
                byte[] fileBytes = File.ReadAllBytes(cleanPath);
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(fileBytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
        }
        // [END SECTION: Theme Wallpaper & Asset Loading]

        // [SECTION: Theme Refresh - Sync Points 2 & 3]
        // DANGER: RefreshThemeBindings must raise OnPropertyChanged for every forwarder property above -
        // this is called after the Options window closes and after theme load/save, so a missing line here
        // means a new theme property silently keeps showing stale/default values until app restart.

        // Fires whenever RefreshThemeBindings runs. MainWindow subscribes to this to repaint UI elements
        // that broke their XAML binding by having code-behind set Foreground/Background directly (e.g. the
        // options gear icon's hover-swap effect) - those elements otherwise never see live theme updates.
        public event Action? ThemeBindingsRefreshed;

        public void RefreshThemeBindings()
        {
            // Re-resolve the marquee now, since ConfigurationSettings.ThemeLogo may have changed and the
            // currently displayed fallback logo would otherwise stay stale until the next game selection
            ResolveMarqueeImage();

            OnPropertyChanged(nameof(ThemeMainColor));
            OnPropertyChanged(nameof(ThemeGamesColor));
            OnPropertyChanged(nameof(ThemeMarqueeColor));
            OnPropertyChanged(nameof(ThemeMediaColor));
            OnPropertyChanged(nameof(ThemeBorderColor));
            OnPropertyChanged(nameof(ThemeBorderWidth));
            OnPropertyChanged(nameof(ThemeBorderCurve));
            OnPropertyChanged(nameof(ThemeMarqueeBorderColor));
            OnPropertyChanged(nameof(ThemeMarqueeBorderWidth));
            OnPropertyChanged(nameof(ThemeSeparatorColor));
            OnPropertyChanged(nameof(ThemeScrollTrackColor));
            OnPropertyChanged(nameof(ThemeScrollTrackHoverColor));
            OnPropertyChanged(nameof(ThemeScrollThumbColor));
            OnPropertyChanged(nameof(ThemeScrollThumbHoverColor));
            OnPropertyChanged(nameof(ThemeScrollThumbDragColor));
            OnPropertyChanged(nameof(ThemeSearchBoxFontSize));
            OnPropertyChanged(nameof(ThemeSearchBoxColor));
            OnPropertyChanged(nameof(ThemeSearchBoxBgColor));
            OnPropertyChanged(nameof(ThemeGameNameHeaderFontSize));
            OnPropertyChanged(nameof(ThemeGameNameHeaderColor));
            OnPropertyChanged(nameof(ThemePubYearRatingFontSize));
            OnPropertyChanged(nameof(ThemePubYearRatingColor));
            OnPropertyChanged(nameof(ThemeInfoHeaderFontSize));
            OnPropertyChanged(nameof(ThemeInfoHeaderColor));
            OnPropertyChanged(nameof(ThemeInfoBodyFontSize));
            OnPropertyChanged(nameof(ThemeInfoBodyColor));
            OnPropertyChanged(nameof(ThemeCreditsHeaderFontSize));
            OnPropertyChanged(nameof(ThemeCreditsHeaderColor));
            OnPropertyChanged(nameof(ThemeCreditsBodyFontSize));
            OnPropertyChanged(nameof(ThemeCreditsBodyColor));
            OnPropertyChanged(nameof(ThemeVideoBgColor));
            OnPropertyChanged(nameof(ThemeVideoBorderSize));
            OnPropertyChanged(nameof(ThemeVideoBorderColor));
            OnPropertyChanged(nameof(ThemeVideoBorderRadius));
            OnPropertyChanged(nameof(ThemeNavIconsColor));
            OnPropertyChanged(nameof(ThemeNavIconsSize));
            OnPropertyChanged(nameof(ThemePreviewBorderSize));
            OnPropertyChanged(nameof(ThemePreviewBorderColor));
            OnPropertyChanged(nameof(ThemePreviewBorderRadius));
            OnPropertyChanged(nameof(ThemeSearchLabelFontSize));
            OnPropertyChanged(nameof(ThemeSearchLabelColor));
            OnPropertyChanged(nameof(ThemeSearchLabelBgColor));
            OnPropertyChanged(nameof(ThemeMainWinBtnFontSize));
            OnPropertyChanged(nameof(ThemeMainWinBtnColor));
            OnPropertyChanged(nameof(ThemeMainWinBtnBgColor));
            OnPropertyChanged(nameof(ThemeMainWinBtnColorHover));
            OnPropertyChanged(nameof(ThemeMainWinBtnBgColorHover));
            OnPropertyChanged(nameof(ThemeMainWinBtnBorderSize));
            OnPropertyChanged(nameof(ThemeMainWinBtnBorderColor));
            OnPropertyChanged(nameof(ThemeMainWinBtnCornerRadius));
            OnPropertyChanged(nameof(ThemeGamesBorderSize));
            OnPropertyChanged(nameof(ThemeGamesBorderColor));
            OnPropertyChanged(nameof(ThemeGamesBorderCornerRadius));
            OnPropertyChanged(nameof(ThemeContextMenuFontColor));
            OnPropertyChanged(nameof(ThemeContextMenuIconColor));
            OnPropertyChanged(nameof(ThemeContextMenuBgColor));
            OnPropertyChanged(nameof(ThemeContextMenuHoverColor));
            OnPropertyChanged(nameof(ThemeContextMenuHoverBgColor));
            OnPropertyChanged(nameof(ThemeMarqueeBorderRadius));
            OnPropertyChanged(nameof(ThemeSubTextFontSize));
            OnPropertyChanged(nameof(ThemeSubTextColor));
            OnPropertyChanged(nameof(ThemeOptionsMenuBgColor));
            OnPropertyChanged(nameof(ThemeOptionsMenuBorderSize));
            OnPropertyChanged(nameof(ThemeOptionsMenuBorderColor));
            OnPropertyChanged(nameof(ThemeOptionsMenuBorderRadius));
            OnPropertyChanged(nameof(ThemeTabColorHover));
            OnPropertyChanged(nameof(ThemeTabBgColorHover));
            OnPropertyChanged(nameof(ThemeOptionsBtnFontSize));
            OnPropertyChanged(nameof(ThemeOptionsBtnColorHover));
            OnPropertyChanged(nameof(ThemeOptionsBtnBorderSize));
            OnPropertyChanged(nameof(ThemeOptionsBtnBorderRadius));
            OnPropertyChanged(nameof(ThemeFolderFontSize));
            OnPropertyChanged(nameof(ThemeSearchBoxColor));
            OnPropertyChanged(nameof(ThemeSearchBoxBgColor));
            OnPropertyChanged(nameof(ThemeFolderFontSize));
            OnPropertyChanged(nameof(ThemeGameFontSize));
            OnPropertyChanged(nameof(ThemeGameColor));
            OnPropertyChanged(nameof(ThemeFolderSelectedColor));
            OnPropertyChanged(nameof(ThemeFolderSelectedBgColor));
            OnPropertyChanged(nameof(ThemeGameHoverColor));
            OnPropertyChanged(nameof(ThemeGameSelectedColor));
            OnPropertyChanged(nameof(ThemeGameSelectedBgColor));
            OnPropertyChanged(nameof(ThemeArrowColor));
            OnPropertyChanged(nameof(ThemeFolderColor));
            OnPropertyChanged(nameof(ThemeTabColor));
            OnPropertyChanged(nameof(ThemeTabBgColor));
            OnPropertyChanged(nameof(ThemeTabActiveColor));
            OnPropertyChanged(nameof(ThemeTabActiveBgColor));
            OnPropertyChanged(nameof(ThemeStandardColor));
            OnPropertyChanged(nameof(ThemeHeaderColor));
            OnPropertyChanged(nameof(ThemeSubHeaderColor));
            OnPropertyChanged(nameof(ThemeMainWallpaper));
            OnPropertyChanged(nameof(ThemeGamesWallpaper));
            OnPropertyChanged(nameof(ThemeMarqueeWallpaper));
            OnPropertyChanged(nameof(ThemeMediaWallpaper));
            OnPropertyChanged(nameof(ThemeLogoAsset));
            OnPropertyChanged(nameof(ThemeBootSplashAsset));
            OnPropertyChanged(nameof(IsBootSplashVideo));
            OnPropertyChanged(nameof(ThemeBootSplashVideoPath));
            OnPropertyChanged(nameof(ThemeMissingPreviewAsset));

            RefreshFolderColorsLive();
            ThemeBindingsRefreshed?.Invoke();
        }

        // Walks the existing tree in-place and reapplies folder/favorites colors from the current theme.
        // Does NOT rebuild the tree (that would lose expand state) - see project notes on RefreshFolderColorsLive.
        // Also pushes FolderFontSize/FolderSelectedBgColor/FolderSelectedColor/ArrowColor onto every node -
        // these were added alongside FolderColor to eliminate RelativeSource AncestorType=TreeView bindings
        // in XAML, which were causing severe UI freezes when generating containers for very large (30k+) folders.
        public void RefreshFolderColorsLive()
        {
            var mainFolderBrush = SafeConvertToBrush(Configuration.FolderColorHex);
            var favoritesBrush = SafeConvertToBrush(Configuration.FavoritesColorHex);
            var folderSelectedBgBrush = SafeConvertToBrush(Configuration.FolderSelectedBgColorHex);
            var folderSelectedBrush = SafeConvertToBrush(Configuration.FolderSelectedColorHex);
            var arrowBrush = SafeConvertToBrush(Configuration.ArrowColorHex);
            double folderFontSize = Configuration.FolderFontSize;

            void Walk(TreeCategoryNode node)
            {
                if (!node.IsCustomColor)
                {
                    node.FolderColor = node.HeaderText.Equals("FAVORITES", StringComparison.OrdinalIgnoreCase)
                        ? favoritesBrush
                        : mainFolderBrush;
                }

                node.FolderFontSize = folderFontSize;
                node.FolderSelectedBgColor = folderSelectedBgBrush;
                node.FolderSelectedColor = folderSelectedBrush;
                node.ArrowColor = arrowBrush;

                foreach (var sub in node.SubFolders)
                {
                    Walk(sub);
                }
            }

            foreach (var root in TreeNodesCollection)
            {
                Walk(root);
            }
        }

        // Walks GamesCollection in-place and reapplies game-row theme properties (FontSize/colors) plus
        // mouse-support flags. Extracted out of UpdateLiveTreeDisplay so theme save/load can trigger a
        // live refresh without needing a full tree rebuild - mirrors the RefreshFolderColorsLive pattern.
        public void RefreshGameColorsLive()
        {
            var gameColorBrush = SafeConvertToBrush(Configuration.GameColorHex);
            var gameHoverBrush = SafeConvertToBrush(Configuration.GameHoverColorHex);
            var gameSelectedBgBrush = SafeConvertToBrush(Configuration.GameSelectedBgColorHex);
            var gameSelectedBrush = SafeConvertToBrush(Configuration.GameSelectedColorHex);
            var arrowBrush = SafeConvertToBrush(Configuration.ArrowColorHex);

            foreach (var game in GamesCollection)
            {
                game.IsMouseSupported = MouseSupportRoms.Contains(game.RomName);
                game.FontSize = Configuration.GameFontSize;
                game.GameColor = gameColorBrush;
                game.GameHoverColor = gameHoverBrush;
                game.GameSelectedBgColor = gameSelectedBgBrush;
                game.GameSelectedColor = gameSelectedBrush;
                game.ArrowColor = arrowBrush;
            }
        }
        // [END SECTION: Theme Refresh - Sync Points 2 & 3]

        // [SECTION: Shared Theme Loader]
        // Extracts a saved theme's zip archive and maps every field onto Configuration - the single
        // source of truth for "load theme X", used both by ThemesTabControl's Load Theme button and by
        // the constructor's first-run/no-active-theme bootstrap (see LoadTheme("default") call below).
        // Asset paths are rewritten relative to Configuration.BaseDirectory rather than left absolute -
        // matches ResolveAssetAbsolutePath's existing expectation on the Save side, and keeps settings.json
        // portable across USB drive-letter changes.
        public bool LoadTheme(string themeName)
        {
            string themeFile = Configuration.GetThemePath(themeName);
            if (!File.Exists(themeFile)) return false;

            string extractDir = Path.Combine(Configuration.GetArcadeStickFilesPath(), "Themes", "Extracted", themeName);
            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, recursive: true);
            }
            ZipFile.ExtractToDirectory(themeFile, extractDir);

            string jsonString = File.ReadAllText(Path.Combine(extractDir, "theme.cfg"));
            var themeData = System.Text.Json.JsonSerializer.Deserialize<ThemeSettings>(jsonString);
            if (themeData == null) return false;

            // Rewrite the 7 asset properties from archive-relative ("assets/x.png") to a path relative to
            // Configuration.BaseDirectory (not absolute) so the theme survives a USB drive-letter change.
            // A rooted (already-absolute) stored value means that asset was missing at save time and was
            // never actually bundled - left untouched rather than mangled.
            string RewriteAssetPath(string storedValue)
            {
                if (string.IsNullOrWhiteSpace(storedValue)) return storedValue;
                if (Path.IsPathRooted(storedValue)) return storedValue;
                string absolutePath = Path.Combine(extractDir, storedValue.Replace('/', Path.DirectorySeparatorChar));
                return Path.GetRelativePath(Configuration.BaseDirectory, absolutePath);
            }

            themeData.MainWindowWallpaper = RewriteAssetPath(themeData.MainWindowWallpaper);
            themeData.GamesListWallpaper = RewriteAssetPath(themeData.GamesListWallpaper);
            themeData.MarqueeWindowWallpaper = RewriteAssetPath(themeData.MarqueeWindowWallpaper);
            themeData.MediaWindowWallpaper = RewriteAssetPath(themeData.MediaWindowWallpaper);
            themeData.ThemeLogo = RewriteAssetPath(themeData.ThemeLogo);
            themeData.ThemeBootSplash = RewriteAssetPath(themeData.ThemeBootSplash);
            themeData.ThemeMissingPreview = RewriteAssetPath(themeData.ThemeMissingPreview);

            // Map ThemeSettings DTO properties back to the main ConfigurationSettings object
            Configuration.MainWindowWallpaper = themeData.MainWindowWallpaper;
            Configuration.DisableMainBgImage = themeData.DisableMainBgImage;
            Configuration.GamesListWallpaper = themeData.GamesListWallpaper;
            Configuration.MarqueeWindowWallpaper = themeData.MarqueeWindowWallpaper;
            Configuration.MediaWindowWallpaper = themeData.MediaWindowWallpaper;
            Configuration.ThemeLogo = themeData.ThemeLogo;
            Configuration.ThemeBootSplash = themeData.ThemeBootSplash;
            Configuration.ThemeMissingPreview = themeData.ThemeMissingPreview;
            Configuration.BackgroundColor = themeData.BackgroundColor;
            Configuration.FileListBg = themeData.FileListBg;
            Configuration.MarqueeBoxBg = themeData.MarqueeBoxBg;
            Configuration.VideoBoxBg = themeData.VideoBoxBg;
            Configuration.OptionsBg = themeData.OptionsBg;
            Configuration.BorderColorFramework = themeData.BorderColorFramework;
            Configuration.ScrollTrackColor = themeData.ScrollTrackColor;
            Configuration.ScrollTrackHoverColor = themeData.ScrollTrackHoverColor;
            Configuration.ScrollThumbColor = themeData.ScrollThumbColor;
            Configuration.ScrollThumbHoverColor = themeData.ScrollThumbHoverColor;
            Configuration.ScrollThumbDragColor = themeData.ScrollThumbDragColor;
            Configuration.BorderWidthValue = themeData.BorderWidthValue;
            Configuration.BorderCurveValue = themeData.BorderCurveValue;
            Configuration.SeparatorColorHex = themeData.SeparatorColorHex;
            Configuration.MarqueeBorderColorHex = themeData.MarqueeBorderColorHex;
            Configuration.MarqueeBorderWidthValue = themeData.MarqueeBorderWidthValue;
            Configuration.FolderFontSize = themeData.FolderFontSize;
            Configuration.FolderColorHex = themeData.FolderColorHex;
            Configuration.FolderSelectedColorHex = themeData.FolderSelectedColorHex;
            Configuration.FolderSelectedBgColorHex = themeData.FolderSelectedBgColorHex;
            Configuration.GameFontSize = themeData.GameFontSize;
            Configuration.GameColorHex = themeData.GameColorHex;
            Configuration.GameHoverColorHex = themeData.GameHoverColorHex;
            Configuration.GameSelectedColorHex = themeData.GameSelectedColorHex;
            Configuration.GameSelectedBgColorHex = themeData.GameSelectedBgColorHex;
            Configuration.FavoritesColorHex = themeData.FavoritesColorHex;
            Configuration.ArrowColorHex = themeData.ArrowColorHex;
            Configuration.TabFontSize = themeData.TabFontSize;
            Configuration.TabColorHex = themeData.TabColorHex;
            Configuration.TabBgColorHex = themeData.TabBgColorHex;
            Configuration.TabActiveColorHex = themeData.TabActiveColorHex;
            Configuration.TabActiveBgColorHex = themeData.TabActiveBgColorHex;
            Configuration.HeaderFontSize = themeData.HeaderFontSize;
            Configuration.HeaderColorHex = themeData.HeaderColorHex;
            Configuration.SubHeaderFontSize = themeData.SubHeaderFontSize;
            Configuration.SubHeaderColorHex = themeData.SubHeaderColorHex;
            Configuration.StandardFontSize = themeData.StandardFontSize;
            Configuration.StandardColorHex = themeData.StandardColorHex;
            Configuration.InputFontSize = themeData.InputFontSize;
            Configuration.InputColorHex = themeData.InputColorHex;
            Configuration.SearchBoxFontSize = themeData.SearchBoxFontSize;
            Configuration.SearchBoxColorHex = themeData.SearchBoxColorHex;
            Configuration.SearchBoxBgColorHex = themeData.SearchBoxBgColorHex;
            Configuration.GameNameHeaderFontSize = themeData.GameNameHeaderFontSize;
            Configuration.GameNameHeaderColorHex = themeData.GameNameHeaderColorHex;
            Configuration.PubYearRatingFontSize = themeData.PubYearRatingFontSize;
            Configuration.PubYearRatingColorHex = themeData.PubYearRatingColorHex;
            Configuration.InfoHeaderFontSize = themeData.InfoHeaderFontSize;
            Configuration.InfoHeaderColorHex = themeData.InfoHeaderColorHex;
            Configuration.InfoBodyFontSize = themeData.InfoBodyFontSize;
            Configuration.InfoBodyColorHex = themeData.InfoBodyColorHex;
            Configuration.CreditsHeaderFontSize = themeData.CreditsHeaderFontSize;
            Configuration.CreditsHeaderColorHex = themeData.CreditsHeaderColorHex;
            Configuration.CreditsBodyFontSize = themeData.CreditsBodyFontSize;
            Configuration.CreditsBodyColorHex = themeData.CreditsBodyColorHex;
            Configuration.VideoBgColorHex = themeData.VideoBgColorHex;
            Configuration.VideoBorderSize = themeData.VideoBorderSize;
            Configuration.VideoBorderColorHex = themeData.VideoBorderColorHex;
            Configuration.VideoBorderRadius = themeData.VideoBorderRadius;
            Configuration.NavIconsColorHex = themeData.NavIconsColorHex;
            Configuration.NavIconsSize = themeData.NavIconsSize;
            Configuration.PreviewBorderSize = themeData.PreviewBorderSize;
            Configuration.PreviewBorderColorHex = themeData.PreviewBorderColorHex;
            Configuration.PreviewBorderRadius = themeData.PreviewBorderRadius;
            Configuration.SearchLabelFontSize = themeData.SearchLabelFontSize;
            Configuration.SearchLabelColorHex = themeData.SearchLabelColorHex;
            Configuration.SearchLabelBgColorHex = themeData.SearchLabelBgColorHex;
            Configuration.MainWinBtnFontSize = themeData.MainWinBtnFontSize;
            Configuration.MainWinBtnColorHex = themeData.MainWinBtnColorHex;
            Configuration.MainWinBtnBgColorHex = themeData.MainWinBtnBgColorHex;
            Configuration.MainWinBtnColorHoverHex = themeData.MainWinBtnColorHoverHex;
            Configuration.MainWinBtnBgColorHoverHex = themeData.MainWinBtnBgColorHoverHex;
            Configuration.MainWinBtnBorderSize = themeData.MainWinBtnBorderSize;
            Configuration.MainWinBtnBorderColorHex = themeData.MainWinBtnBorderColorHex;
            Configuration.MainWinBtnCornerRadius = themeData.MainWinBtnCornerRadius;
            Configuration.GamesBorderSize = themeData.GamesBorderSize;
            Configuration.GamesBorderColorHex = themeData.GamesBorderColorHex;
            Configuration.GamesBorderCornerRadius = themeData.GamesBorderCornerRadius;
            Configuration.ContextMenuFontColorHex = themeData.ContextMenuFontColorHex;
            Configuration.ContextMenuIconColorHex = themeData.ContextMenuIconColorHex;
            Configuration.ContextMenuBgColorHex = themeData.ContextMenuBgColorHex;
            Configuration.ContextMenuHoverColorHex = themeData.ContextMenuHoverColorHex;
            Configuration.ContextMenuHoverBgColorHex = themeData.ContextMenuHoverBgColorHex;
            Configuration.MarqueeBorderRadius = themeData.MarqueeBorderRadius;
            Configuration.SubTextFontSize = themeData.SubTextFontSize;
            Configuration.SubTextColorHex = themeData.SubTextColorHex;
            Configuration.OptionsMenuBgColorHex = themeData.OptionsMenuBgColorHex;
            Configuration.OptionsMenuBorderSize = themeData.OptionsMenuBorderSize;
            Configuration.OptionsMenuBorderColorHex = themeData.OptionsMenuBorderColorHex;
            Configuration.OptionsMenuBorderRadius = themeData.OptionsMenuBorderRadius;
            Configuration.TabColorHoverHex = themeData.TabColorHoverHex;
            Configuration.TabBgColorHoverHex = themeData.TabBgColorHoverHex;
            Configuration.OptionsBtnFontSize = themeData.OptionsBtnFontSize;
            Configuration.OptionsBtnColorHoverHex = themeData.OptionsBtnColorHoverHex;
            Configuration.OptionsBtnBorderSize = themeData.OptionsBtnBorderSize;
            Configuration.OptionsBtnBorderRadius = themeData.OptionsBtnBorderRadius;
            Configuration.BtnBgColorHex = themeData.BtnBgColorHex;
            Configuration.BtnBorderColorHex = themeData.BtnBorderColorHex;
            Configuration.BtnTextColorNormalHex = themeData.BtnTextColorNormalHex;
            Configuration.BtnBgColorHoverHex = themeData.BtnBgColorHoverHex;

            RefreshThemeBindings();
            RefreshGameColorsLive();
            Configuration.ActiveThemeName = themeName;

            return true;
        }
        // [END SECTION: Shared Theme Loader]

        // [SECTION: MAME ROM Path Sync]
        // Rewrites mame.ini's rompath line to match the ROM subfolders currently on disk. IMPORTANT:
        // only ever call this at startup/boot-time - rompath must never be overwritten mid-session
        // (see BtnSaveOptions_Click history - a prior bug did this incorrectly on every options save).
        public Task SyncMameRomPathsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string mameDir = Configuration.GetMamePath();
                    string iniPath = Path.Combine(mameDir, "mame.ini");

                    if (!File.Exists(iniPath))
                    {
                        try
                        {
                            string mameExePath = Path.Combine(mameDir, "mame.exe");
                            if (!File.Exists(mameExePath))
                            {
                                System.Diagnostics.Debug.WriteLine("mame.exe not found, cannot generate default mame.ini.");
                                return;
                            }

                            var ccStartInfo = new ProcessStartInfo
                            {
                                FileName = mameExePath,
                                Arguments = "-cc",
                                WorkingDirectory = mameDir,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using (var ccProcess = Process.Start(ccStartInfo))
                            {
                                ccProcess?.WaitForExit();
                            }

                            if (!File.Exists(iniPath))
                            {
                                System.Diagnostics.Debug.WriteLine("mame.exe -cc did not produce mame.ini as expected.");
                                return;
                            }
                        }
                        catch (Exception exCreate)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to generate default mame.ini via -cc: {exCreate.Message}");
                            return;
                        }
                    }

                    string activeRomsPath = Path.Combine(mameDir, Configuration.RomsSubFolder);
                    if (!Directory.Exists(activeRomsPath)) return;

                    var currentFolders = Directory.GetDirectories(activeRomsPath, "*", SearchOption.AllDirectories).ToList();
                    currentFolders.Insert(0, activeRomsPath);

                    string safeMameDir = mameDir.EndsWith(Path.DirectorySeparatorChar.ToString()) || mameDir.EndsWith(Path.AltDirectorySeparatorChar.ToString())
                        ? mameDir
                        : mameDir + Path.DirectorySeparatorChar;

                    var targetPaths = new List<string>();

                    // ROMs root, BIOS, and CHD lead the rompath list (in that order) rather than trailing
                    // behind every category subfolder - keeps the three System Paths tab folders as the
                    // first entries MAME checks. Neither BIOS nor CHD subfolders are discovered by the
                    // category-folder scan below, so both would otherwise be silently dropped on rewrite.
                    string romsRootEntry = activeRomsPath;
                    if (romsRootEntry.StartsWith(safeMameDir, StringComparison.OrdinalIgnoreCase))
                    {
                        romsRootEntry = romsRootEntry.Substring(safeMameDir.Length);
                    }
                    romsRootEntry = romsRootEntry.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!string.IsNullOrEmpty(romsRootEntry))
                    {
                        targetPaths.Add(romsRootEntry);
                    }

                    if (!string.IsNullOrWhiteSpace(Configuration.BiosPath))
                    {
                        string biosEntry = Configuration.BiosPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (!string.IsNullOrEmpty(biosEntry))
                        {
                            targetPaths.Add(biosEntry);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(Configuration.ChdPath))
                    {
                        string chdEntry = Configuration.ChdPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (!string.IsNullOrEmpty(chdEntry))
                        {
                            targetPaths.Add(chdEntry);
                        }
                    }

                    foreach (var absolutePath in currentFolders)
                    {
                        string relative = absolutePath;
                        if (absolutePath.StartsWith(safeMameDir, StringComparison.OrdinalIgnoreCase))
                        {
                            relative = absolutePath.Substring(safeMameDir.Length);
                        }

                        relative = relative.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (!string.IsNullOrEmpty(relative))
                        {
                            targetPaths.Add(relative);
                        }
                    }

                    // Distinct() preserves first-occurrence order, so the leading ROMs/BIOS/CHD entries
                    // added above stay at the front even though the ROMs root also appears again as the
                    // first entry in currentFolders.
                    targetPaths = targetPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                    var iniLines = File.ReadAllLines(iniPath).ToList();
                    int targetLineIndex = -1;
                    string existingRomPathLine = string.Empty;

                    for (int i = 0; i < iniLines.Count; i++)
                    {
                        if (iniLines[i].TrimStart().StartsWith("rompath", StringComparison.OrdinalIgnoreCase))
                        {
                            targetLineIndex = i;
                            existingRomPathLine = iniLines[i];
                            break;
                        }
                    }

                    if (targetLineIndex == -1) return;

                    string rawPathsPart = string.Empty;
                    int firstSpace = existingRomPathLine.IndexOf(' ');
                    if (firstSpace != -1)
                    {
                        rawPathsPart = existingRomPathLine.Substring(firstSpace + 1).Trim();
                    }
                    else if (existingRomPathLine.Contains('\t'))
                    {
                        int firstTab = existingRomPathLine.IndexOf('\t');
                        rawPathsPart = existingRomPathLine.Substring(firstTab + 1).Trim();
                    }

                    var existingPaths = rawPathsPart.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(p => p.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                                                    .Where(p => !string.IsNullOrEmpty(p))
                                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                                    .ToList();

                    bool hasDisparities = targetPaths.Count != existingPaths.Count ||
                                          targetPaths.Except(existingPaths, StringComparer.OrdinalIgnoreCase).Any() ||
                                          existingPaths.Except(targetPaths, StringComparer.OrdinalIgnoreCase).Any();

                    if (!hasDisparities) return;

                    string updatedPathsJoined = string.Join(";", targetPaths);

                    // MAME's ini parser has a hard line-length ceiling around 4096 characters - past that,
                    // entries silently get dropped (confirmed empirically: CHD/game folders past the cutoff
                    // stopped resolving even though they were correctly present in the file). Bail out with
                    // a warning instead of writing a rompath line that MAME won't fully read.
                    if (updatedPathsJoined.Length > 4000)
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(
                                "Your ROMs folder has too many or too deeply nested category subfolders for MAME to " +
                                "read reliably (the combined path list exceeds MAME's ini line limit). The update was " +
                                "not applied - mame.ini remains unchanged. Try shortening category folder names or " +
                                "reducing the number of subfolders, then try again.",
                                "ROM Path List Too Long", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                        return;
                    }
                    iniLines[targetLineIndex] = $"rompath                   {updatedPathsJoined}";
                    File.WriteAllLines(iniPath, iniLines);
                }
                catch (Exception)
                {
                }
            });
        }
        // [END SECTION: MAME ROM Path Sync]

        // [SECTION: Live ROM Path Rescan]
        // Combines the rompath rewrite and a full game-list rebuild into one callable sequence, so ROM
        // path changes (including the CHD path) can take effect without an app restart. Shares the exact
        // same two methods the boot-time Loaded handler already runs, just triggered on demand.
        public async Task RescanRomPathsAsync()
        {
            await SyncMameRomPathsAsync();
            await InitializeDatabaseAsync();
        }
        // [END SECTION: Live ROM Path Rescan]

        // [SECTION: Database Initialization & Game Discovery]
        // Discovers ROM zip files on disk (respecting an optional storage-options override for the roms path,
        // skipping the "bios" folder), parses them through CacheScannerService, populates GamesCollection,
        // then rebuilds the live tree.
        public async Task InitializeDatabaseAsync()
        {
            // Shows a placeholder row immediately (before the potentially long zip-scan/cache-parse/sort
            // work below runs on a background thread) so a large ROM set doesn't present as a blank tree
            // for 10-20+ seconds with no feedback. UpdateLiveTreeDisplay() clears TreeNodesCollection at
            // the end of this method regardless of outcome, so this placeholder is always replaced.
            TreeNodesCollection.Clear();
            TreeNodesCollection.Add(new TreeCategoryNode
            {
                HeaderText = "LOADING... PLEASE WAIT",
                FolderColor = SafeConvertToBrush(Configuration.FolderColorHex)
            });

            await Task.Run(async () =>
            {
                string configDirectory = Configuration.GetConfigPath();
                string storageFile = Path.Combine(configDirectory, Configuration.StorageOptionsFile);
                string activeRomsPath = Path.Combine(Configuration.GetMamePath(), Configuration.RomsSubFolder);

                if (File.Exists(storageFile))
                {
                    foreach (string line in await File.ReadAllLinesAsync(storageFile))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("roms_path=", StringComparison.OrdinalIgnoreCase))
                        {
                            string rawPath = trimmed.Substring("roms_path=".Length).Trim();
                            activeRomsPath = Path.IsPathRooted(rawPath) ? rawPath : Path.Combine(Configuration.BaseDirectory, rawPath);
                        }
                    }
                }

                var discoveredZipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var folderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (Directory.Exists(activeRomsPath))
                {
                    var archiveFiles = Directory.EnumerateFiles(activeRomsPath, "*.zip", SearchOption.AllDirectories);
                    foreach (var file in archiveFiles)
                    {
                        string relativeSubFolder = Path.GetDirectoryName(file)?
                            .Replace(activeRomsPath, "")
                            .TrimStart(Path.DirectorySeparatorChar) ?? "roms";

                        string topLevelFolder = relativeSubFolder.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                        if (string.Equals(topLevelFolder, "bios", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string shortName = Path.GetFileNameWithoutExtension(file).ToLower();
                        discoveredZipNames.Add(shortName);

                        folderMap[shortName] = string.IsNullOrEmpty(relativeSubFolder) ? "roms" : relativeSubFolder;
                    }
                }

                var resultsMap = await _cacheService.ParseCacheFileAsync(discoveredZipNames, folderMap);

                // Detect rom set drift: if any discovered zip has no matching cache entry, or the cache
                // contains entries no longer present on disk, the mame_cache.txt is stale relative to the
                // current rom folder contents. Regenerate it via mame.exe -listfull and re-parse.
                bool cacheOutOfSync = false;

                string cacheFilePath = Path.Combine(Configuration.GetArcadeStickFilesPath(), Configuration.ConfigSubFolder, "database", "mame_cache.json");
                if (!File.Exists(cacheFilePath))
                {
                    cacheOutOfSync = true;
                }

                if (!cacheOutOfSync)
                {
                    foreach (var zipName in discoveredZipNames)
                    {
                        if (!resultsMap.ContainsKey(zipName))
                        {
                            cacheOutOfSync = true;
                            break;
                        }
                    }
                }

                if (!cacheOutOfSync)
                {
                    foreach (var cachedName in resultsMap.Keys)
                    {
                        if (!discoveredZipNames.Contains(cachedName))
                        {
                            cacheOutOfSync = true;
                            break;
                        }
                    }
                }

                if (cacheOutOfSync)
                {
                    await _cacheService.GenerateCacheFileAsync();
                    resultsMap = await _cacheService.ParseCacheFileAsync(discoveredZipNames, folderMap);
                }

                // Ensure sort_database.ini is parsed and cached before the tree rebuild below, so the
                // virtual category tree is available immediately if the toggle is already enabled.
                await _sortService.EnsureSortDatabaseLoadedAsync();

                // Ensure history.xml (Staff/Technical/Trivia/Tips/Game Info source) is parsed and cached
                // before the first game selection can need it.
                await _historyService.EnsureHistoryLoadedAsync();

                // Ensure wiki_gameinfo.xml (hand-curated Wikipedia extracts, preferred over history.xml's
                // GameInfo when a curated entry exists - see RefreshHistoryPanelsForSelectedGame) is parsed
                // and cached before the first game selection can need it.
                await _wikiService.EnsureWikiGameInfoLoadedAsync();

                App.Current.Dispatcher.Invoke(() =>
                {
                    GamesCollection.Clear();
                    foreach (var game in resultsMap.Values.OrderBy(g => g.FullTitle, StringComparer.OrdinalIgnoreCase))
                    {
                        GamesCollection.Add(game);
                    }

                    UpdateLiveTreeDisplay();
                });

                // Stage 2 stub resolution: builds the CloneOf-based lineage index used to auto-resolve
                // unambiguous history.xml stub entries. Uses the FULL unfiltered mame_cache.json map (not
                // GamesCollection) so a stub can resolve via a sibling regional release even when that
                // sibling isn't physically present in the user's romset - moved outside the Dispatcher.Invoke
                // block above since the cache file read is async I/O, not a UI-thread operation.
                var fullRomToParentMap = await _cacheService.GetFullRomToParentMapAsync();
                _historyService.BuildLineageIndex(fullRomToParentMap);
            });
        }
        // [END SECTION: Database Initialization & Game Discovery]

        // Builds a SortFilterOptions snapshot from persisted settings for the current tree rebuild. When
        // filtering is disabled, returns an options object that imposes no restriction (empty
        // region/player-count sets, revisions included, rating floor at 0) so callers can pass it
        // unconditionally without a separate enabled/disabled branch at every call site.
        private Services.VirtualCategorySortService.SortFilterOptions BuildSortFilterOptions()
        {
            return new Services.VirtualCategorySortService.SortFilterOptions
            {
                FilteringEnabled = Configuration.IsSortFilteringEnabled,
                EnabledRegions = new HashSet<string>(Configuration.SortEnabledRegions, StringComparer.OrdinalIgnoreCase),
                EnabledPlayerCounts = new HashSet<int>(Configuration.SortEnabledPlayerCounts),
                IncludeRevisions = Configuration.SortIncludeRevisions,
                MinimumRating = Configuration.SortMinimumRating,
                MaximumRating = Configuration.SortMaximumRating,
                EnabledGenres = new HashSet<string>(Configuration.SortEnabledGenres, StringComparer.OrdinalIgnoreCase),
                MinimumYear = Configuration.SortMinimumYear,
                MaximumYear = Configuration.SortMaximumYear,
                SelectedManufacturer = Configuration.SortSelectedManufacturer
            };
        }

        // [SECTION: Play Random Game - Pool & Spin Generation]
        // Minimum eligible pool size before a spin is allowed to start - below this, filler frames would
        // repeat too heavily to feel like a real randomizer. Caller (die click handler) checks this via
        // GenerateRandomizerSpin returning null and shows an alert instead of starting the animation.
        private const int RandomizerMinimumPoolSize = 10;
        private const int RandomizerMinFillerFrames = 15;
        private const int RandomizerMaxFillerFrames = 25;

        // Builds one full spin: sources the pool via GetRandomizerPool (always evaluates as if Base
        // Filter were on, regardless of its actual current checkbox state), locks in a winner first, then
        // generates filler frames as independent random draws from that same pool (duplicates allowed -
        // a real slot machine reel can show the same symbol twice). Returns null if the pool is too small
        // to spin. The immediately preceding winner is excluded from this spin's pool only (one-spin
        // cooldown), provided doing so wouldn't drop the pool below the minimum.
        public RandomizerSpinResult? GenerateRandomizerSpin()
        {
            var filterOptions = BuildSortFilterOptions();
            var visibleRomNames = _sortService.GetRandomizerPool(GamesCollection, filterOptions);

            var pool = GamesCollection.Where(g => visibleRomNames.Contains(g.RomName)).ToList();

            if (_lastRandomizerWinnerRomName != null && pool.Count > RandomizerMinimumPoolSize)
            {
                pool = pool.Where(g => !g.RomName.Equals(_lastRandomizerWinnerRomName, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (pool.Count < RandomizerMinimumPoolSize)
                return null;

            var winner = pool[_randomizerRng.Next(pool.Count)];
            _lastRandomizerWinnerRomName = winner.RomName;

            int frameCount = _randomizerRng.Next(RandomizerMinFillerFrames, RandomizerMaxFillerFrames + 1);
            var fillerFrames = new List<GameItem>(frameCount);
            for (int i = 0; i < frameCount; i++)
            {
                fillerFrames.Add(pool[_randomizerRng.Next(pool.Count)]);
            }

            var overshootFrame = pool[_randomizerRng.Next(pool.Count)];

            return new RandomizerSpinResult
            {
                FillerFrames = fillerFrames,
                OvershootFrame = overshootFrame,
                WinnerFrame = winner
            };
        }

        // TEMPORARY - Phase 1 smoke test only, delete once the real die button/overlay exists.
        public void DebugTestRandomizerSpin()
        {
            var result = GenerateRandomizerSpin();

            if (result == null)
            {
                MessageBox.Show("Pool too small - fewer than 10 eligible games.", "Randomizer Debug");
                return;
            }

            string fillerNames = string.Join("\n", result.FillerFrames.Select(g => g.RomName));
            MessageBox.Show(
                $"Winner: {result.WinnerFrame.RomName} ({result.WinnerFrame.FullTitle})\n" +
                $"Overshoot: {result.OvershootFrame.RomName}\n" +
                $"Filler frame count: {result.FillerFrames.Count}\n\n" +
                $"Filler frames:\n{fillerNames}",
                "Randomizer Debug");
        }

        // Lightweight pool-size check only - used at die-click time, before the confirm popup even
        // opens. Deliberately does NOT pick a winner or touch _lastRandomizerWinnerRomName - that only
        // happens once the user actually confirms via GenerateRandomizerSpin, so a cancelled die click
        // never burns a cooldown slot.
        public bool IsRandomizerPoolEligible()
        {
            var filterOptions = BuildSortFilterOptions();
            var visibleRomNames = _sortService.GetRandomizerPool(GamesCollection, filterOptions);
            int poolCount = GamesCollection.Count(g => visibleRomNames.Contains(g.RomName));
            return poolCount >= RandomizerMinimumPoolSize;
        }
        // [END SECTION: Play Random Game - Pool & Spin Generation]

        // [SECTION: Play Random Game - Spin Animation]
        private DispatcherTimer? _randomizerSpinTimer;
        private List<GameItem>? _randomizerFrameSequence;
        private int _randomizerFrameIndex;

        private bool _isRandomizerSpinning;
        public bool IsRandomizerSpinning
        {
            get => _isRandomizerSpinning;
            set
            {
                if (_isRandomizerSpinning != value)
                {
                    _isRandomizerSpinning = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRandomizerOverlayActive));
                }
            }
        }

        // True from the moment a spin lands until the user resolves the reveal popup (Launch/Respin/
        // Cancel) - keeps the spin overlays locked on the winner instead of instantly reverting the
        // instant the timer stops, which is what IsRandomizerSpinning alone would do.
        private bool _isRandomizerResultShowing;
        public bool IsRandomizerResultShowing
        {
            get => _isRandomizerResultShowing;
            set
            {
                if (_isRandomizerResultShowing != value)
                {
                    _isRandomizerResultShowing = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRandomizerOverlayActive));
                }
            }
        }

        // Single umbrella flag the overlays actually bind to - true while spinning OR while the reveal
        // popup is showing, so the marquee/media overlays stay locked on the winner through the whole
        // reveal instead of collapsing the instant the swap-loop timer finishes.
        public bool IsRandomizerOverlayActive => IsRandomizerSpinning || IsRandomizerResultShowing;

        private BitmapImage? _randomizerFrameMarqueeImage;
        public BitmapImage? RandomizerFrameMarqueeImage
        {
            get => _randomizerFrameMarqueeImage;
            set
            {
                if (_randomizerFrameMarqueeImage != value)
                {
                    _randomizerFrameMarqueeImage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RandomizerFrameHasMarquee));
                }
            }
        }

        // Reflects whether a REAL marquee file exists for the current frame's game - deliberately
        // distinct from MarqueeImage/HasMarqueeImage, which always falls back to the theme logo.
        // Phase 4's no-marquee overlay needs the true signal, not "some image happened to load."
        public bool RandomizerFrameHasMarquee => RandomizerFrameMarqueeImage != null;

        private GameItem? _randomizerCurrentFrameGame;
        public GameItem? RandomizerCurrentFrameGame
        {
            get => _randomizerCurrentFrameGame;
            set
            {
                if (_randomizerCurrentFrameGame != value)
                {
                    _randomizerCurrentFrameGame = value;
                    OnPropertyChanged();
                }
            }
        }

        // Raised once the spin sequence finishes, carrying the winner - Phase 5's reveal overlay will
        // subscribe to this.
        public event Action<GameItem>? RandomizerSpinCompleted;

        // Entry point called after the user confirms "Spin" in the confirm popup. Generates the spin
        // (winner + filler sequence), then drives a DispatcherTimer through the sequence with a
        // decelerating interval, swapping RandomizerFrameMarqueeImage on each tick. Deliberately does NOT
        // touch MarqueeImage/SelectedGame - normal tree browsing state is never disturbed mid-spin, since
        // this renders in its own overlay layer, not the real marquee panel.
        public void StartRandomizerSpin()
        {
            var spin = GenerateRandomizerSpin();
            if (spin == null)
            {
                // Shouldn't normally be reachable - the die-click handler already checked
                // IsRandomizerPoolEligible before the confirm popup even opened - but fail safe rather
                // than crash on the edge case where the cooldown exclusion could theoretically matter.
                return;
            }

            _randomizerFrameSequence = new List<GameItem>(spin.FillerFrames)
            {
                spin.OvershootFrame,
                spin.WinnerFrame
            };
            _randomizerFrameIndex = 0;
            IsRandomizerSpinning = true;

            _randomizerSpinTimer?.Stop();
            _randomizerSpinTimer = new DispatcherTimer();
            _randomizerSpinTimer.Tick += RandomizerSpinTimer_Tick;
            AdvanceRandomizerFrame();
            ScheduleNextRandomizerTick();
            _randomizerSpinTimer.Start();
        }

        private void RandomizerSpinTimer_Tick(object? sender, EventArgs e)
        {
            _randomizerSpinTimer!.Stop();
            _randomizerFrameIndex++;

            if (_randomizerFrameSequence == null || _randomizerFrameIndex >= _randomizerFrameSequence.Count)
            {
                IsRandomizerSpinning = false;
                var winner = _randomizerCurrentFrameGame;
                if (winner != null)
                {
                    // Set the real selection and resolve its media immediately, bypassing the normal
                    // 600ms preview debounce (which exists to avoid reloading media on every rapid
                    // arrow-key press while browsing - not applicable to a single deterministic landing
                    // event). This is what lets the real marquee/media panels show the winner correctly
                    // the instant our spin overlays hide, with no separate "locked" overlay state needed.
                    SelectedGame = winner;
                    UpdateActiveMediaPreviews();

                    IsRandomizerResultShowing = true;
                    RandomizerSpinCompleted?.Invoke(winner);
                }
                return;
            }

            AdvanceRandomizerFrame();
            ScheduleNextRandomizerTick();
            _randomizerSpinTimer.Start();
        }

        // Loads and applies the marquee image for the current frame's game, mirroring
        // ResolveMarqueeImage's file-loading logic but WITHOUT the logo fallback - RandomizerFrameHasMarquee
        // needs to reflect whether a REAL marquee file exists for this ROM, since the no-marquee overlay
        // (Phase 4) uses it to decide whether to cover the panel.
        private void AdvanceRandomizerFrame()
        {
            if (_randomizerFrameSequence == null) return;

            var game = _randomizerFrameSequence[_randomizerFrameIndex];
            RandomizerCurrentFrameGame = game;

            string marqueeDir = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.MarqueesPath) ? "marquees" : Configuration.MarqueesPath);
            string? targetMarqueeFile = TryResolveMediaFile(marqueeDir, game.RomName, game.CloneOf, new[] { ".png" });

            if (targetMarqueeFile == null)
            {
                RandomizerFrameMarqueeImage = null;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                byte[] fileBytes = File.ReadAllBytes(targetMarqueeFile);
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(fileBytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                RandomizerFrameMarqueeImage = bitmap;
            }
            catch
            {
                RandomizerFrameMarqueeImage = null;
            }
        }

        // Decelerating interval curve - fast at the start, slowing toward the landing frame. Simple
        // ease-out (t^2); tuning deferred to the later polish pass per plan.
        private void ScheduleNextRandomizerTick()
        {
            if (_randomizerSpinTimer == null || _randomizerFrameSequence == null) return;

            const int startMs = 70;
            const int endMs = 350;
            int totalFrames = _randomizerFrameSequence.Count;
            double t = totalFrames <= 1 ? 1.0 : (double)_randomizerFrameIndex / (totalFrames - 1);
            double eased = t * t;
            int intervalMs = startMs + (int)((endMs - startMs) * eased);

            _randomizerSpinTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
        }
        // [END SECTION: Play Random Game - Spin Animation]

        // [SECTION: Play History]
        // Reads a single-hex-line color file (RecentlyPlayedColorFile / MostPlayedColorFile) - same
        // shape as a custom folder's own .cfg minus the ROM membership lines. Falls back to a sensible
        // gold/yellow default if the file doesn't exist yet (matches FavoritesColorHex's own default).
        private string ReadFolderColorFile(string fileName)
        {
            try
            {
                string filePath = Path.Combine(Configuration.GetConfigPath(), fileName);
                if (File.Exists(filePath))
                {
                    var lines = File.ReadAllLines(filePath);
                    if (lines.Length > 0 && !string.IsNullOrWhiteSpace(lines[0]))
                    {
                        return lines[0].Trim();
                    }
                }
            }
            catch { }

            return "#FFFFCC00";
        }

        // Writes a single-hex-line color file, mirroring SetCustomFolderColor's exact pattern (direct
        // file write, no settings.json/persist-callback involvement needed).
        private void SetFolderColorFile(string fileName, string hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor)) return;

            try
            {
                string filePath = Path.Combine(Configuration.GetConfigPath(), fileName);
                File.WriteAllLines(filePath, new[] { hexColor });
                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error setting color: {ex.Message}");
            }
        }

        public void SetRecentlyPlayedColor(string hexColor) => SetFolderColorFile(Configuration.RecentlyPlayedColorFile, hexColor);
        public void SetMostPlayedColor(string hexColor) => SetFolderColorFile(Configuration.MostPlayedColorFile, hexColor);
        // [END SECTION: Play History - Color]

        // Parses play_history.cfg into a list of (RomName, LastPlayedUnixTimestamp, PlayCount,
        // TotalPlaySeconds) entries. Shared by the Recently Played / Most Played node-building in
        // UpdateLiveTreeDisplay below and by the play-stats footer text lookup.
        private List<(string RomName, long LastPlayed, int PlayCount, double TotalSeconds)> ReadPlayHistoryEntries()
        {
            var entries = new List<(string, long, int, double)>();

            try
            {
                string historyPath = Path.Combine(Configuration.GetConfigPath(), Configuration.PlayHistoryFile);
                if (!File.Exists(historyPath)) return entries;

                foreach (var line in File.ReadAllLines(historyPath))
                {
                    var parts = line.Split('|');
                    if (parts.Length < 4) continue;

                    if (long.TryParse(parts[1], out var lastPlayed) &&
                        int.TryParse(parts[2], out var playCount) &&
                        double.TryParse(parts[3], out var totalSeconds))
                    {
                        entries.Add((parts[0], lastPlayed, playCount, totalSeconds));
                    }
                }
            }
            catch { }

            return entries;
        }

        // Dispatches to the correct per-type clear logic - all four folder types keep the folder itself,
        // only their contents change. folderHeaderText is whatever the context menu resolved (the custom
        // folder's actual on-disk name, or the static folder's own HeaderText).
        public void ClearFolder(string folderHeaderText)
        {
            if (string.IsNullOrWhiteSpace(folderHeaderText)) return;

            if (string.Equals(folderHeaderText, "FAVORITES", StringComparison.OrdinalIgnoreCase))
            {
                ClearFavorites();
            }
            else if (string.Equals(folderHeaderText, "RECENTLY PLAYED", StringComparison.OrdinalIgnoreCase))
            {
                // Only the currently-VISIBLE top-30 entries get their LastPlayed reset - whatever was
                // ranked #31+ surfaces to take their place. PlayCount is left untouched for Most Played's sake.
                ClearVisiblePlayHistorySlice(clearLastPlayed: true, clearPlayCount: false);
            }
            else if (string.Equals(folderHeaderText, "MOST PLAYED", StringComparison.OrdinalIgnoreCase))
            {
                ClearVisiblePlayHistorySlice(clearLastPlayed: false, clearPlayCount: true);
            }
            else
            {
                ClearCustomFolder(folderHeaderText);
            }
        }

        // Empties Favorites entirely, mirroring ToggleFavorite's own persistence format exactly.
        private void ClearFavorites()
        {
            try
            {
                string configDir = Configuration.GetConfigPath();
                string favoritesFilePath = Path.Combine(configDir, Configuration.FavoritesListFile);

                FavoriteRoms.Clear();

                var fileContents = new List<string>
                {
                    "# ARCADE LAUNCHER FAVORITES CONFIG",
                    "# DO NOT MODIFY MANUALLY",
                    ""
                };
                File.WriteAllLines(favoritesFilePath, fileContents);

                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error clearing favorites: {ex.Message}");
            }
        }

        // Truncates a custom folder's .cfg to just its color line, removing every ROM membership line.
        private void ClearCustomFolder(string folderName)
        {
            try
            {
                string playlistsDir = Path.Combine(Configuration.GetConfigPath(), "playlists");
                string filePath = Path.Combine(playlistsDir, $"{folderName}.cfg");
                if (!File.Exists(filePath)) return;

                var lines = File.ReadAllLines(filePath).ToList();
                string colorLine = lines.Count > 0 ? lines[0] : "#FFFFFF";
                File.WriteAllLines(filePath, new[] { colorLine });

                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error clearing folder '{folderName}': {ex.Message}");
            }
        }

        // "Clear Folder" for Recently Played/Most Played only touches the CURRENTLY VISIBLE top-30
        // entries - recomputes the same ranking UpdateLiveTreeDisplay just used, resets just the one
        // field this folder is sorted by, revealing whatever was ranked just below. The other field
        // (which the OTHER static folder depends on) is left completely untouched on every row.
        private void ClearVisiblePlayHistorySlice(bool clearLastPlayed, bool clearPlayCount)
        {
            try
            {
                string historyPath = Path.Combine(Configuration.GetConfigPath(), Configuration.PlayHistoryFile);
                if (!File.Exists(historyPath)) return;

                var entries = ReadPlayHistoryEntries();
                var visibleSlice = clearLastPlayed
                    ? entries.OrderByDescending(e => e.LastPlayed).Take(30).Select(e => e.RomName)
                    : entries.OrderByDescending(e => e.PlayCount).ThenByDescending(e => e.LastPlayed).Take(30).Select(e => e.RomName);
                var visibleSet = new HashSet<string>(visibleSlice, StringComparer.OrdinalIgnoreCase);

                var lines = File.ReadAllLines(historyPath).ToList();
                for (int i = 0; i < lines.Count; i++)
                {
                    var parts = lines[i].Split('|');
                    if (parts.Length < 4 || !visibleSet.Contains(parts[0])) continue;

                    long lastPlayed = clearLastPlayed ? 0 : (long.TryParse(parts[1], out var lp) ? lp : 0);
                    int playCount = clearPlayCount ? 0 : (int.TryParse(parts[2], out var pc) ? pc : 0);
                    lines[i] = $"{parts[0]}|{lastPlayed}|{playCount}|{parts[3]}";
                }

                File.WriteAllLines(historyPath, lines);
                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error clearing folder: {ex.Message}");
            }
        }

        // "Clear All Play History" - unlike ClearFolder above, this touches EVERY entry in the log, not
        // just the visible top-30. Only reachable from Recently Played/Most Played's own context menu.
        public void ClearAllPlayHistory(bool clearLastPlayed, bool clearPlayCount)
        {
            try
            {
                string historyPath = Path.Combine(Configuration.GetConfigPath(), Configuration.PlayHistoryFile);
                if (!File.Exists(historyPath)) return;

                var lines = File.ReadAllLines(historyPath).ToList();
                for (int i = 0; i < lines.Count; i++)
                {
                    var parts = lines[i].Split('|');
                    if (parts.Length < 4) continue;

                    long lastPlayed = clearLastPlayed ? 0 : (long.TryParse(parts[1], out var lp) ? lp : 0);
                    int playCount = clearPlayCount ? 0 : (int.TryParse(parts[2], out var pc) ? pc : 0);
                    lines[i] = $"{parts[0]}|{lastPlayed}|{playCount}|{parts[3]}";
                }

                File.WriteAllLines(historyPath, lines);
                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error clearing play history: {ex.Message}");
            }
        }
        // [END SECTION: Play History]

        // [SECTION: Tree Building - Categories, Playlists & Search]

        // [SECTION: Tree Building - Categories, Playlists & Search]
        // Rebuilds TreeNodesCollection from scratch: groups GamesCollection by FolderPath into nested
        // TreeCategoryNodes, merges in custom playlists from the playlists/*.cfg folder, applies the
        // Favorites node and configured folder ordering (folder_order file), and swaps in a
        // "SEARCH RESULTS" / "No results found..." node when SearchText is active.
        public void UpdateLiveTreeDisplay()
        {
            IsRebuildingTree = true;
            try
            {
                // Snapshot every currently-expanded folder's full path (HeaderText segments joined by "\")// before the tree is rebuilt, so Apply Filter / theme changes / etc. can restore the user's
                // browsing position afterward instead of collapsing everything back to the root.
                var expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectExpandedPaths(TreeNodesCollection, string.Empty, expandedPaths);

                var sortFilterOptions = BuildSortFilterOptions();

                // Total filtered ROM pool, independent of which structure mode (virtual category vs custom
                // folder) is currently rendering it - both branches already funnel through this same
                // eligibility/dedup source, so it's the single correct number regardless of toggle state.
                int filteredGameCount = _sortService.ResolveVisibleFamilyMembers(GamesCollection, sortFilterOptions).Count;
                FilteredGameCountText = $"Game Pool: {filteredGameCount:N0} Games";

                string configDir = Configuration.GetConfigPath();
                string playlistsDir = Path.Combine(configDir, "playlists");
                string folderOrderPath = Path.Combine(configDir, Configuration.FolderOrderFile);

                if (!Directory.Exists(playlistsDir))
            {
                Directory.CreateDirectory(playlistsDir);
            }

            var rootCategories = new Dictionary<string, TreeCategoryNode>(StringComparer.OrdinalIgnoreCase);
            var mainFolderBrush = SafeConvertToBrush(Configuration.FolderColorHex);

            // Prefixes the rom name onto the title when Dev Mode is active, and appends a parenthetical
            // differentiator for clones (e.g. "1941: Counter Attack (World)") so they read distinctly
            // from their parent - mirrors BuildVirtualTree's clone-display logic, extended here since the
            // custom-folder branch previously had no equivalent, leaving same-titled clones and parents
            // visually indistinguishable.
            string GetFormattedTitle(GameItem gameItem)
            {
                string differentiator = !string.IsNullOrEmpty(gameItem.CloneOf)
                    ? Services.VirtualCategorySortService.BuildCloneDifferentiator(gameItem)
                    : string.Empty;

                string baseTitle = string.IsNullOrEmpty(differentiator)
                    ? gameItem.FullTitle
                    : $"{gameItem.FullTitle} {differentiator}".Trim();

                return IsDevMode ? $"[{gameItem.RomName}] {baseTitle}" : baseTitle;
            }

            // Mouse-support flags and theme-driven display properties (FontSize/colors) are pushed here
            // rather than resolved via RelativeSource AncestorType=TreeView in XAML - eliminates the
            // ancestor visual-tree walk that was causing severe UI freezes when generating containers for
            // very large (30k+) folders. Extracted into RefreshGameColorsLive() so theme save/load can
            // also trigger this refresh without needing a full tree rebuild.
            RefreshGameColorsLive();

            if (Configuration.IsCategorySortEnabled)
            {
                // Virtual category sort branch: build from sort_database.ini instead of FolderPath.
                // Playlists and folder_order.cfg are custom-folder-structure concepts and are
                // intentionally skipped here - the underlying custom folder data is never touched,
                // it's just not what's rendered while this toggle is on.
                var virtualNodes = _sortService.BuildVirtualTree(GamesCollection, mainFolderBrush, sortFilterOptions); foreach (var node in virtualNodes)
                {
                    rootCategories[node.HeaderText] = node;
                }

                // Re-apply Dev Mode's rom-name prefix on top of whatever DisplayTitle the sort service
                // already set (clean parent title, or parent + parenthetical differentiator for a
                // surviving clone) - keeps Dev Mode logic living entirely in MainViewModel.
                void ApplyDevModePrefix(TreeCategoryNode node)
                {
                    if (IsDevMode)
                    {
                        foreach (var game in node.ChildGames)
                        {
                            game.DisplayTitle = $"[{game.RomName}] {game.DisplayTitle}";
                        }
                    }
                    foreach (var sub in node.SubFolders)
                    {
                        ApplyDevModePrefix(sub);
                    }
                }

                foreach (var node in rootCategories.Values)
                {
                    ApplyDevModePrefix(node);
                }
            }
            else
            {
                // Filter gate + dedup (independent of folder structure choice - see Sorting window
                // design): ResolveVisibleFamilyMembers is the single shared implementation also used by
                // BuildVirtualTree, so both structure modes can never disagree - same eligibility rules,
                // same parent-region shortcut, same tiebreaker dedup for clones that would otherwise
                // display identically (e.g. multiple same-region builds differing only by internal date).
                var visibleRomNames = _sortService.ResolveVisibleFamilyMembers(GamesCollection, sortFilterOptions);

                // Pass 1: group games into root categories / nested sub-folders based on FolderPath
                foreach (var game in GamesCollection)
                {
                    if (!visibleRomNames.Contains(game.RomName))
                        continue;

                    game.DisplayTitle = GetFormattedTitle(game);

                    string pathStr = game.FolderPath.Trim();
                    bool isUngrouped = string.Equals(pathStr, "roms", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(pathStr);

                    string[] pathParts = isUngrouped
                        ? Array.Empty<string>()
                        : pathStr.Split(new[] { '\\', '/' }, System.StringSplitOptions.RemoveEmptyEntries);

                    string rootHeader = (isUngrouped || pathParts.Length == 0) ? "GAMES" : pathParts[0].ToUpper().Trim();
                    TreeCategoryNode currentPointer;

                    if (!rootCategories.TryGetValue(rootHeader, out currentPointer!))
                    {
                        currentPointer = new TreeCategoryNode { HeaderText = rootHeader, FolderColor = mainFolderBrush };
                        rootCategories[rootHeader] = currentPointer;
                    }

                    if (pathParts.Length <= 1)
                    {
                        currentPointer.TryAddChildGame(game);
                        continue;
                    }

                    for (int i = 1; i < pathParts.Length; i++)
                    {
                        string subHeader = pathParts[i].ToUpper().Trim();
                        var existingSub = currentPointer.SubFolders.FirstOrDefault(sf => string.Equals(sf.HeaderText, subHeader, StringComparison.OrdinalIgnoreCase));

                        if (existingSub == null)
                        {
                            existingSub = new TreeCategoryNode { HeaderText = subHeader, FolderColor = mainFolderBrush };
                            currentPointer.SubFolders.Add(existingSub);
                        }
                        currentPointer = existingSub;
                    }

                        currentPointer.TryAddChildGame(game);
                    }
                }

                // Subfolders are discovered in whatever order games happen to be enumerated in, which is
                // effectively arbitrary - sort every node's SubFolders alphabetically (recursively, since
                // nesting can go more than one level deep) so browsing order is predictable rather than random.
                // Only the SECOND level down (a root's direct subfolders' OWN children, i.e. grandchildren+)
                // gets this treatment - a root's direct subfolders themselves are order-file-aware instead
                // (capped at one level per the Stage 3 design), handled later in Pass 3 below. Runs
                // unconditionally for both structure modes, unlike the old version which only ran for the
                // physical-FolderPath branch.
                void SortSubFoldersRecursively(TreeCategoryNode node)
                    {
                        var sorted = node.SubFolders.OrderBy(sf => sf.HeaderText, StringComparer.OrdinalIgnoreCase).ToList();
                        node.SubFolders.Clear();
                        foreach (var sub in sorted)
                        {
                            node.SubFolders.Add(sub);
                            SortSubFoldersRecursively(sub);
                        }
                    }

                    foreach (var rootNode in rootCategories.Values)
                    {
                        foreach (var directSub in rootNode.SubFolders)
                        {
                            SortSubFoldersRecursively(directSub);
                        }
                    }

                    // Pass 2: merge in custom playlist folders (playlists/*.cfg), each optionally starting with a
                    // #hexcolor line. Runs unconditionally in both structure modes (moved out of the else branch
                    // above) - custom folders are user-curated data independent of virtual/catver structure mode,
                    // same as Favorites already behaves. The underlying .cfg files are untouched either way; this
                    // only ever affected whether the folder was rendered.
                    if (Directory.Exists(playlistsDir))
            {
                foreach (var cfgFile in Directory.GetFiles(playlistsDir, "*.cfg"))
                {
                    string playlistName = Path.GetFileNameWithoutExtension(cfgFile).ToUpper().Trim();
                    if (playlistName == Configuration.MouseSupportFile.ToUpper()) continue;

                    var lines = File.ReadAllLines(cfgFile).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();
                    var brushColor = SafeConvertToBrush(Configuration.VirtualListsColor);

                    if (lines.Count > 0 && lines[0].StartsWith("#") && lines[0].Length == 7)
                    {
                        try { brushColor = SafeConvertToBrush(lines[0]); } catch { }
                    }

                        var pNode = new TreeCategoryNode { HeaderText = playlistName, FolderColor = brushColor, IsCustomColor = true };

                        var matchedGames = lines
                            .Where(l => !l.StartsWith("#"))
                            .Select(l => GamesCollection.FirstOrDefault(g => g.RomName.Equals(l, StringComparison.OrdinalIgnoreCase)))
                            .Where(g => g != null)
                            .OrderBy(g => g!.FullTitle, StringComparer.OrdinalIgnoreCase);

                        foreach (var matchedGame in matchedGames)
                        {
                            pNode.TryAddChildGame(matchedGame!);
                        }

                        rootCategories[playlistName] = pNode;
                    }
            }

                    // Pass 3: apply configured folder ordering - root level, and one level of subfolders (the
                    // Stage 3 nesting cap; anything deeper stays plain-alphabetical from Pass 1/1.5 above).
                    // Virtual sort mode reads its own order file (virtual_order.cfg) rather than folder_order.cfg,
                    // so reordering root categories never collides with or overwrites the custom folder order -
                    // they're independent orderings the user can maintain separately for each mode.
                    string activeOrderPath = Configuration.IsCategorySortEnabled
                        ? Path.Combine(configDir, Configuration.VirtualOrderFile)
                        : folderOrderPath;

                    var allOrderLines = File.Exists(activeOrderPath)
                        ? File.ReadAllLines(activeOrderPath).Select(l => l.Trim().ToUpper()).Where(l => !string.IsNullOrEmpty(l)).ToList()
                        : new List<string>();

                    var customFolderNamesUpper = GetCustomFolderNames().Select(f => f.ToUpper()).ToList();

                    // Orders a set of sibling nodes at a given parent path against the saved order file, with
                    // type-aware fallback for anything not yet listed there: custom folders sink to the top of
                    // the unlisted group, everything else sinks to the bottom. Mirrors MoveFolderInOrder's own
                    // seeding rule, so a folder's effective position is identical whether it got there by an
                    // explicit move or by simply never having been moved yet.
                    List<TreeCategoryNode> ApplyFolderOrder(IEnumerable<TreeCategoryNode> siblings, string parentPathUpper)
                    {
                        var remaining = siblings.ToList();

                        var orderedHeaders = allOrderLines
                            .Where(l =>
                            {
                                int idx = l.LastIndexOf('\\');
                                string lineParent = idx >= 0 ? l.Substring(0, idx) : string.Empty;
                                return lineParent.Equals(parentPathUpper, StringComparison.OrdinalIgnoreCase);
                            })
                            .Select(l =>
                            {
                                int idx = l.LastIndexOf('\\');
                                return idx >= 0 ? l.Substring(idx + 1) : l;
                            })
                            .ToList();

                        var listedNodes = new List<TreeCategoryNode>();
                        foreach (var header in orderedHeaders)
                        {
                            var match = remaining.FirstOrDefault(n => n.HeaderText.Equals(header, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                            {
                                listedNodes.Add(match);
                                remaining.Remove(match);
                            }
                        }

                        var unlistedCustom = remaining.Where(n => customFolderNamesUpper.Contains(n.HeaderText.ToUpper()))
                            .OrderBy(n => n.HeaderText, StringComparer.OrdinalIgnoreCase).ToList();
                        var unlistedOther = remaining.Except(unlistedCustom)
                            .OrderBy(n => n.HeaderText, StringComparer.OrdinalIgnoreCase).ToList();

                        return unlistedCustom.Concat(listedNodes).Concat(unlistedOther).ToList();
                    }

                var finalSortedNodes = new List<TreeCategoryNode>();

                // Favorites always pinned to the top when non-empty
                if (FavoriteRoms.Count > 0)
                {
                    var favoritesRoot = new TreeCategoryNode
                    {
                        HeaderText = "FAVORITES",
                        FolderColor = SafeConvertToBrush(Configuration.FavoritesColorHex)
                    };

                    foreach (var romName in FavoriteRoms)
                    {
                        var matchedGame = GamesCollection.FirstOrDefault(g => g.RomName.Equals(romName, StringComparison.OrdinalIgnoreCase));
                        if (matchedGame != null && !favoritesRoot.ChildGames.Contains(matchedGame))
                        {
                            matchedGame.DisplayTitle = GetFormattedTitle(matchedGame);
                            favoritesRoot.ChildGames.Add(matchedGame);
                        }
                    }

                    if (favoritesRoot.ChildGames.Count > 0)
                    {
                        finalSortedNodes.Add(favoritesRoot);
                    }
                }

                // Recently Played / Most Played - static, pinned like Favorites, bypass all filters the
                // same way. Each independently computes its own top-30 slice from the full (uncapped)
                // play history log at rebuild time, so a game falling out of the visible 30 never
                // loses its recorded history.
                var playHistoryEntries = ReadPlayHistoryEntries();

                if (playHistoryEntries.Count > 0)
                {
                    var recentlyPlayedRoot = new TreeCategoryNode
                    {
                        HeaderText = "RECENTLY PLAYED",
                        FolderColor = SafeConvertToBrush(ReadFolderColorFile(Configuration.RecentlyPlayedColorFile)),
                        IsCustomColor = true // otherwise RefreshFolderColorsLive() overwrites this with the generic folder color
                    };

                    foreach (var entry in playHistoryEntries.Where(e => e.LastPlayed > 0).OrderByDescending(e => e.LastPlayed).Take(30))
                    {
                        var matchedGame = GamesCollection.FirstOrDefault(g => g.RomName.Equals(entry.RomName, StringComparison.OrdinalIgnoreCase));
                        if (matchedGame != null && !recentlyPlayedRoot.ChildGames.Contains(matchedGame))
                        {
                            matchedGame.DisplayTitle = GetFormattedTitle(matchedGame);
                            recentlyPlayedRoot.ChildGames.Add(matchedGame);
                        }
                    }

                    if (recentlyPlayedRoot.ChildGames.Count > 0)
                    {
                        finalSortedNodes.Add(recentlyPlayedRoot);
                    }

                    var mostPlayedRoot = new TreeCategoryNode
                    {
                        HeaderText = "MOST PLAYED",
                        FolderColor = SafeConvertToBrush(ReadFolderColorFile(Configuration.MostPlayedColorFile)),
                        IsCustomColor = true // otherwise RefreshFolderColorsLive() overwrites this with the generic folder color
                    };

                    foreach (var entry in playHistoryEntries.Where(e => e.PlayCount > 0).OrderByDescending(e => e.PlayCount).ThenByDescending(e => e.LastPlayed).Take(30))
                    {
                        var matchedGame = GamesCollection.FirstOrDefault(g => g.RomName.Equals(entry.RomName, StringComparison.OrdinalIgnoreCase));
                        if (matchedGame != null && !mostPlayedRoot.ChildGames.Contains(matchedGame))
                        {
                            matchedGame.DisplayTitle = GetFormattedTitle(matchedGame);
                            mostPlayedRoot.ChildGames.Add(matchedGame);
                        }
                    }

                    if (mostPlayedRoot.ChildGames.Count > 0)
                    {
                        finalSortedNodes.Add(mostPlayedRoot);
                    }
                }

                var orderedRootNodes = ApplyFolderOrder(rootCategories.Values, string.Empty);
                foreach (var node in orderedRootNodes)
                    {
                        var orderedSubs = ApplyFolderOrder(node.SubFolders, node.HeaderText.ToUpper());
                        node.SubFolders.Clear();
                        foreach (var sub in orderedSubs)
                        {
                            node.SubFolders.Add(sub);
                        }

                        finalSortedNodes.Add(node);
                    }

                    // Pass 4: commit to TreeNodesCollection - either the sorted category tree, or search results
                    TreeNodesCollection.Clear();
            string query = SearchText.Trim().ToLower();
            bool isSearching = !string.IsNullOrEmpty(query);

            if (isSearching)
            {
                var matchingGames = GamesCollection.Where(g =>
                    g.RomName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    g.FullTitle.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

                // When category sort + filtering is active, search should only surface roms that
                // survive the same eligibility pipeline the sorted tree uses (parent/variant check,
                // bootleg/hack/prototype denylist, player-count exclusion list) - otherwise search
                // bypasses the filter entirely and floods results with clones the sorted view hides.
                if (Configuration.IsCategorySortEnabled)
                {
                    var searchVisibleRomNames = _sortService.ResolveVisibleFamilyMembers(GamesCollection, sortFilterOptions);
                    matchingGames = matchingGames.Where(g => searchVisibleRomNames.Contains(g.RomName)).ToList();
                }

                if (matchingGames.Count > 0)
                {
                    var searchRootNode = new TreeCategoryNode
                    {
                        HeaderText = "SEARCH RESULTS",
                        FolderColor = mainFolderBrush
                    };

                    foreach (var game in matchingGames)
                    {
                        game.DisplayTitle = GetFormattedTitle(game);
                        searchRootNode.ChildGames.Add(game);
                    }

                    TreeNodesCollection.Add(searchRootNode);
                    searchRootNode.IsNodeExpanded = true;
                }
                else
                {
                    var emptyRootNode = new TreeCategoryNode
                    {
                        HeaderText = "No results found...",
                        FolderColor = Brushes.Red
                    };

                    TreeNodesCollection.Add(emptyRootNode);
                    emptyRootNode.IsNodeExpanded = true;
                }
            }
            else
            {
                foreach (var cat in finalSortedNodes)
                {
                    TreeNodesCollection.Add(cat);
                }

                ApplyExpandedPaths(finalSortedNodes, string.Empty, expandedPaths);
            }

                RefreshFolderColorsLive();
                RebuildFlatVisibleRows();
            }
            finally
            {
                // Deferred one dispatcher pass instead of resetting synchronously here - the old row's
                // container teardown (from clearing/rebuilding TreeNodesCollection above) doesn't
                // necessarily fire its SelectedItemChanged(null) event synchronously with that clear; WPF
                // can defer delivery to a later dispatcher cycle. Resetting the flag immediately left a
                // window where that deferred null could arrive after the guard's protection already
                // lapsed, intermittently wiping SelectedGame for real instead of being caught as the
                // transient artifact it actually is.
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    IsRebuildingTree = false;
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        // Recursively walks a tree of TreeCategoryNode, collecting the full path (HeaderText segments// joined by "\") of every node whose IsNodeExpanded is currently true. Used by
        // UpdateLiveTreeDisplay to snapshot expansion state before a rebuild.
        private static void CollectExpandedPaths(IEnumerable<TreeCategoryNode> nodes, string parentPath, HashSet<string> expandedPaths)
        {
            foreach (var node in nodes)
            {
                string path = string.IsNullOrEmpty(parentPath) ? node.HeaderText : $"{parentPath}\\{node.HeaderText}";

                if (node.IsNodeExpanded)
                {
                    expandedPaths.Add(path);
                }

                CollectExpandedPaths(node.SubFolders, path, expandedPaths);
            }
        }

        // Recursively walks a freshly-built tree, re-expanding any node whose full path was captured by
        // CollectExpandedPaths before the rebuild. Falls back to collapsed (the node's default state) for
        // any path that no longer matches - e.g. after switching between custom-folder and category-sort
        // structure modes, where folder names won't line up.
        private static void ApplyExpandedPaths(IEnumerable<TreeCategoryNode> nodes, string parentPath, HashSet<string> expandedPaths)
        {
            foreach (var node in nodes)
            {
                string path = string.IsNullOrEmpty(parentPath) ? node.HeaderText : $"{parentPath}\\{node.HeaderText}";
                node.IsNodeExpanded = expandedPaths.Contains(path);
                ApplyExpandedPaths(node.SubFolders, path, expandedPaths);
            }
        }
        // [END SECTION: Tree Building - Categories, Playlists & Search]

        private BitmapImage? _previewImage;

        public BitmapImage? PreviewImage
        {
            get => _previewImage;
            set
            {
                if (_previewImage != value)
                {
                    _previewImage = value;
                    OnPropertyChanged();
                }
            }
        }



        // [SECTION: Media Preview Resolution]
        // Resolves marquee image (with fallback to the default theme logo) and the active preview media
        // (video/flyer/screenshot/etc, in PreviewPriorityOrder) for the currently selected game.
        // NOTE: known beta trade-off - video preview can stay black after returning from MAME until reselection.
        // Resolves and applies MarqueeImage for the currently selected game - extracted so it can be re-run
        // independently (e.g. after a theme change closes) without touching video/flyer preview state
        private void ResolveMarqueeImage()
        {
            if (SelectedGame == null)
            {
                MarqueeImage = null;
                return;
            }

            string marqueeDir = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.MarqueesPath) ? "marquees" : Configuration.MarqueesPath);
            // Regional variants (US/UK/Europe/etc.) sharing a parent's marquee is the common, desirable
            // case - a differently-branded bootleg/hack borrowing the wrong marquee name is a rarer
            // trade-off accepted in exchange for this.
            string? targetMarqueeFile = TryResolveMediaFile(marqueeDir, SelectedGame.RomName, SelectedGame.CloneOf, new[] { ".png" });
            // Marquee fallback: if neither the game's own marquee nor its parent's is found, load the default boot logo
            if (targetMarqueeFile == null)
            {
                targetMarqueeFile = !string.IsNullOrWhiteSpace(Configuration.ThemeLogo)
                    ? Path.GetFullPath(Configuration.ThemeLogo, Configuration.GetMamePath())
                    : Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets", "default_marquee.png");
            }

            try
            {
                if (File.Exists(targetMarqueeFile))
                {
                    var bitmap = new BitmapImage();
                    byte[] fileBytes = File.ReadAllBytes(targetMarqueeFile);
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(fileBytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    MarqueeImage = bitmap;
                }
                else
                {
                    MarqueeImage = null;
                }
            }
            catch
            {
                MarqueeImage = null;
            }
        }

        // Checks a target folder for a media file matching the primary ROM name, falling back to the
        // clone's parent ROM name if the primary isn't found. Lets clones/revisions inherit the
        // parent's preview assets (video, flyer, screenshot, marquee, etc.) when they don't have their
        // own - most people only ever source media for the "main" version of a game. Returns null if
        // neither name matches in any of the given extensions.
        // Row 3's Flyer/Cabinet states in the media panel rework - separate from PreviewPriorityOrder's
        // Row 1 resolution (flyers/cabinets were deliberately removed from that list so the same image
        // doesn't show redundantly in both places). Reuses TryResolveMediaFile's existing primary+clone
        // fallback pattern rather than duplicating that logic.
        // Column Two's Rating display (media panel rework) - resolved via VirtualCategorySortService's
        // GetDisplayRating, which handles CloneOf parent resolution and the band-to-1-10 conversion.
        private int? _displayRating;
        public int? DisplayRating
        {
            get => _displayRating;
            private set
            {
                _displayRating = value;
                OnPropertyChanged();
            }
        }

        // history.xml-derived text (media panel rework) - GameInfoBlocks/HistoryTrivia/TipsAndTricks feed
        // Column Two's arrow-cycled body; HistoryStaff/Technical feed Column One Row 3's Staff/Technical
        // states. GameInfoBlocks replaced the old flat HistoryGameInfo string so Column Two can render
        // per-section themed headers (wiki-sourced) instead of one plain paragraph - see
        // RefreshHistoryPanelsForSelectedGame for how it's populated from either source. Never empty -
        // always resolves to at least one block so the bound ItemsControl always has something to show.
        public RangeObservableCollection<GameInfoBlock> GameInfoBlocks { get; } = new RangeObservableCollection<GameInfoBlock>();

        private string _historyTrivia = string.Empty;
        public string HistoryTrivia
        {
            get => _historyTrivia;
            private set { _historyTrivia = value; OnPropertyChanged(); }
        }

        private string _historyTipsAndTricks = string.Empty;
        public string HistoryTipsAndTricks
        {
            get => _historyTipsAndTricks;
            private set { _historyTipsAndTricks = value; OnPropertyChanged(); }
        }

        private string _historyStaff = string.Empty;
        public string HistoryStaff
        {
            get => _historyStaff;
            private set { _historyStaff = value; OnPropertyChanged(); }
        }

        private string _historyTechnical = string.Empty;
        public string HistoryTechnical
        {
            get => _historyTechnical;
            private set { _historyTechnical = value; OnPropertyChanged(); }
        }

        // Looks up history.xml content for the given game and populates the five History* properties
        // above. Direct RomName lookup first (history.xml usually lists every clone/revision explicitly
        // under one entry's <systems> block), falling back to CloneOf for the rare bootleg/hack variant
        // history.xml doesn't list by name - same two-pass shape as the existing artwork/media resolution
        // elsewhere in this file. Extracted out of the SelectedGame setter so ReloadHistoryOverrides below
        // can re-run the same lookup on-demand without duplicating this logic.
        private void RefreshHistoryPanelsForSelectedGame(GameItem? game)
        {
            HistoryEntry? historyEntry = null;
            WikiEntry? wikiEntry = null;
            if (game != null)
            {
                historyEntry = _historyService.GetHistoryEntry(game.RomName);
                if (historyEntry == null && !string.IsNullOrEmpty(game.CloneOf))
                {
                    historyEntry = _historyService.GetHistoryEntry(game.CloneOf);
                }

                // wiki_gameinfo.xml is hand-curated per romname (not CloneOf-resolved like history.xml -
                // every entry here was manually verified against a specific romname, so there's no lineage
                // fallback to attempt).
                wikiEntry = _wikiService.GetWikiEntry(game.RomName);
            }

            // GameInfo: prefer the curated Wikipedia extract when one exists for this romname, since it's
            // consistently richer than history.xml's often-sparse GameInfo text. Falls back to history.xml's
            // GameInfo, then the "not available" placeholder, same as before - just reshaped into blocks
            // instead of one flat string so per-section headers can render themed in XAML.
            var newGameInfoBlocks = new List<GameInfoBlock>();
            if (wikiEntry != null)
            {
                newGameInfoBlocks.Add(new GameInfoBlock { Header = string.Empty, Body = wikiEntry.Intro });
                newGameInfoBlocks.AddRange(wikiEntry.Sections);
            }
            else
            {
                string fallbackGameInfo = string.IsNullOrEmpty(historyEntry?.GameInfo) ? "No game info available for this title." : historyEntry.GameInfo;
                newGameInfoBlocks.Add(new GameInfoBlock { Header = string.Empty, Body = fallbackGameInfo });
            }
            GameInfoBlocks.ReplaceAll(newGameInfoBlocks);

            HistoryTrivia = string.IsNullOrEmpty(historyEntry?.Trivia) ? "No trivia available for this title." : historyEntry.Trivia; HistoryTrivia = string.IsNullOrEmpty(historyEntry?.Trivia) ? "No trivia available for this title." : historyEntry.Trivia;
            HistoryTipsAndTricks = string.IsNullOrEmpty(historyEntry?.TipsAndTricks) ? "No tips available for this title." : historyEntry.TipsAndTricks;
            HistoryStaff = string.IsNullOrEmpty(historyEntry?.Staff) ? "No staff information available for this title." : historyEntry.Staff;
            HistoryTechnical = string.IsNullOrEmpty(historyEntry?.Technical) ? "No technical information available for this title." : historyEntry.Technical;
        }

        // Hotkey-triggered (Ctrl+Shift+H): re-reads history_overrides.cfg from disk and re-resolves the
        // currently selected game's history panels against the fresh overrides, so a manually-edited
        // override line takes effect immediately instead of requiring a full app restart.
        public void ReloadHistoryOverrides()
        {
            _historyService.ReloadOverrides();
            RefreshHistoryPanelsForSelectedGame(SelectedGame);
        }

        private BitmapImage? _flyerImage;
        public BitmapImage? FlyerImage
        {
            get => _flyerImage;
            private set
            {
                _flyerImage = value;
                OnPropertyChanged();
            }
        }

        private BitmapImage? _cabinetImage;
        public BitmapImage? CabinetImage
        {
            get => _cabinetImage;
            private set
            {
                _cabinetImage = value;
                OnPropertyChanged();
            }
        }

        // Column One's lower content slot cycles Flyer > Title Screen > Snap. Title/Snap resolve with the
        // same primary+clone fallback as Flyer. MediaPanelControl listens for these property changes to
        // rebuild its dot row, since they only resolve after the preview debounce fires.
        private BitmapImage? _titleImage;
        public BitmapImage? TitleImage
        {
            get => _titleImage;
            private set
            {
                _titleImage = value;
                OnPropertyChanged();
            }
        }

        private BitmapImage? _snapImage;
        public BitmapImage? SnapImage
        {
            get => _snapImage;
            private set
            {
                _snapImage = value;
                OnPropertyChanged();
            }
        }

        private BitmapImage? LoadBitmapFromFile(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                byte[] fileBytes = File.ReadAllBytes(path);
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(fileBytes);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private void UpdateRow3FlyerAndCabinet()
        {
            if (SelectedGame == null)
            {
                FlyerImage = null;
                TitleImage = null;
                SnapImage = null;
                CabinetImage = null;
                return;
            }

            string flyerFolder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.FlyersPath) ? "flyers" : Configuration.FlyersPath);
            string? flyerPath = TryResolveMediaFile(flyerFolder, SelectedGame.RomName, SelectedGame.CloneOf, new[] { ".png", ".jpg" });
            FlyerImage = flyerPath != null ? LoadBitmapFromFile(flyerPath) : null;

            string titleFolder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.TitlescreensPath) ? "titles" : Configuration.TitlescreensPath);
            string? titlePath = TryResolveMediaFile(titleFolder, SelectedGame.RomName, SelectedGame.CloneOf, new[] { ".png", ".jpg" });
            TitleImage = titlePath != null ? LoadBitmapFromFile(titlePath) : null;

            string snapFolder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.ScreenshotsPath) ? "snap" : Configuration.ScreenshotsPath);
            string? snapPath = TryResolveMediaFile(snapFolder, SelectedGame.RomName, SelectedGame.CloneOf, new[] { ".png", ".jpg" });
            SnapImage = snapPath != null ? LoadBitmapFromFile(snapPath) : null;

            string cabinetFolder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.CabinetsPath) ? "cabinets" : Configuration.CabinetsPath);
            string? cabinetPath = TryResolveMediaFile(cabinetFolder, SelectedGame.RomName, SelectedGame.CloneOf, new[] { ".png", ".jpg" });
            CabinetImage = cabinetPath != null ? LoadBitmapFromFile(cabinetPath) : null;
        }

        private static string? TryResolveMediaFile(string folder, string primaryRomName, string? fallbackRomName, string[] extensions)
        {
            foreach (var ext in extensions)
            {
                string testPath = Path.Combine(folder, $"{primaryRomName}{ext}");
                if (File.Exists(testPath)) return testPath;
            }

            if (!string.IsNullOrEmpty(fallbackRomName))
            {
                foreach (var ext in extensions)
                {
                    string testPath = Path.Combine(folder, $"{fallbackRomName}{ext}");
                    if (File.Exists(testPath)) return testPath;
                }
            }

            return null;
        }

        private void UpdateActiveMediaPreviews()
        {
            if (SelectedGame == null)
            {
                MarqueeImage = null;
                PreviewImage = null;
                VideoSourcePath = string.Empty;
                HasActiveMedia = false;
                FlyerImage = null;
                TitleImage = null;
                SnapImage = null;
                CabinetImage = null;
                return;
            }

            ResolveMarqueeImage();
            UpdateRow3FlyerAndCabinet();

            string foundMediaFile = string.Empty;
            bool gameHasMedia = false;

            // Resolves a PreviewPriorityOrder category name to its folder + expected extensions - shared
            // by both passes below. Returns null for an unrecognized category (skip it).
            (string folder, string[] extensions)? ResolveCategoryTarget(string category)
            {
                switch (category)
                {
                    case "videos":
                        return (Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.VideosPath) ? "videos" : Configuration.VideosPath), new[] { ".mp4", ".avi" });
                    case "flyers":
                        return (Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.FlyersPath) ? "flyers" : Configuration.FlyersPath), new[] { ".png", ".jpg" });
                    case "screenshots":
                    case "snapshots":
                    case "gameplay":
                        return (Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.ScreenshotsPath) ? "snap" : Configuration.ScreenshotsPath), new[] { ".png", ".jpg" });
                    case "titlescreens":
                        return (Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.TitlescreensPath) ? "titles" : Configuration.TitlescreensPath), new[] { ".png", ".jpg" });
                    case "cabinets":
                        return (Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.CabinetsPath) ? "cabinets" : Configuration.CabinetsPath), new[] { ".png", ".jpg" });
                    case "marquees":
                        return (Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.MarqueesPath) ? "marquees" : Configuration.MarqueesPath), new[] { ".png", ".jpg" });
                    default:
                        return null;
                }
            }

            // Pass 1: walk every category using ONLY the game's own ROM name. A title with even one asset
            // of its own should never blend in a differently-named clone parent's media for a category
            // it's still missing - see project notes on the marquee/media clone-fallback fix.
            foreach (var category in PreviewPriorityOrder)
            {
                var target = ResolveCategoryTarget(category);
                if (target == null) continue;

                string? matchedPath = TryResolveMediaFile(target.Value.folder, SelectedGame.RomName, null, target.Value.extensions);
                if (matchedPath != null)
                {
                    foundMediaFile = matchedPath;
                    gameHasMedia = true;
                    break;
                }
            }

            // Pass 2: only if the game has ZERO assets of its own in any category, fall back to the clone
            // parent's assets (still walked in PreviewPriorityOrder).
            if (!gameHasMedia && !string.IsNullOrEmpty(SelectedGame.CloneOf))
            {
                foreach (var category in PreviewPriorityOrder)
                {
                    var target = ResolveCategoryTarget(category);
                    if (target == null) continue;

                    string? matchedPath = TryResolveMediaFile(target.Value.folder, SelectedGame.CloneOf, null, target.Value.extensions);
                    if (matchedPath != null)
                    {
                        foundMediaFile = matchedPath;
                        gameHasMedia = true;
                        break;
                    }
                }
            }

            HasActiveMedia = gameHasMedia;

            if (gameHasMedia && !string.IsNullOrEmpty(foundMediaFile))
            {
                if (foundMediaFile.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || foundMediaFile.EndsWith(".avi", StringComparison.OrdinalIgnoreCase))
                {
                    PreviewImage = null;
                    VideoSourcePath = foundMediaFile;
                }
                else
                {
                    VideoSourcePath = string.Empty;
                    try
                    {
                        var bitmap = new BitmapImage();
                        byte[] fileBytes = File.ReadAllBytes(foundMediaFile);
                        bitmap.BeginInit();
                        bitmap.StreamSource = new MemoryStream(fileBytes);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        PreviewImage = bitmap;
                    }
                    catch
                    {
                        PreviewImage = null;
                    }
                }
            }
            else
            {
                PreviewImage = null;
                VideoSourcePath = string.Empty;
            }
        }

        // Resolves a static fallback image for the options-open state, walking the same PreviewPriorityOrder
        // as UpdateActiveMediaPreviews but skipping "videos" - falls back to the missing-preview asset if nothing matches
        private ImageSource? ResolveOptionsFallbackImage()
        {
            if (SelectedGame == null) return null;

            foreach (var category in PreviewPriorityOrder)
            {
                if (category == "videos") continue;

                string folder = string.Empty;
                string[] extensions = { ".png", ".jpg" };

                switch (category)
                {
                    case "flyers":
                        folder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.FlyersPath) ? "flyers" : Configuration.FlyersPath);
                        break;
                    case "screenshots":
                    case "snapshots":
                    case "gameplay":
                        folder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.ScreenshotsPath) ? "snap" : Configuration.ScreenshotsPath);
                        break;
                    case "titlescreens":
                        folder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.TitlescreensPath) ? "titles" : Configuration.TitlescreensPath);
                        break;
                    case "cabinets":
                        folder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.CabinetsPath) ? "cabinets" : Configuration.CabinetsPath);
                        break;
                    case "marquees":
                        folder = Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(Configuration.MarqueesPath) ? "marquees" : Configuration.MarqueesPath);
                        break;
                    default:
                        continue;
                }

                string? matchedPath = TryResolveMediaFile(folder, SelectedGame.RomName, SelectedGame.CloneOf, extensions);
                if (matchedPath != null)
                {
                    return LoadThemeImage(matchedPath);
                }
            }

            return ThemeMissingPreviewAsset;
        }
        // [END SECTION: Media Preview Resolution]

        // [SECTION: Game Launch]
        // Dispatches to ProcessLaunchService with the right Window overload depending on the passed
        // command parameter, then fires GameLaunchCompleted so MainWindow can refresh the video preview.
        private async Task ExecuteLaunchAsync(object? parameter)
        {
            // Fire-and-forget: kicks off in the background alongside the launch below and is never
            // awaited, so a slow/failed ADB call can never delay showing MAME or restoring the window.
            if (Configuration.ScraperEnabled && Configuration.ScraperFetchOnLaunch && SelectedGame != null)
            {
                _ = _scraperService.FetchArtworkForGameAsync(SelectedGame);
            }

            if (parameter is MainWindow mainWin && SelectedGame != null)
            {
                await _launchService.LaunchGameAsync(SelectedGame, mainWin, mainWin.GamepadService);
            }
            else if (parameter is Window parentWindow && SelectedGame != null)
            {
                await _launchService.LaunchGameAsync(SelectedGame, parentWindow);
            }

            // Rebuilds the tree so Recently Played / Most Played reflect this session immediately, rather
            // than waiting for some unrelated action to trigger the next rebuild. Expand state survives
            // this automatically via CollectExpandedPaths/ApplyExpandedPaths, same as every other rebuild
            // trigger.
            UpdateLiveTreeDisplay();

            // SelectedGame is still the SAME object reference it was before launch (nothing nulls/reselects
            // it during a rebuild), so its setter's change-guard never re-fires on its own here - the
            // footer text needs this explicit refresh to pick up the play that was just recorded.
            SelectedGamePlayStatsText = ResolvePlayStatsText(SelectedGame);

            GameLaunchCompleted?.Invoke();
        }

        public event Action? GameLaunchCompleted;
        // Context-menu "Get Artwork" trigger - separate from the launch trigger's own gate
        // (ScraperFetchOnContextMenu, not ScraperFetchOnLaunch), with an in-flight guard so
        // double-clicking the menu item can't queue up duplicate requests for the same game.
        public void FetchArtworkForGame(GameItem game)
        {
            if (!Configuration.ScraperEnabled || !Configuration.ScraperFetchOnContextMenu) return;
            if (game == null || string.IsNullOrWhiteSpace(game.RomName)) return;

            lock (_scraperInFlightRoms)
            {
                if (!_scraperInFlightRoms.Add(game.RomName)) return;
            }

            _ = FetchArtworkForGameInternalAsync(game);
        }

        private async Task FetchArtworkForGameInternalAsync(GameItem game)
        {
            try
            {
                await _scraperService.FetchArtworkForGameAsync(game);
            }
            finally
            {
                lock (_scraperInFlightRoms)
                {
                    _scraperInFlightRoms.Remove(game.RomName);
                }
            }
        }
        // [END SECTION: Game Launch]

        // [SECTION: Mouse Support Persistence]
        // Toggles per-ROM mouse support on/off and persists the full MouseSupportRoms set to disk.
        public void ToggleMouseSupport(GameItem? game)
        {
            if (game == null) return;

            string configDir = Configuration.GetConfigPath();
            string mouseConfigPath = Path.Combine(configDir, Configuration.MouseSupportFile);

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            if (!MouseSupportRoms.Contains(game.RomName))
            {
                MouseSupportRoms.Add(game.RomName);
                game.IsMouseSupported = true;
            }
            else
            {
                MouseSupportRoms.Remove(game.RomName);
                game.IsMouseSupported = false;
            }

            try
            {
                var fileContents = new List<string>
                {
                    "# ARCADE LAUNCHER MOUSE SUPPORT CONFIG",
                    "# DO NOT MODIFY MANUALLY",
                    ""
                };

                fileContents.AddRange(MouseSupportRoms);
                File.WriteAllLines(mouseConfigPath, fileContents);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving mouse support config: {ex.Message}");
            }
        }

        // Loads the previously saved mouse-support ROM set from disk at startup
        private void LoadMouseSupportFromDisk()
        {
            try
            {
                string mousePath = Path.Combine(Configuration.GetConfigPath(), Configuration.MouseSupportFile);
                if (File.Exists(mousePath))
                {
                    var lines = File.ReadAllLines(mousePath)
                                    .Select(l => l.Trim())
                                    .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("#"));

                    foreach (var rom in lines)
                    {
                        MouseSupportRoms.Add(rom);
                    }
                }
            }
            catch { }
        }
        // [END SECTION: Mouse Support Persistence]

        // [SECTION: Favorites Persistence]
        // Toggles a game's favorite status, persists FavoriteRoms to disk, rebuilds the tree, then
        // re-expands and re-selects the game inside the Favorites node if it was just added - unless
        // jumpToFavorite is false, in which case the current selection/view stays put (used by the
        // "Add to Folder" context menu, same no-jump behavior as AddGameToCustomFolder).
        public void ToggleFavorite(GameItem? game, bool jumpToFavorite = true)
        {
            if (game == null) return;

            string configDir = Configuration.GetConfigPath();
            string favoritesFilePath = Path.Combine(configDir, Configuration.FavoritesListFile);

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            bool isAdding = !FavoriteRoms.Contains(game.RomName);

            if (isAdding) FavoriteRoms.Add(game.RomName);
            else FavoriteRoms.Remove(game.RomName);

            try
            {
                var fileContents = new List<string>
                {
                    "# ARCADE LAUNCHER FAVORITES CONFIG",
                    "# DO NOT MODIFY MANUALLY",
                    ""
                };

                fileContents.AddRange(FavoriteRoms);
                File.WriteAllLines(favoritesFilePath, fileContents);

                UpdateLiveTreeDisplay();

                UpdateLiveTreeDisplay();

                if (jumpToFavorite)
                {
                    var favoritesNode = TreeNodesCollection.FirstOrDefault(n => string.Equals(n.HeaderText, "FAVORITES", StringComparison.OrdinalIgnoreCase));
                    if (favoritesNode != null)
                    {
                        favoritesNode.IsNodeExpanded = true;

                        if (isAdding)
                        {
                            var targetGame = favoritesNode.ChildGames.FirstOrDefault(g => g.RomName.Equals(game.RomName, StringComparison.OrdinalIgnoreCase));
                            if (targetGame != null)
                            {
                                SelectedGame = targetGame;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving favorites config: {ex.Message}");
            }
        }

        // Loads the previously saved favorites ROM set from disk at startup
        private void LoadFavoritesFromDisk()
        {
            try
            {
                string favPath = Path.Combine(Configuration.GetConfigPath(), Configuration.FavoritesListFile);
                if (File.Exists(favPath))
                {
                    var lines = File.ReadAllLines(favPath)
                                    .Select(l => l.Trim())
                                    .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("#"));

                    foreach (var rom in lines)
                    {
                        FavoriteRoms.Add(rom);
                    }
                }
            }
            catch { }
        }
        // [END SECTION: Favorites Persistence]

        // [SECTION: Context Menu Folder Actions]
        // Add-only/remove-only wrappers used by the right-click "Add to Folder" / "Remove from Folder"
        // menu items. Unlike Ctrl+F's ToggleFavorite, these never flip state the opposite direction -
        // "Add to Folder" must be a silent no-op if the game is already in the target, never a removal.
        public void AddGameToFavorites(GameItem? game)
        {
            if (game == null) return;
            if (FavoriteRoms.Contains(game.RomName)) return; // already a favorite - silent no-op

            ToggleFavorite(game, jumpToFavorite: false); // not present, so this toggles it on, no jump
        }

        public void RemoveGameFromFavorites(GameItem? game)
        {
            if (game == null) return;
            if (!FavoriteRoms.Contains(game.RomName)) return; // not present - nothing to remove

            ToggleFavorite(game); // present, so this toggles it off
        }

        // Returns every existing custom folder's display name (playlist .cfg filename, minus extension),
        // used to populate the "Add to Folder" submenu. Sorted alphabetically for predictable menu order.
        public List<string> GetCustomFolderNames()
        {
            string playlistsDir = Path.Combine(Configuration.GetConfigPath(), "playlists");
            if (!Directory.Exists(playlistsDir)) return new List<string>();

            return Directory.GetFiles(playlistsDir, "*.cfg")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Finds the ancestor chain (root-to-leaf) of the main-tree TreeCategoryNode containing the given
        // game, for the randomizer's "jump to winner" feature. Deliberately skips FAVORITES/RECENTLY
        // PLAYED/MOST PLAYED and any custom folder root - those are separate sibling branches from the
        // real genre/virtual-category tree, not nested inside it, so they're excluded by name rather than
        // searched. Returns null if the game isn't found anywhere in the main tree (e.g. it's excluded by
        // an active sort filter), in which case the caller should simply skip the jump.
        private static readonly HashSet<string> MainTreeJumpExcludedRoots =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FAVORITES", "RECENTLY PLAYED", "MOST PLAYED" };

        public List<TreeCategoryNode>? FindMainTreePathToGame(GameItem game)
        {
            var customFolderNamesUpper = GetCustomFolderNames().Select(f => f.ToUpper()).ToList();

            foreach (var root in TreeNodesCollection)
            {
                if (MainTreeJumpExcludedRoots.Contains(root.HeaderText)) continue;
                if (customFolderNamesUpper.Contains(root.HeaderText.ToUpper())) continue;

                var path = SearchNodeForGame(root, game);
                if (path != null) return path;
            }

            return null;
        }

        private List<TreeCategoryNode>? SearchNodeForGame(TreeCategoryNode node, GameItem game)
        {
            if (node.ChildGames.Any(g => g.RomName.Equals(game.RomName, StringComparison.OrdinalIgnoreCase)))
            {
                return new List<TreeCategoryNode> { node };
            }

            foreach (var sub in node.SubFolders)
            {
                var subPath = SearchNodeForGame(sub, game);
                if (subPath != null)
                {
                    subPath.Insert(0, node);
                    return subPath;
                }
            }

            return null;
        }

        // Adds a game's ROM name to an existing custom folder's .cfg file (line 0 is the folder's hex
        // color, preserved untouched; remaining lines are ROM names). Silent no-op if already present -
        // mirrors ManagePlayListsWindow.ConfirmButton_Click's dedupe check.
        public void AddGameToCustomFolder(GameItem? game, string folderName)
        {
            if (game == null || string.IsNullOrWhiteSpace(folderName)) return;

            try
            {
                string playlistsDir = Path.Combine(Configuration.GetConfigPath(), "playlists");
                string filePath = Path.Combine(playlistsDir, $"{folderName}.cfg");

                var fileLines = File.Exists(filePath) ? File.ReadAllLines(filePath).ToList() : new List<string>();
                if (fileLines.Count == 0) fileLines.Add("#FFFFFF");

                if (!fileLines.Skip(1).Contains(game.RomName, StringComparer.OrdinalIgnoreCase))
                {
                    fileLines.Add(game.RomName);
                    File.WriteAllLines(filePath, fileLines);
                    UpdateLiveTreeDisplay();

                    // Deliberately no reselect/follow here - the current selection and media preview stay
                    // put after "Add to Folder", so a user can add several games to a folder in a row
                    // without the view jumping to the destination each time.
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error adding game to folder '{folderName}': {ex.Message}");
            }
        }

        // Removes a game's ROM name from a custom folder's .cfg file. Silent no-op if not present.
        public void RemoveGameFromCustomFolder(GameItem? game, string folderName)
        {
            if (game == null || string.IsNullOrWhiteSpace(folderName)) return;

            try
            {
                string playlistsDir = Path.Combine(Configuration.GetConfigPath(), "playlists");
                string filePath = Path.Combine(playlistsDir, $"{folderName}.cfg");
                if (!File.Exists(filePath)) return;

                var lines = File.ReadAllLines(filePath).ToList();
                if (lines.RemoveAll(l => l.Equals(game.RomName, StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    File.WriteAllLines(filePath, lines);
                    UpdateLiveTreeDisplay();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error removing game from folder '{folderName}': {ex.Message}");
            }
        }

        // Pins a newly-created custom folder to the very top of BOTH order files (folder_order.cfg AND
        // virtual_order.cfg), so its position is identical regardless of which structure mode (custom-
        // folder or virtual/catver) is active - matches how Favorites is already always pinned to the
        // top. Only meant to run once, at folder creation - after this, position is user-controlled via
        // the Folder Order tab or (future) context menu reorder items.
        public void PinFolderToTopOfOrder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName)) return;

            string configDir = Configuration.GetConfigPath();
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            string upperFolderName = folderName.Trim().ToUpper();

            void PrependToOrderFile(string orderFileName)
            {
                string orderFilePath = Path.Combine(configDir, orderFileName);
                var lines = File.Exists(orderFilePath)
                    ? File.ReadAllLines(orderFilePath).ToList()
                    : new List<string>();

                lines.RemoveAll(l => l.Trim().Equals(upperFolderName, StringComparison.OrdinalIgnoreCase));
                lines.Insert(0, upperFolderName);

                File.WriteAllLines(orderFilePath, lines);
            }

            try
            {
                PrependToOrderFile(Configuration.FolderOrderFile);
                PrependToOrderFile(Configuration.VirtualOrderFile);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error pinning folder '{folderName}' to top of order: {ex.Message}");
            }
        }
        // Deletes a custom folder's .cfg file entirely. The games it contained are untouched - they still
        // exist in GamesCollection and remain wherever else they're organized. Mirrors
        // ManagePlayListsWindow.DeleteFolderButton_Click's delete logic; confirmation happens in
        // MainWindow_xaml.cs before this is ever called.
        public void DeleteCustomFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName)) return;

            try
            {
                string playlistsDir = Path.Combine(Configuration.GetConfigPath(), "playlists");
                string filePath = Path.Combine(playlistsDir, $"{folderName}.cfg");

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    UpdateLiveTreeDisplay();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error deleting folder '{folderName}': {ex.Message}");
            }
        }

        // Creates a new, empty custom folder .cfg (default white color, no games yet), then pins it to
        // the top of both order files - same as the existing pin-on-create behavior from Ctrl+G. Duplicate
        // names are already checked in MainWindow_xaml.cs's Enter-key handler before this is ever called;
        // the check here is just a fail-safe against a race (e.g. two ways of creating a folder at once).
        public void CreateCustomFolder(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName)) return;

            try
            {
                string playlistsDir = Path.Combine(Configuration.GetConfigPath(), "playlists");
                if (!Directory.Exists(playlistsDir))
                {
                    Directory.CreateDirectory(playlistsDir);
                }

                string filePath = Path.Combine(playlistsDir, $"{folderName}.cfg");
                if (File.Exists(filePath)) return;

                File.WriteAllLines(filePath, new[] { "#FFFFFF" });

                PinFolderToTopOfOrder(folderName);
                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error creating folder '{folderName}': {ex.Message}");
            }
        }

        // Renames a custom folder's .cfg file, then swaps its entry in place within BOTH order files
        // (rather than removing/re-adding) so it keeps its current position - it only moves if the user
        // explicitly reorders it via the Folder Order tab or (future) context menu Move items.
        public void RenameCustomFolder(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;
            if (oldName.Equals(newName, StringComparison.OrdinalIgnoreCase)) return;

            try
            {
                string playlistsDir = Path.Combine(Configuration.GetConfigPath(), "playlists");
                string oldFilePath = Path.Combine(playlistsDir, $"{oldName}.cfg");
                string newFilePath = Path.Combine(playlistsDir, $"{newName}.cfg");

                if (!File.Exists(oldFilePath)) return;
                if (File.Exists(newFilePath)) return;

                File.Move(oldFilePath, newFilePath);

                string configDir = Configuration.GetConfigPath();
                string oldUpper = oldName.Trim().ToUpper();
                string newUpper = newName.Trim().ToUpper();

                void ReplaceInOrderFile(string orderFileName)
                {
                    string orderFilePath = Path.Combine(configDir, orderFileName);
                    if (!File.Exists(orderFilePath)) return;

                    var lines = File.ReadAllLines(orderFilePath).ToList();
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].Trim().Equals(oldUpper, StringComparison.OrdinalIgnoreCase))
                        {
                            lines[i] = newUpper;
                        }
                    }
                    File.WriteAllLines(orderFilePath, lines);
                }

                ReplaceInOrderFile(Configuration.FolderOrderFile);
                ReplaceInOrderFile(Configuration.VirtualOrderFile);

                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error renaming folder '{oldName}' to '{newName}': {ex.Message}");
            }
        }

        // Rewrites only line 0 (the hex color) of a custom folder's .cfg file, preserving every ROM name
        // below it untouched.
        public void SetCustomFolderColor(string folderName, string hexColor)
        {
            if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(hexColor)) return;

            try
            {
                string playlistsDir = Path.Combine(Configuration.GetConfigPath(), "playlists");
                string filePath = Path.Combine(playlistsDir, $"{folderName}.cfg");

                var lines = File.Exists(filePath) ? File.ReadAllLines(filePath).ToList() : new List<string>();
                if (lines.Count == 0)
                {
                    lines.Add(hexColor);
                }
                else
                {
                    lines[0] = hexColor;
                }

                File.WriteAllLines(filePath, lines);
                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error setting color for folder '{folderName}': {ex.Message}");
            }
        }
        // [SECTION: Folder Move/Reorder]
        // Resolves a TreeCategoryNode by full nested path (root.HeaderText, or root.HeaderText\sub.HeaderText
        // for the one supported subfolder level), walking down from TreeNodesCollection.
        private TreeCategoryNode? FindNodeByPath(IEnumerable<TreeCategoryNode> nodes, string targetPath)
        {
            foreach (var node in nodes)
            {
                if (node.HeaderText.Equals(targetPath, StringComparison.OrdinalIgnoreCase)) return node;

                string prefix = node.HeaderText + "\\";
                if (targetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return FindNodeByPath(node.SubFolders, targetPath.Substring(prefix.Length));
                }
            }
            return null;
        }

        // Returns the currently-visible sibling nodes at a given parent path (empty = root level),
        // in their current rendered order. Favorites is excluded at root level - it's never moveable.
        private List<TreeCategoryNode> GetLiveSiblingNodes(string parentPath)
        {
            if (string.IsNullOrEmpty(parentPath))
            {
                return TreeNodesCollection.Where(n => !n.HeaderText.Equals("FAVORITES", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var parentNode = FindNodeByPath(TreeNodesCollection, parentPath);
            return parentNode?.SubFolders.ToList() ?? new List<TreeCategoryNode>();
        }

        // Moves a folder within its own sibling group (root level, or one level of subfolders) and
        // persists the change to whichever order file matches the currently-active structure mode. Up/
        // Down are visible-relative (skip over any sibling currently hidden by a filter); Top/Bottom are
        // absolute (ignore visibility entirely). Only this specific sibling group's lines in the order
        // file are ever touched - every other group, including hidden entries elsewhere in the file,
        // is left completely untouched.
        public void MoveFolderInOrder(string folderFullPath, MoveDirection direction)
        {
            if (string.IsNullOrWhiteSpace(folderFullPath)) return;

            try
            {
                string configDir = Configuration.GetConfigPath();
                string activeOrderFileName = Configuration.IsCategorySortEnabled ? Configuration.VirtualOrderFile : Configuration.FolderOrderFile;
                string orderFilePath = Path.Combine(configDir, activeOrderFileName);

                int lastBackslash = folderFullPath.LastIndexOf('\\');
                string parentPath = lastBackslash >= 0 ? folderFullPath.Substring(0, lastBackslash) : string.Empty;
                string parentPathUpper = parentPath.ToUpper();
                string targetHeaderUpper = (lastBackslash >= 0 ? folderFullPath.Substring(lastBackslash + 1) : folderFullPath).Trim().ToUpper();

                var allLines = File.Exists(orderFilePath)
                    ? File.ReadAllLines(orderFilePath).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList()
                    : new List<string>();

                bool IsDirectChildOfParent(string line)
                {
                    int lineBackslash = line.LastIndexOf('\\');
                    string lineParent = lineBackslash >= 0 ? line.Substring(0, lineBackslash) : string.Empty;
                    return lineParent.Equals(parentPathUpper, StringComparison.OrdinalIgnoreCase);
                }

                string GetLastSegment(string line)
                {
                    int idx = line.LastIndexOf('\\');
                    return idx >= 0 ? line.Substring(idx + 1) : line;
                }

                var rawGroup = allLines.Where(IsDirectChildOfParent).ToList();

                var visibleSiblingNodes = GetLiveSiblingNodes(parentPath);
                var customFolderNames = GetCustomFolderNames();

                // Seed: every currently-visible sibling with no explicit line yet gets one, using the same
                // unlisted-fallback rule as Pass 3 will use - custom folders as a block at the top,
                // everything else as a block at the bottom - preserving each block's own live relative
                // order. This guarantees the folder being moved always has an explicit slot to operate on.
                var rawHeadersUpper = rawGroup.Select(GetLastSegment).Select(h => h.ToUpper()).ToHashSet();
                var unlistedCustom = new List<string>();
                var unlistedOther = new List<string>();

                foreach (var node in visibleSiblingNodes)
                {
                    if (rawHeadersUpper.Contains(node.HeaderText.ToUpper())) continue;

                    string fullLine = string.IsNullOrEmpty(parentPathUpper) ? node.HeaderText.ToUpper() : $"{parentPathUpper}\\{node.HeaderText.ToUpper()}";
                    bool isCustom = customFolderNames.Any(f => f.Equals(node.HeaderText, StringComparison.OrdinalIgnoreCase));

                    if (isCustom) unlistedCustom.Add(fullLine);
                    else unlistedOther.Add(fullLine);
                }

                rawGroup = unlistedCustom.Concat(rawGroup).Concat(unlistedOther).ToList();

                var visibleHeadersUpper = visibleSiblingNodes.Select(n => n.HeaderText.ToUpper()).ToHashSet();
                int targetIndex = rawGroup.FindIndex(l => GetLastSegment(l).Equals(targetHeaderUpper, StringComparison.OrdinalIgnoreCase));
                if (targetIndex < 0) return; // shouldn't happen - the moved folder must be visible to have been right-clicked

                if (direction == MoveDirection.Top || direction == MoveDirection.Bottom)
                {
                    string selfLine = rawGroup[targetIndex];
                    rawGroup.RemoveAt(targetIndex);
                    if (direction == MoveDirection.Top) rawGroup.Insert(0, selfLine);
                    else rawGroup.Add(selfLine);
                }
                else
                {
                    int step = direction == MoveDirection.Up ? -1 : 1;
                    int swapIndex = targetIndex + step;

                    // Skip over any hidden (currently-invisible) entries to find the next VISIBLE neighbor.
                    while (swapIndex >= 0 && swapIndex < rawGroup.Count && !visibleHeadersUpper.Contains(GetLastSegment(rawGroup[swapIndex]).ToUpper()))
                    {
                        swapIndex += step;
                    }

                    if (swapIndex < 0 || swapIndex >= rawGroup.Count) return; // already at the visible edge - no-op

                    (rawGroup[targetIndex], rawGroup[swapIndex]) = (rawGroup[swapIndex], rawGroup[targetIndex]);
                }

                var updatedLines = allLines.Where(l => !IsDirectChildOfParent(l)).Concat(rawGroup).ToList();
                File.WriteAllLines(orderFilePath, updatedLines);

                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error moving folder '{folderFullPath}': {ex.Message}");
            }
        }
        // Persists both the Folder Order tab's lists in one commit: the full root-level order (safe to
        // replace wholesale - the tab's left list is always the complete discovered set) and, if a root
        // with subfolders is currently selected, that one root's subfolder order. Every other root's
        // subfolder group in the file is left completely untouched - same merge-safe principle as
        // MoveFolderInOrder, just applied as a bulk rewrite instead of a single swap.
        public void SaveFolderOrderBulk(List<string> orderedRootHeaders, string? selectedRootHeader, List<string> orderedSubfolderHeaders)
        {
            try
            {
                string configDir = Configuration.GetConfigPath();
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                string activeOrderFileName = Configuration.IsCategorySortEnabled ? Configuration.VirtualOrderFile : Configuration.FolderOrderFile;
                string orderFilePath = Path.Combine(configDir, activeOrderFileName);

                var existingLines = File.Exists(orderFilePath)
                    ? File.ReadAllLines(orderFilePath).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList()
                    : new List<string>();

                string? selectedRootUpper = selectedRootHeader?.Trim().ToUpper();

                bool IsRootLevel(string line) => !line.Contains('\\');
                bool IsSelectedRootSubfolder(string line)
                {
                    if (selectedRootUpper == null) return false;
                    int idx = line.LastIndexOf('\\');
                    if (idx < 0) return false;
                    return line.Substring(0, idx).Equals(selectedRootUpper, StringComparison.OrdinalIgnoreCase);
                }

                var untouchedLines = existingLines.Where(l => !IsRootLevel(l) && !IsSelectedRootSubfolder(l)).ToList();

                var newRootLines = orderedRootHeaders.Select(h => h.Trim().ToUpper()).Where(h => !string.IsNullOrEmpty(h)).ToList();
                var newSubfolderLines = selectedRootUpper != null
                    ? orderedSubfolderHeaders.Select(h => $"{selectedRootUpper}\\{h.Trim().ToUpper()}").ToList()
                    : new List<string>();

                var finalLines = newRootLines.Concat(newSubfolderLines).Concat(untouchedLines).ToList();

                File.WriteAllLines(orderFilePath, finalLines);
                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving folder order: {ex.Message}");
            }
        }
        // [END SECTION: Folder Move/Reorder]

        // Adds a game's ROM name to the manual exclusion list (rom_exclusion_list.cfg), used by the
        // "Add to ROM exclusion list" context menu item. Delegates to
        // VirtualCategorySortService.AddRomExclusion so the write and the sort service's own cached
        // exclusion set stay in sync - see that method for why this can't just write the file directly.
        public void AddGameToExclusionList(GameItem? game)
        {
            if (game == null) return;

            try
            {
                _sortService.AddRomExclusion(game.RomName);
                UpdateLiveTreeDisplay();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error adding '{game.RomName}' to exclusion list: {ex.Message}");
            }
        }
        // [END SECTION: Context Menu Folder Actions]

        // [SECTION: Flat Row Navigation Support]
        // Maintains FlatVisibleRows, a flattened projection of the tree (respecting expand state) used
        // for linear navigation. Expanding one root category collapses all sibling roots and sub-folders
        // (single-branch-open behavior).
        public void ToggleNodeExpanded(TreeCategoryNode targetNode)
        {
            if (targetNode == null) return;

            bool newExpandedState = !targetNode.IsNodeExpanded;

            if (newExpandedState)
            {
                foreach (var cat in TreeNodesCollection)
                {
                    if (cat != targetNode)
                    {
                        cat.IsNodeExpanded = false;
                    }

                    foreach (var sub in cat.SubFolders)
                    {
                        if (sub != targetNode)
                        {
                            sub.IsNodeExpanded = false;
                        }
                    }
                }
            }

            targetNode.IsNodeExpanded = newExpandedState;
            RebuildFlatVisibleRows();
        }

        // Rebuilds FlatVisibleRows from scratch by walking TreeNodesCollection. Builds into a plain
        // List first (no per-item notification overhead at all) and commits via ReplaceAll in one shot,
        // instead of the old one-Add()-per-item approach that fired a separate CollectionChanged event
        // for every visible row - a real bottleneck once a single expanded folder holds tens of thousands
        // of games (confirmed via stress-testing the virtual category sort feature on a 30k+ ROM set).
        public void RebuildFlatVisibleRows()
        {
            var flatList = new List<object>();
            foreach (var node in TreeNodesCollection)
            {
                AddNodeToFlatList(node, flatList);
            }
            FlatVisibleRows.ReplaceAll(flatList);
        }

        // Recursively appends a node (and, if expanded, its sub-folders/games) into the working list
        private void AddNodeToFlatList(TreeCategoryNode node, List<object> flatList)
        {
            flatList.Add(node);
            if (node.IsNodeExpanded)
            {
                foreach (var sub in node.SubFolders)
                {
                    AddNodeToFlatList(sub, flatList);
                }
                foreach (var game in node.ChildGames)
                {
                    flatList.Add(game);
                }
            }
        }
        // [END SECTION: Flat Row Navigation Support]

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
// [END SECTION: File Overrides]