// [SECTION: File Overrides] - Layered preview and asset management overrides for MainWindow
using ArcadeStick.Services;
using ArcadeStick.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ArcadeStick.Services
{
    // Directional/action inputs normalized from raw gamepad events for navigation handling below
    public enum GamepadAction
    {
        None,
        Up,
        Down,
        Left,
        Right,
        Select,
        Back
    }
}

namespace ArcadeStick
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private WGIService? _gamepadService;
        private RomCopyService? _romCopyService;
        private bool _isTogglingMouseSupport;
        private bool _isOptionsWindowOpen;

        // FFmpeg game-preview player: owns the runtime and the presenter that draws into
        // VideoPreviewLayer.PreviewVideoSurface. Created right after the view model in the constructor.
        private ArcadeStick.Playback.PreviewPlayerHost? _previewPlayer;

        // [SECTION: Folder Flyout State]
        // State for the shared Create/Rename folder-name Popup and the Choose Color Popup. Grouped here
        // rather than scattered near each handler since these fields are shared across several methods.
        private FrameworkElement? _folderContextPlacementElement; // the folder row that opened the current context menu, needed by Rename/Color to anchor their Popup
        private string? _folderNameFlyoutTargetFolder; // null = Create mode, set = Rename mode (holds the folder being renamed)
        private bool _folderNameFlyoutCommitted; // true only when Enter was pressed - distinguishes a real commit from Escape/click-away
        private Models.TreeCategoryNode? _colorFlyoutTargetNode;
        private string? _colorFlyoutTargetFolderName;
        private Brush? _colorFlyoutOriginalColor; // snapshot to revert to on Escape
        private bool _colorFlyoutEscaped;
        private bool _suppressColorHexSync; // prevents the picker's ColorChanged and the hex textbox's LostFocus from re-triggering each other
        // [END SECTION: Folder Flyout State]

        public WGIService? GamepadService => _gamepadService;
        // Game-preview video plays through the FFmpeg player (see _previewPlayer); the boot splash plays through MediaElement.

        // Tracks which video path is currently loaded into the boot splash MediaElement, so
        // RefreshBootSplashVideo() can skip a redundant Stop()/reload when nothing has actually
        // changed (e.g. the RefreshThemeBindings() cascade firing on every Options close) instead of
        // unconditionally restarting playback.
        private string? _currentBootSplashPath;

        // [SECTION: Constructor]
        // Sets up the view model and the FFmpeg preview player, wires view-model events, and hooks
        // window lifecycle + gamepad input.
        // Manual tooltip delay - WPF's built-in ToolTipService.BetweenShowDelay does not reliably
        // re-apply the full delay when the mouse moves quickly between adjacent elements (it favors
        // fast reshow instead), so ToolTipService is disabled entirely on any element using this and
        // this timer drives open/close manually instead, guaranteeing the same full delay every time.
        // Originally built for game rows only; now shared by the top action buttons (Random/Sort/Options)
        // as well, hence the generic naming.
        private readonly DispatcherTimer _delayedTooltipTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        private FrameworkElement? _pendingTooltipTarget;

        // Transient status feedback for Ctrl+V rom copy - same ToolTip/IsOpen mechanics as the hover
        // tooltip system above, but opened programmatically (no hover delay - Ctrl+V already signals
        // intent) and auto-closed after 2 seconds instead of on MouseLeave.
        private readonly DispatcherTimer _romCopyStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private System.Windows.Controls.ToolTip? _romCopyStatusTooltip;

        public MainWindow()
        {
            _delayedTooltipTimer.Tick += DelayedTooltipTimer_Tick;
            _romCopyStatusTimer.Tick += RomCopyStatusTimer_Tick;

            // Game-preview video plays through the FFmpeg player (created below, after the view model);
            // the boot splash video plays through MediaElement.

            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            // FFmpeg preview player. The surface Image lives in VideoPreviewLayer. If the DLLs fail to load,
            // the host quietly does nothing and the preview slot just stays empty.
            _previewPlayer = new ArcadeStick.Playback.PreviewPlayerHost(
            _viewModel.Configuration.GetFfmpegRuntimeDirectory(), VideoPreviewLayer.PreviewVideoSurface);

            _romCopyService = new RomCopyService(_viewModel.Configuration);

            _viewModel.GameLaunchCompleted += RefreshVideoPreview;
            _viewModel.GameLaunchCompleted += RestoreGameListScrollPosition;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.ThemeBindingsRefreshed += RefreshMainButtonsRestingColors;
            _viewModel.ThemeBindingsRefreshed += RefreshBootSplashImageCornerClip;
            _viewModel.ThemeBindingsRefreshed += RefreshMainBgVideo;
            _viewModel.ThemeBindingsRefreshed += ApplyLayout;
            _viewModel.ThemeBindingsRefreshed += ApplyGlobalThemeResources;
            _viewModel.ThemeExtractionStarting += ReleaseCustomLayers;
            _viewModel.GameLaunchCompleted += ResumeMainBgVideo;

            // Position every layer from the active layout right away (the first ThemeBindingsRefreshed
            // may have fired inside MainViewModel's constructor, before this subscription existed).
            ApplyLayout();

            // Publish the Global tab values as app resources now, for the same reason (the view model's first
            // theme load and refresh ran before the subscription above existed).
            ApplyGlobalThemeResources();

            // Paint the four main window buttons' resting colors immediately - if MainViewModel's constructor
            // above already fired ThemeBindingsRefreshed as part of its own theme-load setup, that event
            // was missed entirely since this subscription didn't exist yet, leaving the TextBlocks with
            // no Foreground set at all (black) until the first manual MouseEnter/MouseLeave cycle.
            RefreshMainButtonsRestingColors();

            _viewModel.RandomizerSpinCompleted += winner =>
            {
                // Jump the game tree to the winner - the spin just lands on it, no reveal popup anymore.
                ExpandAndSelectGameInMainTree(winner);
                RestartPreviewAfterSpin();
            };

            // Fixes a pre-existing WPF quirk affecting every AllowsTransparency="True" Popup in this
            // file (not something introduced by the randomizer) - such popups don't automatically
            // recompute their screen position when the owning Window moves. Nudging HorizontalOffset
            // forces WPF to recalculate placement against the current window position.
            LocationChanged += MainWindow_LocationChanged;

            // Smart fullscreen reruns its size test when the window's DPI, monitor or the display settings change
            DpiChanged += (s, e) => Dispatcher.BeginInvoke(new Action(RecomputeSmartFullscreen));
            LocationChanged += SmartFullscreen_LocationChanged;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SmartFullscreen_DisplaySettingsChanged;
            Closed += (s, e) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SmartFullscreen_DisplaySettingsChanged;

            // Post-load startup sequence: sync ROM paths, load the database, set idle media state, start gamepad polling
            Loaded += async (s, e) =>
            {
                // Load the wallpaper video paused first so its first frame shows while the list builds.
                RefreshMainBgVideo();

                _ = _viewModel.SyncMameRomPathsAsync();
                await _viewModel.InitializeDatabaseAsync();
                ApplyDefaultMedia();
                FinishMainBgVideoStartup();
                InitializeGamepadInput();
            };

            // Dispose native/unmanaged resources on window close
            Unloaded += (s, e) =>
            {
                _gamepadService?.Dispose();
            };

            KeyDown += MainWindow_KeyDown;
            Closing += MainWindow_Closing;
        }
        // [END SECTION: Constructor]


        // Called by Theme Builder after each committed Advanced edit so layers move live (nothing saved).
        public void ApplyLayoutNow() => ApplyLayout();

        // Rebuilds the Global tab's app resources from the current settings (startup and every ThemeBindingsRefreshed)
        private void ApplyGlobalThemeResources() => Services.GlobalThemeResources.Apply(_viewModel.Configuration);

        // The Games List style values last pushed onto the rows. ApplyLayout runs often (every position
        // edit, reorder and theme refresh), so the rows are only repainted when this actually changes.
        private string? _lastGamesListStyleKey;
        private string? _lastGameTitleStyleKey;
        private string? _lastMarqueeLogoPath;
        private string? _lastMarqueeShadowKey;
        private string? _lastSplashPath;
        private string? _lastVideoFallbackPath;
        private string? _lastSearchButtonsStyleKey;
        private string? _lastGameInfoStyleKey;
        private string? _lastRandomDisplayStyleKey;
        private string? _lastMarqueeBannerStyleKey;
        private string? _lastMainBackgroundKey;
        private string? _lastMainBackgroundColor;

        // [SECTION: Theme Builder Alignment Guides]
        private Views.AdvancedThemesTabControl.GuideInfo? _lastGuideInfo; // redrawn when the overlay resizes (fullscreen toggle)

        // Draws (or hides, when info is null) the guides on the full-window GuideOverlay: four 1px lines at the
        // selected layer's pending rect (ShowRect) and a cross at that rect's center (ShowCenter). Every line runs
        // to the window edge; coordinates are offset by LayoutCanvas' origin in the overlay. Right and bottom
        // lines sit on the last pixel inside the layer (X + Width - 0.5) so they hug its edge.
        public void ShowGuides(Views.AdvancedThemesTabControl.GuideInfo? info)
        {
            _lastGuideInfo = info;
            foreach (var l in new[] { GuideLeft, GuideRight, GuideTop, GuideBottom, GuideCenterV, GuideCenterH })
                l.Visibility = Visibility.Collapsed;

            if (info == null) return;

            Brush brush;
            try { brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(info.ColorHex)); }
            catch { brush = Brushes.Cyan; }

            Point origin = LayoutCanvas.TranslatePoint(new Point(0, 0), GuideOverlay);
            double canvasW = LayoutCanvas.Width;
            double canvasH = LayoutCanvas.Height;
            double fullW = Math.Max(GuideOverlay.ActualWidth, origin.X + canvasW);
            double fullH = Math.Max(GuideOverlay.ActualHeight, origin.Y + canvasH);

            void Place(Line line, double x1, double y1, double x2, double y2)
            {
                line.X1 = x1; line.Y1 = y1; line.X2 = x2; line.Y2 = y2;
                line.Stroke = brush;
                line.Visibility = Visibility.Visible;
            }

            if (info.ShowRect)
            {
                double left = origin.X + info.X + 0.5;
                double right = origin.X + info.X + info.Width - 0.5;
                double top = origin.Y + info.Y + 0.5;
                double bottom = origin.Y + info.Y + info.Height - 0.5;

                Place(GuideLeft, left, 0, left, fullH);
                Place(GuideRight, right, 0, right, fullH);
                Place(GuideTop, 0, top, fullW, top);
                Place(GuideBottom, 0, bottom, fullW, bottom);
            }

            if (info.ShowCenter)
            {
                // On the pixel at the guide rect's center, so the cross follows the layer as it moves or resizes
                double centerX = origin.X + info.X + Math.Floor(info.Width / 2) + 0.5;
                double centerY = origin.Y + info.Y + Math.Floor(info.Height / 2) + 0.5;

                Place(GuideCenterV, centerX, 0, centerX, fullH);
                Place(GuideCenterH, 0, centerY, fullW, centerY);
            }
        }

        // Redraws the visible guides when the overlay changes size (fullscreen on or off), so they still reach the edge
        private void GuideOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_lastGuideInfo != null) ShowGuides(_lastGuideInfo);
        }
        // [END SECTION: Theme Builder Alignment Guides]

        // [SECTION: Theme Builder Background Grid]
        // Session-only editor aid (never saved): ThemeBuilderWindow forwards the Advanced tab's Grid checkbox, spacing and
        // offsets here, on only while THEME BUILDER is the selected tab. The line color is derived from the Main Window
        // Background color, blended toward white on a dark color and toward black on a light one.
        private bool _backgroundGridOn;
        private double _backgroundGridSpacing = 20;
        private double _backgroundGridOffsetX;
        private double _backgroundGridOffsetY;
        private const double BackgroundGridContrastOnDark = 0.18; // blend toward white
        private const double BackgroundGridContrastOnLight = 0.30; // blend toward black
        private SolidColorBrush? _backgroundGridCover; // frozen; rebuilt only when the background color changes

        // Stores the grid state and redraws; safe to call before the window has loaded
        public void SetBackgroundGrid(bool on, double spacing, double offsetX = 0, double offsetY = 0)
        {
            _backgroundGridOn = on;
            _backgroundGridSpacing = spacing;
            _backgroundGridOffsetX = offsetX;
            _backgroundGridOffsetY = offsetY;
            UpdateBackgroundGrid();
        }

        // Shows the grid over a solid Main Window Background fill (wallpaper image and video hidden), or hides it and
        // hands the background back to the root grid's Style and the video host
        private void UpdateBackgroundGrid()
        {
            if (!_backgroundGridOn || !(_backgroundGridSpacing > 0))
            {
                BackgroundGridLayer.Visibility = Visibility.Collapsed;
                RootLayoutGrid.ClearValue(Grid.BackgroundProperty);
                MainBgVideoHost.ClearValue(UIElement.VisibilityProperty);
                return;
            }

            // A local Background beats the Style's wallpaper ImageBrush, the same look as "Disable background image"
            Color background = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainColor).Color;
            if (_backgroundGridCover == null || _backgroundGridCover.Color != background)
            {
                var cover = new SolidColorBrush(background);
                cover.Freeze();
                _backgroundGridCover = cover;
            }
            RootLayoutGrid.Background = _backgroundGridCover;

            // The host, not MainBgVideo itself, so RefreshMainBgVideo keeps owning the player's own visibility
            MainBgVideoHost.Visibility = Visibility.Collapsed;

            BackgroundGridLayer.SetGrid(_backgroundGridSpacing, _backgroundGridOffsetX, _backgroundGridOffsetY, GetBackgroundGridLineColor(background));
            BackgroundGridLayer.Visibility = Visibility.Visible;
        }

        // Returns the opaque line color: the background's RGB (alpha ignored) blended BackgroundGridContrastOnDark toward
        // white when its luminance is under 128, otherwise BackgroundGridContrastOnLight toward black
        private static Color GetBackgroundGridLineColor(Color background)
        {
            double luminance = (0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B);
            bool dark = luminance < 128;
            double target = dark ? 255 : 0;
            double contrast = dark ? BackgroundGridContrastOnDark : BackgroundGridContrastOnLight;
            byte Blend(byte channel) => (byte)Math.Round(channel + ((target - channel) * contrast));
            return Color.FromRgb(Blend(background.R), Blend(background.G), Blend(background.B));
        }
        // [END SECTION: Theme Builder Background Grid]

        // [SECTION: Layout Application]
        // Positions every layer on LayoutCanvas from the active resolution's layout (saved entries plus
        // registry defaults for anything missing). Runs at startup and on every ThemeBindingsRefreshed,
        // so Save Adjustments and theme loads reposition live. The window itself sizes to the canvas
        // (SizeToContent), so the client area always equals the layout resolution.
        // Bordered layers (shapes, custom layers, Splash Image, Video Preview, Random Game Display) treat the entry's
        // Width / Height as the content size and X / Y as the ring's outer corner (PlaceOutsideBorder).
        private void ApplyLayout()
        {
            if (_viewModel == null) return;

            // The displayed layout: the active resolution, or Smart fullscreen's session-only pick
            int width = _viewModel.DisplayLayoutWidth;
            int height = _viewModel.DisplayLayoutHeight;
            var layout = _viewModel.ResolveDisplayLayout();

            // Repaint the Games List rows only when its style values changed since the last paint
            var gamesListEntry = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.GamesList);
            string gamesListStyleKey = $"{gamesListEntry?.FolderFontSize}|{gamesListEntry?.FolderColorHex}|{gamesListEntry?.FolderSelectedColorHex}|{gamesListEntry?.FolderSelectedBgColorHex}|{gamesListEntry?.FolderFontFamily}|{gamesListEntry?.FolderBold}|{gamesListEntry?.ArrowColorHex}|{gamesListEntry?.ArrowNudgeY}|{gamesListEntry?.FolderShadowEnabled}|{gamesListEntry?.FolderShadowColorHex}|{gamesListEntry?.FolderShadowBlur}|{gamesListEntry?.FolderShadowDepth}|{gamesListEntry?.FolderSelectedShadowEnabled}|{gamesListEntry?.FolderSelectedShadowColorHex}|{gamesListEntry?.FolderSelectedShadowBlur}|{gamesListEntry?.FolderSelectedShadowDepth}";
            if (gamesListStyleKey != _lastGamesListStyleKey)
            {
                _lastGamesListStyleKey = gamesListStyleKey;
                _viewModel.RefreshFolderColorsLive();
            }

            // Same for the game title rows (separate key, so folder-only edits don't walk the games list)
            string gameTitleStyleKey = $"{gamesListEntry?.GameFontSize}|{gamesListEntry?.GameColorHex}|{gamesListEntry?.GameSelectedColorHex}|{gamesListEntry?.GameSelectedBgColorHex}|{gamesListEntry?.GameHoverColorHex}|{gamesListEntry?.GameFontFamily}|{gamesListEntry?.GameBold}|{gamesListEntry?.GameShadowEnabled}|{gamesListEntry?.GameShadowColorHex}|{gamesListEntry?.GameShadowBlur}|{gamesListEntry?.GameShadowDepth}|{gamesListEntry?.GameSelectedShadowEnabled}|{gamesListEntry?.GameSelectedShadowColorHex}|{gamesListEntry?.GameSelectedShadowBlur}|{gamesListEntry?.GameSelectedShadowDepth}";
            if (gameTitleStyleKey != _lastGameTitleStyleKey)
            {
                _lastGameTitleStyleKey = gameTitleStyleKey;
                _viewModel.RefreshGameColorsLive();
            }

            // Marquee logo: re-raise the logo binding only when the layer's logo path changed
            string marqueeLogoPath = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.MarqueePanel)?.ImagePath ?? "";
            if (marqueeLogoPath != _lastMarqueeLogoPath)
            {
                _lastMarqueeLogoPath = marqueeLogoPath;
                _viewModel.RefreshMarqueeLogo();
            }

            // Marquee shadow: re-raise the shared logo / marquee / spin frame shadow only when one of its values changed
            var marqueeEntry = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.MarqueePanel);
            string marqueeShadowKey = $"{marqueeEntry?.TextShadowEnabled}|{marqueeEntry?.TextShadowColorHex}|{marqueeEntry?.TextShadowBlur}|{marqueeEntry?.TextShadowDepth}|{marqueeEntry?.ShadowTarget}";
            if (marqueeShadowKey != _lastMarqueeShadowKey)
            {
                _lastMarqueeShadowKey = marqueeShadowKey;
                _viewModel.RefreshMarqueeBindings();
            }

            // Marquee no-art banner: re-raise the shared title card's bindings only when one of its values changed
            string marqueeBannerStyleKey = $"{marqueeEntry?.TextFontFamily}|{marqueeEntry?.TextBold}|{marqueeEntry?.TextFontSize}|{marqueeEntry?.TextColorHex}|{marqueeEntry?.TextWrap}|{marqueeEntry?.BannerTextMargin}"
                + $"|{marqueeEntry?.BgColorHex}|{marqueeEntry?.BorderColorHex}|{marqueeEntry?.BorderSize}|{marqueeEntry?.CornerRadius}"
                + $"|{marqueeEntry?.BannerShadowEnabled}|{marqueeEntry?.BannerShadowColorHex}|{marqueeEntry?.BannerShadowBlur}|{marqueeEntry?.BannerShadowDepth}";
            if (marqueeBannerStyleKey != _lastMarqueeBannerStyleKey)
            {
                _lastMarqueeBannerStyleKey = marqueeBannerStyleKey;
                _viewModel.RefreshMarqueeBannerBindings();
            }

            // Boot splash: re-raise the splash bindings only when the Splash Image layer's file changed. Raising
            // IsBootSplashVideo makes ViewModel_PropertyChanged reload the splash video (it skips an unchanged path).
            string splashPath = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.SplashImage)?.ImagePath ?? "";
            if (splashPath != _lastSplashPath)
            {
                _lastSplashPath = splashPath;
                _viewModel.RefreshBootSplash();
            }

            // Video Preview fallback ("no preview available") image: re-raise only when the layer's file changed
            string videoFallbackPath = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.VideoPreview)?.ImagePath ?? "";
            if (videoFallbackPath != _lastVideoFallbackPath)
            {
                _lastVideoFallbackPath = videoFallbackPath;
                _viewModel.RefreshMissingPreview();
            }

            // Main window wallpaper: re-raise its bindings and reload the video layer only when the Main Window
            // Background entry's file or Disable flag changed. Before the window has loaded, the Loaded handler's
            // own first-frame startup call loads the video, so it is not started early from here.
            var mainBackgroundEntry = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.MainBackground);
            string mainBackgroundKey = $"{mainBackgroundEntry?.ImagePath}|{mainBackgroundEntry?.ImageDisabled}";
            if (mainBackgroundKey != _lastMainBackgroundKey)
            {
                _lastMainBackgroundKey = mainBackgroundKey;
                _viewModel.RefreshMainWallpaper();
                if (IsLoaded) RefreshMainBgVideo();
            }

            // Main window background color: kept apart from the wallpaper key so a color change never touches the video.
            // Re-raises ThemeMainColor (main window fill and its popups), and re-derives the background grid's
            // cover and line colors while the grid is on.
            string mainBackgroundColor = mainBackgroundEntry?.BgColorHex ?? Models.LayerRegistry.DefaultMainBackgroundColorHex;
            if (mainBackgroundColor != _lastMainBackgroundColor)
            {
                _lastMainBackgroundColor = mainBackgroundColor;
                _viewModel.RefreshMainBackgroundColor();
                if (_backgroundGridOn) UpdateBackgroundGrid();
            }

            // Search & Buttons: re-raise the label, box and button bindings and repaint the buttons' resting
            // colors only when one of their style values changed
            var searchLabelEntry = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.SearchLabel);
            var searchBoxEntry = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.SearchBox);
            var buttonEntry = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.RandomButton);
            string searchButtonsStyleKey = $"{searchLabelEntry?.TextFontSize}|{searchLabelEntry?.TextColorHex}|{searchLabelEntry?.BgColorHex}|{searchLabelEntry?.TextFontFamily}|{searchLabelEntry?.TextBold}|{searchLabelEntry?.TextAlign}|{searchLabelEntry?.TextShadowEnabled}|{searchLabelEntry?.TextShadowColorHex}|{searchLabelEntry?.TextShadowBlur}|{searchLabelEntry?.TextShadowDepth}|{searchLabelEntry?.TextNudgeY}"
                + $"|{searchBoxEntry?.TextFontSize}|{searchBoxEntry?.TextColorHex}|{searchBoxEntry?.BgColorHex}"
                + $"|{buttonEntry?.TextFontSize}|{buttonEntry?.TextColorHex}|{buttonEntry?.BgColorHex}|{buttonEntry?.HoverColorHex}|{buttonEntry?.HoverBgColorHex}|{buttonEntry?.BorderSize}|{buttonEntry?.BorderColorHex}|{buttonEntry?.HoverBorderColorHex}|{buttonEntry?.CornerRadius}";
            if (searchButtonsStyleKey != _lastSearchButtonsStyleKey)
            {
                _lastSearchButtonsStyleKey = searchButtonsStyleKey;
                _viewModel.RefreshSearchButtonBindings();
                RefreshMainButtonsRestingColors();
            }

            // Game Info: re-raise the title, publisher, header, body and nav bindings only when one of their style
            // values changed (the nav color forwarders also repaint the active dots in GameInfoLayerControl)
            string gameInfoStyleKey = string.Join("#",
                new[] { Models.LayerRegistry.GameTitle, Models.LayerRegistry.PublisherBlock, Models.LayerRegistry.GameInfoBody, Models.LayerRegistry.GameInfoNav, Models.LayerRegistry.ImagePreviewsNav, Models.LayerRegistry.ImagePreviewsBody }
                    .Select(id => layout.Layers.FirstOrDefault(e => e.Id == id))
                    .Select(e => $"{e?.TextFontFamily}|{e?.TextBold}|{e?.TextFontSize}|{e?.TextColorHex}|{e?.TextAlign}|{e?.TextShadowEnabled}|{e?.TextShadowColorHex}|{e?.TextShadowBlur}|{e?.TextShadowDepth}|{e?.TextRightMargin}"
                        + $"|{e?.SectionHeaderFontFamily}|{e?.SectionHeaderBold}|{e?.SectionHeaderFontSize}|{e?.SectionHeaderColorHex}|{e?.SectionHeaderShadowEnabled}|{e?.SectionHeaderShadowColorHex}|{e?.SectionHeaderShadowBlur}|{e?.SectionHeaderShadowDepth}"));
            if (gameInfoStyleKey != _lastGameInfoStyleKey)
            {
                _lastGameInfoStyleKey = gameInfoStyleKey;
                _viewModel.RefreshGameInfoBindings();
            }

            // Random Game Display: re-raise the spin card's bindings only when a style value on either part changed
            string randomDisplayStyleKey = string.Join("#",
                new[] { Models.LayerRegistry.RandomGameDisplay, Models.LayerRegistry.RandomGameDisplaySplash }
                    .Select(id => layout.Layers.FirstOrDefault(e => e.Id == id))
                    .Select(e => $"{e?.TextMessage}|{e?.TextWrap}|{e?.TextFontFamily}|{e?.TextBold}|{e?.TextFontSize}|{e?.TextColorHex}|{e?.TextAlign}"
                        + $"|{e?.TextShadowEnabled}|{e?.TextShadowColorHex}|{e?.TextShadowBlur}|{e?.TextShadowDepth}"
                        + $"|{e?.BgColorHex}|{e?.BorderSize}|{e?.BorderColorHex}|{e?.CornerRadius}"));
            if (randomDisplayStyleKey != _lastRandomDisplayStyleKey)
            {
                _lastRandomDisplayStyleKey = randomDisplayStyleKey;
                _viewModel.RefreshRandomDisplayBindings();
            }

            LayoutCanvas.Width = width;
            LayoutCanvas.Height = height;

            var map = new Dictionary<string, FrameworkElement>
            {
                { Models.LayerRegistry.SearchLabel, SearchLabel },
                { Models.LayerRegistry.SearchBox, SearchBox },
                { Models.LayerRegistry.RandomButton, BtnPlayRandom },
                { Models.LayerRegistry.SortButton, BtnOpenSortingGear },
                { Models.LayerRegistry.OptionsButton, BtnOpenOptionsGear },
                { Models.LayerRegistry.HamburgerButton, BtnOpenMenuHamburger },
                { Models.LayerRegistry.GamesList, GamesListHost },
                { Models.LayerRegistry.MarqueePanel, MarqueePanelBorder },
                { Models.LayerRegistry.SplashImage, SplashLayer },
                { Models.LayerRegistry.VideoPreview, VideoPreviewLayer },
                { Models.LayerRegistry.GameTitle, GameInfoLayer.PartTitle },
                { Models.LayerRegistry.PublisherBlock, GameInfoLayer.PartPublisher },
                { Models.LayerRegistry.GameInfoNav, GameInfoLayer.PartInfoNav },
                { Models.LayerRegistry.GameInfoBody, GameInfoLayer.PartInfoBody },
                { Models.LayerRegistry.ImagePreviewsNav, GameInfoLayer.PartPreviewsNav },
                { Models.LayerRegistry.ImagePreviewsBody, GameInfoLayer.PartPreviewsBody },
                { Models.LayerRegistry.Footer, FooterPanel }
            };

            // The Game Info host fills the whole canvas (its six parts are positioned inside it) and takes the
            // group's shared Z. It has no background, so empty areas don't block clicks on the layers below.
            Canvas.SetLeft(GameInfoLayer, 0);
            Canvas.SetTop(GameInfoLayer, 0);
            GameInfoLayer.Width = width;
            GameInfoLayer.Height = height;
            var gameInfoGroupEntry = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.GameTitle);
            if (gameInfoGroupEntry != null) Panel.SetZIndex(GameInfoLayer, gameInfoGroupEntry.Z);

            foreach (var entry in layout.Layers)
            {
                if (!map.TryGetValue(entry.Id, out var element)) continue;

                Canvas.SetLeft(element, entry.X);
                Canvas.SetTop(element, entry.Y);
                element.Width = entry.Width;
                element.Height = entry.Height;
                Panel.SetZIndex(element, entry.Z);

                // Games List stays locked visible (the registry already forces it, this is a safety net)
                element.Visibility = (element == GamesListHost || entry.Visible) ? Visibility.Visible : Visibility.Collapsed;
            }

            ApplyShapeLayers(layout);

            // Splash Image and Video Preview draw their own border from their entries (Video Preview also its background).
            // Both entries' Width / Height are the content size and X / Y the ring's outer corner, so each control sits at (X, Y) sized W / H plus 2 x BorderSize.
            var splashEntry = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.SplashImage);
            if (splashEntry != null)
            {
                PlaceOutsideBorder(SplashLayer, splashEntry, splashEntry.BorderSize ?? 0);
                SplashLayer.ApplyBorder(splashEntry.BorderSize ?? 0, splashEntry.BorderColorHex ?? Models.LayerRegistry.DefaultShapeBorderColorHex, splashEntry.CornerRadius ?? Models.LayerRegistry.DefaultShapeCornerRadius);
            }
            var videoEntry = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.VideoPreview);
            if (videoEntry != null)
            {
                // The entry's Width / Height are the video area and X / Y the ring's outer corner, so the video starts at (X + B, Y + B)
                PlaceOutsideBorder(VideoPreviewLayer, videoEntry, videoEntry.BorderSize ?? 0);
                VideoPreviewLayer.ApplyBorder(videoEntry.BorderSize ?? 0, videoEntry.BorderColorHex ?? Models.LayerRegistry.DefaultShapeBorderColorHex, videoEntry.CornerRadius ?? Models.LayerRegistry.DefaultShapeCornerRadius);
                VideoPreviewLayer.ApplyBackground(videoEntry.BgColorHex ?? Models.LayerRegistry.DefaultVideoBgColorHex);
            }

            ApplyCustomLayers(layout);
            ApplyRandomGameDisplayRect(layout);
            ApplyRandomGameDisplayFile(layout);

            // The decoder follows the Video Preview layer's size, so a theme or layout-size change re-targets it.
            UpdatePreviewTargetSize();

            // Flyers are decoded near the size of the Image Previews part (device pixels, Smart fullscreen's factor included),
            // so a theme or layout-size change tells the view model the new slot size. It only reloads when the size changed.
            var previewsDpi = VisualTreeHelper.GetDpi(this);
            double previewsWidth = GameInfoLayer.PartPreviewsBody.Width;
            double previewsHeight = GameInfoLayer.PartPreviewsBody.Height;
            if (!double.IsNaN(previewsWidth) && !double.IsNaN(previewsHeight) && previewsWidth >= 1 && previewsHeight >= 1)
            {
                _viewModel.SetFlyerSlotSize(
                    (int)Math.Round(previewsWidth * previewsDpi.DpiScaleX * _layoutScaleFactor),
                    (int)Math.Round(previewsHeight * previewsDpi.DpiScaleY * _layoutScaleFactor));
            }

            // If the theme just hid the Video Preview layer, stop decoding into it. When it is shown again,
            // the next selection (or returning from a game) starts the preview as usual.
            if (VideoPreviewLayer.Visibility != Visibility.Visible)
            {
                StopMediaElementPreview();
            }
        }
        // [SECTION: Custom Image Layers]
        // Custom layers are drawn by the same box renderer as the shape layers (see ApplyCustomLayers below).
        // Their image or video stretches to fill the box, and a file only reloads when its path changes.

        // [SECTION: Shape Layers]
        // Shape Layer 01 to 04: each is a Border (fill, border, corner radius) with a host Grid that holds the
        // layer's image or video, stretched to fill. Position, size, Z, fill, border and file all come from the
        // layer entry. Visibility follows the entry's mode, and is re-evaluated whenever a game is selected or
        // deselected (see UpdateShapeVisibility).
        private readonly Dictionary<string, (FrameworkElement Element, string Path)> _shapeFileElements = new();
        private readonly Dictionary<string, string> _shapeVisibilityModes = new();

        // Does the shared border-ring placement: the entry's X / Y are the ring's outer top-left corner and its Width / Height
        // the content size, so the element sits at (X, Y) sized (W + 2B, H + 2B) and the content starts at (X + B, Y + B).
        private static void PlaceOutsideBorder(FrameworkElement element, Models.LayerLayoutEntry entry, double borderSize)
        {
            double border = Math.Max(0, borderSize);
            Canvas.SetLeft(element, entry.X);
            Canvas.SetTop(element, entry.Y);
            element.Width = entry.Width + (border * 2);
            element.Height = entry.Height + (border * 2);
        }

        // Does the shape / custom layer box styling, same as VideoPreviewLayerControl.ApplyClip: the ring (on top) takes the
        // border, the fill and the file host are pulled in by the border minus 1px so they reach 1px under the ring, and
        // their corner radius and the host's rounded clip shrink by the same inset to stay concentric.
        private static void ApplyRingBox(Models.LayerLayoutEntry entry, Border fill, Grid host, Border ring)
        {
            double borderSize = Math.Max(0, entry.BorderSize ?? Models.LayerRegistry.DefaultShapeBorderSize);
            double radius = Math.Max(0, entry.CornerRadius ?? Models.LayerRegistry.DefaultShapeCornerRadius);
            double inset = Math.Max(0, borderSize - 1);
            double innerRadius = Math.Max(0, radius - inset);

            ring.BorderBrush = MainViewModel.SafeConvertToBrush(entry.BorderColorHex ?? Models.LayerRegistry.DefaultShapeBorderColorHex);
            ring.BorderThickness = new Thickness(borderSize);
            ring.CornerRadius = new CornerRadius(radius);

            fill.Background = MainViewModel.SafeConvertToBrush(entry.BgColorHex ?? Models.LayerRegistry.DefaultShapeBgColorHex);
            fill.Margin = new Thickness(inset);
            fill.CornerRadius = new CornerRadius(innerRadius);

            // The host is the content rect plus the 1px under the ring on each side (ClipToBounds alone clips to a plain rectangle)
            host.Margin = new Thickness(inset);
            double overlap = borderSize - inset;
            var clip = new RectangleGeometry(new Rect(0, 0, entry.Width + (overlap * 2), entry.Height + (overlap * 2)), innerRadius, innerRadius);
            clip.Freeze();
            host.Clip = clip;
            host.Opacity = (entry.FileOpacity ?? Models.LayerRegistry.DefaultFileOpacity) / 100.0; // file opacity
        }

        // Places and styles Shape Layer 01 to 04 from their entries (border ring outside the rect) and loads their files.
        private void ApplyShapeLayers(Models.ThemeLayout layout)
        {
            var shapes = new (string Id, Border Box, Border Fill, Grid Host, Border Ring)[]
            {
                (Models.LayerRegistry.ShapeLayer01, ShapeLayer01, ShapeLayer01Fill, ShapeLayer01Host, ShapeLayer01Ring),
                (Models.LayerRegistry.ShapeLayer02, ShapeLayer02, ShapeLayer02Fill, ShapeLayer02Host, ShapeLayer02Ring),
                (Models.LayerRegistry.ShapeLayer03, ShapeLayer03, ShapeLayer03Fill, ShapeLayer03Host, ShapeLayer03Ring),
                (Models.LayerRegistry.ShapeLayer04, ShapeLayer04, ShapeLayer04Fill, ShapeLayer04Host, ShapeLayer04Ring)
            };

            foreach (var (id, box, fill, host, ring) in shapes)
            {
                var entry = layout.Layers.FirstOrDefault(e => e.Id == id);
                if (entry == null) continue;

                PlaceOutsideBorder(box, entry, entry.BorderSize ?? Models.LayerRegistry.DefaultShapeBorderSize);
                Panel.SetZIndex(box, entry.Z);
                ApplyRingBox(entry, fill, host, ring);

                ApplyShapeFile(id, host, entry.ImagePath);
                _shapeVisibilityModes[id] = entry.VisibilityMode ?? Models.LayerRegistry.DefaultVisibilityMode;
            }

            UpdateShapeVisibility();
        }

        // Shows or hides each shape layer from its mode and whether a game is selected.
        private void UpdateShapeVisibility()
        {
            if (_viewModel == null) return;

            var shapes = new List<(string Id, Border Box)>
            {
                (Models.LayerRegistry.ShapeLayer01, ShapeLayer01),
                (Models.LayerRegistry.ShapeLayer02, ShapeLayer02),
                (Models.LayerRegistry.ShapeLayer03, ShapeLayer03),
                (Models.LayerRegistry.ShapeLayer04, ShapeLayer04)
            };

            // Custom layers follow the same visibility rules
            foreach (var custom in _customBoxes) shapes.Add((custom.Key, custom.Value.Box));

            bool gameSelected = _viewModel.IsGameSelected;
            foreach (var (id, box) in shapes)
            {
                string mode = _shapeVisibilityModes.TryGetValue(id, out var m) ? m : Models.LayerRegistry.DefaultVisibilityMode;
                bool show = mode switch
                {
                    Models.LayerRegistry.VisibilityGameSelected => gameSelected,
                    Models.LayerRegistry.VisibilityNoGameSelected => !gameSelected,
                    Models.LayerRegistry.VisibilityNever => false,
                    _ => true
                };
                box.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // Builds (or rebuilds, only when the file changed) the image or video inside one shape's host Grid.
        private void ApplyShapeFile(string id, Grid host, string storedPath)
        {
            string? fullPath = ResolveCustomImagePath(storedPath);

            if (_shapeFileElements.TryGetValue(id, out var existing))
            {
                if (existing.Path == fullPath) return;
                ReleaseShapeFile(id, host);
            }

            if (fullPath == null) return;

            var element = CreateCustomLayerImage(fullPath);
            if (element == null) return;

            // Shape layers stretch to fill (custom layers crop to fill)
            if (element is Image image) image.Stretch = Stretch.Fill;

            host.Children.Add(element);
            _shapeFileElements[id] = (element, fullPath);
        }

        private void ReleaseShapeFile(string id, Grid host)
        {
            if (!_shapeFileElements.TryGetValue(id, out var entry)) return;

            if (entry.Element is MediaElement video)
            {
                video.Stop();
                video.Source = null;
                video.Close();
            }
            else if (entry.Element is Image image)
            {
                image.ClearValue(Image.SourceProperty);
                image.Source = null;
            }

            host.Children.Remove(entry.Element);
            _shapeFileElements.Remove(id);
        }

        // Releases every shape layer's file so nothing stays open (called just before a theme is re-extracted).
        private void ReleaseAllShapeFiles()
        {
            ReleaseShapeFile(Models.LayerRegistry.ShapeLayer01, ShapeLayer01Host);
            ReleaseShapeFile(Models.LayerRegistry.ShapeLayer02, ShapeLayer02Host);
            ReleaseShapeFile(Models.LayerRegistry.ShapeLayer03, ShapeLayer03Host);
            ReleaseShapeFile(Models.LayerRegistry.ShapeLayer04, ShapeLayer04Host);
        }
        // [END SECTION: Shape Layers]

        // Custom layers: one bordered box (fill, border, corner radius) per layer with a host Grid for the
        // image or video, created on demand and kept on LayoutCanvas. Same look and rules as the shape layers.
        private readonly Dictionary<string, (Border Box, Border Fill, Grid Host, Border Ring)> _customBoxes = new();

        // Creates, places and styles the custom layer boxes from their entries (same ring structure as the shape layers),
        // dropping boxes whose layer no longer exists at this resolution.
        private void ApplyCustomLayers(Models.ThemeLayout layout)
        {
            var wanted = layout.Layers.Where(e => Models.LayerRegistry.IsCustomId(e.Id)).ToList();

            // Drop boxes for layers that no longer exist at this resolution
            foreach (var staleId in _customBoxes.Keys.Where(id => wanted.All(e => e.Id != id)).ToList())
            {
                var stale = _customBoxes[staleId];
                ReleaseShapeFile(staleId, stale.Host);
                LayoutCanvas.Children.Remove(stale.Box);
                _customBoxes.Remove(staleId);
                _shapeVisibilityModes.Remove(staleId);
            }

            foreach (var entry in wanted)
            {
                // Custom layer box: the positioned outer Border holding the fill, the file host and the ring on top
                if (!_customBoxes.TryGetValue(entry.Id, out var parts))
                {
                    var fill = new Border();
                    var host = new Grid();
                    var ring = new Border { Background = Brushes.Transparent };
                    var content = new Grid();
                    content.Children.Add(fill);
                    content.Children.Add(host);
                    content.Children.Add(ring);
                    var box = new Border { IsHitTestVisible = false, ClipToBounds = true, Child = content };
                    LayoutCanvas.Children.Add(box);
                    parts = (box, fill, host, ring);
                    _customBoxes[entry.Id] = parts;
                }

                PlaceOutsideBorder(parts.Box, entry, entry.BorderSize ?? Models.LayerRegistry.DefaultShapeBorderSize);
                Panel.SetZIndex(parts.Box, entry.Z);
                ApplyRingBox(entry, parts.Fill, parts.Host, parts.Ring);

                ApplyShapeFile(entry.Id, parts.Host, entry.ImagePath);
                _shapeVisibilityModes[entry.Id] = entry.VisibilityMode ?? Models.LayerRegistry.DefaultVisibilityMode;
            }

            UpdateShapeVisibility();
        }

        // Stops and removes every custom layer element so no file stays open. Called just before LoadTheme
        // deletes and re-extracts the theme's Extracted folder. The next ApplyLayout recreates whatever
        // the new theme needs.
        public void ReleaseCustomLayers()
        {
            // The Random Game Display layer's file is held open the same way
            ReleaseRandomGameDisplayFile();

            // ...and so are the shape layers' files
            ReleaseAllShapeFiles();

            // Custom layers: release each box's file and remove the box. The next ApplyLayout recreates
            // whatever the new theme needs.
            foreach (var custom in _customBoxes.ToList())
            {
                ReleaseShapeFile(custom.Key, custom.Value.Host);
                LayoutCanvas.Children.Remove(custom.Value.Box);
                _shapeVisibilityModes.Remove(custom.Key);
            }
            _customBoxes.Clear();
        }

        // [SECTION: Random Game Display Rect]
        // The Random Game Display layer has two rects: one used when the die is clicked with a game selected,
        // one used when it's clicked on the splash page (no game selected). Which one applies is decided at
        // the moment of the click and stays fixed for the whole spin, so the animation can't jump when the
        // winner lands. While the Theme Builder's card preview is on, the preview's chosen state is used instead.
        private bool _randomSpinFromSplash;

        // Session-only card preview from the Theme Builder (Random Game Display group): shows the card as a spin
        // would, without a spin. Never stored; a real spin turns it off first.
        private bool _randomCardPreview;
        private bool _randomPreviewFromSplash;
        private bool RandomDisplayFromSplash => _randomCardPreview ? _randomPreviewFromSplash : _randomSpinFromSplash;

        private void ApplyRandomGameDisplayRect(Models.ThemeLayout layout)
        {
            string id = RandomDisplayFromSplash ? Models.LayerRegistry.RandomGameDisplaySplash : Models.LayerRegistry.RandomGameDisplay;
            var entry = layout.Layers.FirstOrDefault(e => e.Id == id);
            if (entry == null) return;

            // The state's Width / Height are the card's content size and X / Y the ring's outer corner (this state's border)
            PlaceOutsideBorder(RandomGameDisplayLayer, entry, entry.BorderSize ?? 0);
            Panel.SetZIndex(RandomGameDisplayLayer, entry.Z);
            // The preview ignores the part's Hide flag
            RandomGameDisplayLayer.Visibility = (entry.Visible || _randomCardPreview) ? Visibility.Visible : Visibility.Collapsed;

            // The card's background color and border follow the same state pick
            _viewModel.RandomDisplaySplashState = RandomDisplayFromSplash;
            ApplyRandomGameDisplayCardSize(layout, entry);
        }

        // Turns the Theme Builder's card preview on (in the chosen state) or off. On: the card shows exactly as a spin
        // would draw it (rect, background, border, text) with a card video paused on its first frame; the preview video,
        // Splash Image and spin state are left alone. Off: the card hides again. Ignored while a real spin runs.
        public void SetRandomCardPreview(bool on, bool fromSplash)
        {
            if (_viewModel == null || _viewModel.IsRandomizerSpinning) return;

            _randomCardPreview = on;
            _randomPreviewFromSplash = fromSplash;

            // A local value beats the spin's DataTrigger; clearing it hands visibility back to the trigger
            if (on) RandomSpinContent.Visibility = Visibility.Visible;
            else RandomSpinContent.ClearValue(VisibilityProperty);

            ApplyRandomGameDisplayRect();
            UpdateRandomGameDisplayPlayback();
        }

        // Session-only banner preview from the Theme Builder (Marquee layer, BANNER tab): shows the no-art title card
        // regardless of MarqueeMissing, with the logo and the marquee image hidden so a translucent background shows the
        // panel behind. Local Visibility values beat the style triggers; clearing them hands visibility back.
        private bool _bannerPreview;

        // Turns the banner preview on or off. Selection and media state are untouched. Ignored while a real spin runs.
        public void SetBannerPreview(bool on)
        {
            if (_viewModel == null || _viewModel.IsRandomizerSpinning) return;

            _bannerPreview = on;
            if (on)
            {
                NoMarqueeBanner.Visibility = Visibility.Visible;
                MarqueeLogoImage.Visibility = Visibility.Collapsed;
                MarqueeDisplay.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoMarqueeBanner.ClearValue(VisibilityProperty);
                MarqueeLogoImage.ClearValue(VisibilityProperty);
                MarqueeDisplay.ClearValue(VisibilityProperty);
            }
        }

        // Ends the banner preview because a real spin is starting, and tells the Theme Builder so its checkbox unchecks
        private void EndBannerPreviewForSpin()
        {
            if (!_bannerPreview) return;

            SetBannerPreview(false);
            _themeBuilderWindow?.BannerPreviewEndedBySpin();
        }

        // Ends the card preview because a real spin is starting, and tells the Theme Builder so its checkbox unchecks
        private void EndRandomCardPreviewForSpin()
        {
            if (!_randomCardPreview) return;

            SetRandomCardPreview(false, false);
            _themeBuilderWindow?.RandomCardPreviewEndedBySpin();
        }

        // Inner padding between the card's content edge and the message (the Viewbox's margin, measured from the visible edge)
        private const double RandomSpinTextPadding = 10;

        // Sets the parts of the spin card that depend on the active state, with the Video Preview rule: the card (fill,
        // file, message) pulls in by this state's border minus 1px, so it reaches 1px under the ring, and its corner
        // radius and the file's rounded clip shrink by the same inset. Also this state's max text size and the wrapped
        // text width (the content width minus the padding). Only the wrap flag is shared, read from the RandomGameDisplay entry.
        private void ApplyRandomGameDisplayCardSize(Models.ThemeLayout layout, Models.LayerLayoutEntry stateEntry)
        {
            var style = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.RandomGameDisplay) ?? stateEntry;
            double defaultSize = stateEntry.Id == Models.LayerRegistry.RandomGameDisplaySplash
                ? Models.LayerRegistry.DefaultRandomDisplaySplashFontSize
                : Models.LayerRegistry.DefaultRandomDisplayFontSize;
            RandomSpinText.FontSize = stateEntry.TextFontSize ?? defaultSize;

            double borderSize = Math.Max(0, stateEntry.BorderSize ?? 0);
            double radius = Math.Max(0, stateEntry.CornerRadius ?? Models.LayerRegistry.DefaultShapeCornerRadius);
            double inset = Math.Max(0, borderSize - 1);
            double overlap = borderSize - inset;
            double innerRadius = Math.Max(0, radius - inset);

            RandomSpinCard.Margin = new Thickness(inset);
            RandomSpinCard.CornerRadius = new CornerRadius(innerRadius);

            // The padding counts from the visible content edge, so the Viewbox also skips the 1px under the ring
            RandomSpinTextBox.Margin = new Thickness(RandomSpinTextPadding + overlap);

            bool wrap = style.TextWrap ?? Models.LayerRegistry.DefaultRandomDisplayWrap;
            RandomSpinText.Width = wrap ? Math.Max(1, stateEntry.Width - (RandomSpinTextPadding * 2)) : double.NaN;

            var clip = new RectangleGeometry(new Rect(0, 0, stateEntry.Width + (overlap * 2), stateEntry.Height + (overlap * 2)), innerRadius, innerRadius);
            clip.Freeze();
            RandomSpinFileHost.Clip = clip;
        }

        // Same layout resolution ApplyLayout uses - lets the die click re-apply just this layer's rect.
        private void ApplyRandomGameDisplayRect()
        {
            if (_viewModel == null) return;

            ApplyRandomGameDisplayRect(_viewModel.ResolveDisplayLayout());
        }
        // [END SECTION: Random Game Display Rect]

        // [SECTION: Random Game Display File]
        // The Random Game Display layer can show a file (image, gif/APNG or mp4) during a spin, drawn over the card's
        // background color and under its message. Built with the same loader as the custom layers; rebuilt only when
        // the layer's file changes. A video is kept stopped until a spin starts (see UpdateRandomGameDisplayPlayback).
        private string? _randomDisplayFilePath;
        private FrameworkElement? _randomDisplayElement;

        private void ApplyRandomGameDisplayFile(Models.ThemeLayout layout)
        {
            var entry = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.RandomGameDisplay);
            string? fullPath = entry == null ? null : ResolveCustomImagePath(entry.ImagePath);

            if (fullPath == _randomDisplayFilePath) return;

            ReleaseRandomGameDisplayFile();
            _randomDisplayFilePath = fullPath;
            if (fullPath == null) return;

            var element = CreateCustomLayerImage(fullPath);
            if (element == null) return;

            if (element is MediaElement video)
            {
                video.Stop();
                // Unsupported codec: hide the file, the background and message still show
                video.MediaFailed += (s, e) => RandomSpinFileHost.Visibility = Visibility.Collapsed;
            }

            RandomSpinFileHost.Children.Add(element);
            RandomSpinFileHost.Visibility = Visibility.Visible;
            _randomDisplayElement = element;

            // A file picked while the card preview is on shows its still frame straight away
            UpdateRandomGameDisplayPlayback();
        }

        // Stops and removes the layer's file so nothing stays open (the card's background and message stay).
        public void ReleaseRandomGameDisplayFile()
        {
            if (_randomDisplayElement is MediaElement video)
            {
                video.Stop();
                video.Source = null;
                video.Close();
            }
            else if (_randomDisplayElement is Image image)
            {
                image.ClearValue(Image.SourceProperty);
                image.Source = null;
            }

            if (_randomDisplayElement != null) RandomSpinFileHost.Children.Remove(_randomDisplayElement);

            _randomDisplayElement = null;
            _randomDisplayFilePath = null;
            RandomSpinFileHost.Visibility = Visibility.Collapsed;
        }

        // A video starts from the beginning each time a spin starts, and stops when it ends. During the card preview
        // it shows its first frame, paused.
        private void UpdateRandomGameDisplayPlayback()
        {
            if (_randomDisplayElement is not MediaElement video) return;

            if (_viewModel.IsRandomizerSpinning)
            {
                video.Position = TimeSpan.Zero;
                video.Play();
            }
            else if (_randomCardPreview)
            {
                video.ScrubbingEnabled = true; // so the paused frame actually renders
                video.Pause();
                video.Position = TimeSpan.Zero;
            }
            else
            {
                video.Stop();
            }
        }
        // [END SECTION: Random Game Display File]

        // Relative paths resolve against the app folder (same as the other theme assets). Null if the file is missing.
        private string? ResolveCustomImagePath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return null;

            try
            {
                string full = System.IO.Path.IsPathRooted(storedPath)
                    ? storedPath
                    : System.IO.Path.GetFullPath(System.IO.Path.Combine(_viewModel.Configuration.BaseDirectory, storedPath));
                return File.Exists(full) ? full : null;
            }
            catch { return null; }
        }

        // Animated gif/APNG go through AnimatedImage.Wpf (built from XAML so no extra namespace is needed in
        // code); static png/jpg load as a normal frozen bitmap read fully into memory so the file isn't locked.
        private static FrameworkElement? CreateCustomLayerImage(string fullPath)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(fullPath).ToLowerInvariant();

                // Video: muted, looping, stretched to fill the layer box so the whole picture always shows
                if (ext == ".mp4")
                {
                    var video = new MediaElement
                    {
                        LoadedBehavior = MediaState.Manual,
                        UnloadedBehavior = MediaState.Manual,
                        IsMuted = true,
                        Stretch = Stretch.Fill,
                        IsHitTestVisible = false,
                        Source = new Uri(fullPath, UriKind.Absolute)
                    };
                    // MediaElement has no built-in repeat, so rewind and keep playing (same as the wallpaper video)
                    video.MediaEnded += (s, e) => { video.Position = TimeSpan.Zero; video.Play(); };
                    // Unsupported codec: hide the layer instead of showing a black or frozen box
                    video.MediaFailed += (s, e) => { video.Stop(); video.Visibility = Visibility.Collapsed; };
                    video.Play();
                    return video;
                }

                bool animated = ext == ".gif" || (ext == ".png" && IsAnimatedPngFile(fullPath));

                if (animated)
                {
                    string uri = System.Security.SecurityElement.Escape(new Uri(fullPath, UriKind.Absolute).AbsoluteUri) ?? "";
                    string xaml =
                        "<Image xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
                        "xmlns:anim=\"https://github.com/whistyun/AnimatedImage.Wpf\" " +
                        "Stretch=\"UniformToFill\" IsHitTestVisible=\"False\" ClipToBounds=\"True\" " +
                        $"anim:ImageBehavior.AnimatedSource=\"{uri}\" anim:ImageBehavior.RepeatBehavior=\"Forever\"/>";
                    return (Image)System.Windows.Markup.XamlReader.Parse(xaml);
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(File.ReadAllBytes(fullPath));
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                return new Image { Source = bitmap, Stretch = Stretch.UniformToFill, IsHitTestVisible = false, ClipToBounds = true };
            }
            catch { return null; }
        }

        // An APNG carries an "acTL" chunk near the start of the file, before the first image data
        private static bool IsAnimatedPngFile(string fullPath)
        {
            try
            {
                using var stream = File.OpenRead(fullPath);
                var buffer = new byte[1024];
                int read = stream.Read(buffer, 0, buffer.Length);
                string head = System.Text.Encoding.ASCII.GetString(buffer, 0, read);
                int actl = head.IndexOf("acTL", StringComparison.Ordinal);
                int idat = head.IndexOf("IDAT", StringComparison.Ordinal);
                return actl >= 0 && (idat < 0 || actl < idat);
            }
            catch { return false; }
        }
        // [END SECTION: Custom Image Layers]
        // [END SECTION: Layout Application]

        // [SECTION: Gamepad Input Handling]
        // Subscribes to WGIService events and routes directional/button input into tree navigation and launch actions.
        private void InitializeGamepadInput()
        {
            _gamepadService = new WGIService(_viewModel.Configuration);

            _gamepadService.GamepadDirectionTriggered += direction =>
            {
                if (_isOptionsWindowOpen) return;

                if (direction == "Up") HandleGamepadMovement(GamepadAction.Up);
                else if (direction == "Down") HandleGamepadMovement(GamepadAction.Down);
                else if (direction == "Left") HandleGamepadButton(GamepadAction.Left);
                else if (direction == "Right") HandleGamepadButton(GamepadAction.Right);
            };

            _gamepadService.GamepadButtonDownTriggered += button =>
            {
                if (_isOptionsWindowOpen) return;

                if (button == Windows.Gaming.Input.GamepadButtons.A)
                {
                    HandleGamepadButton(GamepadAction.Select);
                }
                else if (button == Windows.Gaming.Input.GamepadButtons.Y)
                {
                    if (_viewModel.SelectedGame != null)
                    {
                        Dispatcher.BeginInvoke(new Action(() => _viewModel.ToggleFavorite(_viewModel.SelectedGame)));
                    }
                }
                else if (button == Windows.Gaming.Input.GamepadButtons.Menu)
                {
                    Dispatcher.BeginInvoke(new Action(() => TriggerActiveSelectionLaunch()));
                }
            };

            _gamepadService.StartPollingLoop();
        }

        // Handles Up/Down gamepad movement by walking the flattened visible tree rows
        private void HandleGamepadMovement(GamepadAction action)
        {
            Dispatcher.Invoke(() =>
            {
                if (action == GamepadAction.Up) NavigateTreeRowsFlat(-1);
                else if (action == GamepadAction.Down) NavigateTreeRowsFlat(1);
            });
        }

        // Handles Select/Back gamepad button presses: expand/collapse a folder or launch the selected game
        private void HandleGamepadButton(GamepadAction action)
        {
            Dispatcher.Invoke(() =>
            {
                if (action == GamepadAction.Back)
                {
                    Close();
                }
                else if (action == GamepadAction.Select)
                {
                    if (GameTree.SelectedItem != null)
                    {
                        var selectedContainer = FindTreeViewItemContainer(GameTree, GameTree.SelectedItem);

                        if (selectedContainer != null && selectedContainer.HasItems)
                        {
                            if (selectedContainer.DataContext is Models.TreeCategoryNode toggledNode)
                                toggledNode.IsNodeExpanded = !toggledNode.IsNodeExpanded;
                            else
                                selectedContainer.IsExpanded = !selectedContainer.IsExpanded;
                        }
                        else if (_viewModel.SelectedGame != null)
                        {
                            TriggerActiveSelectionLaunch();
                        }
                    }
                }
            });
        }
        // [END SECTION: Gamepad Input Handling]

        // [SECTION: TreeView Navigation / Flattening]
        // Builds a flat list of currently visible TreeViewItems (respecting expand/collapse state) to
        // support linear Up/Down gamepad navigation across nested folders and games.
        private void NavigateTreeRowsFlat(int offset)
        {
            var visibleContainers = new List<TreeViewItem>();

            foreach (var item in GameTree.Items)
            {
                var rootContainer = GameTree.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (rootContainer != null)
                {
                    BuildFlatVisibleTreeList(rootContainer, visibleContainers);
                }
            }

            if (visibleContainers.Count == 0) return;

            int currentIndex = -1;
            for (int i = 0; i < visibleContainers.Count; i++)
            {
                if (visibleContainers[i].IsSelected)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = currentIndex + offset;
            if (currentIndex == -1) nextIndex = 0;

            if (nextIndex >= 0 && nextIndex < visibleContainers.Count)
            {
                var targetContainer = visibleContainers[nextIndex];
                targetContainer.IsSelected = true;
                targetContainer.Focus();

                if (targetContainer.DataContext is Models.GameItem selectedGame)
                {
                    _viewModel.SelectedGame = selectedGame;
                }
                else
                {
                    _viewModel.SelectedGame = null;
                }
            }
        }

        // Recursively appends a container and its expanded children to the flat visible-rows list
        private void BuildFlatVisibleTreeList(TreeViewItem container, List<TreeViewItem> flatList)
        {
            flatList.Add(container);

            if (container.IsExpanded)
            {
                foreach (var item in container.Items)
                {
                    var parsedContainer = container.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                    if (parsedContainer != null)
                    {
                        BuildFlatVisibleTreeList(parsedContainer, flatList);
                    }
                }
            }
        }
        // [END SECTION: TreeView Navigation / Flattening]

        // [SECTION: Video Preview & Media Cleanup]
        // Manages the FFmpeg preview player's visibility and playback state as game selection changes.
        // NOTE: known beta trade-off - preview stays black after returning from MAME until reselection.

        // Fired whenever the ViewModel's SelectedGame/VideoSourcePath actually resolves (post-debounce) -
        // starts/stops video preview here rather than in GameTree_SelectedItemChanged, so rapid gamepad
        // scrolling doesn't start a video load on every intermediate step, only the final selection.
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Shape layers set to "only when a game is selected / not selected" follow the selection
            if (e.PropertyName == nameof(_viewModel.IsGameSelected))
            {
                UpdateShapeVisibility();
            }

            if (e.PropertyName == nameof(_viewModel.VideoSourcePath))
            {
                if (_viewModel.SelectedGame != null && !string.IsNullOrEmpty(_viewModel.VideoSourcePath))
                {
                    StartActivePreview(_viewModel.VideoSourcePath);
                }
                else
                {
                    StopActivePreview();
                }
            }

            if (e.PropertyName == nameof(_viewModel.IsGameSelected) || e.PropertyName == nameof(_viewModel.IsBootSplashVideo))
            {
                RefreshBootSplashVideo();
            }

            if (e.PropertyName == nameof(_viewModel.IsRandomizerSpinning))
            {
                UpdateRandomGameDisplayPlayback();
            }

        }

        // Starts/stops the boot splash mp4 based on current selection state and whether the resolved splash asset is a video
        private void RefreshBootSplashVideo()
        {
            var splash = SplashLayer.BootSplashVideoPreview;

            if (!_viewModel.IsGameSelected && _viewModel.IsBootSplashVideo && !string.IsNullOrEmpty(_viewModel.ThemeBootSplashVideoPath))
            {
                // Skip the reload if the correct video is already loaded - RefreshThemeBindings()
                // re-raises IsBootSplashVideo on every Options close regardless of whether anything
                // actually changed, and restarting the loop each time would be a visible hiccup.
                if (_currentBootSplashPath == _viewModel.ThemeBootSplashVideoPath && splash.Source != null) return;

                splash.Stop();
                splash.Source = new Uri(_viewModel.ThemeBootSplashVideoPath, UriKind.Absolute);
                splash.Play();
                _currentBootSplashPath = _viewModel.ThemeBootSplashVideoPath;
            }
            else
            {
                splash.Stop();
                splash.Source = null;
                _currentBootSplashPath = null;
            }
        }

        // True while the random-winner tree jump runs (ExpandAndSelectGameInMainTree). The jump collapses
        // folders, which destroys the previously selected row's container and fires a null selection - that
        // would clear the winner's SelectedGame for a moment and flash the default logo, so null selections
        // are ignored until the winner's row is selected.
        private bool _isJumpingToRandomWinner;

        // Fired when the TreeView selection changes; just updates SelectedGame - video preview playback
        // is handled separately in ViewModel_PropertyChanged, once VideoSourcePath actually resolves.
        private void GameTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_viewModel != null)
            {
                // Skip a null selection while a rebuild is in progress - TreeNodesCollection.Clear()
                // destroying the old row's container fires this as a side effect, not a genuine user
                // deselection. Letting it through would transiently null SelectedGame/VideoSourcePath,
                // stopping the preview player and starting the boot splash player almost simultaneously.
                if (e.NewValue == null && _viewModel.IsRebuildingTree) return;

                // While the random-winner jump runs, collapsing a folder makes WPF select that folder (not null),
                // which would set SelectedGame to null and flash the default logo. Ignore anything that isn't
                // a game until the winner's row is selected.
                if (_isJumpingToRandomWinner && e.NewValue is not Models.GameItem) return;

                _viewModel.SelectedGame = e.NewValue as Models.GameItem;
            }
        }

        // StartActivePreview / StopActivePreview drive the FFmpeg preview player (see the FFmpeg Preview Player section below).

        // Re-evaluates and restarts the video preview for the currently selected game (called after returning from a game launch)
        private void RefreshVideoPreview()
        {
            if (_viewModel.SelectedGame != null && !string.IsNullOrEmpty(_viewModel.VideoSourcePath))
            {
                StartActivePreview(_viewModel.VideoSourcePath);
            }
            else
            {
                StopActivePreview();
            }
        }

        // [SECTION: FFmpeg Preview Player]
        // Game-preview video plays through the FFmpeg player (PreviewPlayerHost), drawing into the Image in
        // VideoPreviewLayer. It loops on its own and is muted. If the runtime failed to load the host does
        // nothing, so the slot just stays empty.
        private void StartActivePreview(string path)
        {
            if (_previewPlayer == null || !_previewPlayer.IsAvailable) return;

            // A theme can hide the Video Preview layer; don't decode video nobody can see.
            if (VideoPreviewLayer.Visibility != Visibility.Visible) return;

            UpdatePreviewTargetSize();
            VideoPreviewLayer.PreviewVideoSurface.Visibility = Visibility.Visible;
            _previewPlayer.Play(path);
        }

        // Stops the preview player and hides its surface.
        private void StopActivePreview()
        {
            StopMediaElementPreview();
        }

        private void StopMediaElementPreview()
        {
            _previewPlayer?.Stop();
            VideoPreviewLayer.PreviewVideoSurface.Visibility = Visibility.Collapsed;
        }

        // Tells the decoder how many pixels the Video Preview's media slot occupies (from the layer entry,
        // scaled by the screen DPI and Smart fullscreen's factor), so the picture is decoded at the size it is shown at.
        private void UpdatePreviewTargetSize()
        {
            if (_previewPlayer == null) return;

            // The entry's Width / Height are the video area and the border sits outside it. The media slot reaches 1px under the
            // ring on each side when there is a border (same rule as VideoPreviewLayerControl.ApplyClip), so decode to that size.
            var resolvedLayout = _viewModel.ResolveDisplayLayout();
            var videoEntry = resolvedLayout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.VideoPreview);
            if (videoEntry == null) return;
            double videoBorderSize = Math.Max(0, videoEntry.BorderSize ?? 0);
            double overlap = videoBorderSize - Math.Max(0, videoBorderSize - 1);
            double width = videoEntry.Width + (overlap * 2);
            double height = videoEntry.Height + (overlap * 2);
            if (double.IsNaN(width) || double.IsNaN(height) || width < 1 || height < 1) return;

            var dpi = VisualTreeHelper.GetDpi(this);
            _previewPlayer.SetTargetSize((int)Math.Round(width * dpi.DpiScaleX * _layoutScaleFactor), (int)Math.Round(height * dpi.DpiScaleY * _layoutScaleFactor));
        }
        // [END SECTION: FFmpeg Preview Player]

        // Sets the idle/boot-splash media state at startup (no game selected, no video playing)
        // Stops and hides the video preview right as a spin begins, so the previous game's video doesn't
        // keep playing under an opaque spin card. A spin from the game-selected state whose card background is
        // translucent leaves it playing, since it shows through (the player is always muted, and the winner's
        // video swaps in through the normal Play call). VideoPreview's visibility is otherwise owned entirely by
        // ViewModel_PropertyChanged/RefreshVideoPreview/etc., so this only ever hides it - the normal
        // selection-change pipeline is what reveals it again once a winner is set (or leaves it hidden if
        // the winner has none).
        private void StopVideoPreviewForSpin()
        {
            if (!_randomSpinFromSplash && IsRandomDisplayBackgroundTranslucent()) return;
            StopActivePreview();
            _previewStoppedForSpin = true;
        }

        // True from a spin's preview stop until the spin lands (see RestartPreviewAfterSpin)
        private bool _previewStoppedForSpin;

        // Restarts the preview when the spin stopped it and the winner's video did not start on its own: the winner
        // is the game already selected, so VideoSourcePath did not change and nothing called Play. A different
        // winner has already started (or correctly cleared) its video, so the surface check leaves it alone.
        private void RestartPreviewAfterSpin()
        {
            if (!_previewStoppedForSpin) return;
            _previewStoppedForSpin = false;

            if (VideoPreviewLayer.PreviewVideoSurface.Visibility != Visibility.Visible) RefreshVideoPreview();
        }

        // Does the check for a see-through card: true when the game-selected part's background color alpha is below 255
        private bool IsRandomDisplayBackgroundTranslucent()
        {
            var layout = _viewModel.ResolveDisplayLayout();
            string hex = layout.Layers.FirstOrDefault(e => e.Id == Models.LayerRegistry.RandomGameDisplay)?.BgColorHex
                ?? Models.LayerRegistry.DefaultRandomDisplayBgColorHex;

            try { return ((Color)ColorConverter.ConvertFromString(hex)).A < 255; }
            catch { return false; }
        }

        private void ApplyDefaultMedia()
        {
            StopMediaElementPreview();
            RefreshBootSplashVideo();
        }
        // [END SECTION: Video Preview & Media Cleanup]

        // [SECTION: Mouse Double-Click Launch]
        // Double-clicking a game row launches it; double-clicking a folder row toggles expand/collapse.
        private void GameTree_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var rowContainer = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);

            if (rowContainer != null)
            {
                if (rowContainer.DataContext is Models.GameItem clickedGame)
                {
                    e.Handled = true;
                    _viewModel.SelectedGame = clickedGame;
                    TriggerActiveSelectionLaunch();
                }
                else if (rowContainer.DataContext is Models.TreeCategoryNode clickedNode)
                {
                    e.Handled = true;
                    clickedNode.IsNodeExpanded = !clickedNode.IsNodeExpanded;
                    rowContainer.Focus();
                }
            }
        }

        // Walks up the visual tree from a click/event source to find the nearest ancestor of type T
        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        // Walks down the visual tree from a container (e.g. GameTree itself) to find the first
        // descendant of type T - used to locate the TreeView's own internal ScrollViewer, which isn't
        // exposed as a named element in XAML the way GameRowBorder etc. are.
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
        // [END SECTION: Mouse Double-Click Launch]

        // [SECTION: Game Row Context Menu]
        // Right-clicking a game row selects it first (consistent with how Ctrl+F/Ctrl+M already operate
        // on _viewModel.SelectedGame) before the ContextMenu opens.
        private void GameRowBorder_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var rowContainer = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (rowContainer != null)
            {
                rowContainer.IsSelected = true;
                rowContainer.Focus();
            }
        }

        // [SECTION: Manual Tooltip Delay]
        // Starts (or restarts) the 1-second timer on entry - Stop()+Start() rather than a bare Start()
        // so re-entering the same element before the timer fires resets the full delay rather than
        // resuming a partially-elapsed one. Shared by any element with ToolTipService.IsEnabled="False"
        // and a manually-assigned ToolTip (game rows previously, now also the top action buttons).
        private void DelayedTooltip_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not FrameworkElement element || element.ToolTip is not System.Windows.Controls.ToolTip) return;

            _pendingTooltipTarget = element;
            _delayedTooltipTimer.Stop();
            _delayedTooltipTimer.Start();
        }

        private void DelayedTooltipTimer_Tick(object? sender, EventArgs e)
        {
            _delayedTooltipTimer.Stop();
            if (_pendingTooltipTarget?.ToolTip is System.Windows.Controls.ToolTip tooltip)
            {
                // ToolTipService normally sets this automatically when it opens a tooltip - since we're
                // opening it manually (bypassing ToolTipService, which is disabled on the target
                // element), that inheritance never happens on its own, leaving any bindings inside blank.
                tooltip.DataContext = _pendingTooltipTarget.DataContext;
                tooltip.IsOpen = true;
            }
        }

        // Leaving an element always cancels the pending timer AND force-closes an already-open tooltip
        // immediately - no lingering fade/delay on exit, matching the "reset every time" behavior.
        private void DelayedTooltip_MouseLeave(object sender, MouseEventArgs e)
        {
            _delayedTooltipTimer.Stop();
            if (sender is FrameworkElement element && element.ToolTip is System.Windows.Controls.ToolTip tooltip)
            {
                tooltip.IsOpen = false;
            }
            _pendingTooltipTarget = null;
        }
        // [END SECTION: Manual Tooltip Delay]

        // [SECTION: ROM Copy Status Tooltip]
        // Shows a transient status message (success or failure) near the selected game row after
        // Ctrl+V. Game rows no longer carry their own hover ToolTip, so there's nothing to collide
        // with - this builds a fresh ToolTip in code, anchors it to the row's TreeViewItem container,
        // opens it immediately (no hover-intent delay needed - Ctrl+V already signals intent), and
        // auto-closes it after 2 seconds via _romCopyStatusTimer.
        private void ShowRomCopyStatusTooltip(string message)
        {
            _romCopyStatusTimer.Stop();
            if (_romCopyStatusTooltip != null)
            {
                _romCopyStatusTooltip.IsOpen = false;
                _romCopyStatusTooltip = null;
            }

            if (_viewModel.SelectedGame == null) return;
            var targetContainer = FindTreeViewItemContainer(GameTree, _viewModel.SelectedGame);
            if (targetContainer == null) return;

            // Built in code, so Smart fullscreen's scale is set on it directly rather than trusting the implicit ToolTip style
            _romCopyStatusTooltip = new System.Windows.Controls.ToolTip
            {
                Content = message,
                PlacementTarget = targetContainer,
                Placement = PlacementMode.Bottom,
                LayoutTransform = (Transform)FindResource(PopupScaleKey),
                UseLayoutRounding = (bool)FindResource(PopupLayoutRoundingKey),
                IsOpen = true
            };

            _romCopyStatusTimer.Start();
        }

        private void RomCopyStatusTimer_Tick(object? sender, EventArgs e)
        {
            _romCopyStatusTimer.Stop();
            if (_romCopyStatusTooltip != null)
            {
                _romCopyStatusTooltip.IsOpen = false;
                _romCopyStatusTooltip = null;
            }
        }
        // [END SECTION: ROM Copy Status Tooltip]

        // Walks up from the right-clicked game row past its own TreeViewItem container to find the
        // parent TreeViewItem - i.e. the TreeCategoryNode this row is actually being displayed under
        // right now (Favorites, a custom folder, a virtual category, GAMES, or Search Results). This
        // can't be resolved by searching the data tree for containment, since the same GameItem instance
        // can legitimately appear under multiple nodes at once.
        private static Models.TreeCategoryNode? FindContainingNode(DependencyObject? source)
        {
            var gameContainer = FindVisualParent<TreeViewItem>(source);
            if (gameContainer == null) return null;

            var parentContainer = FindVisualParent<TreeViewItem>(VisualTreeHelper.GetParent(gameContainer));
            return parentContainer?.DataContext as Models.TreeCategoryNode;
        }

        // Determines whether "Remove from Folder" should be enabled: only when the right-clicked row is
        // currently viewed inside Favorites or inside a custom folder. Disabled everywhere else
        // (virtual/catver categories, GAMES root bucket, Search Results). The resolved on-disk folder
        // name is stashed in the MenuItem's Tag for RemoveFromFolderMenuItem_Click to use.
        private void GameRowContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;

            // x:Name'd elements inside a ContextMenu defined in TreeView.Resources live in that
            // ContextMenu's own local NameScope - they're never promoted to fields on this partial
            // class, so the target MenuItem has to be resolved from the menu's Items at runtime instead.
            if (menu.Items.OfType<MenuItem>().FirstOrDefault(mi => (string)mi.Header == "Remove from Folder") is not MenuItem removeItem) return;

            var containingNode = FindContainingNode(menu.PlacementTarget as DependencyObject);
            string? headerText = containingNode?.HeaderText;

            bool isFavorites = string.Equals(headerText, "FAVORITES", StringComparison.OrdinalIgnoreCase);
            string? matchedFolderName = (!isFavorites && headerText != null)
                ? _viewModel.GetCustomFolderNames().FirstOrDefault(f => f.Equals(headerText, StringComparison.OrdinalIgnoreCase))
                : null;

            removeItem.IsEnabled = isFavorites || matchedFolderName != null;
            removeItem.Tag = isFavorites ? "FAVORITES" : matchedFolderName;
        }

        // Rebuilds the "Add to Folder" submenu every time it's opened: Favorites + every existing custom
        // folder. Rebuilding on open (rather than caching) keeps it in sync with folders created/deleted
        // via ManagePlayListsWindow without needing extra invalidation plumbing.
        private void AddToFolderMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem submenuItem) return;

            submenuItem.Items.Clear();

            // Favorites' star icon deliberately has no local Foreground set, same as every other context
            // menu icon (e.g. "Create folder") - that lets it inherit the MenuItem template's
            // TextElement.Foreground (Global.Menu.Icon normally, Global.Menu.TextHover on
            // hover) instead of fighting it. A local Foreground value always wins over an inherited/
            // triggered one, which is exactly why the old hardcoded color here never responded to hover.
            var favoritesItem = new MenuItem
            {
                Header = "Favorites",
                Icon = new TextBlock
                {
                    Text = "\uE735",
                    FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                    FontSize = 14
                }
            };
            var selectedGameForFavorites = _viewModel.SelectedGame;
            // Deferred via Dispatcher.BeginInvoke: MenuItem.Click still fires from inside the ContextMenu
            // popup's own dismissal frame. Calling straight into AddGameToFavorites -> UpdateLiveTreeDisplay
            // -> the video preview stop from there could hang the UI thread (same class of issue as the
            // documented Options-close/splash-video collision, just a different popup boundary). Deferring
            // lets the popup fully tear down and control return to the normal message loop first.
            favoritesItem.Click += (s, args) =>
            {
                // Preserve the current scroll position across the add's UpdateLiveTreeDisplay rebuild - same
                // double-deferred restore pattern as the custom-folder items below, so nothing appears to move.
                var preRebuildScrollViewer = FindVisualChild<ScrollViewer>(GameTree);
                double preservedOffset = preRebuildScrollViewer?.VerticalOffset ?? 0;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _viewModel.AddGameToFavorites(selectedGameForFavorites);

                    // Two nested Loaded-priority hops, not one - with VirtualizingPanel Recycling mode,
                    // a single Loaded pass on a large tree fires before the panel's content extent is
                    // fully re-established after the rebuild, so an early ScrollToVerticalOffset lands
                    // against an incomplete extent (snapping toward the top). Same fix as
                    // RestoreGameListScrollPosition's proven 3-level pattern.
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var postRebuildScrollViewer = FindVisualChild<ScrollViewer>(GameTree);
                            postRebuildScrollViewer?.ScrollToVerticalOffset(preservedOffset);
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }));
            };
            submenuItem.Items.Add(favoritesItem);

            var customFolders = _viewModel.GetCustomFolderNames();
            if (customFolders.Count > 0)
            {
                

                foreach (var folderName in customFolders)
                {
                    var folderItem = new MenuItem
                    {
                        Header = folderName,
                        Icon = new TextBlock { Text = "\uE8B7", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14 }
                    };
                    var selectedGameForFolder = _viewModel.SelectedGame;
                    folderItem.Click += (s, args) =>
                    {
                        // Preserve the current scroll position across the add's UpdateLiveTreeDisplay rebuild -
                        // same double-deferred restore pattern as RestoreGameListScrollPosition, but restoring
                        // the raw offset instead of centering, since nothing should appear to move at all here.
                        var preRebuildScrollViewer = FindVisualChild<ScrollViewer>(GameTree);
                        double preservedOffset = preRebuildScrollViewer?.VerticalOffset ?? 0;

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _viewModel.AddGameToCustomFolder(selectedGameForFolder, folderName);

                            // Two nested Loaded-priority hops, not one - with VirtualizingPanel Recycling
                            // mode, a single Loaded pass on a large tree fires before the panel's content
                            // extent is fully re-established after the rebuild, so an early
                            // ScrollToVerticalOffset lands against an incomplete extent (snapping toward
                            // the top). Same fix as RestoreGameListScrollPosition's proven 3-level pattern.
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    var postRebuildScrollViewer = FindVisualChild<ScrollViewer>(GameTree);
                                    postRebuildScrollViewer?.ScrollToVerticalOffset(preservedOffset);
                                }), System.Windows.Threading.DispatcherPriority.Loaded);
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }));
                    };
                    submenuItem.Items.Add(folderItem);
                }
            }
        }

        // Removes the selected game from whichever folder it was right-clicked from (resolved and
        // stashed in RemoveFromFolderMenuItem.Tag by GameRowContextMenu_Opened). Only reachable when enabled.
        private void RemoveFromFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedGame == null) return;
            if (sender is not MenuItem clickedItem) return;
            string? folderName = clickedItem.Tag as string;
            if (string.IsNullOrEmpty(folderName)) return;

            // Deferred for the same reason as the "Add to Folder" submenu items above - Click still fires
            // from inside the ContextMenu popup's own dismissal frame, and the tree rebuild this triggers
            // stops the video preview.
            var selectedGame = _viewModel.SelectedGame;

            if (string.Equals(folderName, "FAVORITES", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.BeginInvoke(new Action(() => _viewModel.RemoveGameFromFavorites(selectedGame)));
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => _viewModel.RemoveGameFromCustomFolder(selectedGame, folderName)));
            }
        }

        // Reuses the same toggle logic Ctrl+M already calls. ToggleMouseSupport doesn't call
        // UpdateLiveTreeDisplay (it only flips GameItem.IsMouseSupported and persists to disk), so it
        // doesn't touch the video player - deferral isn't strictly required here, but it's applied anyway
        // for consistency with the other menu items on this same ContextMenu.
        private void ToggleRawMouseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedGame == null) return;
            var selectedGame = _viewModel.SelectedGame;
            Dispatcher.BeginInvoke(new Action(() => _viewModel.ToggleMouseSupport(selectedGame)));
        }

        private void GetArtworkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedGame == null) return;
            var selectedGame = _viewModel.SelectedGame;
            Dispatcher.BeginInvoke(new Action(() => _viewModel.FetchArtworkForGame(selectedGame)));
        }

        // Confirms first - there's currently no "Remove from exclusion list" menu item anywhere, so
        // undoing this today means manually editing rom_exclusion_list.cfg. Same reasoning as Delete
        // Folder's confirmation. Deferred for the same reentrancy reason as every other menu item that
        // triggers UpdateLiveTreeDisplay - Click still fires from inside the ContextMenu popup's own
        // dismissal frame.
        private void AddToExclusionListMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedGame == null) return;
            var selectedGame = _viewModel.SelectedGame;

            var result = MessageBox.Show(
                $"Add '{selectedGame.RomName}' to the ROM exclusion list?\n\nThis hides it from both folder structure modes (virtual/catver and your own custom folder structure), but only while \"Enable Base Filter\" is turned on in Sorting Options - it has no effect while that filter is off. There's currently no menu option to undo this - reversing it requires manually removing the entry from rom_exclusion_list.cfg.",
                "Confirm Add to Exclusion List",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            Dispatcher.BeginInvoke(new Action(() => _viewModel.AddGameToExclusionList(selectedGame)));
        }
        // [END SECTION: Game Row Context Menu]

        // [SECTION: Folder Color Picker Flyout]
        // Opens the Choose Color flyout, anchored under the folder row that was right-clicked. Snapshots
        // the current color so Escape can revert to it, then pre-loads the picker/hex textbox with that
        // same starting color.
        private void ChooseFolderColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem clickedItem) return;
            string? folderName = clickedItem.Tag as string;
            if (string.IsNullOrEmpty(folderName)) return;
            if (_folderContextPlacementElement == null) return;
            if (_folderContextPlacementElement.DataContext is not Models.TreeCategoryNode node) return;

            _colorFlyoutTargetNode = node;
            _colorFlyoutTargetFolderName = folderName;
            _colorFlyoutOriginalColor = node.FolderColor;
            _colorFlyoutEscaped = false;

            _suppressColorHexSync = true;
            if (node.FolderColor is SolidColorBrush originalBrush)
            {
                FolderColorPickerControl.SelectedColor = originalBrush.Color;
                FolderColorHexTextBox.Text = $"#{originalBrush.Color.R:X2}{originalBrush.Color.G:X2}{originalBrush.Color.B:X2}";
            }
            _suppressColorHexSync = false;

            FolderColorPickerPopup.PlacementTarget = _folderContextPlacementElement;
            FolderColorPickerPopup.Placement = PlacementMode.Bottom;
            FolderColorPickerPopup.IsOpen = true;

            // Deselect the folder row right as the flyout opens - visual feedback for the color change
            // comes from the swatch/live-updating icon color instead, and staying selected the whole time
            // would mean an abrupt jump to the selected-color state the instant the flyout closes.
            // Deliberately stays deselected afterward too, regardless of commit/cancel - the user can
            // reselect it themselves if they want to expand it.
            var rowContainer = FindVisualParent<TreeViewItem>(_folderContextPlacementElement);
            if (rowContainer != null)
            {
                rowContainer.IsSelected = false;
            }
        }

        // Live-applies the picked color to the folder's TreeCategoryNode as the user drags/picks, and
        // mirrors it into the hex textbox (guarded to avoid re-triggering LostFocus below).
        private void FolderColorPickerControl_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressColorHexSync) return;
            if (_colorFlyoutTargetNode == null) return;

            var newColor = FolderColorPickerControl.SelectedColor;

            _suppressColorHexSync = true;
            FolderColorHexTextBox.Text = $"#{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _suppressColorHexSync = false;

            _colorFlyoutTargetNode.FolderColor = new SolidColorBrush(newColor);
        }

        // Pushes a manually-typed hex value back into the picker (and live-applies it). Invalid text is left alone.
        private void FolderColorHexTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressColorHexSync) return;
            if (_colorFlyoutTargetNode == null) return;

            var parsedColor = TryParseFolderColor(FolderColorHexTextBox.Text.Trim());
            if (parsedColor.HasValue)
            {
                _suppressColorHexSync = true;
                FolderColorPickerControl.SelectedColor = parsedColor.Value;
                _suppressColorHexSync = false;

                _colorFlyoutTargetNode.FolderColor = new SolidColorBrush(parsedColor.Value);
            }
        }

        // Escape reverts the tree's color back to the pre-open snapshot and discards - handled directly
        // here rather than in Closed, since Color's commit/cancel semantics are reversed from the name
        // flyout (click-away commits here, so Closed below only needs to handle the "did NOT escape" case).
        private void FolderColorHexTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;

            _colorFlyoutEscaped = true;

            if (_colorFlyoutTargetNode != null && _colorFlyoutOriginalColor != null)
            {
                _colorFlyoutTargetNode.FolderColor = _colorFlyoutOriginalColor;
            }

            FolderColorPickerPopup.IsOpen = false;
        }

        // Fires on every close. If Escape already handled the revert above, do nothing further. Otherwise
        // (click-away, via the Popup's StaysOpen="False") this is a commit - write the picked color to disk.
        private void FolderColorPickerPopup_Closed(object? sender, EventArgs e)
        {
            bool wasEscaped = _colorFlyoutEscaped;
            _colorFlyoutEscaped = false;

            if (wasEscaped) return;
            if (_colorFlyoutTargetFolderName == null) return;

            Color selectedColor = FolderColorPickerControl.SelectedColor;
            string hex = $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
            string colorFolderName = _colorFlyoutTargetFolderName;

            if (string.Equals(colorFolderName, "RECENTLY PLAYED", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.BeginInvoke(new Action(() => _viewModel.SetRecentlyPlayedColor(hex)));
            }
            else if (string.Equals(colorFolderName, "MOST PLAYED", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.BeginInvoke(new Action(() => _viewModel.SetMostPlayedColor(hex)));
            }
            else if (string.Equals(colorFolderName, "FAVORITES", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.BeginInvoke(new Action(() => _viewModel.SetFavoritesColor(hex)));
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(() => _viewModel.SetCustomFolderColor(colorFolderName, hex)));
            }
        }

        // Parses a hex string into a Color, returning null instead of throwing on invalid input.
        private static Color? TryParseFolderColor(string hexText)
        {
            try
            {
                return new BrushConverter().ConvertFromString(hexText) is SolidColorBrush brush ? brush.Color : null;
            }
            catch
            {
                return null;
            }
        }
        // [END SECTION: Folder Color Picker Flyout]

        // [SECTION: Folder Row Context Menu]
        // Right-clicking a folder row selects it first, same as game rows already do - Rename depends on
        // "the folder is selected". Applied uniformly to all folder rows (Favorites, virtual categories,
        // GAMES, custom folders); this only affects which row highlights, not what's actually actionable -
        // Rename/Color/Delete's enabled state is still gated per folder type below.
        private void FolderRowBorder_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var rowContainer = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (rowContainer != null)
            {
                rowContainer.IsSelected = true;
                rowContainer.Focus();
            }
        }

        // "Rename folder" and "Delete folder" are enabled only for custom folders (playlist .cfg files);
        // "Choose folder color" is also enabled for Favorites, Recently Played and Most Played. Virtual/catver
        // categories and physical GAMES-bucket folders are never renamable/recolorable/deletable this way. The resolved on-disk folder name is stashed
        // in each MenuItem's Tag, same pattern as RemoveFromFolderMenuItem. "Move" is separate - it's
        // available for any folder type except Favorites, gated only by nesting depth (capped at one
        // subfolder level), not by folder type.
        private void FolderRowContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu) return;
            if (menu.PlacementTarget is not FrameworkElement placementElement) return;
            if (placementElement.DataContext is not Models.TreeCategoryNode node) return;

            _folderContextPlacementElement = placementElement;

            string? matchedFolderName = _viewModel.GetCustomFolderNames().FirstOrDefault(f => f.Equals(node.HeaderText, StringComparison.OrdinalIgnoreCase));
            bool isCustomFolder = matchedFolderName != null;
            bool isRecentlyPlayed = string.Equals(node.HeaderText, "RECENTLY PLAYED", StringComparison.OrdinalIgnoreCase);
            bool isMostPlayed = string.Equals(node.HeaderText, "MOST PLAYED", StringComparison.OrdinalIgnoreCase);

            // Rename/Delete are custom-folder-only concepts - a static reserved folder can't be renamed
            // or deleted, only recolored (and, via a separate menu item, cleared).
            foreach (var header in new[] { "Rename folder", "Delete folder" })
            {
                if (menu.Items.OfType<MenuItem>().FirstOrDefault(mi => (string)mi.Header == header) is MenuItem targetItem)
                {
                    targetItem.IsEnabled = isCustomFolder;
                    targetItem.Tag = matchedFolderName;
                }
            }

            // Choose Color is available for custom folders AND the static folders (Favorites and the two
            // play-history folders). The Tag becomes the node's own HeaderText for the static folders, since they have no separate
            // "on-disk name" the way a custom folder's filename does.
            bool canRecolor = isCustomFolder || isRecentlyPlayed || isMostPlayed
    || string.Equals(node.HeaderText, "FAVORITES", StringComparison.OrdinalIgnoreCase);
            if (menu.Items.OfType<MenuItem>().FirstOrDefault(mi => (string)mi.Header == "Choose folder color") is MenuItem colorItem)
            {
                colorItem.IsEnabled = canRecolor;
                colorItem.Tag = matchedFolderName ?? node.HeaderText;
            }

            bool isFavorites = string.Equals(node.HeaderText, "FAVORITES", StringComparison.OrdinalIgnoreCase);

            // Clear Folder: custom folders, Favorites, and the two play-history folders - never virtual/
            // catver categories or physical folders, since those aren't user-curated lists to begin with.
            // Clear All Play History: only Recently Played/Most Played, since it operates on the full
            // underlying log rather than just what's currently displayed.
            bool canClearFolder = isCustomFolder || isFavorites || isRecentlyPlayed || isMostPlayed;
            if (menu.Items.OfType<MenuItem>().FirstOrDefault(mi => (string)mi.Header == "Clear Folder") is MenuItem clearFolderItem)
            {
                clearFolderItem.IsEnabled = canClearFolder;
                clearFolderItem.Tag = matchedFolderName ?? node.HeaderText;
            }

            bool canClearAllHistory = isRecentlyPlayed || isMostPlayed;
            if (menu.Items.OfType<MenuItem>().FirstOrDefault(mi => (string)mi.Header == "Clear All Play History") is MenuItem clearAllItem)
            {
                clearAllItem.IsEnabled = canClearAllHistory;
                clearAllItem.Tag = node.HeaderText;
            }
            var (_, depth) = GetFolderPathAndDepth(placementElement);
            bool isMoveable = !isFavorites && depth <= 2;

            if (menu.Items.OfType<MenuItem>().FirstOrDefault(mi => (string)mi.Header == "Move") is MenuItem moveItem)
            {
                moveItem.IsEnabled = isMoveable;
            }
        }

        // Confirms first, same reasoning as Delete Folder - emptying a folder is not undoable from the
        // UI. Deferred via Dispatcher.BeginInvoke for the same reentrancy reason as every other folder-
        // action menu item - Click still fires from inside the ContextMenu popup's own dismissal frame.
        private void ClearFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem clickedItem) return;
            string? folderName = clickedItem.Tag as string;
            if (string.IsNullOrEmpty(folderName)) return;

            var result = MessageBox.Show(
                $"Clear the folder '{folderName}'?\n\nThis empties it but keeps the folder itself. This cannot be undone.",
                "Confirm Clear Folder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            Dispatcher.BeginInvoke(new Action(() => _viewModel.ClearFolder(folderName)));
        }

        // Wipes the ENTIRE underlying play history log for this stat (not just the visible top-30) -
        // the folder goes genuinely empty until new plays accumulate, rather than revealing whatever
        // was ranked just below the current top 30 the way Clear Folder does.
        private void ClearAllPlayHistoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem clickedItem) return;
            string? folderName = clickedItem.Tag as string;
            if (string.IsNullOrEmpty(folderName)) return;

            var result = MessageBox.Show(
                $"Clear ALL play history for '{folderName}'?\n\nUnlike Clear Folder, this wipes the entire underlying history, not just what's currently visible. This cannot be undone.",
                "Confirm Clear All Play History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            bool clearLastPlayed = string.Equals(folderName, "RECENTLY PLAYED", StringComparison.OrdinalIgnoreCase);
            bool clearPlayCount = string.Equals(folderName, "MOST PLAYED", StringComparison.OrdinalIgnoreCase);

            Dispatcher.BeginInvoke(new Action(() => _viewModel.ClearAllPlayHistory(clearLastPlayed, clearPlayCount)));
        }

        // Walks the visual tree upward from a folder row, collecting every ancestor TreeCategoryNode's
        // HeaderText (including the row's own) to build its full nested path (root-to-leaf, backslash-
        // joined - same convention as CollectExpandedPaths/ApplyExpandedPaths and the order files) and its
        // depth (1 = root level, 2 = direct subfolder, etc.), for gating Move against the one-subfolder
        // cap and for identifying exactly which folder to move.
        private static (string FullPath, int Depth) GetFolderPathAndDepth(FrameworkElement placementElement)
        {
            var segments = new List<string>();
            var currentContainer = FindVisualParent<TreeViewItem>(placementElement);

            while (currentContainer?.DataContext is Models.TreeCategoryNode node)
            {
                segments.Insert(0, node.HeaderText);
                currentContainer = FindVisualParent<TreeViewItem>(VisualTreeHelper.GetParent(currentContainer));
            }

            return (string.Join("\\", segments), segments.Count);
        }

        // Confirms with the user (folder deletion only removes the folder mapping, never the games
        // themselves) before deleting. Deferred via Dispatcher.BeginInvoke for the same reason as the
        // other folder-action menu items - Click still fires from inside the ContextMenu popup's own
        // dismissal frame, and the tree rebuild this triggers stops the video preview.
        // All four Move actions resolve the full nested path from _folderContextPlacementElement
        // (stashed by FolderRowContextMenu_Opened when the menu was opened) and hand off to
        // MainViewModel.MoveFolderInOrder, which owns all the actual order-file logic. Deferred via
        // Dispatcher.BeginInvoke for the same reentrancy reason as every other folder-action menu item -
        // Click still fires from inside the ContextMenu popup's own dismissal frame.
        private void MoveFolderToTop_Click(object sender, RoutedEventArgs e) => MoveFolder(MoveDirection.Top);
        private void MoveFolderUp_Click(object sender, RoutedEventArgs e) => MoveFolder(MoveDirection.Up);
        private void MoveFolderDown_Click(object sender, RoutedEventArgs e) => MoveFolder(MoveDirection.Down);
        private void MoveFolderToBottom_Click(object sender, RoutedEventArgs e) => MoveFolder(MoveDirection.Bottom);

        private void MoveFolder(MoveDirection direction)
        {
            if (_folderContextPlacementElement == null) return;

            var (fullPath, _) = GetFolderPathAndDepth(_folderContextPlacementElement);
            if (string.IsNullOrEmpty(fullPath)) return;

            Dispatcher.BeginInvoke(new Action(() => _viewModel.MoveFolderInOrder(fullPath, direction)));
        }

        private void DeleteFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem clickedItem) return;
            string? folderName = clickedItem.Tag as string;
            if (string.IsNullOrEmpty(folderName)) return;

            var result = MessageBox.Show(
                $"Delete the custom folder '{folderName}'?\n\nThis only removes the folder itself - the games inside it are NOT deleted and remain wherever else they're organized (GAMES list, other folders, Favorites).\n\nThis cannot be undone.",
                "Confirm Delete Folder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            Dispatcher.BeginInvoke(new Action(() => _viewModel.DeleteCustomFolder(folderName)));
        }

        // Opens the shared name-input flyout in Create mode: empty text, positioned at the mouse point.
        // Reachable from the TreeView's own background ContextMenu (empty tree space), independent of any
        // specific folder row.
        private void CreateFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _folderNameFlyoutTargetFolder = null; // Create mode
            _folderNameFlyoutCommitted = false;

            FolderNameInputTextBox.Text = string.Empty;
            FolderNameInputPlaceholder.Visibility = Visibility.Visible;
            FolderNameInputPopup.PlacementTarget = GameTree;
            FolderNameInputPopup.Placement = PlacementMode.MousePoint;
            // Offsets are in unscaled units, so they follow Smart fullscreen's factor to keep the box under the cursor
            FolderNameInputPopup.HorizontalOffset = -20 * _layoutScaleFactor;
            FolderNameInputPopup.VerticalOffset = -18 * _layoutScaleFactor;
            FolderNameInputPopup.IsOpen = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                FolderNameInputTextBox.Focus();
                Keyboard.Focus(FolderNameInputTextBox);
            }), DispatcherPriority.Input);
        }

        // Opens the shared name-input flyout in Rename mode: pre-filled with the current name, anchored
        // under the folder row that was right-clicked.
        private void RenameFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem clickedItem) return;
            string? folderName = clickedItem.Tag as string;
            if (string.IsNullOrEmpty(folderName)) return;
            if (_folderContextPlacementElement == null) return;

            _folderNameFlyoutTargetFolder = folderName; // Rename mode
            _folderNameFlyoutCommitted = false;

            FolderNameInputTextBox.Text = folderName;
            FolderNameInputPlaceholder.Visibility = Visibility.Collapsed;
            FolderNameInputPopup.PlacementTarget = _folderContextPlacementElement;
            FolderNameInputPopup.Placement = PlacementMode.Bottom;
            FolderNameInputPopup.HorizontalOffset = 0;
            FolderNameInputPopup.VerticalOffset = 0;
            FolderNameInputPopup.IsOpen = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                FolderNameInputTextBox.Focus();
                FolderNameInputTextBox.SelectAll();
            }), DispatcherPriority.Input);
        }

        // Enter commits (Create or Rename, depending on _folderNameFlyoutTargetFolder), Escape cancels.
        // Click-away cancels too, via the Popup's own StaysOpen="False" - handled in the Closed event below.
        // Shows the placeholder whenever the box is empty (Create's initial state, or Rename backspaced
        // all the way down) and hides it the instant real text exists - covers both modes with one rule.
        private void FolderNameInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FolderNameInputPlaceholder.Visibility = string.IsNullOrEmpty(FolderNameInputTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void FolderNameInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;

                string enteredName = FolderNameInputTextBox.Text.Trim();
                bool isRename = _folderNameFlyoutTargetFolder != null;
                bool nameActuallyChanged = !isRename || !enteredName.Equals(_folderNameFlyoutTargetFolder, StringComparison.OrdinalIgnoreCase);

                // Duplicate check has to happen here, before the Popup closes - not in the Closed handler
                // below - so a duplicate name can keep the flyout open for the user to just retype,
                // instead of closing and forcing them to re-trigger Create/Rename from scratch.
                if (nameActuallyChanged && !string.IsNullOrEmpty(enteredName))
                {
                    bool isDuplicate = _viewModel.GetCustomFolderNames().Any(f => f.Equals(enteredName, StringComparison.OrdinalIgnoreCase));
                    if (isDuplicate)
                    {
                        MessageBox.Show($"A folder named '{enteredName}' already exists. Please choose a different name.", "Duplicate Folder Name");
                        FolderNameInputTextBox.SelectAll();
                        FolderNameInputTextBox.Focus();
                        return;
                    }
                }

                _folderNameFlyoutCommitted = true;
                FolderNameInputPopup.IsOpen = false;
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _folderNameFlyoutCommitted = false;
                FolderNameInputPopup.IsOpen = false;
            }
        }

        // Fires on every close, whether from Enter (committed), Escape, or click-away (StaysOpen="False").
        // Only actually does anything if _folderNameFlyoutCommitted was set by the Enter key above.
        private void FolderNameInputPopup_Closed(object? sender, EventArgs e)
        {
            if (!_folderNameFlyoutCommitted)
            {
                return;
            }

            string enteredName = FolderNameInputTextBox.Text.Trim();
            _folderNameFlyoutCommitted = false;

            if (string.IsNullOrEmpty(enteredName)) return;

            if (_folderNameFlyoutTargetFolder == null)
            {
                Dispatcher.BeginInvoke(new Action(() => _viewModel.CreateCustomFolder(enteredName)));
            }
            else
            {
                string oldFolderName = _folderNameFlyoutTargetFolder;
                Dispatcher.BeginInvoke(new Action(() => _viewModel.RenameCustomFolder(oldFolderName, enteredName)));
            }
        }
        // [END SECTION: Folder Row Context Menu]

        // Captured right before launch so RestoreGameListScrollPosition can force-reselect this exact
        // game afterward, regardless of whether the rebuild's IsRebuildingTree guard actually caught the
        // deferred null SelectedItemChanged event in time - deterministic reselection instead of relying
        // on a timing race not happening.
        private Models.GameItem? _gameBeingLaunched;

        // Stops the video preview and hands off to the LaunchGameCommand for the currently selected game
        private void TriggerActiveSelectionLaunch()
        {
            if (_viewModel.SelectedGame != null && _viewModel.LaunchGameCommand.CanExecute(this))
            {
                _gameBeingLaunched = _viewModel.SelectedGame;

                StopActivePreview();
                PauseMainBgVideo();
                _viewModel.LaunchGameCommand.Execute(this);
            }
        }

        // Re-centers whatever game is still selected (SelectedGame's object reference survives the
        // launch/exit cycle unchanged - see ExecuteLaunchAsync's comment) after the post-exit
        // UpdateLiveTreeDisplay() rebuild. A raw scroll-offset restore was tried first but is fragile -
        // the rebuild can shift how many items render above any given point (Recently Played changing
        // size, etc.), so the same pixel offset doesn't reliably land on the same game. Centering the
        // actual selected container instead, same approach and double-deferred pattern as the
        // random-winner centering fix, guarantees the just-played game is always visible afterward.
        private void RestoreGameListScrollPosition()
        {
            // Force-reselect the exact game that was launched, regardless of whether the rebuild's
            // deferred null SelectedItemChanged event slipped past IsRebuildingTree's guard and wiped
            // SelectedGame for real - deterministic instead of relying on that timing race not happening.
            var selectedGame = _gameBeingLaunched;
            _gameBeingLaunched = null;
            if (selectedGame == null) return;

            _viewModel.SelectedGame = selectedGame;

            // No force-expand-elsewhere step here: UpdateLiveTreeDisplay's own expand-path
            // snapshot/restore (CollectExpandedPaths/ApplyExpandedPaths) already keeps whatever
            // folder the game was launched from - custom or genre tree alike - correctly expanded
            // across the rebuild. Forcing FindMainTreePathToGame's path open here caused it to expand
            // an unrelated genre-tree branch for games living in a custom folder, since that lookup
            // excludes custom folders by design. We just try to find the already-realized container
            // below and center it; if it's not generated yet, we simply skip centering.

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var selectedContainer = FindTreeViewItemContainer(GameTree, selectedGame);
                if (selectedContainer == null) return;

                selectedContainer.IsSelected = true;
                selectedContainer.BringIntoView();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var scrollViewer = FindVisualChild<ScrollViewer>(GameTree);
                    if (scrollViewer != null)
                    {
                        Point relativePosition = selectedContainer.TransformToAncestor(scrollViewer).Transform(new Point(0, 0));
                        double targetOffset = scrollViewer.VerticalOffset + relativePosition.Y
                            - (scrollViewer.ViewportHeight / 2) + (selectedContainer.ActualHeight / 2);
                        scrollViewer.ScrollToVerticalOffset(targetOffset);
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // [SECTION: Keyboard Shortcuts]
        // Global hotkey handling: Ctrl+O (Options), F11 (fullscreen toggle), Ctrl+F (favorite),
        // Ctrl+D (dev mode), Ctrl+C (copy ROM name), Escape (close), Ctrl+G (playlists), Ctrl+M (mouse support toggle).
        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (isCtrl && e.Key == Key.O)
            {
                e.Handled = true;
                OpenOptionsWindow();
                return;
            }

            if (isCtrl && e.Key == Key.T)
            {
                e.Handled = true;
                OpenThemeBuilderWindow();
                return;
            }

            if (e.Key == Key.F11)
            {
                e.Handled = true;
                ToggleFullscreen();
                return;
            }

            if (isCtrl && e.Key == Key.F)
            {
                e.Handled = true;
                if (_viewModel.SelectedGame != null)
                {
                    _viewModel.ToggleFavorite(_viewModel.SelectedGame);
                }
                return;
            }

            if (isCtrl && e.Key == Key.D)
            {
                e.Handled = true;
                // Dev mode only toggles the ROM-name prefix in the tree now. The window is fixed-size
                // (sized to the layout resolution), so there is nothing to resize here anymore.
                _viewModel.IsDevMode = !_viewModel.IsDevMode;
                return;
            }

            if (isCtrl && e.Key == Key.C)
            {
                e.Handled = true;
                if (_viewModel.SelectedGame != null)
                {
                    Clipboard.SetText(_viewModel.SelectedGame.RomName);
                }
                return;
            }

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
                return;
            }

            if (isCtrl && e.Key == Key.G)
            {
                e.Handled = true;
                if (_viewModel.SelectedGame != null)
                {
                    var customFoldersDialog = new Views.ManagePlayListsWindow(_viewModel) { Owner = this };
                    customFoldersDialog.ShowDialog();
                    BringToFrontAfterChildClose();
                }
                return;
            }

            if (isCtrl && e.Key == Key.M)
            {
                e.Handled = true;
                if (_viewModel.SelectedGame != null)
                {
                    _isTogglingMouseSupport = true;
                    try
                    {
                        _viewModel.ToggleMouseSupport(_viewModel.SelectedGame);
                    }
                    finally
                    {
                        _isTogglingMouseSupport = false;
                    }
                }
                return;
            }

            if (isCtrl && !isShift && e.Key == Key.V)
            {
                e.Handled = true;
                if (_viewModel.SelectedGame != null && _romCopyService != null)
                {
                    var result = _romCopyService.CopyRomToDestination(_viewModel.SelectedGame);
                    ShowRomCopyStatusTooltip(result.Message);
                }
                return;
            }

            if (isCtrl && !isShift && e.Key == Key.X)
            {
                e.Handled = true;
                if (_viewModel.SelectedGame != null && _romCopyService != null)
                {
                    var result = _romCopyService.MoveRomToDestination(_viewModel.SelectedGame);
                    ShowRomCopyStatusTooltip(result.Message);
                }
                return;
            }

            if (isCtrl && isShift && e.Key == Key.V)
            {
                e.Handled = true;
                var pathDialog = new Views.RomCopyPathWindow(_viewModel) { Owner = this };
                pathDialog.ShowDialog();
                BringToFrontAfterChildClose();
                return;
            }

            // Hot-reloads history_overrides.cfg and re-resolves the currently selected game's history
            // panels against it - lets a manually-edited override take effect immediately instead of
            // requiring a full app restart.
            if (isCtrl && isShift && e.Key == Key.H)
            {
                e.Handled = true;
                _viewModel.ReloadHistoryOverrides();
                return;
            }
        }
        // [END SECTION: Keyboard Shortcuts]

        // [SECTION: Options Window]
        // Opens the Options window modally, pauses video playback while it's open, wires live gamepad
        // diagnostics, and refreshes theme bindings + the video preview once it closes.
        private void OpenOptionsWindow()
        {
            // The boot splash video is intentionally left untouched here - no Pause() on open, no
            // restart on close. It was previously paused/resumed around the Options dialog, but the
            // resulting reload cascade (via RefreshThemeBindings() below re-raising IsBootSplashVideo)
            // caused a hang with the old video player. Letting it keep playing uninterrupted avoids the
            // stop/restart calls entirely for the common case where nothing about it actually changed.
            _previewPlayer?.Pause();

            _isOptionsWindowOpen = true;
            _viewModel.IsOptionsWindowOpen = true;

            var adjustmentsPanel = new Views.OptionsWindow(_viewModel) { Owner = this };

            if (_gamepadService != null)
            {
                adjustmentsPanel.WireLiveDiagnostics(_gamepadService);
                _gamepadService.TriggerDiagnosticsUpdate();
            }

            adjustmentsPanel.ShowDialog();
            BringToFrontAfterChildClose();

            _isOptionsWindowOpen = false;
            _viewModel.IsOptionsWindowOpen = false;

            // TEMP TEST: commented out to check whether this is still needed now that buttons were
            // rewired to their own plain Theme* properties, or whether it's the source of the residual
            // memory growth on Options close. Re-enable if anything stops updating live after this test.
            // _viewModel.RefreshThemeBindings();
            RefreshMainButtonsRestingColors();

            // Resume rather than reload - the active player was paused (not stopped) on open, so Play() with
            // no arguments just continues from where it left off instead of restarting from the beginning
            _previewPlayer?.Resume();
        }

        // Footer gear icon click handler - opens the Options window
        private void BtnOpenOptionsGear_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenOptionsWindow();
        }
        // [END SECTION: Options Window]

        // [SECTION: Sorting Window]
        // Opens the Sorting window modally - structure mode (custom folders vs. catver-derived
        // categories) plus region/revision/player-count/rating filters, applied via a single Apply
        // button rather than live per-checkbox like the old SORT checkbox was.
        private void BtnOpenSortingGear_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var sortingWindow = new Views.SortingWindow(_viewModel) { Owner = this };
            sortingWindow.ShowDialog();
            BringToFrontAfterChildClose();
        }
        // [END SECTION: Sorting Window]

        // [SECTION: Child Window Re-activation]
        // Closing an owned window can leave the launcher behind other programs' windows. Deferred to Background
        // priority so it runs after the close and its own activation churn have finished. Not Topmost.
        // Does the restore / Activate / Focus on this window after a child window closes
        public void BringToFrontAfterChildClose()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                Activate();
                Focus();
            }));
        }
        // [END SECTION: Child Window Re-activation]

        // [SECTION: Preview Player Shutdown]
        // The first Closing is cancelled while the FFmpeg player stops and joins its workers (the native DLLs
        // must not unload while they run). Close() is then queued again; awaiting may complete synchronously,
        // so re-entering Close inside the Closing event itself would be invalid.
        private bool _isClosingPreviewPlayer;
        private bool _previewPlayerClosed;

        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_previewPlayerClosed) return;

            e.Cancel = true;
            if (_isClosingPreviewPlayer) return;
            _isClosingPreviewPlayer = true;
            IsEnabled = false;

            try
            {
                if (_previewPlayer != null) await _previewPlayer.CloseAsync();
            }
            catch
            {
            }

            _previewPlayerClosed = true;
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
        // [END SECTION: Preview Player Shutdown]

        // [SECTION: Fullscreen Toggle]
        // Shared by the F11 hotkey and the hamburger menu's "Toggle Fullscreen" item so the two can't drift apart.
        // With Smart fullscreen on and the Theme Builder closed, entering also runs the size test (ApplySmartFullscreen).
        private void ToggleFullscreen()
        {
            if (WindowStyle != WindowStyle.None)
            {
                // SizeToContent has to be released while maximized, otherwise it fights the maximize.
                // The layout canvas keeps its native size and stays anchored top-left; the root grid's
                // background fills the rest of the screen.
                SizeToContent = SizeToContent.Manual;
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;

                if (_viewModel.Configuration.SmartFullscreen && _themeBuilderWindow == null) ApplySmartFullscreen();
            }
            else
            {
                // Smart fullscreen's scale, size and black background go first, so SizeToContent sizes the window to the
                // active layout again.
                EndSmartFullscreen();

                // Back to a normal window sized to the layout (client area = layout resolution).
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.ThreeDBorderWindow;
                ResizeMode = ResizeMode.NoResize;
                SizeToContent = SizeToContent.WidthAndHeight;
            }
        }
        // [END SECTION: Fullscreen Toggle]

        // [SECTION: Smart Fullscreen]
        // Session-only whole-number upscale in fullscreen (smart-fullscreen-pass1.md). The size test picks one of the theme's
        // saved layout sizes and a scale n for the window's monitor in physical pixels; RootLayoutGrid (wallpaper, video,
        // background grid, LayoutCanvas and the guides) is sized to that layout, scaled by n / dpiScale with a LayoutTransform
        // (cancelling the Windows scale) and centered on a black window background, so any leftover screen is black bars.
        // Layout rounding is off on the grid while scaled: WPF rounds to device pixels ignoring the transform, which would
        // put whole layout units on fractional pixels at 125% / 150%. The picked size is a view model display override only.
        private bool _smartFullscreenActive;
        private IntPtr _smartFullscreenMonitor;
        private double _layoutScaleFactor = 1; // WPF units per layout unit; 1 unless Smart fullscreen is scaling

        // Window resources read by the main window's menus, popups and tooltips (MainWindow.xaml, Smart Fullscreen Popup Scale).
        // Set only in ApplySmartFullscreen and put back to the defaults (no scale, rounding on) only in EndSmartFullscreen.
        private const string PopupScaleKey = "Launcher.PopupScale";
        private const string PopupLayoutRoundingKey = "Launcher.PopupLayoutRounding";

        // Does the size test for the window's monitor and applies the picked layout and scale; falls back to plain
        // fullscreen when the monitor cannot be read or no layout fits
        private void ApplySmartFullscreen()
        {
            if (_viewModel == null) return;

            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (!Services.MonitorBounds.TryGetPhysicalSize(hwnd, out IntPtr monitor, out int screenWidth, out int screenHeight))
            {
                EndSmartFullscreen();
                return;
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            var sizes = _viewModel.Configuration.SavedThemeLayouts.Select(l => (l.Width, l.Height)).Distinct().ToList();
            var result = Services.SmartFullscreen.Choose(screenWidth, screenHeight, dpi.DpiScaleX, sizes);
            if (!result.Fits)
            {
                EndSmartFullscreen();
                return;
            }

            _smartFullscreenActive = true;
            _smartFullscreenMonitor = monitor;
            _layoutScaleFactor = result.TransformFactor;

            var scale = new ScaleTransform(result.TransformFactor, result.TransformFactor);
            scale.Freeze();
            RootLayoutGrid.LayoutTransform = scale;
            RootLayoutGrid.Width = result.LayoutWidth;
            RootLayoutGrid.Height = result.LayoutHeight;
            RootLayoutGrid.HorizontalAlignment = HorizontalAlignment.Center;
            RootLayoutGrid.VerticalAlignment = VerticalAlignment.Center;
            RootLayoutGrid.UseLayoutRounding = false;
            Background = Brushes.Black;

            // Menus, popups and tooltips are separate windows: the same factor reaches them through the window resources
            Resources[PopupScaleKey] = scale;
            Resources[PopupLayoutRoundingKey] = false;

            // Refreshes the theme bindings, which re-applies the layout (and the decode sizes with the new factor)
            _viewModel.SetDisplayLayoutOverride((result.LayoutWidth, result.LayoutHeight));
        }

        // Removes Smart fullscreen's scale, size, centering, rounding and black background and drops the display override,
        // leaving plain fullscreen at the active size (or the normal window). Does nothing while Smart fullscreen is not scaling.
        private void EndSmartFullscreen()
        {
            if (!_smartFullscreenActive) return;

            _smartFullscreenActive = false;
            _smartFullscreenMonitor = IntPtr.Zero;
            _layoutScaleFactor = 1;

            RootLayoutGrid.ClearValue(FrameworkElement.LayoutTransformProperty);
            RootLayoutGrid.ClearValue(FrameworkElement.WidthProperty);
            RootLayoutGrid.ClearValue(FrameworkElement.HeightProperty);
            RootLayoutGrid.ClearValue(FrameworkElement.HorizontalAlignmentProperty);
            RootLayoutGrid.ClearValue(FrameworkElement.VerticalAlignmentProperty);
            RootLayoutGrid.ClearValue(FrameworkElement.UseLayoutRoundingProperty);
            ClearValue(BackgroundProperty);

            Resources[PopupScaleKey] = Transform.Identity;
            Resources[PopupLayoutRoundingKey] = true;

            _viewModel.SetDisplayLayoutOverride(null);
        }

        // Reruns the size test while Smart fullscreen is scaling (DPI, monitor or display change, theme switch, layout size pick)
        private void RecomputeSmartFullscreen()
        {
            if (_smartFullscreenActive) ApplySmartFullscreen();
        }

        // Reruns the size test when the window has moved to another monitor
        private void SmartFullscreen_LocationChanged(object? sender, EventArgs e)
        {
            if (!_smartFullscreenActive) return;

            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (Services.MonitorBounds.TryGetPhysicalSize(hwnd, out IntPtr monitor, out _, out _) && monitor != _smartFullscreenMonitor)
                Dispatcher.BeginInvoke(new Action(RecomputeSmartFullscreen));
        }

        // Reruns the size test after a display resolution or arrangement change (raised off the UI thread)
        private void SmartFullscreen_DisplaySettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RecomputeSmartFullscreen));
        }

        // Flips the Smart fullscreen preference and saves settings.json. Turning it off while it is scaling drops to plain
        // fullscreen at the active size; turning it on takes effect the next time fullscreen is entered.
        private void HamburgerSmartFullscreenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var config = _viewModel.Configuration;
                config.SmartFullscreen = !config.SmartFullscreen;
                if (!config.SmartFullscreen) EndSmartFullscreen();
                _viewModel.PersistConfigurationToDisk();
            }));
        }
        // [END SECTION: Smart Fullscreen]

        // [SECTION: Hamburger Menu]
        // Same hover/resting-color pattern as the Sort/Options/Random buttons. The popup itself is the
        // HamburgerContextMenu resource in MainWindow.xaml, opened here and anchored under the button.
        private void BtnOpenMenuHamburger_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var menu = (ContextMenu)FindResource("HamburgerContextMenu");
            PopulateHamburgerSubmenus(menu);
            menu.PlacementTarget = BtnOpenMenuHamburger;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        // [SECTION: Hamburger Theme & Layout Size Submenus]
        // Rebuilt every time the menu opens, so the lists always match the themes on disk and the sizes
        // saved in the active theme's zip. The active entry gets a checkmark in the icon column; the
        // inactive entries get no icon, which keeps the text aligned.
        private static TextBlock CreateCheckIcon()
        {
            return new TextBlock { Text = "\uE73E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
        }

        private void PopulateHamburgerSubmenus(ContextMenu menu)
        {
            var config = _viewModel.Configuration;

            // Smart fullscreen: a check mark while the preference is on
            if (menu.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Tag as string == "SmartFullscreenMenu") is MenuItem smartItem)
            {
                smartItem.Icon = config.SmartFullscreen ? CreateCheckIcon() : null;
            }

            // Themes: every .zip in the Themes folder, check mark on the active one
            if (menu.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Tag as string == "ThemesMenu") is MenuItem themesMenu)
            {
                themesMenu.Items.Clear();

                string themeDirectory = System.IO.Path.Combine(config.GetArcadeStickFilesPath(), "Themes");
                var themeNames = Directory.Exists(themeDirectory)
                    ? Directory.GetFiles(themeDirectory, "*.zip")
                        .Select(f => System.IO.Path.GetFileNameWithoutExtension(f))
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    : new List<string>();

                foreach (var themeName in themeNames)
                {
                    var item = new MenuItem { Header = themeName, Tag = themeName };
                    if (string.Equals(themeName, config.ActiveThemeName, StringComparison.OrdinalIgnoreCase))
                    {
                        item.Icon = CreateCheckIcon();
                    }
                    item.Click += HamburgerThemeItem_Click;
                    themesMenu.Items.Add(item);
                }

                themesMenu.IsEnabled = themeNames.Count > 0;
            }

            // Layout size: every size saved in the active theme's zip, hidden when there are fewer than two
            if (menu.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Tag as string == "LayoutSizeMenu") is MenuItem sizeMenu)
            {
                sizeMenu.Items.Clear();

                var sizes = config.SavedThemeLayouts
                    .OrderBy(l => l.Width).ThenBy(l => l.Height)
                    .ToList();

                sizeMenu.Visibility = sizes.Count >= 2 ? Visibility.Visible : Visibility.Collapsed;

                foreach (var layout in sizes)
                {
                    var item = new MenuItem { Header = $"{layout.Width} x {layout.Height}", Tag = (layout.Width, layout.Height) };
                    if (layout.Width == config.ActiveResolutionWidth && layout.Height == config.ActiveResolutionHeight)
                    {
                        item.Icon = CreateCheckIcon();
                    }
                    item.Click += HamburgerLayoutSizeItem_Click;
                    sizeMenu.Items.Add(item);
                }
            }
        }

        // Deferred via Dispatcher.BeginInvoke like every other menu item here. Picking the active theme
        // does nothing: LoadTheme deletes and re-extracts that theme's folder, which could collide with a
        // file the running wallpaper video has open.
        private void HamburgerThemeItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem clicked || clicked.Tag is not string themeName) return;
            if (string.Equals(themeName, _viewModel.Configuration.ActiveThemeName, StringComparison.OrdinalIgnoreCase)) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // LoadTheme also switches the active size to the one the theme was saved at
                    if (_viewModel.LoadTheme(themeName))
                    {
                        _viewModel.PersistConfigurationToDisk();
                        _themeBuilderWindow?.RefreshAfterExternalThemeSwitch();
                        RecomputeSmartFullscreen(); // the new theme's sizes decide the fullscreen layout
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not load theme '{themeName}': {ex.Message}", "Load Theme", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }));
        }

        private void HamburgerLayoutSizeItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem clicked || clicked.Tag is not ValueTuple<int, int> size) return;

            var config = _viewModel.Configuration;
            int width = size.Item1;
            int height = size.Item2;
            if (width == config.ActiveResolutionWidth && height == config.ActiveResolutionHeight) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                // If the working layouts don't hold this size yet, take it from the theme's saved copy so
                // the theme's positions are used instead of the registry defaults.
                if (!config.Layouts.Any(l => l.Width == width && l.Height == height))
                {
                    var source = config.SavedThemeLayouts.FirstOrDefault(l => l.Width == width && l.Height == height);
                    if (source != null)
                    {
                        config.Layouts.Add(Models.ConfigurationSettings.CloneLayouts(new[] { source }).First());
                    }
                }

                config.ActiveResolutionWidth = width;
                config.ActiveResolutionHeight = height;
                ApplyLayout();
                _viewModel.PersistConfigurationToDisk();
                _themeBuilderWindow?.RefreshAfterExternalSizeSwitch();
                RecomputeSmartFullscreen();
            }));
        }
        // [END SECTION: Hamburger Theme & Layout Size Submenus]

        // Closing the menu always returns the button to its resting colors, even if the mouse is no
        // longer over it (MouseLeave already fired when the cursor moved onto the menu).
        private void HamburgerContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            RefreshMainButtonsRestingColors();
        }

        // Menu item handlers are deferred via Dispatcher.BeginInvoke, same as every other menu item in this
        // file - Click still fires from inside the ContextMenu popup's own dismissal frame, and these
        // open modal windows / change window state.
        private void HamburgerSortingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var sortingWindow = new Views.SortingWindow(_viewModel) { Owner = this };
                sortingWindow.ShowDialog();
                BringToFrontAfterChildClose();
            }));
        }

        private void HamburgerOptionsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => OpenOptionsWindow()));
        }

        private void HamburgerThemeBuilderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => OpenThemeBuilderWindow()));
        }

        // Non-modal and single-instance: if Theme Builder is already open, bring it forward instead of
        // creating a second one. Owner = this closes it automatically with the main window. Unlike the
        // Options window, this deliberately does not pause video or block gamepad input.
        private Views.ThemeBuilderWindow? _themeBuilderWindow;

        private void OpenThemeBuilderWindow()
        {
            if (_themeBuilderWindow != null)
            {
                if (_themeBuilderWindow.WindowState == WindowState.Minimized)
                {
                    _themeBuilderWindow.WindowState = WindowState.Normal;
                }
                _themeBuilderWindow.Activate();
                return;
            }

            // The builder edits the active resolution, so Smart fullscreen drops to plain fullscreen at that size first
            // (it stays plain after the builder closes, until fullscreen is toggled again)
            EndSmartFullscreen();

            _themeBuilderWindow = new Views.ThemeBuilderWindow(_viewModel) { Owner = this };
            _themeBuilderWindow.Closed += (s, e) => _themeBuilderWindow = null;
            _themeBuilderWindow.Show();
        }

        private void HamburgerFullscreenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => ToggleFullscreen()));
        }
        // [END SECTION: Hamburger Menu]

        // [SECTION: Main Window Buttons Hover]
        // The four main window buttons (Random, Sort, Options, Hamburger) share one style, read from the
        // Random Game Button entry. Colors are pushed from code-behind rather than XAML triggers (the same
        // direct-push convention as the games list rows). The sender is the button's Border; its Child is
        // the icon TextBlock. A locally set BorderBrush replaces the XAML binding, so the resting refresh
        // sets it explicitly too. The search box borrows only the resting border, through its binding.

        // Applies the hover colors and border to the button under the mouse
        private void MainButton_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border button)
            {
                // The button Border carries the fill. Once a button has its inner border ring (the corner fix),
                // the ring is the child Border and holds the border color and the icon; until then the button itself does.
                button.Background = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnBgColorHover);
                var ring = button.Child as Border ?? button;
                ring.BorderBrush = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnBorderColorHover);
                if (ring.Child is TextBlock icon) icon.Foreground = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnColorHover);
            }
            DelayedTooltip_MouseEnter(sender, e);
        }

        // Returns the button to its resting colors when the mouse leaves
        private void MainButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border button) ApplyMainButtonRestingColors(button);
            DelayedTooltip_MouseLeave(sender, e);
        }

        // Applies the resting colors to all four buttons. Called at startup, on every theme refresh, when a
        // Search & Buttons style value changes in ApplyLayout, after the Options window closes, and when the
        // hamburger menu closes, so the buttons reflect new values without waiting for a hover.
        private void RefreshMainButtonsRestingColors()
        {
            ApplyMainButtonRestingColors(BtnPlayRandom);
            ApplyMainButtonRestingColors(BtnOpenSortingGear);
            ApplyMainButtonRestingColors(BtnOpenOptionsGear);
            ApplyMainButtonRestingColors(BtnOpenMenuHamburger);
        }

        // Applies the resting background, border and icon color to one button
        private void ApplyMainButtonRestingColors(Border button)
        {
            // Same rule as the hover handler: the fill is on the button, the border color and icon are on the
            // inner border ring when the button has one, otherwise on the button itself.
            button.Background = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnBgColor);
            var ring = button.Child as Border ?? button;
            ring.BorderBrush = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnBorderColor);
            if (ring.Child is TextBlock icon) icon.Foreground = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnColor);
        }
        // [END SECTION: Main Window Buttons Hover]

        // [SECTION: Randomizer Confirm Flyout]
        // Opens the small "Spin?" confirm popup anchored directly under the die button - guards against
        // accidental clicks (e.g. a miss-click intended for Sort or the search box) before any spin logic
        // runs.
        private void BtnPlayRandom_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Ignore clicks mid-spin, so rapid clicks can't stack spins.
            if (_viewModel.IsRandomizerSpinning) return;
            // Pool-size guard fires here, at die-click time, before the confirm popup even opens -
            // per the agreed design, rather than letting the user confirm first and find out after.
            if (!_viewModel.IsRandomizerPoolEligible())
            {
                MessageBox.Show(
                    "Not enough games match your current filter settings to spin (fewer than 10 eligible). Try loosening your filter in the Sorting window.",
                    "Play Random Game", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // A real spin wins over the Theme Builder's card and banner previews
            EndRandomCardPreviewForSpin();
            EndBannerPreviewForSpin();

            // Which of the layer's two rects applies is decided now and stays fixed for the whole spin. Both states
            // draw over what is behind them; the card's background alpha decides how much shows through.
            _randomSpinFromSplash = !_viewModel.IsGameSelected;
            ApplyRandomGameDisplayRect();

            StopVideoPreviewForSpin();
            _viewModel.StartRandomizerSpin();
        }

        // Randomizer confirm popup handlers removed - the die button now spins immediately.
        // [END SECTION: Randomizer Confirm Flyout]

        // [SECTION: Popup Position Correction on Window Move]
        // AllowsTransparency="True" Popups don't automatically recompute their screen position when the
        // owning Window moves - a known WPF limitation affecting every popup in this file, not just the
        // randomizer's. Nudging HorizontalOffset by a throwaway amount forces WPF to recalculate
        // placement against the window's current position, without any visible flicker.
        private void MainWindow_LocationChanged(object? sender, EventArgs e)
        {
            RepositionOpenPopup(FolderNameInputPopup);
            RepositionOpenPopup(FolderColorPickerPopup);
        }

        private static void RepositionOpenPopup(System.Windows.Controls.Primitives.Popup popup)
        {
            if (!popup.IsOpen) return;

            double originalOffset = popup.HorizontalOffset;
            popup.HorizontalOffset = originalOffset + 1;
            popup.HorizontalOffset = originalOffset;
        }
        // [END SECTION: Popup Position Correction on Window Move]

        // [SECTION: Popup Centering]
        // PlacementMode.Bottom anchors a popup's top-LEFT corner to the target's bottom-left corner, not
        // centered - so this measures the popup's actual content width (which varies for the Reveal
        // popup depending on the winning game's title length) and offsets it to sit centered under the
        // target instead. Measuring works even while the popup is still closed, since Popup.Child is
        // already part of the logical tree (and already reflects any DataContext-bound text, like the
        // winner's title) the moment it's assigned in XAML - so there's no visible jump on open.
        // CenterPopupOverTarget removed - the randomizer popups it positioned no longer exist.
        // [END SECTION: Popup Centering]

        // [SECTION: Randomizer Reveal Flyout]
        // Launch reuses the exact same pipeline as the tree's own double-click launch - set SelectedGame
        // to the winner, then hand off to TriggerActiveSelectionLaunch. This gets play-history logging,
        // tree refresh, and video-preview handling for free, and confirms setting SelectedGame from code
        // does NOT force the TreeView's visual selection to jump/scroll (no two-way IsSelected binding
        // wired up) - consistent with the "no tree sync needed" design decision.
        // Randomizer reveal popup handlers removed - the spin now just lands on the winner in the list.

        // Fires whenever the popup closes for ANY reason - explicit button clicks above already set
        // IsOpen=false themselves (which triggers this too, redundantly but harmlessly), and now also
        // an outside click (StaysOpen="False"), which otherwise would have left IsRandomizerResultShowing
        // stuck true with no equivalent cleanup, same gap Cancel's handler already covers.
        // RandomizerRevealPopup_Closed removed along with the reveal popup.
        // [END SECTION: Randomizer Reveal Flyout]

        // [SECTION: Main Background Video]
        // Drives the MediaElement wallpaper layer (MainBgVideo). Runs on every ThemeBindingsRefreshed plus once at
        // startup. Skips a reload when the same file is already loaded, so saving unrelated theme tweaks doesn't
        // restart the loop. When the wallpaper isn't an mp4 (or is disabled), stops and hides the layer - the root
        // Grid's ImageBrush/solid color background takes over as before. A two-player gapless-loop swap was tried
        // and reverted: WPF MediaElement doesn't hand off cleanly between instances (visible flash at the swap),
        // so the single player's slight loop-point slowdown is the better trade-off.
        private string? _currentMainBgVideoPath;

        // True from launch until the game list is built. While set, the wallpaper video loads paused so
        // only its first frame shows (a static image); FinishMainBgVideoStartup() then starts playback.
        private bool _mainBgVideoHoldFirstFrame = true;

        private void RefreshMainBgVideo()
        {
            string? videoPath = _viewModel.ThemeMainWallpaperVideoPath;

            if (string.IsNullOrEmpty(videoPath))
            {
                MainBgVideo.Stop();
                MainBgVideo.Source = null;
                MainBgVideo.Visibility = Visibility.Collapsed;
                _currentMainBgVideoPath = null;
                return;
            }

            if (_currentMainBgVideoPath == videoPath) return;

            MainBgVideo.Source = new Uri(videoPath, UriKind.Absolute);
            MainBgVideo.Visibility = Visibility.Visible;
            if (_mainBgVideoHoldFirstFrame) MainBgVideo.Pause();
            else MainBgVideo.Play();
            _currentMainBgVideoPath = videoPath;
        }

        // Called once the game list is built: releases the first-frame hold and starts the loop.
        private void FinishMainBgVideoStartup()
        {
            _mainBgVideoHoldFirstFrame = false;
            if (_currentMainBgVideoPath != null) MainBgVideo.Play();
        }

        // Loop: MediaElement has no built-in repeat, so rewind to the start and keep playing
        private void MainBgVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            MainBgVideo.Position = TimeSpan.Zero;
            MainBgVideo.Play();
        }

        // Unsupported codec / missing Media Foundation (e.g. Windows N) - hide the layer so the root Grid's
        // solid ThemeMainColor background shows instead of a black or frozen rectangle
        private void MainBgVideo_MediaFailed(object? sender, ExceptionRoutedEventArgs e)
        {
            MainBgVideo.Stop();
            MainBgVideo.Source = null;
            MainBgVideo.Visibility = Visibility.Collapsed;
        }

        // Paused while a game runs so it isn't decoding in the background alongside MAME; resumed on return
        private void PauseMainBgVideo()
        {
            if (_currentMainBgVideoPath != null) MainBgVideo.Pause();

            // Custom video layers pause too, so nothing keeps decoding behind MAME
            foreach (var entry in _shapeFileElements.Values)
            {
                if (entry.Element is MediaElement customVideo) customVideo.Pause();
            }
        }

        private void ResumeMainBgVideo()
        {
            if (_currentMainBgVideoPath != null) MainBgVideo.Play();

            foreach (var entry in _shapeFileElements.Values)
            {
                if (entry.Element is MediaElement customVideo && customVideo.Visibility == Visibility.Visible) customVideo.Play();
            }
        }
        // [END SECTION: Main Background Video]

        // [SECTION: Boot Splash Corner Clip Refresh]
        // MediaLayer0_Back's CornerRadius is evaluated inside the Viewbox's pre-scaled 1600x900 canvas, so
        // its rounded-corner render doesn't self-repaint on a theme change the way it does on a manual
        // window resize (which forces a full layout pass). Force that same invalidation here so the boot
        // splash image's corners stay correctly rounded immediately after a theme swap.
        private void RefreshBootSplashImageCornerClip()
        {
            SplashLayer.MediaLayer0_Back.InvalidateMeasure();
            SplashLayer.MediaLayer0_Back.InvalidateArrange();
            SplashLayer.MediaLayer0_Back.InvalidateVisual();
        }
        // [END SECTION: Boot Splash Corner Clip Refresh]

        // [SECTION: TreeView Expand/Collapse Sync]
        // Enforces single-branch-open behavior: expanding a folder collapses sibling folders that
        // aren't ancestors of the expanded node, and scrolls the expanded node into view.
        private void GameTree_Expanded(object sender, RoutedEventArgs e)
        {
            if (_isTogglingMouseSupport) return;

            if (e.OriginalSource is TreeViewItem expandedContainer)
            {
                if (expandedContainer.DataContext is not Models.TreeCategoryNode categoryNode) return;

                categoryNode.IsNodeExpanded = true;

                CollapseSiblingsRecursive(GameTree.ItemContainerGenerator, GameTree.Items, expandedContainer.DataContext);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    expandedContainer.BringIntoView();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        // Now that the ItemContainerStyle's IsExpanded binding is OneWay (model -> view only), the
        // model's IsNodeExpanded is no longer auto-updated by WPF when a container collapses - this
        // handler is the explicit write-back for user-initiated collapse (arrow click). Deliberately
        // has no sibling-collapse logic; that's only relevant on expand.
        private void GameTree_Collapsed(object sender, RoutedEventArgs e)
        {
            if (_isTogglingMouseSupport) return;

            if (e.OriginalSource is TreeViewItem collapsedContainer)
            {
                if (collapsedContainer.DataContext is not Models.TreeCategoryNode categoryNode) return;

                categoryNode.IsNodeExpanded = false;
            }
        }

        // Recursively collapses any sibling node that is not the expanded node or one of its ancestors
        private void CollapseSiblingsRecursive(ItemContainerGenerator generator, System.Collections.IEnumerable items, object expandedDataContext)
        {
            foreach (var item in items)
            {
                if (generator.ContainerFromItem(item) is not TreeViewItem container) continue;

                bool isTargetOrAncestor = item == expandedDataContext || IsChildOfNode(item, expandedDataContext);

                if (!isTargetOrAncestor)
                {
                    // Collapse through the model so the container's one-way IsExpanded binding stays
                    // alive - setting container.IsExpanded directly replaces that binding for good.
                    if (item is Models.TreeCategoryNode siblingNode) siblingNode.IsNodeExpanded = false;
                    else container.IsExpanded = false;
                    continue;
                }

                if (item != expandedDataContext && container.IsExpanded)
                {
                    CollapseSiblingsRecursive(container.ItemContainerGenerator, container.Items, expandedDataContext);
                }
            }
        }

        // Recursively checks whether targetItem is a descendant (sub-folder or game) of parentNode
        private bool IsChildOfNode(object parentNode, object targetItem)
        {
            if (parentNode is Models.TreeCategoryNode categoryNode)
            {
                if (categoryNode.ChildGames.Contains(targetItem)) return true;
                foreach (var sub in categoryNode.SubFolders)
                {
                    if (sub == targetItem || sub.ChildGames.Contains(targetItem) || IsChildOfNode(sub, targetItem))
                        return true;
                }
            }
            return false;
        }
        // [END SECTION: TreeView Expand/Collapse Sync]

        // [SECTION: TreeView Container Lookup Helpers]
        // Recursively searches the TreeView's generated containers to find the TreeViewItem for a given data item.
        private TreeViewItem? FindTreeViewItemContainer(ItemsControl parent, object item)
        {
            var container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container != null) return container;

            foreach (var childItem in parent.Items)
            {
                var childContainer = parent.ItemContainerGenerator.ContainerFromItem(childItem) as TreeViewItem;
                if (childContainer != null)
                {
                    var result = FindTreeViewItemContainer(childContainer, item);
                    if (result != null) return result;
                }
            }
            return null;
        }
        // [END SECTION: TreeView Container Lookup Helpers]

        // [SECTION: Randomizer Tree Jump]
        // Expands the winner's ancestor chain in the MAIN tree only (FindMainTreePathToGame already
        // excludes Favorites/Recently Played/Most Played/custom folders), then waits one Loaded-priority
        // dispatcher pass for the now-expanded containers to be generated before selecting and scrolling
        // to the winner's row. No-ops silently if the winner isn't found in the main tree at all (e.g.
        // filtered out by an active sort option) - the tree is simply left as-is in that case.
        private void ExpandAndSelectGameInMainTree(Models.GameItem winner)
        {
            var path = _viewModel.FindMainTreePathToGame(winner);
            if (path == null || path.Count == 0) return;

            // From here until the winner's row is selected (cleared below), null selections caused by the
            // folder collapses are ignored, so SelectedGame stays on the winner and the logo never flashes.
            _isJumpingToRandomWinner = true;

            // Collapse everything NOT on the winner's path first, through the model. Doing this up front
            // means GameTree_Expanded's sibling-collapse has nothing left to collapse afterward, so it
            // can't pull the selection onto a folder mid-jump.
            var pathSet = new HashSet<Models.TreeCategoryNode>(path);
            void CollapseOffPath(IEnumerable<Models.TreeCategoryNode> nodes)
            {
                foreach (var n in nodes)
                {
                    if (!pathSet.Contains(n)) n.IsNodeExpanded = false;
                    CollapseOffPath(n.SubFolders);
                }
            }
            CollapseOffPath(_viewModel.TreeNodesCollection);

            foreach (var node in path)
            {
                node.IsNodeExpanded = true;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Re-assert the real selection - a folder collapse above can have nulled it transiently.
                _viewModel.SelectedGame = winner;

                // The tree is virtualized, so a far-down winner's row may not exist yet. Ask the
                // winner's parent folder's panel to realize that index before looking for the container.
                var parentContainer = FindTreeViewItemContainer(GameTree, path[path.Count - 1]);
                if (parentContainer != null)
                {
                    int index = parentContainer.Items.IndexOf(winner);
                    var panel = FindVisualChild<VirtualizingPanel>(parentContainer);
                    if (panel != null && index >= 0)
                    {
                        panel.BringIndexIntoViewPublic(index);
                    }
                }
                GameTree.UpdateLayout();

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var winnerContainer = FindTreeViewItemContainer(GameTree, winner);
                    if (winnerContainer == null)
                    {
                        _isJumpingToRandomWinner = false;
                        return;
                    }

                    winnerContainer.IsSelected = true;
                    _isJumpingToRandomWinner = false;
                    winnerContainer.BringIntoView();

                    // BringIntoView only guarantees the item is somewhere within the viewport (often
                    // landing at the nearest edge) - this final pass, deferred one more Loaded-priority
                    // dispatch so its own layout/scroll pass has settled, recenters the winner.
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _isJumpingToRandomWinner = false; // safety net in case an earlier step bailed out
                        var scrollViewer = FindVisualChild<ScrollViewer>(GameTree);
                        if (scrollViewer != null)
                        {
                            Point relativePosition = winnerContainer.TransformToAncestor(scrollViewer).Transform(new Point(0, 0));
                            double targetOffset = scrollViewer.VerticalOffset + relativePosition.Y
                                - (scrollViewer.ViewportHeight / 2) + (winnerContainer.ActualHeight / 2);
                            scrollViewer.ScrollToVerticalOffset(targetOffset);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        // [END SECTION: Randomizer Tree Jump]
    }
}
// [END SECTION: File Overrides]