// 🏗️ START: MAIN VIEW MODEL LAYERED PREVIEW BINDING EXTENSIONS
using ArcadeStick.Models;
using ArcadeStick.Services;
using ArcadeStick.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
    public class TreeCategoryNode : INotifyPropertyChanged
    {
        private bool _isNodeExpanded;
        public string HeaderText { get; set; } = string.Empty;
        private Brush _folderColor = Brushes.Cyan;
        public Brush FolderColor
        {
            get => _folderColor;
            set
            {
                if (_folderColor != value)
                {
                    _folderColor = value;
                    OnPropertyChanged();
                }
            }
        }
        public bool IsCustomColor { get; set; } = false;
        public ObservableCollection<GameItem> ChildGames { get; set; } = new ObservableCollection<GameItem>();
        public ObservableCollection<TreeCategoryNode> SubFolders { get; set; } = new ObservableCollection<TreeCategoryNode>();

        public System.Collections.IEnumerable DisplayItems
        {
            get
            {
                foreach (var folder in SubFolders) yield return folder;
                foreach (var game in ChildGames) yield return game;
            }
        }

        public bool IsNodeExpanded
        {
            get => _isNodeExpanded;
            set
            {
                if (_isNodeExpanded != value)
                {
                    _isNodeExpanded = value;
                    OnPropertyChanged();

                    if (!_isNodeExpanded)
                    {
                        foreach (var sub in SubFolders)
                        {
                            sub.IsNodeExpanded = false;
                        }
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CacheScannerService _cacheService;
        private readonly ProcessLaunchService _launchService;
        private string _searchText = string.Empty;
        private DispatcherTimer? _searchDebounceTimer;
        private GameItem? _selectedGame;
        private BitmapImage? _marqueeImage;
        private string _videoSourcePath = string.Empty;
        private bool _isDevMode;

        public List<string> PreviewPriorityOrder { get; private set; } = new List<string>();

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
                    }
                }
                catch
                {
                }
            }

            _cacheService = new CacheScannerService(Configuration);
            _launchService = new ProcessLaunchService(Configuration);

            GamesCollection = new ObservableCollection<GameItem>();
            TreeNodesCollection = new ObservableCollection<TreeCategoryNode>();

            RefreshCacheCommand = new RelayCommand(async _ => await InitializeDatabaseAsync());
            LaunchGameCommand = new RelayCommand(async param => await ExecuteLaunchAsync(param));

            LoadFavoritesFromDisk();
            LoadMouseSupportFromDisk();
            LoadPreviewOrderConfig();
            RefreshThemeBindings();
        }

        private void LoadPreviewOrderConfig()
        {
            string configDir = Configuration.GetConfigPath();
            string configFile = Path.Combine(configDir, "preview_order.cfg");

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }

            if (!File.Exists(configFile))
            {
                string[] defaultOrder = { "videos", "flyers", "screenshots", "titlescreens", "cabinets" };
                File.WriteAllLines(configFile, defaultOrder);
                PreviewPriorityOrder = new List<string>(defaultOrder);
            }
            else
            {
                PreviewPriorityOrder = File.ReadAllLines(configFile)
                                          .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                                          .Select(line => line.Trim().ToLower())
                                          .ToList();
            }
        }

        public ConfigurationSettings Configuration { get; }
        public ObservableCollection<GameItem> GamesCollection { get; }
        public ObservableCollection<TreeCategoryNode> TreeNodesCollection { get; }
        public ObservableCollection<object> FlatVisibleRows { get; } = new ObservableCollection<object>();
        public HashSet<string> FavoriteRoms { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> MouseSupportRoms { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public ICommand RefreshCacheCommand { get; }
        public ICommand LaunchGameCommand { get; }

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
                    UpdateActiveMediaPreviews();
                }
            }
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

        public string ThemeMainColor => Configuration.BackgroundColor;
        public string ThemeGamesColor => Configuration.FileListBg;
        public string ThemeMarqueeColor => Configuration.MarqueeBoxBg;
        public string ThemeMediaColor => Configuration.VideoBoxBg;
        public string ThemeBorderColor => Configuration.BorderColorFramework;
        public double ThemeBorderWidth => Configuration.BorderWidthValue;
        public double ThemeBorderCurve => Configuration.BorderCurveValue;
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
        public string ThemeTabColor => Configuration.TabColorHex;
        public string ThemeTabBgColor => Configuration.TabBgColorHex;
        public string ThemeTabActiveColor => Configuration.TabActiveColorHex;
        public string ThemeTabActiveBgColor => Configuration.TabActiveBgColorHex;

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

        public ImageSource? ThemeBootSplashAsset => LoadThemeImage(
            !string.IsNullOrWhiteSpace(Configuration.ThemeBootSplash)
                ? Configuration.ThemeBootSplash
                : Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets", "default_mediabg.png"));

        public ImageSource? ThemeMissingPreviewAsset => LoadThemeImage(
            !string.IsNullOrWhiteSpace(Configuration.ThemeMissingPreview)
                ? Configuration.ThemeMissingPreview
                : Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets", "no_preview.png"));

        public ImageSource? MouseSupportIcon => LoadThemeImage(
            Path.Combine(Configuration.GetArcadeStickFilesPath(), "assets", "mouseicon.png"));

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

        public void RefreshThemeBindings()
        {
            OnPropertyChanged(nameof(ThemeMainColor));
            OnPropertyChanged(nameof(ThemeGamesColor));
            OnPropertyChanged(nameof(ThemeMarqueeColor));
            OnPropertyChanged(nameof(ThemeMediaColor));
            OnPropertyChanged(nameof(ThemeBorderColor));
            OnPropertyChanged(nameof(ThemeBorderWidth));
            OnPropertyChanged(nameof(ThemeBorderCurve));
            OnPropertyChanged(nameof(ThemeSeparatorColor));
            OnPropertyChanged(nameof(ThemeScrollTrackColor));
            OnPropertyChanged(nameof(ThemeScrollTrackHoverColor));
            OnPropertyChanged(nameof(ThemeScrollThumbColor));
            OnPropertyChanged(nameof(ThemeScrollThumbHoverColor));
            OnPropertyChanged(nameof(ThemeScrollThumbDragColor));
            OnPropertyChanged(nameof(ThemeFolderFontSize));
            OnPropertyChanged(nameof(ThemeGameFontSize));
            OnPropertyChanged(nameof(ThemeGameColor));
            OnPropertyChanged(nameof(ThemeFolderSelectedColor));
            OnPropertyChanged(nameof(ThemeFolderSelectedBgColor));
            OnPropertyChanged(nameof(ThemeGameHoverColor));
            OnPropertyChanged(nameof(ThemeGameSelectedColor));
            OnPropertyChanged(nameof(ThemeGameSelectedBgColor));
            OnPropertyChanged(nameof(ThemeTabColor));
            OnPropertyChanged(nameof(ThemeTabBgColor));
            OnPropertyChanged(nameof(ThemeTabActiveColor));
            OnPropertyChanged(nameof(ThemeTabActiveBgColor));
            OnPropertyChanged(nameof(ThemeMainWallpaper));
            OnPropertyChanged(nameof(ThemeGamesWallpaper));
            OnPropertyChanged(nameof(ThemeMarqueeWallpaper));
            OnPropertyChanged(nameof(ThemeMediaWallpaper));
            OnPropertyChanged(nameof(ThemeLogoAsset));
            OnPropertyChanged(nameof(ThemeBootSplashAsset));
            OnPropertyChanged(nameof(ThemeMissingPreviewAsset));
            OnPropertyChanged(nameof(MouseSupportIcon));

            RefreshFolderColorsLive();
        }

        public void RefreshFolderColorsLive()
        {
            var mainFolderBrush = (SolidColorBrush)new BrushConverter().ConvertFrom(Configuration.FolderColorHex)!;
            var favoritesBrush = (SolidColorBrush)new BrushConverter().ConvertFrom(Configuration.FavoritesColorHex)!;

            void Walk(TreeCategoryNode node)
            {
                if (!node.IsCustomColor)
                {
                    node.FolderColor = node.HeaderText.Equals("FAVORITES", StringComparison.OrdinalIgnoreCase)
                        ? favoritesBrush
                        : mainFolderBrush;
                }

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

        public Task SyncMameRomPathsAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    string mameDir = Configuration.GetMamePath();
                    string iniPath = Path.Combine(mameDir, "mame.ini");

                    if (!File.Exists(iniPath)) return;

                    string activeRomsPath = Path.Combine(mameDir, Configuration.RomsSubFolder);
                    if (!Directory.Exists(activeRomsPath)) return;

                    var currentFolders = Directory.GetDirectories(activeRomsPath, "*", SearchOption.AllDirectories).ToList();
                    currentFolders.Insert(0, activeRomsPath);

                    string safeMameDir = mameDir.EndsWith(Path.DirectorySeparatorChar.ToString()) || mameDir.EndsWith(Path.AltDirectorySeparatorChar.ToString())
                        ? mameDir
                        : mameDir + Path.DirectorySeparatorChar;

                    var targetPaths = new List<string>();
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
                    iniLines[targetLineIndex] = $"rompath                   {updatedPathsJoined}";
                    File.WriteAllLines(iniPath, iniLines);
                }
                catch (Exception)
                {
                }
            });
        }

        public async Task InitializeDatabaseAsync()
        {
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

                App.Current.Dispatcher.Invoke(() =>
                {
                    GamesCollection.Clear();
                    foreach (var game in resultsMap.Values)
                    {
                        GamesCollection.Add(game);
                    }

                    UpdateLiveTreeDisplay();
                });
            });
        }

        public void UpdateLiveTreeDisplay()
        {
            string configDir = Configuration.GetConfigPath();
            string playlistsDir = Path.Combine(configDir, "playlists");
            string folderOrderPath = Path.Combine(configDir, Configuration.FolderOrderFile);

            if (!Directory.Exists(playlistsDir))
            {
                Directory.CreateDirectory(playlistsDir);
            }

            var rootCategories = new Dictionary<string, TreeCategoryNode>(StringComparer.OrdinalIgnoreCase);
            var mainFolderBrush = (SolidColorBrush)new BrushConverter().ConvertFrom(Configuration.FolderColorHex)!;

            string GetFormattedTitle(GameItem gameItem)
            {
                return IsDevMode ? $"[{gameItem.RomName}] {gameItem.FullTitle}" : gameItem.FullTitle;
            }

            foreach (var game in GamesCollection)
            {
                game.DisplayTitle = GetFormattedTitle(game);
                game.IsMouseSupported = MouseSupportRoms.Contains(game.RomName);

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
                    if (!currentPointer.ChildGames.Contains(game))
                    {
                        currentPointer.ChildGames.Add(game);
                    }
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

                if (!currentPointer.ChildGames.Contains(game))
                {
                    currentPointer.ChildGames.Add(game);
                }
            }

            if (Directory.Exists(playlistsDir))
            {
                foreach (var cfgFile in Directory.GetFiles(playlistsDir, "*.cfg"))
                {
                    string playlistName = Path.GetFileNameWithoutExtension(cfgFile).ToUpper().Trim();
                    if (playlistName == Configuration.MouseSupportFile.ToUpper()) continue;

                    var lines = File.ReadAllLines(cfgFile).Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();
                    var brushColor = (SolidColorBrush)new BrushConverter().ConvertFrom(Configuration.VirtualListsColor)!;

                    if (lines.Count > 0 && lines[0].StartsWith("#") && lines[0].Length == 7)
                    {
                        try { brushColor = (SolidColorBrush)new BrushConverter().ConvertFrom(lines[0])!; } catch { }
                    }

                    var pNode = new TreeCategoryNode { HeaderText = playlistName, FolderColor = brushColor, IsCustomColor = true };

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("#")) continue;
                        var matchedGame = GamesCollection.FirstOrDefault(g => g.RomName.Equals(line, StringComparison.OrdinalIgnoreCase));
                        if (matchedGame != null) pNode.ChildGames.Add(matchedGame);
                    }

                    rootCategories[playlistName] = pNode;
                }
            }

            var orderedHeaders = new List<string>();
            if (File.Exists(folderOrderPath))
            {
                orderedHeaders = File.ReadAllLines(folderOrderPath)
                    .Select(l => l.Trim().ToUpper())
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToList();
            }

            var finalSortedNodes = new List<TreeCategoryNode>();

            if (FavoriteRoms.Count > 0)
            {
                var favoritesRoot = new TreeCategoryNode
                {
                    HeaderText = "FAVORITES",
                    FolderColor = (SolidColorBrush)new BrushConverter().ConvertFrom(Configuration.FavoritesColorHex)!
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

            var remainingUnlisted = rootCategories.Values
                .Where(n => !orderedHeaders.Contains(n.HeaderText))
                .OrderBy(n => n.HeaderText);

            foreach (var unlistedNode in remainingUnlisted)
            {
                finalSortedNodes.Add(unlistedNode);
                rootCategories.Remove(unlistedNode.HeaderText);
            }

            foreach (var header in orderedHeaders)
            {
                if (rootCategories.TryGetValue(header, out var node))
                {
                    finalSortedNodes.Add(node);
                    rootCategories.Remove(header);
                }
            }

            TreeNodesCollection.Clear();
            string query = SearchText.Trim().ToLower();
            bool isSearching = !string.IsNullOrEmpty(query);

            if (isSearching)
            {
                var matchingGames = GamesCollection.Where(g =>
                    g.RomName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    g.FullTitle.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

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
                    cat.IsNodeExpanded = false;

                    foreach (var sub in cat.SubFolders)
                    {
                        sub.IsNodeExpanded = false;
                    }

                    TreeNodesCollection.Add(cat);
                }
            }

            RebuildFlatVisibleRows();
        }

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

        // 🏗️ START MEDIA PATH RESOLUTION FIX
        // 🏗️ START MARQUEE FALLBACK LOGIC
        private void UpdateActiveMediaPreviews()
        {
            if (SelectedGame == null)
            {
                MarqueeImage = null;
                PreviewImage = null;
                VideoSourcePath = string.Empty;
                HasActiveMedia = false;
                return;
            }

            // Ensure we anchor to the absolute root of the Arcade Stick folder
            string rootDir = Configuration.BaseDirectory;

            // Helper to resolve paths relative to the root cleanly, without hardcoding a 'data' subfolder
            string ResolvePath(string inputPath)
            {
                if (string.IsNullOrWhiteSpace(inputPath)) return string.Empty;

                // Strip relative dot-slashes and leading slashes
                string cleanPath = inputPath.Replace(@".\", "").TrimStart('\\', '/');

                // If it's somehow an absolute path already, trust it
                if (Path.IsPathRooted(cleanPath)) return cleanPath;

                return Path.Combine(rootDir, cleanPath);
            }

            string marqueeDir = ResolvePath(string.IsNullOrWhiteSpace(Configuration.MarqueesPath) ? "media/marquees" : Configuration.MarqueesPath);
            string targetMarqueeFile = Path.Combine(marqueeDir, $"{SelectedGame.RomName}.png");

            // FALLBACK LOGIC: If the specific game's marquee is missing, load the default boot logo
            if (!File.Exists(targetMarqueeFile))
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

            string foundMediaFile = string.Empty;
            bool gameHasMedia = false;

            foreach (var category in PreviewPriorityOrder)
            {
                string targetFolder = string.Empty;
                string[] extensions = { ".png", ".jpg" }; // Default image extensions

                switch (category)
                {
                    case "videos":
                        targetFolder = ResolvePath(string.IsNullOrWhiteSpace(Configuration.VideosPath) ? "videos" : Configuration.VideosPath);
                        extensions = new[] { ".mp4", ".avi" };
                        break;
                    case "flyers":
                        targetFolder = ResolvePath(string.IsNullOrWhiteSpace(Configuration.FlyersPath) ? "flyers" : Configuration.FlyersPath);
                        break;
                    case "screenshots":
                    case "snapshots":
                    case "gameplay":
                        targetFolder = ResolvePath(string.IsNullOrWhiteSpace(Configuration.ScreenshotsPath) ? "snap" : Configuration.ScreenshotsPath);
                        break;
                    case "titlescreens":
                        targetFolder = ResolvePath(string.IsNullOrWhiteSpace(Configuration.TitlescreensPath) ? "titles" : Configuration.TitlescreensPath);
                        break;
                    case "cabinets":
                        targetFolder = ResolvePath(string.IsNullOrWhiteSpace(Configuration.CabinetsPath) ? "cabinets" : Configuration.CabinetsPath);
                        break;
                    case "marquees":
                        targetFolder = ResolvePath(string.IsNullOrWhiteSpace(Configuration.MarqueesPath) ? "marquees" : Configuration.MarqueesPath);
                        break;
                    default:
                        continue;
                }

                foreach (var ext in extensions)
                {
                    string testPath = Path.Combine(targetFolder, $"{SelectedGame.RomName}{ext}");
                    if (File.Exists(testPath))
                    {
                        foundMediaFile = testPath;
                        gameHasMedia = true;
                        break;
                    }
                }

                if (gameHasMedia) break;
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
        // 🏗️ END MARQUEE FALLBACK LOGIC
        // 🏗️ END MEDIA PATH RESOLUTION FIX

        private async Task ExecuteLaunchAsync(object? parameter)
        {
            if (parameter is MainWindow mainWin && SelectedGame != null)
            {
                await _launchService.LaunchGameAsync(SelectedGame, mainWin, mainWin.GamepadService);
            }
            else if (parameter is Window parentWindow && SelectedGame != null)
            {
                await _launchService.LaunchGameAsync(SelectedGame, parentWindow);
            }

            GameLaunchCompleted?.Invoke();
        }

        public event Action? GameLaunchCompleted;

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

        public void ToggleFavorite(GameItem? game)
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
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving favorites config: {ex.Message}");
            }
        }

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

        public void RebuildFlatVisibleRows()
        {
            FlatVisibleRows.Clear();
            foreach (var node in TreeNodesCollection)
            {
                AddNodeToFlatList(node);
            }
        }

        private void AddNodeToFlatList(TreeCategoryNode node)
        {
            FlatVisibleRows.Add(node);
            if (node.IsNodeExpanded)
            {
                foreach (var sub in node.SubFolders)
                {
                    AddNodeToFlatList(sub);
                }
                foreach (var game in node.ChildGames)
                {
                    FlatVisibleRows.Add(game);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


// 🏗️ END: MAIN VIEW MODEL LAYERED PREVIEW BINDING EXTENSIONS