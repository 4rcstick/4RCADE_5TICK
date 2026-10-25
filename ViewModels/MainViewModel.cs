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

        // In-flight guard for the context-menu "Get Artwork" trigger - prevents rapid-fire clicking// from stacking parallel requests for the same romname. Not used by the launch trigger, since
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
        // no "#" (e.g. "1C1C1E") are prepended with "#" before conversion. Falls back to
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

                        Configuration.ShowGuideLines = loadedSettings.ShowGuideLines;
                        Configuration.ShowCenterGuide = loadedSettings.ShowCenterGuide;
                        Configuration.GuideLineColorHex = loadedSettings.GuideLineColorHex;
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
            LoadSavedThemeSnapshot();

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

        // [SECTION: Saved Theme Snapshot]
        // On a normal launch the theme isn't loaded through LoadTheme (settings come from settings.json),
        // so the restore point and the Theme sizes row would be empty until a theme was loaded by hand.
        // This reads the active theme's zip directly (no extraction) and fills the snapshot from it.
        private void LoadSavedThemeSnapshot()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Configuration.ActiveThemeName)) return;

                string themeFile = Configuration.GetThemePath(Configuration.ActiveThemeName);
                if (!File.Exists(themeFile)) return;

                using var zip = ZipFile.OpenRead(themeFile);
                var entry = zip.GetEntry("theme.json") ?? zip.GetEntry("theme.cfg");
                if (entry == null) return;

                using var reader = new StreamReader(entry.Open());
                var themeData = System.Text.Json.JsonSerializer.Deserialize<ThemeSettings>(reader.ReadToEnd());
                if (themeData == null) return;

                var snapshotLayouts = ConfigurationSettings.CloneLayouts(themeData.Layouts ?? new List<ThemeLayout>());

                // Custom layer images are archive-relative in the zip. Point them at the extracted copy that
                // LoadTheme made (same rewrite as LoadTheme), so Reset Layer / Reset Layout restore working paths.
                string snapshotExtractDir = Path.Combine(Configuration.GetArcadeStickFilesPath(), "Themes", "Extracted", Configuration.ActiveThemeName);
                foreach (var customEntry in snapshotLayouts.SelectMany(l => l.Layers).Where(e => !string.IsNullOrWhiteSpace(e.ImagePath)))
                {
                    if (string.IsNullOrWhiteSpace(customEntry.ImagePath) || Path.IsPathRooted(customEntry.ImagePath)) continue;

                    string absolutePath = Path.Combine(snapshotExtractDir, customEntry.ImagePath.Replace('/', Path.DirectorySeparatorChar));
                    customEntry.ImagePath = Path.GetRelativePath(Configuration.BaseDirectory, absolutePath);
                }

                Configuration.SavedThemeLayouts = snapshotLayouts;

                if (themeData.ActiveWidth > 0 && themeData.ActiveHeight > 0)
                {
                    Configuration.SavedThemeWidth = themeData.ActiveWidth;
                    Configuration.SavedThemeHeight = themeData.ActiveHeight;
                }
            }
            catch
            {
            }
        }
        // [END SECTION: Saved Theme Snapshot]

        // [SECTION: Preview Order Config]
        // Loads (or creates with defaults) preview_order.cfg, which controls the priority order media
        // types are checked in when resolving what to show in the main media panel (see ResolvePreviewMedia).
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
        // simultaneously - a collision with the old video player that hung the UI thread.
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
            RebuildRandomizerPoolCache();
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

        // SelectedGame drives the whole media preview pipeline in two parts. The instant part (ApplyInstantMedia)
        // runs right here: file existence decides HasActiveMedia and MarqueeMissing at once, cached art applies
        // synchronously, and anything not cached is decoded on a background task (see Instant Media Loading).
        // Only the video path waits for the 600 ms debounce, so rapid gamepad scrolling never starts a video on
        // every intermediate step. The previous video stops immediately (VideoSourcePath cleared).
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

                    // Every selection gets a new version; background loads for an older one are discarded
                    int mediaVersion = System.Threading.Interlocked.Increment(ref _mediaSelectionVersion);
                    VideoSourcePath = string.Empty;

                    // Instant part: marquee, banner flag, flyer/title/snap/cabinet, the video slot's static
                    // image and HasActiveMedia. Nothing selected (a folder was clicked) clears them all at once.
                    if (value == null) ClearInstantMedia();
                    else ApplyInstantMedia(value, mediaVersion);

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

                    // Video part: only the video path waits for the debounce
                    _previewDebounceTimer?.Stop();
                    if (value != null)
                    {
                        _previewDebounceTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(600)
                        };
                        _previewDebounceTimer.Tick += (s, e) =>
                        {
                            _previewDebounceTimer.Stop();
                            ApplyPreviewVideo();
                        };
                        _previewDebounceTimer.Start();
                    }
                }
            }
        }

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

        // Video player toggle removed - MediaElement is now the only video player.
        // [END SECTION: Video Player Test Toggle - TEMPORARY]

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
        // DANGER - SYNC POINT 1 of 3: these are pass-through bindings from layer entries to XAML. Global theme
        // values do not use forwarders: they are DynamicResources published
        // by GlobalThemeResources. A forwarder must also be raised in RefreshThemeBindings() below (sync point 2) -
        // one added here but missing there won't update live after Theme Builder saves.
        // The Main Window Background layer's color for the active resolution (its BgColorHex). Fills the main window
        // under the wallpaper (and alone when the wallpaper is disabled), and also its own popups.
        public string ThemeMainColor => GetLayerEntry(LayerRegistry.MainBackground).BgColorHex ?? LayerRegistry.DefaultMainBackgroundColorHex;
        // Returns one layer's entry for the active resolution, where its styling lives (per resolution).
        // Resolved with registry defaults filling any gaps, so it is never null for a registry layer.
        private LayerLayoutEntry GetLayerEntry(string id)
        {
            int width = Configuration.ActiveResolutionWidth;
            int height = Configuration.ActiveResolutionHeight;
            var saved = Configuration.Layouts.FirstOrDefault(l => l.Width == width && l.Height == height);
            var layout = LayerRegistry.ResolveLayout(saved, width, height);
            return layout.Layers.First(e => e.Id == id);
        }

        public int ThemeFolderFontSize => GetLayerEntry(LayerRegistry.GamesList).FolderFontSize ?? LayerRegistry.DefaultFolderFontSize;
        public string ThemeArrowColor => GetLayerEntry(LayerRegistry.GamesList).ArrowColorHex ?? LayerRegistry.DefaultArrowColorHex;
        public string ThemeFolderColor => GetLayerEntry(LayerRegistry.GamesList).FolderColorHex ?? LayerRegistry.DefaultFolderColorHex;

        // [SECTION: Search & Buttons Forwarders]
        // Search label, search box and the style shared by the four main window buttons, read from the
        // SearchLabel, SearchBox and RandomButton entries of the active resolution.
        public int ThemeSearchLabelFontSize => GetLayerEntry(LayerRegistry.SearchLabel).TextFontSize ?? LayerRegistry.DefaultSearchLabelFontSize;
        public string ThemeSearchLabelColor => GetLayerEntry(LayerRegistry.SearchLabel).TextColorHex ?? LayerRegistry.DefaultSearchLabelColorHex;
        public string ThemeSearchLabelBgColor => GetLayerEntry(LayerRegistry.SearchLabel).BgColorHex ?? LayerRegistry.DefaultSearchLabelBgColorHex;
        public FontFamily ThemeSearchLabelFontFamily
        {
            get
            {
                string? family = GetLayerEntry(LayerRegistry.SearchLabel).TextFontFamily;
                return new FontFamily(string.IsNullOrWhiteSpace(family) ? LayerRegistry.DefaultFontFamily : family);
            }
        }
        // Unchecked Bold keeps the label's original DemiBold look
        public FontWeight ThemeSearchLabelFontWeight => (GetLayerEntry(LayerRegistry.SearchLabel).TextBold ?? LayerRegistry.DefaultSearchLabelBold) ? FontWeights.Bold : FontWeights.DemiBold;
        public TextAlignment ThemeSearchLabelTextAlignment => GetLayerEntry(LayerRegistry.SearchLabel).TextAlign switch
        {
            "Left" => TextAlignment.Left,
            "Center" => TextAlignment.Center,
            _ => TextAlignment.Right
        };
        public System.Windows.Media.Effects.Effect? ThemeSearchLabelEffect
        {
            get
            {
                var entry = GetLayerEntry(LayerRegistry.SearchLabel);
                return BuildShadowEffect(
                    entry.TextShadowEnabled ?? LayerRegistry.DefaultShadowEnabled,
                    entry.TextShadowColorHex ?? LayerRegistry.DefaultShadowColorHex,
                    entry.TextShadowBlur ?? LayerRegistry.DefaultShadowBlur,
                    entry.TextShadowDepth ?? LayerRegistry.DefaultShadowDepth);
            }
        }
        // Vertical offset of the label text only (bound to the inner TextBlock's TranslateTransform)
        public double ThemeSearchLabelTextNudgeY => GetLayerEntry(LayerRegistry.SearchLabel).TextNudgeY ?? LayerRegistry.DefaultSearchLabelNudgeY;
        public int ThemeSearchBoxFontSize => GetLayerEntry(LayerRegistry.SearchBox).TextFontSize ?? LayerRegistry.DefaultSearchBoxFontSize;
        public string ThemeSearchBoxColor => GetLayerEntry(LayerRegistry.SearchBox).TextColorHex ?? LayerRegistry.DefaultSearchBoxColorHex;
        public string ThemeSearchBoxBgColor => GetLayerEntry(LayerRegistry.SearchBox).BgColorHex ?? LayerRegistry.DefaultSearchBoxBgColorHex;
        public int ThemeMainWinBtnFontSize => GetLayerEntry(LayerRegistry.RandomButton).TextFontSize ?? LayerRegistry.DefaultButtonFontSize;
        public string ThemeMainWinBtnColor => GetLayerEntry(LayerRegistry.RandomButton).TextColorHex ?? LayerRegistry.DefaultButtonColorHex;
        public string ThemeMainWinBtnBgColor => GetLayerEntry(LayerRegistry.RandomButton).BgColorHex ?? LayerRegistry.DefaultButtonBgColorHex;
        public string ThemeMainWinBtnColorHover => GetLayerEntry(LayerRegistry.RandomButton).HoverColorHex ?? LayerRegistry.DefaultButtonHoverColorHex;
        public string ThemeMainWinBtnBgColorHover => GetLayerEntry(LayerRegistry.RandomButton).HoverBgColorHex ?? LayerRegistry.DefaultButtonHoverBgColorHex;
        public double ThemeMainWinBtnBorderSize => GetLayerEntry(LayerRegistry.RandomButton).BorderSize ?? LayerRegistry.DefaultButtonBorderSize;
        public string ThemeMainWinBtnBorderColor => GetLayerEntry(LayerRegistry.RandomButton).BorderColorHex ?? LayerRegistry.DefaultButtonBorderColorHex;
        public string ThemeMainWinBtnBorderColorHover => GetLayerEntry(LayerRegistry.RandomButton).HoverBorderColorHex ?? LayerRegistry.DefaultButtonHoverBorderColorHex;
        public double ThemeMainWinBtnCornerRadius => GetLayerEntry(LayerRegistry.RandomButton).CornerRadius ?? LayerRegistry.DefaultButtonCornerRadius;

        // Re-raises the Search & Buttons forwarders after their entry values changed (a lighter path than
        // RefreshThemeBindings, used by MainWindow.ApplyLayout's style key).
        public void RefreshSearchButtonBindings()
        {
            OnPropertyChanged(nameof(ThemeSearchLabelFontSize));
            OnPropertyChanged(nameof(ThemeSearchLabelColor));
            OnPropertyChanged(nameof(ThemeSearchLabelBgColor));
            OnPropertyChanged(nameof(ThemeSearchLabelFontFamily));
            OnPropertyChanged(nameof(ThemeSearchLabelFontWeight));
            OnPropertyChanged(nameof(ThemeSearchLabelTextAlignment));
            OnPropertyChanged(nameof(ThemeSearchLabelEffect));
            OnPropertyChanged(nameof(ThemeSearchLabelTextNudgeY));
            OnPropertyChanged(nameof(ThemeSearchBoxFontSize));
            OnPropertyChanged(nameof(ThemeSearchBoxColor));
            OnPropertyChanged(nameof(ThemeSearchBoxBgColor));
            OnPropertyChanged(nameof(ThemeMainWinBtnFontSize));
            OnPropertyChanged(nameof(ThemeMainWinBtnColor));
            OnPropertyChanged(nameof(ThemeMainWinBtnBgColor));
            OnPropertyChanged(nameof(ThemeMainWinBtnColorHover));
            OnPropertyChanged(nameof(ThemeMainWinBtnBgColorHover));
            OnPropertyChanged(nameof(ThemeMainWinBtnBorderSize));
            OnPropertyChanged(nameof(ThemeMainWinBtnBorderColor));
            OnPropertyChanged(nameof(ThemeMainWinBtnBorderColorHover));
            OnPropertyChanged(nameof(ThemeMainWinBtnCornerRadius));
        }
        // [END SECTION: Search & Buttons Forwarders]

        // [SECTION: Game Info Forwarders]
        // Game Info text and nav styling, read from the active resolution's entries: title on GameTitle, publisher
        // block on PublisherBlock, section headers (SectionHeader*) and body text (Text*) on GameInfoBody, and the
        // two navs on GameInfoNav and ImagePreviewsNav. The Specs/Credits panes use the header and body styles.
        public int ThemeGameNameHeaderFontSize => GetLayerEntry(LayerRegistry.GameTitle).TextFontSize ?? LayerRegistry.DefaultTitleFontSize;
        public string ThemeGameNameHeaderColor => GetLayerEntry(LayerRegistry.GameTitle).TextColorHex ?? LayerRegistry.DefaultGameInfoTextColorHex;
        public FontFamily ThemeGameNameHeaderFontFamily => ToFontFamily(GetLayerEntry(LayerRegistry.GameTitle).TextFontFamily);
        public FontWeight ThemeGameNameHeaderFontWeight => (GetLayerEntry(LayerRegistry.GameTitle).TextBold ?? LayerRegistry.DefaultTitleBold) ? FontWeights.Bold : FontWeights.Normal;
        public System.Windows.Media.Effects.Effect? ThemeGameNameHeaderEffect => BuildTextShadowEffect(GetLayerEntry(LayerRegistry.GameTitle));

        public int ThemePubYearRatingFontSize => GetLayerEntry(LayerRegistry.PublisherBlock).TextFontSize ?? LayerRegistry.DefaultPublisherFontSize;
        public string ThemePubYearRatingColor => GetLayerEntry(LayerRegistry.PublisherBlock).TextColorHex ?? LayerRegistry.DefaultGameInfoTextColorHex;
        public FontFamily ThemePubYearRatingFontFamily => ToFontFamily(GetLayerEntry(LayerRegistry.PublisherBlock).TextFontFamily);
        public FontWeight ThemePubYearRatingFontWeight => (GetLayerEntry(LayerRegistry.PublisherBlock).TextBold ?? LayerRegistry.DefaultPublisherBold) ? FontWeights.Bold : FontWeights.Normal;
        public System.Windows.Media.Effects.Effect? ThemePubYearRatingEffect => BuildTextShadowEffect(GetLayerEntry(LayerRegistry.PublisherBlock));
        public TextAlignment ThemePubYearRatingTextAlignment => GetLayerEntry(LayerRegistry.PublisherBlock).TextAlign switch
        {
            "Left" => TextAlignment.Left,
            "Center" => TextAlignment.Center,
            _ => TextAlignment.Right
        };

        public int ThemeInfoHeaderFontSize => GetLayerEntry(LayerRegistry.GameInfoBody).SectionHeaderFontSize ?? LayerRegistry.DefaultInfoHeaderFontSize;
        public string ThemeInfoHeaderColor => GetLayerEntry(LayerRegistry.GameInfoBody).SectionHeaderColorHex ?? LayerRegistry.DefaultGameInfoTextColorHex;
        public FontFamily ThemeInfoHeaderFontFamily => ToFontFamily(GetLayerEntry(LayerRegistry.GameInfoBody).SectionHeaderFontFamily);
        public FontWeight ThemeInfoHeaderFontWeight => (GetLayerEntry(LayerRegistry.GameInfoBody).SectionHeaderBold ?? LayerRegistry.DefaultInfoHeaderBold) ? FontWeights.Bold : FontWeights.Normal;
        public System.Windows.Media.Effects.Effect? ThemeInfoHeaderEffect
        {
            get
            {
                var entry = GetLayerEntry(LayerRegistry.GameInfoBody);
                return BuildShadowEffect(
                    entry.SectionHeaderShadowEnabled ?? LayerRegistry.DefaultShadowEnabled,
                    entry.SectionHeaderShadowColorHex ?? LayerRegistry.DefaultShadowColorHex,
                    entry.SectionHeaderShadowBlur ?? LayerRegistry.DefaultShadowBlur,
                    entry.SectionHeaderShadowDepth ?? LayerRegistry.DefaultShadowDepth);
            }
        }

        public int ThemeInfoBodyFontSize => GetLayerEntry(LayerRegistry.GameInfoBody).TextFontSize ?? LayerRegistry.DefaultInfoBodyFontSize;
        public string ThemeInfoBodyColor => GetLayerEntry(LayerRegistry.GameInfoBody).TextColorHex ?? LayerRegistry.DefaultGameInfoTextColorHex;
        public FontFamily ThemeInfoBodyFontFamily
        {
            get
            {
                string? family = GetLayerEntry(LayerRegistry.GameInfoBody).TextFontFamily;
                return new FontFamily(string.IsNullOrWhiteSpace(family) ? LayerRegistry.DefaultInfoBodyFontFamily : family);
            }
        }
        public FontWeight ThemeInfoBodyFontWeight => (GetLayerEntry(LayerRegistry.GameInfoBody).TextBold ?? LayerRegistry.DefaultInfoBodyBold) ? FontWeights.Bold : FontWeights.Normal;
        public System.Windows.Media.Effects.Effect? ThemeInfoBodyEffect => BuildTextShadowEffect(GetLayerEntry(LayerRegistry.GameInfoBody));

        // The body text's right margin (gap to the scrollbar or the scroll area's edge), 0 to 100. Two Thicknesses because
        // the containers keep their own bottom spacing: 5 under each Game Info block, 20 under the other pages.
        private double InfoBodyRightMargin => Math.Clamp(GetLayerEntry(LayerRegistry.GameInfoBody).TextRightMargin ?? LayerRegistry.DefaultInfoBodyRightMargin,
            LayerRegistry.MinInfoBodyRightMargin, LayerRegistry.MaxInfoBodyRightMargin);
        public Thickness ThemeInfoBodyBlockMargin => new Thickness(0, 0, InfoBodyRightMargin, 5);
        public Thickness ThemeInfoBodyPageMargin => new Thickness(0, 0, InfoBodyRightMargin, 20);

        public string ThemeGameInfoNavColor => GetLayerEntry(LayerRegistry.GameInfoNav).TextColorHex ?? LayerRegistry.DefaultGameInfoTextColorHex;
        public double ThemeGameInfoNavSize => GetLayerEntry(LayerRegistry.GameInfoNav).TextFontSize ?? LayerRegistry.DefaultNavSize;
        public System.Windows.Media.Effects.Effect? ThemeGameInfoNavEffect => BuildTextShadowEffect(GetLayerEntry(LayerRegistry.GameInfoNav));
        public string ThemePreviewsNavColor => GetLayerEntry(LayerRegistry.ImagePreviewsNav).TextColorHex ?? LayerRegistry.DefaultGameInfoTextColorHex;
        public double ThemePreviewsNavSize => GetLayerEntry(LayerRegistry.ImagePreviewsNav).TextFontSize ?? LayerRegistry.DefaultNavSize;
        public System.Windows.Media.Effects.Effect? ThemePreviewsNavEffect => BuildTextShadowEffect(GetLayerEntry(LayerRegistry.ImagePreviewsNav));

        // Image Previews shadow/glow (flyer, title screen and snap; the cabinet image is not part of it)
        public System.Windows.Media.Effects.Effect? ThemePreviewsEffect => BuildTextShadowEffect(GetLayerEntry(LayerRegistry.ImagePreviewsBody));

        // Turns a saved font family name into a FontFamily (Segoe UI when empty)
        private static FontFamily ToFontFamily(string? family) =>
            new FontFamily(string.IsNullOrWhiteSpace(family) ? LayerRegistry.DefaultFontFamily : family);

        // Builds the drop shadow from an entry's generic TextShadow* fields (null when off)
        private static System.Windows.Media.Effects.Effect? BuildTextShadowEffect(LayerLayoutEntry entry) =>
            BuildShadowEffect(
                entry.TextShadowEnabled ?? LayerRegistry.DefaultShadowEnabled,
                entry.TextShadowColorHex ?? LayerRegistry.DefaultShadowColorHex,
                entry.TextShadowBlur ?? LayerRegistry.DefaultShadowBlur,
                entry.TextShadowDepth ?? LayerRegistry.DefaultShadowDepth);

        // Re-raises the Game Info forwarders after their entry values changed (used by RefreshThemeBindings and
        // MainWindow.ApplyLayout's Game Info style key).
        public void RefreshGameInfoBindings()
        {
            OnPropertyChanged(nameof(ThemeGameNameHeaderFontSize));
            OnPropertyChanged(nameof(ThemeGameNameHeaderColor));
            OnPropertyChanged(nameof(ThemeGameNameHeaderFontFamily));
            OnPropertyChanged(nameof(ThemeGameNameHeaderFontWeight));
            OnPropertyChanged(nameof(ThemeGameNameHeaderEffect));
            OnPropertyChanged(nameof(ThemePubYearRatingFontSize));
            OnPropertyChanged(nameof(ThemePubYearRatingColor));
            OnPropertyChanged(nameof(ThemePubYearRatingFontFamily));
            OnPropertyChanged(nameof(ThemePubYearRatingFontWeight));
            OnPropertyChanged(nameof(ThemePubYearRatingEffect));
            OnPropertyChanged(nameof(ThemePubYearRatingTextAlignment));
            OnPropertyChanged(nameof(ThemeInfoHeaderFontSize));
            OnPropertyChanged(nameof(ThemeInfoHeaderColor));
            OnPropertyChanged(nameof(ThemeInfoHeaderFontFamily));
            OnPropertyChanged(nameof(ThemeInfoHeaderFontWeight));
            OnPropertyChanged(nameof(ThemeInfoHeaderEffect));
            OnPropertyChanged(nameof(ThemeInfoBodyFontSize));
            OnPropertyChanged(nameof(ThemeInfoBodyColor));
            OnPropertyChanged(nameof(ThemeInfoBodyFontFamily));
            OnPropertyChanged(nameof(ThemeInfoBodyFontWeight));
            OnPropertyChanged(nameof(ThemeInfoBodyEffect));
            OnPropertyChanged(nameof(ThemeInfoBodyBlockMargin));
            OnPropertyChanged(nameof(ThemeInfoBodyPageMargin));
            OnPropertyChanged(nameof(ThemeGameInfoNavColor));
            OnPropertyChanged(nameof(ThemeGameInfoNavSize));
            OnPropertyChanged(nameof(ThemeGameInfoNavEffect));
            OnPropertyChanged(nameof(ThemePreviewsNavColor));
            OnPropertyChanged(nameof(ThemePreviewsNavSize));
            OnPropertyChanged(nameof(ThemePreviewsNavEffect));
            OnPropertyChanged(nameof(ThemePreviewsEffect));
        }
        // [END SECTION: Game Info Forwarders]

        // [SECTION: Random Game Display Forwarders]
        // The spin card's style. Text (message, font, color, alignment, wrap, shadow) is shared by both states and read
        // from the RandomGameDisplay entry; background color and border are per state, read from the active state's
        // part. Each state's max text size, the wrapped text width and the file clip depend on the active rect, so
        // MainWindow.ApplyRandomGameDisplayRect sets those (and sets RandomDisplaySplashState at the same pick).
        private LayerLayoutEntry RandomDisplayEntry => GetLayerEntry(LayerRegistry.RandomGameDisplay);
        private LayerLayoutEntry RandomDisplayStateEntry => GetLayerEntry(_randomDisplaySplashState ? LayerRegistry.RandomGameDisplaySplash : LayerRegistry.RandomGameDisplay);

        // True while the card shows the splash-page state. Set by MainWindow from the die-click pick; a change
        // re-raises the per-state forwarders.
        private bool _randomDisplaySplashState;
        public bool RandomDisplaySplashState
        {
            get => _randomDisplaySplashState;
            set
            {
                if (_randomDisplaySplashState == value) return;
                _randomDisplaySplashState = value;
                OnPropertyChanged(nameof(ThemeRandomDisplayBgColor));
                OnPropertyChanged(nameof(ThemeRandomDisplayBorderColor));
                OnPropertyChanged(nameof(ThemeRandomDisplayBorderThickness));
                OnPropertyChanged(nameof(ThemeRandomDisplayCornerRadius));
            }
        }

        public string ThemeRandomDisplayMessage => RandomDisplayEntry.TextMessage ?? LayerRegistry.DefaultRandomDisplayMessage;
        public FontFamily ThemeRandomDisplayFontFamily => ToFontFamily(RandomDisplayEntry.TextFontFamily);
        public FontWeight ThemeRandomDisplayFontWeight => (RandomDisplayEntry.TextBold ?? LayerRegistry.DefaultRandomDisplayBold) ? FontWeights.Bold : FontWeights.Normal;
        public string ThemeRandomDisplayColor => RandomDisplayEntry.TextColorHex ?? LayerRegistry.DefaultRandomDisplayColorHex;
        public System.Windows.Media.Effects.Effect? ThemeRandomDisplayEffect => BuildTextShadowEffect(RandomDisplayEntry);
        public TextWrapping ThemeRandomDisplayTextWrapping => (RandomDisplayEntry.TextWrap ?? LayerRegistry.DefaultRandomDisplayWrap) ? TextWrapping.Wrap : TextWrapping.NoWrap;

        // Alignment: TextAlignment lines up wrapped lines; the HorizontalAlignment places a one-line message that is
        // narrower than the box (the shrink-only Viewbox is then only as wide as the text).
        public TextAlignment ThemeRandomDisplayTextAlignment => RandomDisplayEntry.TextAlign switch
        {
            "Left" => TextAlignment.Left,
            "Right" => TextAlignment.Right,
            _ => TextAlignment.Center
        };
        public HorizontalAlignment ThemeRandomDisplayHorizontalAlignment => RandomDisplayEntry.TextAlign switch
        {
            "Left" => HorizontalAlignment.Left,
            "Right" => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center
        };

        public string ThemeRandomDisplayBgColor => RandomDisplayStateEntry.BgColorHex ?? LayerRegistry.DefaultRandomDisplayBgColorHex;
        public string ThemeRandomDisplayBorderColor => RandomDisplayStateEntry.BorderColorHex ?? LayerRegistry.DefaultShapeBorderColorHex;
        public Thickness ThemeRandomDisplayBorderThickness => new Thickness(RandomDisplayStateEntry.BorderSize ?? 0);
        public CornerRadius ThemeRandomDisplayCornerRadius => new CornerRadius(RandomDisplayStateEntry.CornerRadius ?? LayerRegistry.DefaultShapeCornerRadius);

        // Re-raises the Random Game Display forwarders after their entry values changed (used by RefreshThemeBindings
        // and MainWindow.ApplyLayout's Random Game Display style key).
        public void RefreshRandomDisplayBindings()
        {
            OnPropertyChanged(nameof(ThemeRandomDisplayMessage));
            OnPropertyChanged(nameof(ThemeRandomDisplayFontFamily));
            OnPropertyChanged(nameof(ThemeRandomDisplayFontWeight));
            OnPropertyChanged(nameof(ThemeRandomDisplayColor));
            OnPropertyChanged(nameof(ThemeRandomDisplayEffect));
            OnPropertyChanged(nameof(ThemeRandomDisplayTextWrapping));
            OnPropertyChanged(nameof(ThemeRandomDisplayTextAlignment));
            OnPropertyChanged(nameof(ThemeRandomDisplayHorizontalAlignment));
            OnPropertyChanged(nameof(ThemeRandomDisplayBgColor));
            OnPropertyChanged(nameof(ThemeRandomDisplayBorderColor));
            OnPropertyChanged(nameof(ThemeRandomDisplayBorderThickness));
            OnPropertyChanged(nameof(ThemeRandomDisplayCornerRadius));
        }
        // [END SECTION: Random Game Display Forwarders]
        // [END SECTION: Theme Property Forwarders]

        // [SECTION: Theme Wallpaper & Asset Loading]
        // Resolves theme-configured image paths to ImageSource, falling back to bundled defaults in
        // 4rcade5tick_files/assets when a theme doesn't specify its own asset.
        // The main window wallpaper lives on the Main Window Background layer entry (per resolution): the file in
        // ImagePath (empty = the built-in default image) and the Disable flag in ImageDisabled.
        private string MainWallpaperPath => GetLayerEntry(LayerRegistry.MainBackground).ImagePath ?? "";
        private bool MainWallpaperDisabled => GetLayerEntry(LayerRegistry.MainBackground).ImageDisabled ?? LayerRegistry.DefaultImageDisabled;

        public ImageSource? ThemeMainWallpaper => (MainWallpaperDisabled || IsMainWallpaperVideo)
    ? null
    : LoadThemeImage(
        !string.IsNullOrWhiteSpace(MainWallpaperPath)
            ? MainWallpaperPath
            : Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets", "default_background.png"));

        // True when the main window wallpaper is an mp4 - played by WPF's MediaElement, since
        // MediaElement renders as a normal WPF element that respects z-order and can sit behind every panel.
        // Respects the layer's Disable flag the same way the image wallpaper does.
        public bool IsMainWallpaperVideo => !MainWallpaperDisabled
            && !string.IsNullOrWhiteSpace(MainWallpaperPath)
            && Path.GetExtension(MainWallpaperPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase);

        // Full resolved path for the MediaElement wallpaper; null unless the wallpaper is an mp4 that exists on disk
        public string? ThemeMainWallpaperVideoPath => IsMainWallpaperVideo ? ResolveThemeFilePath(MainWallpaperPath) : null;

        // Re-raises the background color binding after the Main Window Background entry's color changed (MainWindow.ApplyLayout)
        public void RefreshMainBackgroundColor()
        {
            OnPropertyChanged(nameof(ThemeMainColor));
        }

        // Re-raises the three wallpaper bindings after the Main Window Background entry changed (MainWindow.ApplyLayout's key)
        public void RefreshMainWallpaper()
        {
            OnPropertyChanged(nameof(ThemeMainWallpaper));
            OnPropertyChanged(nameof(IsMainWallpaperVideo));
            OnPropertyChanged(nameof(ThemeMainWallpaperVideoPath));
        }
        

        // The logo path currently set on the Marquee layer (empty = use the built-in logo). MainWindow compares
        // this between ApplyLayout runs to know when the logo needs refreshing.
        public string MarqueeLogoPath => GetLayerEntry(LayerRegistry.MarqueePanel).ImagePath ?? "";

        public ImageSource? ThemeLogoAsset => LoadThemeImage(
            !string.IsNullOrWhiteSpace(MarqueeLogoPath)
                ? MarqueeLogoPath
                : Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets", "default_marquee.png"));

        // Re-raises the logo binding after the Marquee layer's logo path changed.
        public void RefreshMarqueeLogo()
        {
            OnPropertyChanged(nameof(ThemeLogoAsset));
        }

        // The Marquee layer's one shadow/glow (its TextShadow* fields), split by ShadowTarget ("Both", "Logo",
        // "Marquees"; unknown or blank = "Both"). The no-art banner and the spin's no-marquee card take the "Marquees" side.
        private string MarqueeShadowTarget
        {
            get
            {
                string? target = GetLayerEntry(LayerRegistry.MarqueePanel).ShadowTarget;
                return LayerRegistry.IsShadowTarget(target) ? target! : LayerRegistry.DefaultShadowTarget;
            }
        }

        // The game's marquee image, the spin frames and the no-art title card (null when the target is Logo only)
        public System.Windows.Media.Effects.Effect? ThemeMarqueeEffect =>
            MarqueeShadowTarget != LayerRegistry.ShadowTargetLogo ? BuildTextShadowEffect(GetLayerEntry(LayerRegistry.MarqueePanel)) : null;

        // The idle logo (null when the target is Marquees only)
        public System.Windows.Media.Effects.Effect? ThemeMarqueeLogoEffect =>
            MarqueeShadowTarget != LayerRegistry.ShadowTargetMarquees ? BuildTextShadowEffect(GetLayerEntry(LayerRegistry.MarqueePanel)) : null;

        // Does the marquee shadow re-raise after the Marquee layer's shadow values or target changed
        // (RefreshThemeBindings and MainWindow.ApplyLayout's marquee shadow key)
        public void RefreshMarqueeBindings()
        {
            OnPropertyChanged(nameof(ThemeMarqueeEffect));
            OnPropertyChanged(nameof(ThemeMarqueeLogoEffect));
        }

        // [SECTION: Marquee Banner Forwarders]
        // The no-art title card's style (the idle banner and the spin's no-marquee card share it), read from the
        // MarqueePanel entry. The card also takes ThemeMarqueeEffect (the shared image shadow, "Marquees" side).
        private LayerLayoutEntry MarqueeBannerEntry => GetLayerEntry(LayerRegistry.MarqueePanel);

        // Width of the card's reference box in MainWindow.xaml (400x100); a wrapped title wraps at this width
        private const double MarqueeBannerBoxWidth = 400;

        public FontFamily ThemeMarqueeBannerFontFamily => ToFontFamily(MarqueeBannerEntry.TextFontFamily);
        public FontWeight ThemeMarqueeBannerFontWeight => (MarqueeBannerEntry.TextBold ?? LayerRegistry.DefaultBannerBold) ? FontWeights.Bold : FontWeights.Normal;
        public double ThemeMarqueeBannerFontSize => MarqueeBannerEntry.TextFontSize ?? LayerRegistry.DefaultBannerFontSize;
        public string ThemeMarqueeBannerColor => MarqueeBannerEntry.TextColorHex ?? LayerRegistry.DefaultBannerColorHex;
        public TextWrapping ThemeMarqueeBannerTextWrapping => (MarqueeBannerEntry.TextWrap ?? LayerRegistry.DefaultBannerWrap) ? TextWrapping.Wrap : TextWrapping.NoWrap;
        public double ThemeMarqueeBannerTextWidth => (MarqueeBannerEntry.TextWrap ?? LayerRegistry.DefaultBannerWrap) ? MarqueeBannerBoxWidth : double.NaN;

        // The one-line title's left and right inset in reference box units (0 to 100), on the shrink-only Viewbox so a long
        // title shrinks before it reaches it; none when wrapped, which keeps the wrapped look unchanged
        public Thickness ThemeMarqueeBannerTextMargin
        {
            get
            {
                if (MarqueeBannerEntry.TextWrap ?? LayerRegistry.DefaultBannerWrap) return new Thickness(0);
                double margin = Math.Clamp(MarqueeBannerEntry.BannerTextMargin ?? LayerRegistry.DefaultBannerTextMargin,
                    LayerRegistry.MinBannerTextMargin, LayerRegistry.MaxBannerTextMargin);
                return new Thickness(margin, 0, margin, 0);
            }
        }
        public string ThemeMarqueeBannerBgColor => MarqueeBannerEntry.BgColorHex ?? LayerRegistry.DefaultBannerBgColorHex;
        public string ThemeMarqueeBannerBorderColor => MarqueeBannerEntry.BorderColorHex ?? LayerRegistry.DefaultBannerBorderColorHex;
        public Thickness ThemeMarqueeBannerBorderThickness => new Thickness(MarqueeBannerEntry.BorderSize ?? LayerRegistry.DefaultBannerBorderSize);
        public CornerRadius ThemeMarqueeBannerCornerRadius => new CornerRadius(MarqueeBannerEntry.CornerRadius ?? LayerRegistry.DefaultBannerCornerRadius);

        // The card's outer margin, minus the border size: the 4:1 content fits the Marquee rect and the border draws outside it
        public Thickness ThemeMarqueeBannerOutsideMargin => new Thickness(-Math.Max(0, MarqueeBannerEntry.BorderSize ?? LayerRegistry.DefaultBannerBorderSize));
        public System.Windows.Media.Effects.Effect? ThemeMarqueeBannerTextEffect =>
            BuildShadowEffect(
                MarqueeBannerEntry.BannerShadowEnabled ?? LayerRegistry.DefaultShadowEnabled,
                MarqueeBannerEntry.BannerShadowColorHex ?? LayerRegistry.DefaultShadowColorHex,
                MarqueeBannerEntry.BannerShadowBlur ?? LayerRegistry.DefaultShadowBlur,
                MarqueeBannerEntry.BannerShadowDepth ?? LayerRegistry.DefaultShadowDepth);

        // Re-raises the banner forwarders after their entry values changed (RefreshThemeBindings and MainWindow.ApplyLayout's
        // banner style key)
        public void RefreshMarqueeBannerBindings()
        {
            OnPropertyChanged(nameof(ThemeMarqueeBannerFontFamily));
            OnPropertyChanged(nameof(ThemeMarqueeBannerFontWeight));
            OnPropertyChanged(nameof(ThemeMarqueeBannerFontSize));
            OnPropertyChanged(nameof(ThemeMarqueeBannerColor));
            OnPropertyChanged(nameof(ThemeMarqueeBannerTextWrapping));
            OnPropertyChanged(nameof(ThemeMarqueeBannerTextWidth));
            OnPropertyChanged(nameof(ThemeMarqueeBannerTextMargin));
            OnPropertyChanged(nameof(ThemeMarqueeBannerBgColor));
            OnPropertyChanged(nameof(ThemeMarqueeBannerBorderColor));
            OnPropertyChanged(nameof(ThemeMarqueeBannerBorderThickness));
            OnPropertyChanged(nameof(ThemeMarqueeBannerCornerRadius));
            OnPropertyChanged(nameof(ThemeMarqueeBannerOutsideMargin));
            OnPropertyChanged(nameof(ThemeMarqueeBannerTextEffect));
        }
        // [END SECTION: Marquee Banner Forwarders]

        // The splash file set on the Splash Image layer for the active resolution (empty = the built-in splash).
        // MainWindow compares this between ApplyLayout runs to know when the splash needs refreshing.
        public string SplashImagePath => GetLayerEntry(LayerRegistry.SplashImage).ImagePath ?? "";

        // Re-raises every boot splash binding after the Splash Image layer's file changed
        public void RefreshBootSplash()
        {
            OnPropertyChanged(nameof(ThemeBootSplashAsset));
            OnPropertyChanged(nameof(IsBootSplashVideo));
            OnPropertyChanged(nameof(ThemeBootSplashVideoPath));
            OnPropertyChanged(nameof(IsBootSplashAnimated));
            OnPropertyChanged(nameof(ThemeBootSplashAnimatedAsset));
        }

        // Resolves the boot splash to the Splash Image layer's file if set, else the bundled default - preferring
        // an mp4 companion file over the png when falling back to the default asset
        private string ResolveBootSplashPath()
        {
            if (!string.IsNullOrWhiteSpace(SplashImagePath))
            {
                string splashPath = SplashImagePath;
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

        // File path for the boot splash MediaElement when the splash resolves to an mp4; null when it's a png
        public string? ThemeBootSplashVideoPath => IsBootSplashVideo ? ResolveBootSplashPath() : null;

        // True when the resolved boot splash asset is animated - any .gif, or a .png that is actually an APNG.
        // Routed to its own AnimatedImage.Wpf-driven Image layer instead of the ImageBrush background, since
        // WPF's built-in decoding only shows the first frame. Static pngs stay on the ImageBrush (keeps corner clipping).
        public bool IsBootSplashAnimated
        {
            get
            {
                string? fullPath = ResolveThemeFilePath(ResolveBootSplashPath());
                if (fullPath == null) return false;

                string ext = Path.GetExtension(fullPath);
                if (ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)) return true;
                if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)) return IsAnimatedPng(fullPath);
                return false;
            }
        }

        // Bitmap for the boot splash ImageBrush when it resolves to a static image (png/jpg); null for mp4 and animated
        public ImageSource? ThemeBootSplashAsset => (IsBootSplashVideo || IsBootSplashAnimated) ? null : LoadThemeImage(ResolveBootSplashPath());

        // Source for the animated splash layer; null unless the splash is animated. Loaded by file Uri (not a
        // memory stream like LoadThemeImage) since that's the form AnimatedImage.Wpf documents for its decoder.
        public ImageSource? ThemeBootSplashAnimatedAsset
        {
            get
            {
                if (!IsBootSplashAnimated) return null;
                string? fullPath = ResolveThemeFilePath(ResolveBootSplashPath());
                if (fullPath == null) return null;

                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return bitmap;
                }
                catch { return null; }
            }
        }

        // Same relative-to-BaseDirectory resolution LoadThemeImage uses; returns null if the file doesn't exist.
        private string? ResolveThemeFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                string cleanPath = path;
                if (!Path.IsPathRooted(cleanPath))
                {
                    if (cleanPath.StartsWith(@".\")) cleanPath = cleanPath.Substring(2);
                    cleanPath = Path.GetFullPath(Path.Combine(Configuration.BaseDirectory, cleanPath));
                }
                return File.Exists(cleanPath) ? cleanPath : null;
            }
            catch { return null; }
        }

        // APNG detection: an animated png carries an "acTL" chunk BEFORE its first "IDAT" chunk. Walks the chunk
        // headers only (skipping chunk data), so it reads a few hundred bytes at most, not the whole file.
        private static bool IsAnimatedPng(string fullPath)
        {
            try
            {
                using var stream = File.OpenRead(fullPath);
                using var reader = new BinaryReader(stream);

                // 8-byte PNG signature
                byte[] signature = reader.ReadBytes(8);
                if (signature.Length < 8 || signature[0] != 0x89 || signature[1] != 0x50) return false;

                while (stream.Position + 8 <= stream.Length)
                {
                    byte[] lengthBytes = reader.ReadBytes(4);
                    int chunkLength = (lengthBytes[0] << 24) | (lengthBytes[1] << 16) | (lengthBytes[2] << 8) | lengthBytes[3];
                    string chunkType = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));

                    if (chunkType == "acTL") return true;
                    if (chunkType == "IDAT" || chunkType == "IEND") return false;
                    if (chunkLength < 0) return false;

                    stream.Seek(chunkLength + 4, SeekOrigin.Current); // skip chunk data + CRC
                }
            }
            catch { }
            return false;
        }

        // The "no preview available" image set on the Video Preview layer's Fallback Image tab (empty = built-in).
        // MainWindow compares this between ApplyLayout runs to know when the fallback needs refreshing.
        public string VideoFallbackPath => GetLayerEntry(LayerRegistry.VideoPreview).ImagePath ?? "";

        public ImageSource? ThemeMissingPreviewAsset => LoadThemeImage(
            !string.IsNullOrWhiteSpace(VideoFallbackPath)
                ? VideoFallbackPath
                : Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets", "no_preview.png"));

        // Re-raises the fallback image binding after the Video Preview layer's fallback file changed
        public void RefreshMissingPreview()
        {
            OnPropertyChanged(nameof(ThemeMissingPreviewAsset));
        }

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
            RefreshGameInfoBindings();
            RefreshRandomDisplayBindings();
            RefreshMarqueeBindings();
            RefreshMarqueeBannerBindings();
            RefreshSearchButtonBindings();
            OnPropertyChanged(nameof(ThemeFolderFontSize));
            OnPropertyChanged(nameof(ThemeArrowColor));
            OnPropertyChanged(nameof(ThemeFolderColor));
            OnPropertyChanged(nameof(ThemeMainWallpaper));
            OnPropertyChanged(nameof(IsMainWallpaperVideo));
            OnPropertyChanged(nameof(ThemeMainWallpaperVideoPath));
            OnPropertyChanged(nameof(ThemeLogoAsset));
            RefreshBootSplash();
            RefreshMissingPreview();

            RefreshFolderColorsLive();
            ThemeBindingsRefreshed?.Invoke();
        }

        // Walks the existing tree in-place and reapplies the folder color from the current theme (folders with their own color file are left alone).
        // Does NOT rebuild the tree (that would lose expand state) - see project notes on RefreshFolderColorsLive.
        // Also pushes FolderFontSize/FolderSelectedBgColor/FolderSelectedColor/ArrowColor onto every node -
        // these were added alongside FolderColor to eliminate RelativeSource AncestorType=TreeView bindings
        // in XAML, which were causing severe UI freezes when generating containers for very large (30k+) folders.
        public void RefreshFolderColorsLive()
        {
            var gamesListEntry = GetLayerEntry(LayerRegistry.GamesList);
            var mainFolderBrush = SafeConvertToBrush(gamesListEntry.FolderColorHex ?? LayerRegistry.DefaultFolderColorHex);
            var folderSelectedBgBrush = SafeConvertToBrush(gamesListEntry.FolderSelectedBgColorHex ?? LayerRegistry.DefaultFolderSelectedBgColorHex);
            var folderSelectedBrush = SafeConvertToBrush(gamesListEntry.FolderSelectedColorHex ?? LayerRegistry.DefaultFolderSelectedColorHex);
            var arrowBrush = SafeConvertToBrush(gamesListEntry.ArrowColorHex ?? LayerRegistry.DefaultArrowColorHex);
            var arrowMargin = new Thickness(2, 2 + (gamesListEntry.ArrowNudgeY ?? LayerRegistry.DefaultArrowNudgeY), 0, 0);
            double folderFontSize = gamesListEntry.FolderFontSize ?? LayerRegistry.DefaultFolderFontSize;
            var folderFontFamily = new FontFamily(string.IsNullOrWhiteSpace(gamesListEntry.FolderFontFamily) ? LayerRegistry.DefaultFontFamily : gamesListEntry.FolderFontFamily);
            var folderFontWeight = (gamesListEntry.FolderBold ?? LayerRegistry.DefaultFolderBold) ? FontWeights.Bold : FontWeights.Normal;
            var folderTextEffect = BuildShadowEffect(
                gamesListEntry.FolderShadowEnabled ?? LayerRegistry.DefaultShadowEnabled,
                gamesListEntry.FolderShadowColorHex ?? LayerRegistry.DefaultShadowColorHex,
                gamesListEntry.FolderShadowBlur ?? LayerRegistry.DefaultShadowBlur,
                gamesListEntry.FolderShadowDepth ?? LayerRegistry.DefaultShadowDepth);
            var folderSelectedTextEffect = BuildShadowEffect(
                gamesListEntry.FolderSelectedShadowEnabled ?? LayerRegistry.DefaultShadowEnabled,
                gamesListEntry.FolderSelectedShadowColorHex ?? LayerRegistry.DefaultShadowColorHex,
                gamesListEntry.FolderSelectedShadowBlur ?? LayerRegistry.DefaultShadowBlur,
                gamesListEntry.FolderSelectedShadowDepth ?? LayerRegistry.DefaultShadowDepth);

            void Walk(TreeCategoryNode node)
            {
                if (!node.IsCustomColor)
                {
                    node.FolderColor = mainFolderBrush;
                }

                node.FolderFontSize = folderFontSize;
                node.FolderFontFamily = folderFontFamily;
                node.FolderFontWeight = folderFontWeight;
                node.FolderTextEffect = folderTextEffect;
                node.FolderSelectedTextEffect = folderSelectedTextEffect;
                node.FolderSelectedBgColor = folderSelectedBgBrush;
                node.FolderSelectedColor = folderSelectedBrush;
                node.ArrowColor = arrowBrush;
                node.ArrowMargin = arrowMargin;

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
            var gamesListEntry = GetLayerEntry(LayerRegistry.GamesList);
            var gameColorBrush = SafeConvertToBrush(gamesListEntry.GameColorHex ?? LayerRegistry.DefaultGameColorHex);
            var gameHoverBrush = SafeConvertToBrush(gamesListEntry.GameHoverColorHex ?? LayerRegistry.DefaultGameHoverColorHex);
            var gameSelectedBgBrush = SafeConvertToBrush(gamesListEntry.GameSelectedBgColorHex ?? LayerRegistry.DefaultGameSelectedBgColorHex);
            var gameSelectedBrush = SafeConvertToBrush(gamesListEntry.GameSelectedColorHex ?? LayerRegistry.DefaultGameSelectedColorHex);
            double gameFontSize = gamesListEntry.GameFontSize ?? LayerRegistry.DefaultGameFontSize;
            var gameFontFamily = new FontFamily(string.IsNullOrWhiteSpace(gamesListEntry.GameFontFamily) ? LayerRegistry.DefaultFontFamily : gamesListEntry.GameFontFamily);
            var gameFontWeight = (gamesListEntry.GameBold ?? LayerRegistry.DefaultGameBold) ? FontWeights.Bold : FontWeights.Normal;
            var gameTextEffect = BuildShadowEffect(
                gamesListEntry.GameShadowEnabled ?? LayerRegistry.DefaultShadowEnabled,
                gamesListEntry.GameShadowColorHex ?? LayerRegistry.DefaultShadowColorHex,
                gamesListEntry.GameShadowBlur ?? LayerRegistry.DefaultShadowBlur,
                gamesListEntry.GameShadowDepth ?? LayerRegistry.DefaultShadowDepth);
            var gameSelectedTextEffect = BuildShadowEffect(
                gamesListEntry.GameSelectedShadowEnabled ?? LayerRegistry.DefaultShadowEnabled,
                gamesListEntry.GameSelectedShadowColorHex ?? LayerRegistry.DefaultShadowColorHex,
                gamesListEntry.GameSelectedShadowBlur ?? LayerRegistry.DefaultShadowBlur,
                gamesListEntry.GameSelectedShadowDepth ?? LayerRegistry.DefaultShadowDepth);

            foreach (var game in GamesCollection)
            {
                game.IsMouseSupported = MouseSupportRoms.Contains(game.RomName);
                game.FontSize = gameFontSize;
                game.GameFontFamily = gameFontFamily;
                game.GameFontWeight = gameFontWeight;
                game.GameTextEffect = gameTextEffect;
                game.GameSelectedTextEffect = gameSelectedTextEffect;
                game.GameColor = gameColorBrush;
                game.GameHoverColor = gameHoverBrush;
                game.GameSelectedBgColor = gameSelectedBgBrush;
                game.GameSelectedColor = gameSelectedBrush;
            }
        }
        // Builds the drop shadow for one kind of text from the Games List entry's values. Returns null when the
        // shadow is off. The picked color's alpha sets the shadow's strength: it becomes the effect's Opacity,
        // and the effect's own color is made fully opaque so the strength is not applied twice.
        // The result is frozen so every row can share the one object.
        private static System.Windows.Media.Effects.Effect? BuildShadowEffect(bool enabled, string colorHex, int blur, int depth)
        {
            if (!enabled) return null;

            System.Windows.Media.Color color;
            try { color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex); }
            catch { color = System.Windows.Media.Colors.Black; }

            var effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Color.FromRgb(color.R, color.G, color.B),
                Opacity = color.A / 255.0,
                BlurRadius = blur,
                ShadowDepth = depth,
                Direction = 315,
                RenderingBias = System.Windows.Media.Effects.RenderingBias.Quality
            };
            effect.Freeze();
            return effect;
        }
        // [END SECTION: Theme Refresh - Sync Points 2 & 3]

        // [SECTION: Shared Theme Loader]
        // Extracts a saved theme's zip archive and maps every field onto Configuration - the single
        // source of truth for "load theme X", used both by ThemesManagerControl's Load Theme button and by
        // the constructor's first-run/no-active-theme bootstrap (see LoadTheme("default") call below).
        // Asset paths are rewritten relative to Configuration.BaseDirectory rather than left absolute -
        // matches ResolveAssetAbsolutePath's existing expectation on the Save side, and keeps settings.json
        // portable across USB drive-letter changes.
        // Raised just before LoadTheme deletes and re-extracts a theme's Extracted folder, so MainWindow can
        // release any custom layer files (video, animated images) that are still open from it.
        public event Action? ThemeExtractionStarting;

        public bool LoadTheme(string themeName)
        {
            string themeFile = Configuration.GetThemePath(themeName);
            if (!File.Exists(themeFile)) return false;

            // Use the zip's real file name from disk (e.g. "Default" for a "default" request), so ActiveThemeName and
            // the Extracted folder match the name the theme list shows.
            var realFile = new DirectoryInfo(Path.GetDirectoryName(themeFile)!).GetFiles(Path.GetFileName(themeFile)).FirstOrDefault();
            if (realFile != null)
            {
                themeName = Path.GetFileNameWithoutExtension(realFile.Name);
                themeFile = realFile.FullName;
            }

            string extractDir = Path.Combine(Configuration.GetArcadeStickFilesPath(), "Themes", "Extracted", themeName);
            if (Directory.Exists(extractDir))
            {
                ThemeExtractionStarting?.Invoke();
                Directory.Delete(extractDir, recursive: true);
            }
            ZipFile.ExtractToDirectory(themeFile, extractDir);

            // New themes store theme.json; older ones store theme.cfg (same JSON content). Prefer the new
            // name and fall back so existing themes, including the bundled default.zip, keep loading.
            string themeJsonPath = Path.Combine(extractDir, "theme.json");
            if (!File.Exists(themeJsonPath)) themeJsonPath = Path.Combine(extractDir, "theme.cfg");

            string jsonString = File.ReadAllText(themeJsonPath);
            var themeData = System.Text.Json.JsonSerializer.Deserialize<ThemeSettings>(jsonString);
            if (themeData == null) return false;

            // Rewrites a layout entry's asset path (used for every entry's ImagePath below) from archive-relative
            // ("assets/x.png") to a path relative to Configuration.BaseDirectory (not absolute) so the theme
            // survives a USB drive-letter change. A rooted (already-absolute) stored value means that asset was
            // missing at save time and was never actually bundled - left untouched rather than mangled.
            string RewriteAssetPath(string storedValue)
            {
                if (string.IsNullOrWhiteSpace(storedValue)) return storedValue;
                if (Path.IsPathRooted(storedValue)) return storedValue;
                string absolutePath = Path.Combine(extractDir, storedValue.Replace('/', Path.DirectorySeparatorChar));
                return Path.GetRelativePath(Configuration.BaseDirectory, absolutePath);
            }

            // Map ThemeSettings DTO properties back to the main ConfigurationSettings object
            Configuration.OptionsBg = themeData.OptionsBg;
            Configuration.ScrollTrackColor = themeData.ScrollTrackColor;
            Configuration.ScrollTrackHoverColor = themeData.ScrollTrackHoverColor;
            Configuration.ScrollThumbColor = themeData.ScrollThumbColor;
            Configuration.ScrollThumbHoverColor = themeData.ScrollThumbHoverColor;
            Configuration.ScrollThumbDragColor = themeData.ScrollThumbDragColor;
            Configuration.ScrollbarWidth = themeData.ScrollbarWidth;
            Configuration.SeparatorColorHex = themeData.SeparatorColorHex;
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
            Configuration.ContextMenuFontColorHex = themeData.ContextMenuFontColorHex;
            Configuration.ContextMenuIconColorHex = themeData.ContextMenuIconColorHex;
            Configuration.ContextMenuBgColorHex = themeData.ContextMenuBgColorHex;
            Configuration.ContextMenuHoverColorHex = themeData.ContextMenuHoverColorHex;
            Configuration.ContextMenuHoverBgColorHex = themeData.ContextMenuHoverBgColorHex;
            Configuration.ContextMenuBorderColorHex = themeData.ContextMenuBorderColorHex;
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

            // Layouts come from the theme file; any resolution or layer not saved there falls back to
            // LayerRegistry defaults at resolve time. The active resolution (a user choice stored in
            // settings.json) is deliberately left alone.
            // Custom layer images are stored archive-relative in the theme file; point them at the extracted
            // copy (same rewrite as the flat asset properties above). A rooted path means the file was missing
            // at save time and is left untouched.
            foreach (var customEntry in (themeData.Layouts ?? new List<ThemeLayout>()).SelectMany(l => l.Layers).Where(e => !string.IsNullOrWhiteSpace(e.ImagePath)))
            {
                customEntry.ImagePath = RewriteAssetPath(customEntry.ImagePath);
            }
            Configuration.Layouts = themeData.Layouts ?? new List<ThemeLayout>();

            // The window follows the theme: switch to the size the theme was saved at (older themes keep the current size)
            if (themeData.ActiveWidth > 0 && themeData.ActiveHeight > 0)
            {
                Configuration.ActiveResolutionWidth = themeData.ActiveWidth;
                Configuration.ActiveResolutionHeight = themeData.ActiveHeight;
            }

            // Restore point for Reset Layer / Reset Layout: an independent copy of what the theme file holds.
            Configuration.SavedThemeLayouts = ConfigurationSettings.CloneLayouts(Configuration.Layouts);
            Configuration.SavedThemeWidth = Configuration.ActiveResolutionWidth;
            Configuration.SavedThemeHeight = Configuration.ActiveResolutionHeight;

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
                FolderColor = SafeConvertToBrush(GetLayerEntry(LayerRegistry.GamesList).FolderColorHex ?? LayerRegistry.DefaultFolderColorHex)
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
                    RebuildRandomizerPoolCache();
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

        // Cached randomizer pool (full eligible list, before the one-spin cooldown is applied). Rebuilt on a
        // background thread whenever its inputs change, so the die click only needs a size check and random
        // picks instead of re-running the full family/filter resolution over the whole collection.
        // _randomizerPoolVersion lets a stale background result be discarded if another rebuild started
        // while it was still running.
        private List<GameItem>? _randomizerPoolCache;
        private int _randomizerPoolVersion;

        public void RebuildRandomizerPoolCache()
        {
            _randomizerPoolCache = null;
            int version = ++_randomizerPoolVersion;

            // Snapshot everything the background work reads while still on the UI thread.
            var gamesSnapshot = GamesCollection.ToList();
            var filterOptions = BuildSortFilterOptions();

            _ = Task.Run(() =>
            {
                var visibleRomNames = _sortService.GetRandomizerPool(gamesSnapshot, filterOptions);
                var pool = gamesSnapshot.Where(g => visibleRomNames.Contains(g.RomName)).ToList();

                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (version == _randomizerPoolVersion)
                    {
                        _randomizerPoolCache = pool;
                    }
                }), DispatcherPriority.ApplicationIdle);
            });
        }

        // Returns the cached pool if it's ready; otherwise builds it on the spot (the old behavior) and
        // stores it, so a click that beats the background rebuild is slower but never wrong or empty.
        private List<GameItem> GetRandomizerPoolList()
        {
            if (_randomizerPoolCache != null) return _randomizerPoolCache;

            var visibleRomNames = _sortService.GetRandomizerPool(GamesCollection, BuildSortFilterOptions());
            _randomizerPoolCache = GamesCollection.Where(g => visibleRomNames.Contains(g.RomName)).ToList();
            return _randomizerPoolCache;
        }

        // Builds one full spin: sources the pool via GetRandomizerPool (always evaluates as if Base
        // Filter were on, regardless of its actual current checkbox state), locks in a winner first, then
        // generates filler frames as independent random draws from that same pool (duplicates allowed -
        // a real slot machine reel can show the same symbol twice). Returns null if the pool is too small
        // to spin. The immediately preceding winner is excluded from this spin's pool only (one-spin
        // cooldown), provided doing so wouldn't drop the pool below the minimum.
        public RandomizerSpinResult? GenerateRandomizerSpin()
        {
            var pool = GetRandomizerPoolList();

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

        // Lightweight pool-size check only - used at die-click time, before the confirm popup even
        // opens. Deliberately does NOT pick a winner or touch _lastRandomizerWinnerRomName - that only
        // happens once the user actually confirms via GenerateRandomizerSpin, so a cancelled die click
        // never burns a cooldown slot.
        public bool IsRandomizerPoolEligible()
        {
            return GetRandomizerPoolList().Count >= RandomizerMinimumPoolSize;
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

        // The flag the spin overlays bind to (MainWindow.xaml triggers). True while a spin runs; the reveal
        // popup that used to keep it on after the spin is gone, so it now simply follows IsRandomizerSpinning.
        public bool IsRandomizerOverlayActive => IsRandomizerSpinning;

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

        // Winner blink: after the last frame lands, the winner's marquee blinks off/on before the overlay
        // lifts. Runs while IsRandomizerSpinning is still true, so the overlay and the die-click guard
        // stay active with no extra flag. Each blink is one hide step plus one show step.
        private const int RandomizerWinnerBlinkCount = 3;
        private const int RandomizerWinnerBlinkStepMs = 250;
        private DispatcherTimer? _randomizerBlinkTimer;
        private int _randomizerBlinkStepsRemaining;

        private bool _isRandomizerBlinkHidden;
        public bool IsRandomizerBlinkHidden
        {
            get => _isRandomizerBlinkHidden;
            private set
            {
                if (_isRandomizerBlinkHidden != value)
                {
                    _isRandomizerBlinkHidden = value;
                    OnPropertyChanged();
                }
            }
        }

        private void StartRandomizerWinnerBlink()
        {
            _randomizerBlinkStepsRemaining = (RandomizerWinnerBlinkCount * 2) - 1;
            IsRandomizerBlinkHidden = true;

            _randomizerBlinkTimer?.Stop();
            _randomizerBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RandomizerWinnerBlinkStepMs) };
            _randomizerBlinkTimer.Tick += RandomizerBlinkTimer_Tick;
            _randomizerBlinkTimer.Start();
        }

        private void RandomizerBlinkTimer_Tick(object? sender, EventArgs e)
        {
            if (_randomizerBlinkStepsRemaining <= 0)
            {
                _randomizerBlinkTimer?.Stop();
                FinishRandomizerSpin();
                return;
            }

            IsRandomizerBlinkHidden = !IsRandomizerBlinkHidden;
            _randomizerBlinkStepsRemaining--;
        }

        // The original end-of-spin logic, moved here unchanged so it can run after the blink instead of
        // immediately on the last frame.
        private void FinishRandomizerSpin()
        {
            IsRandomizerBlinkHidden = false;
            IsRandomizerSpinning = false;
            var winner = _randomizerCurrentFrameGame;
            if (winner != null)
            {
                // Set the real selection (its setter already ran the instant art part), then start the video
                // part now instead of after the 600ms debounce (which exists for rapid arrow-key browsing -
                // not applicable to a single deterministic landing event).
                SelectedGame = winner;
                _previewDebounceTimer?.Stop();
                ApplyPreviewVideo();

                RandomizerSpinCompleted?.Invoke(winner);
            }
        }

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
                // The last frame (the winner) is already showing - blink it, then FinishRandomizerSpin
                // lifts the overlay and applies the real selection.
                StartRandomizerWinnerBlink();
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

            // Loaded synchronously as before (the spin timing is unchanged), but through the decoded-image cache,
            // so repeat frames and the winner's marquee are reused. Null still means "no real marquee file".
            var request = MakeImageRequest(targetMarqueeFile, isFlyer: false);
            RandomizerFrameMarqueeImage = request != null ? LoadCachedImage(request) : null;
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
        // Reads a single-hex-line color file (RecentlyPlayedColorFile / MostPlayedColorFile /
        // FavoritesColorFile) - same shape as a custom folder's own .cfg minus the ROM membership lines.
        // Falls back to a sensible gold/yellow default if the file doesn't exist yet.
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
        public void SetFavoritesColor(string hexColor) => SetFolderColorFile(Configuration.FavoritesColorFile, hexColor);
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
                FilteredGameCountText = $"{filteredGameCount:N0}";

                string configDir = Configuration.GetConfigPath();
                string playlistsDir = Path.Combine(configDir, "playlists");
                string folderOrderPath = Path.Combine(configDir, Configuration.FolderOrderFile);

                if (!Directory.Exists(playlistsDir))
            {
                Directory.CreateDirectory(playlistsDir);
            }

                var rootCategories = new Dictionary<string, TreeCategoryNode>(StringComparer.OrdinalIgnoreCase);
                var mainFolderBrush = SafeConvertToBrush(GetLayerEntry(LayerRegistry.GamesList).FolderColorHex ?? LayerRegistry.DefaultFolderColorHex);

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
                        FolderColor = SafeConvertToBrush(ReadFolderColorFile(Configuration.FavoritesColorFile)),
                        IsCustomColor = true // otherwise RefreshFolderColorsLive() overwrites this with the generic folder color
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
        // True only when the selected game's marquee lookup found no file. Drives the no-art banner.
        private bool _marqueeMissing;
        public bool MarqueeMissing
        {
            get => _marqueeMissing;
            private set
            {
                if (_marqueeMissing != value)
                {
                    _marqueeMissing = value;
                    OnPropertyChanged();
                }
            }
        }

        // Does the marquee re-resolve for the current game (RefreshThemeBindings): the same instant logic as a
        // selection, under the current selection version so the game's other pending loads are kept.
        private void ResolveMarqueeImage()
        {
            if (SelectedGame == null)
            {
                MarqueeImage = null;
                MarqueeMissing = false;
                return;
            }

            ApplyInstantMarquee(SelectedGame, System.Threading.Volatile.Read(ref _mediaSelectionVersion));
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
        // same primary+clone fallback as Flyer. GameInfoLayerControl listens for these property changes to
        // rebuild its dot row; they are set together at selection time (SetRow3Images).
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

        // Does a full-size decode into a frozen BitmapImage (null on failure). Static and touches no view model
        // state, so the background loader can call it.
        private static BitmapImage? LoadBitmapFromFile(string path)
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

        // Flyer slot size in device pixels, set from MainWindow.ApplyLayout. 0 = unknown, which loads the
        // flyer at full size as before.
        private int _flyerSlotWidth = 0;
        private int _flyerSlotHeight = 0;

        // Decode at this multiple of the fitted display size, so HighQuality scaling still has some extra
        // source detail for its final step. Tune by eye: 1.0 = exact size, 2.0 = more headroom.
        private const double FlyerDecodeOversample = 1;

        // Loads a flyer, decoding it near the size it will be shown at (Uniform fit inside the slot).
        // Portrait flyers are limited by the slot's height and wide ones by its width. Never decodes
        // larger than the source, and falls back to a normal full-size load if anything goes wrong.
        // The slot size is passed in (snapshotted on the UI thread) so this can run on a background task.
        private static BitmapImage? LoadFlyerBitmap(string path, int slotWidth, int slotHeight)
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(path);
                int decodeWidth = 0;
                int decodeHeight = 0;

                if (slotWidth > 0 && slotHeight > 0)
                {
                    using var probeStream = new MemoryStream(fileBytes);
                    var decoder = BitmapDecoder.Create(probeStream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    int sourceWidth = decoder.Frames[0].PixelWidth;
                    int sourceHeight = decoder.Frames[0].PixelHeight;

                    if (sourceWidth > 0 && sourceHeight > 0)
                    {
                        double widthFit = (double)slotWidth / sourceWidth;
                        double heightFit = (double)slotHeight / sourceHeight;
                        double scale = Math.Min(widthFit, heightFit) * FlyerDecodeOversample;

                        if (scale < 1.0)
                        {
                            if (widthFit <= heightFit) decodeWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
                            else decodeHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
                        }
                    }
                }

                var bitmap = new BitmapImage();
                using var stream = new MemoryStream(fileBytes);
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                if (decodeWidth > 0) bitmap.DecodePixelWidth = decodeWidth;
                else if (decodeHeight > 0) bitmap.DecodePixelHeight = decodeHeight;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return LoadBitmapFromFile(path);
            }
        }

        // Called by MainWindow.ApplyLayout with the Image Previews part's size in device pixels. When the
        // size actually changes, the selected game's flyer is re-requested through the instant loader; the
        // size is part of the flyer's cache key, so a new size decodes fresh (no manual invalidation). The old
        // flyer stays up until the new decode lands. Unchanged sizes do nothing, so ApplyLayout costs nothing.
        public void SetFlyerSlotSize(int width, int height)
        {
            if (width < 1 || height < 1) return;
            if (width == _flyerSlotWidth && height == _flyerSlotHeight) return;

            _flyerSlotWidth = width;
            _flyerSlotHeight = height;

            if (SelectedGame == null) return;

            var request = MakeImageRequest(ResolveArtPath(Configuration.FlyersPath, "flyers", SelectedGame, ArtImageExtensions), isFlyer: true);
            if (request == null)
            {
                FlyerImage = null;
                return;
            }
            if (GetCachedImage(request.Key) is BitmapImage cachedFlyer)
            {
                FlyerImage = cachedFlyer;
                return;
            }
            LoadImagesInBackground(System.Threading.Volatile.Read(ref _mediaSelectionVersion), new[] { request }, images => FlyerImage = images[0]);
        }

        // [SECTION: Media File Resolution]
        // Same lookup order as TryResolveMediaFile (own ROM name first, then the fallback/parent name).
        // Every video file that exists plays directly through the FFmpeg player, so there is no
        // playability check or background probing.
        private string? TryResolveMediaFileChecked(string folder, string primaryRomName, string? fallbackRomName, string[] extensions)
        {
            var romNames = new List<string> { primaryRomName };
            if (!string.IsNullOrEmpty(fallbackRomName)) romNames.Add(fallbackRomName);

            foreach (string romName in romNames)
            {
                foreach (var ext in extensions)
                {
                    string testPath = Path.Combine(folder, $"{romName}{ext}");
                    if (File.Exists(testPath)) return testPath;
                }
            }

            return null;
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

        // [SECTION: Instant Media Loading]
        // Selection-time art loading. File existence is checked on the UI thread (cheap) so HasActiveMedia and
        // MarqueeMissing are right at once; art already in the decoded-image cache applies synchronously; the rest
        // is read and decoded on a background task (frozen BitmapImages) and applied on the UI thread. Every
        // selection increments _mediaSelectionVersion: background work checks it before each file read and again
        // before applying, so a stale game's art is discarded. Order: marquee first (applied as soon as it is
        // ready), then the video slot's static image, then flyer/title/snap/cabinet together in one callback so
        // GameInfoLayerControl rebuilds the preview nav dots once. Only the video path waits for the debounce.
        private int _mediaSelectionVersion;

        private static readonly string[] ArtImageExtensions = { ".png", ".jpg" };

        // One image to show: the full path, its cache key (path + last-write time + length, plus the slot size
        // for flyers) and, for flyers, the slot size it decodes for (snapshotted on the UI thread)
        private sealed record ImageRequest(string Path, string Key, bool IsFlyer, int SlotWidth, int SlotHeight);

        // Does the request build on the UI thread: null when there is no path or the file is gone ("missing" is
        // always re-checked from disk, never cached)
        private ImageRequest? MakeImageRequest(string? path, bool isFlyer)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) return null;

                int slotWidth = isFlyer ? _flyerSlotWidth : 0;
                int slotHeight = isFlyer ? _flyerSlotHeight : 0;
                string key = $"{info.FullName}|{info.LastWriteTimeUtc.Ticks}|{info.Length}|{slotWidth}x{slotHeight}";
                return new ImageRequest(info.FullName, key, isFlyer, slotWidth, slotHeight);
            }
            catch
            {
                return null;
            }
        }

        // Does one art lookup with the primary-then-parent rule (marquee, flyer, title, snap, cabinet)
        private string? ResolveArtPath(string? configuredFolder, string defaultFolder, GameItem game, string[] extensions) =>
            TryResolveMediaFile(Configuration.GetMediaCategoryPath(string.IsNullOrWhiteSpace(configuredFolder) ? defaultFolder : configuredFolder),
                game.RomName, game.CloneOf, extensions);

        // [SECTION: Decoded Image Cache]
        // Small LRU cache of decoded, frozen images, shared by the UI thread and the background loader (locked).
        // Only successful decodes are stored. The key includes the file's last-write time and length, so art
        // replaced by the Get Artwork scraper decodes fresh without a restart.
        private const int DecodedImageCacheSize = 30;
        private readonly object _imageCacheLock = new object();
        private readonly LinkedList<(string Key, BitmapImage Image)> _imageCacheOrder = new LinkedList<(string Key, BitmapImage Image)>();
        private readonly Dictionary<string, LinkedListNode<(string Key, BitmapImage Image)>> _imageCacheIndex = new Dictionary<string, LinkedListNode<(string Key, BitmapImage Image)>>();

        // Does a cache lookup (marks the entry most recently used); null on a miss
        private BitmapImage? GetCachedImage(string key)
        {
            lock (_imageCacheLock)
            {
                if (!_imageCacheIndex.TryGetValue(key, out var node)) return null;
                _imageCacheOrder.Remove(node);
                _imageCacheOrder.AddFirst(node);
                return node.Value.Image;
            }
        }

        // Does a cache insert, evicting the least recently used entry past the size limit
        private void AddCachedImage(string key, BitmapImage image)
        {
            lock (_imageCacheLock)
            {
                if (_imageCacheIndex.TryGetValue(key, out var existing)) _imageCacheOrder.Remove(existing);
                var node = _imageCacheOrder.AddFirst((key, image));
                _imageCacheIndex[key] = node;

                while (_imageCacheOrder.Count > DecodedImageCacheSize && _imageCacheOrder.Last is LinkedListNode<(string Key, BitmapImage Image)> oldest)
                {
                    _imageCacheOrder.RemoveLast();
                    _imageCacheIndex.Remove(oldest.Value.Key);
                }
            }
        }

        // Does a cached load: the cached image, or a fresh decode that is then cached. Safe on any thread.
        private BitmapImage? LoadCachedImage(ImageRequest request)
        {
            if (GetCachedImage(request.Key) is BitmapImage cached) return cached;

            var image = request.IsFlyer
                ? LoadFlyerBitmap(request.Path, request.SlotWidth, request.SlotHeight)
                : LoadBitmapFromFile(request.Path);
            if (image != null) AddCachedImage(request.Key, image);
            return image;
        }
        // [END SECTION: Decoded Image Cache]

        // Does the synchronous path: true when every request is either absent (no file) or already cached
        private bool TryGetAllCached(ImageRequest?[] requests, out BitmapImage?[] images)
        {
            images = new BitmapImage?[requests.Length];
            for (int i = 0; i < requests.Length; i++)
            {
                if (requests[i] is not ImageRequest request) continue;
                if (GetCachedImage(request.Key) is not BitmapImage cached) return false;
                images[i] = cached;
            }
            return true;
        }

        // Does the background decode for one selection version, then applies the results on the UI thread only
        // if that version is still current. Touches no UI-thread-only state (the requests carry what it needs).
        private void LoadImagesInBackground(int version, ImageRequest?[] requests, Action<BitmapImage?[]> apply)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            _ = Task.Run(() =>
            {
                var images = new BitmapImage?[requests.Length];
                for (int i = 0; i < requests.Length; i++)
                {
                    if (System.Threading.Volatile.Read(ref _mediaSelectionVersion) != version) return;
                    if (requests[i] is ImageRequest request) images[i] = LoadCachedImage(request);
                }

                dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_mediaSelectionVersion == version) apply(images);
                }));
            });
        }

        // Does the instant part for a newly selected game (marquee first, then the static preview, then Row 3)
        private void ApplyInstantMedia(GameItem game, int version)
        {
            ApplyInstantMarquee(game, version);
            ApplyInstantPreview(game, version);
            ApplyInstantRow3(game, version);
        }

        // Does the clear when nothing is selected: marquee, banner flag, flyer/title/snap/cabinet and the static
        // preview, all at once
        private void ClearInstantMedia()
        {
            MarqueeImage = null;
            MarqueeMissing = false;
            PreviewImage = null;
            HasActiveMedia = false;
            SetRow3Images(new BitmapImage?[4]);
        }

        // Does the marquee: the banner flag comes from the file check right away (a game with a marquee file never
        // shows the banner), the image from the cache or a background decode. Regional variants sharing a parent's
        // marquee is the common, desirable case, so the parent fallback stays.
        private void ApplyInstantMarquee(GameItem game, int version)
        {
            string? marqueePath = ResolveArtPath(Configuration.MarqueesPath, "marquees", game, new[] { ".png" });
            MarqueeMissing = marqueePath == null;

            var requests = new[] { MakeImageRequest(marqueePath, isFlyer: false) };
            if (TryGetAllCached(requests, out var cached))
            {
                MarqueeImage = cached[0];
                return;
            }

            MarqueeImage = null; // never leave the previous game's marquee under the new title
            LoadImagesInBackground(version, requests, images => MarqueeImage = images[0]);
        }

        // Does the video slot's static part: HasActiveMedia from the shared resolver, and the static image when the
        // resolved media is an image (a video leaves PreviewImage null; its path is set by ApplyPreviewVideo)
        private void ApplyInstantPreview(GameItem game, int version)
        {
            var media = ResolvePreviewMedia(game);
            HasActiveMedia = media != null;

            var requests = new[] { media is { IsVideo: false } image ? MakeImageRequest(image.Path, isFlyer: false) : null };
            if (TryGetAllCached(requests, out var cached))
            {
                PreviewImage = cached[0];
                return;
            }

            PreviewImage = null;
            LoadImagesInBackground(version, requests, images => PreviewImage = images[0]);
        }

        // Does Row 3 (flyer, title, snap) and the cabinet: applied together, so the preview nav dots update once
        private void ApplyInstantRow3(GameItem game, int version)
        {
            var requests = new[]
            {
                MakeImageRequest(ResolveArtPath(Configuration.FlyersPath, "flyers", game, ArtImageExtensions), isFlyer: true),
                MakeImageRequest(ResolveArtPath(Configuration.TitlescreensPath, "titles", game, ArtImageExtensions), isFlyer: false),
                MakeImageRequest(ResolveArtPath(Configuration.ScreenshotsPath, "snap", game, ArtImageExtensions), isFlyer: false),
                MakeImageRequest(ResolveArtPath(Configuration.CabinetsPath, "cabinets", game, ArtImageExtensions), isFlyer: false)
            };
            if (TryGetAllCached(requests, out var cached))
            {
                SetRow3Images(cached);
                return;
            }

            SetRow3Images(new BitmapImage?[4]); // never leave the previous game's art under the new title
            LoadImagesInBackground(version, requests, SetRow3Images);
        }

        // Does the Row 3 apply in one go: flyer, title, snap, cabinet
        private void SetRow3Images(BitmapImage?[] images)
        {
            FlyerImage = images[0];
            TitleImage = images[1];
            SnapImage = images[2];
            CabinetImage = images[3];
        }

        // Does the video part (after the debounce, or at once after a spin): the video path when the preview
        // slot's media is a video, otherwise empty
        private void ApplyPreviewVideo()
        {
            var media = SelectedGame != null ? ResolvePreviewMedia(SelectedGame) : null;
            VideoSourcePath = media is { IsVideo: true } video ? video.Path : string.Empty;
        }

        // Does the shared preview-slot resolve (HasActiveMedia, the static image and the video all use it).
        // Pass 1 walks every PreviewPriorityOrder category with ONLY the game's own ROM name: a title with even one
        // asset of its own never blends in a differently-named clone parent's media. Pass 2, only when the game has
        // nothing of its own, walks them again with the clone parent's name. Null when nothing is found.
        private (string Path, bool IsVideo)? ResolvePreviewMedia(GameItem game)
        {
            string? found = FindPreviewMedia(game.RomName);
            if (found == null && !string.IsNullOrEmpty(game.CloneOf)) found = FindPreviewMedia(game.CloneOf);
            if (found == null) return null;

            bool isVideo = found.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || found.EndsWith(".avi", StringComparison.OrdinalIgnoreCase);
            return (found, isVideo);
        }

        // Does one pass of ResolvePreviewMedia: the first file for this ROM name in PreviewPriorityOrder
        private string? FindPreviewMedia(string romName)
        {
            foreach (var category in PreviewPriorityOrder)
            {
                var target = ResolveCategoryTarget(category);
                if (target == null) continue;

                string? matchedPath = TryResolveMediaFileChecked(target.Value.folder, romName, null, target.Value.extensions);
                if (matchedPath != null) return matchedPath;
            }
            return null;
        }

        // Resolves a PreviewPriorityOrder category name to its folder + expected extensions. Returns null for an
        // unrecognized category (skip it).
        private (string folder, string[] extensions)? ResolveCategoryTarget(string category)
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
        // [END SECTION: Instant Media Loading]

        // Resolves a static fallback image for the options-open state, walking the same PreviewPriorityOrder
        // as ResolvePreviewMedia but skipping "videos" - falls back to the missing-preview asset if nothing matches
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
                RebuildRandomizerPoolCache();
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