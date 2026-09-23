// [SECTION: File Overrides] - Layered preview and asset management overrides for MainWindow
using ArcadeStick.Services;
using ArcadeStick.ViewModels;
using LibVLCSharp.Shared;
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
using System.Windows.Threading;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

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
        private LibVLC? _libVLC;

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
        private bool _suppressColorHexSync; // prevents the picker's ColorChanged and the hex textbox's LostFocus from re-triggering each other, same pattern as ThemesTabControl
        // [END SECTION: Folder Flyout State]

        public WGIService? GamepadService => _gamepadService;
        public MediaPlayer? VlcMediaPlayer { get; private set; }
        public MediaPlayer? BootSplashMediaPlayer { get; private set; }

        // Tracks which video path is currently loaded into BootSplashMediaPlayer, so
        // RefreshBootSplashVideo() can skip a redundant Stop()/reload when nothing has actually
        // changed (e.g. the RefreshThemeBindings() cascade firing on every Options close) instead of
        // unconditionally restarting playback.
        private string? _currentBootSplashPath;

        // [SECTION: Constructor & LibVLC Setup]
        // Initializes LibVLC (portable path if bundled, else system install), wires media player events,
        // sets up the view model, and hooks window lifecycle + gamepad input.
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

            string libVlcPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "4rcade5tick_files", "libvlc");

            if (Directory.Exists(libVlcPath))
            {
                Core.Initialize(libVlcPath);
            }
            else
            {
                Core.Initialize();
            }

            _libVLC = new LibVLC();
            VlcMediaPlayer = new MediaPlayer(_libVLC);
            VlcMediaPlayer.Mute = true;

            // Looping is handled natively via the input-repeat media option (see media creation),
            // so EndReached no longer needs to manually restart playback.

            // Swallow playback errors (e.g. missing/corrupt preview file) by stopping cleanly
            VlcMediaPlayer.EncounteredError += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() => VlcMediaPlayer.Stop()));
            };

            // Separate player for the boot splash mp4, so it never conflicts with the foreground flyer/video preview
            BootSplashMediaPlayer = new MediaPlayer(_libVLC);
            BootSplashMediaPlayer.Mute = true;

            // Looping is handled natively via the input-repeat media option (see media creation),
            // so EndReached no longer needs to manually restart playback.

            BootSplashMediaPlayer.EncounteredError += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() => BootSplashMediaPlayer.Stop()));
            };

            InitializeComponent();

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _romCopyService = new RomCopyService(_viewModel.Configuration);

            _viewModel.GameLaunchCompleted += RefreshVideoPreview;
            _viewModel.GameLaunchCompleted += RestoreGameListScrollPosition;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.ThemeBindingsRefreshed += RefreshOptionsGearIconRestingColors;
            _viewModel.ThemeBindingsRefreshed += RefreshSortGearIconRestingColors;
            _viewModel.ThemeBindingsRefreshed += RefreshPlayRandomIconRestingColors;
            _viewModel.ThemeBindingsRefreshed += RefreshBootSplashImageCornerClip;

            // Paint the three icon buttons' resting colors immediately - if MainViewModel's constructor
            // above already fired ThemeBindingsRefreshed as part of its own theme-load setup, that event
            // was missed entirely since these subscriptions didn't exist yet, leaving the TextBlocks with
            // no Foreground set at all (black) until the first manual MouseEnter/MouseLeave cycle.
            RefreshOptionsGearIconRestingColors();
            RefreshSortGearIconRestingColors();
            RefreshPlayRandomIconRestingColors();

            _viewModel.RandomizerSpinCompleted += winner =>
            {
                // Jump the game tree to the winner before the reveal popup opens, so it's already
                // expanded/selected underneath by the time the user picks Launch/Respin/Cancel.
                ExpandAndSelectGameInMainTree(winner);

                // Anchored to the same spot as the Confirm popup (under the die button), rather than
                // centered over the marquee panel - keeps both randomizer popups appearing in one
                // consistent location instead of jumping between two different spots mid-flow.
                RandomizerRevealPopup.PlacementTarget = BtnPlayRandom;
                RandomizerRevealPopup.Placement = PlacementMode.Bottom;
                CenterPopupOverTarget(RandomizerRevealPopup, RandomizerRevealContentBorder, BtnPlayRandom);
                RandomizerRevealPopup.IsOpen = true;
            };

            // Fixes a pre-existing WPF quirk affecting every AllowsTransparency="True" Popup in this
            // file (not something introduced by the randomizer) - such popups don't automatically
            // recompute their screen position when the owning Window moves. Nudging HorizontalOffset
            // forces WPF to recalculate placement against the current window position.
            LocationChanged += MainWindow_LocationChanged;

            // Post-load startup sequence: sync ROM paths, load the database, set idle media state, start gamepad polling
            Loaded += async (s, e) =>
            {
                _ = _viewModel.SyncMameRomPathsAsync();
                await _viewModel.InitializeDatabaseAsync();
                ApplyDefaultMedia();
                InitializeGamepadInput();

                // Force a synchronous, fully-settled layout pass before reading any ActualHeight/Margin
                // values - Loaded/LayoutUpdated/Dispatcher priorities can all still fire on a premature
                // intermediate pass during the very first layout cycle.
                UpdateLayout();
                UpdateGameListColumnWidth();
            };

            // Dispose native/unmanaged resources on window close
            Unloaded += (s, e) =>
            {
                _gamepadService?.Dispose();
                VlcMediaPlayer?.Dispose();
                BootSplashMediaPlayer?.Dispose();
                _libVLC?.Dispose();
            };

            KeyDown += MainWindow_KeyDown;
            SizeChanged += MainWindow_SizeChanged;
        }
        // [END SECTION: Constructor & LibVLC Setup]


        // [SECTION: Dynamic Game List Column Sizing]
        // Recalculates the left column's exact pixel width on every resize so the game list panel
        // maintains a fixed 540:1000 (width:height) ratio - measured directly off the Photoshop
        // mockup at fullscreen 1920x1080 - and can never shrink/collapse to the point of clipping
        // game names, regardless of window size. The media panel (right column) simply receives
        // whatever space remains (RightColumnDef stays Star-sized in XAML) - it no longer has its
        // own forced-16:9 pixel computation here; that lock now lives inside the media panel's own
        // control instead.
        private const double GameListWidthToHeightRatio = 540.0 / 1040.0;
        private const double MinGameListWidth = 360.0;

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateGameListColumnWidth();
        }

        private void UpdateGameListColumnWidth()
        {
            // RootLayoutGrid is the true client content area - unlike Window.ActualHeight, it excludes
            // the title bar/chrome, which was the actual source of the boot-size pillarboxing bug
            // (ActualHeight included ~39px of title bar that was never accounted for).
            double rootHeight = RootLayoutGrid.ActualHeight;
            if (rootHeight <= 0) return;

            // Root Row0 height = client area height minus the footer row's actual height (self-derived,
            // no hardcoded constant), minus the left column grid's own top+bottom margin.
            double footerHeight = FooterRowDef.ActualHeight;
            double leftColumnVerticalMargin = LeftColumnGrid.Margin.Top + LeftColumnGrid.Margin.Bottom;
            double availableHeight = rootHeight - footerHeight - leftColumnVerticalMargin;
            if (availableHeight <= 0) return;

            // Ratio calibrated against the real measured runtime availableHeight at fullscreen (1040px),
            // not the Photoshop mockup's assumed 1000px reference - the two didn't line up exactly, which
            // was the actual source of the earlier few-pixel gap (the left/right margin turned out to be
            // a non-factor, since it's drawn inside the column and cancels out of the visible-width math).
            // The 540:1040 ratio describes the visible background content area, but LeftColumnGrid's own
            // left+right margin is drawn inside the column and insets that visible area below the column's
            // actual width - add the margin back so the column ends up wider by exactly that amount,
            // landing the true visible content at 540 regardless of window size (not just at fullscreen).
            double leftColumnHorizontalMargin = LeftColumnGrid.Margin.Left + LeftColumnGrid.Margin.Right;
            double neededWidth = (availableHeight * GameListWidthToHeightRatio) + leftColumnHorizontalMargin;

            // Floor so the game list never shrinks past legibility on very short windows (deeply nested
            // category names truncating, as seen when height drops too low) - the media panel (Star column)
            // simply absorbs whatever extra width this floor leaves unclaimed once the ratio-computed
            // width would otherwise fall below it.
            neededWidth = Math.Max(neededWidth, MinGameListWidth);

            LeftColumnDef.Width = new GridLength(neededWidth, GridUnitType.Pixel);
            RightColumnDef.Width = new GridLength(1, GridUnitType.Star);
        }
        // [END SECTION: Dynamic Game List Column Sizing]

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
        // Manages the LibVLC VideoPreview control's visibility and playback state as game selection changes.
        // NOTE: known beta trade-off - preview stays black after returning from MAME until reselection.

        // Fired whenever the ViewModel's SelectedGame/VideoSourcePath actually resolves (post-debounce) -
        // starts/stops video preview here rather than in GameTree_SelectedItemChanged, so rapid gamepad
        // scrolling doesn't trigger a LibVLC Media load on every intermediate step, only the final selection.
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.VideoSourcePath))
            {
                if (_viewModel.SelectedGame != null && !string.IsNullOrEmpty(_viewModel.VideoSourcePath))
                {
                    MediaPanel.VideoPreview.Visibility = Visibility.Visible;
                    PlayVideoPreview(_viewModel.VideoSourcePath);
                }
                else
                {
                    MediaPanel.VideoPreview.Visibility = Visibility.Collapsed;
                    VlcMediaPlayer?.Stop();
                }
            }

            if (e.PropertyName == nameof(_viewModel.IsGameSelected) || e.PropertyName == nameof(_viewModel.IsBootSplashVideo))
            {
                RefreshBootSplashVideo();
            }
        }

        // Starts/stops the boot splash mp4 based on current selection state and whether the resolved splash asset is a video
        private void RefreshBootSplashVideo()
        {
            if (_libVLC == null || BootSplashMediaPlayer == null) return;

            if (!_viewModel.IsGameSelected && _viewModel.IsBootSplashVideo && !string.IsNullOrEmpty(_viewModel.ThemeBootSplashVideoPath))
            {
                // Skip a fresh load while Options still has window focus - RefreshThemeBindings() re-raises
                // IsBootSplashVideo on close, safely retrying this once the modal dialog is gone
                if (_isOptionsWindowOpen) return;

                // Skip the reload entirely if the correct video is already loaded and actively playing -
                // RefreshThemeBindings() re-raises this property on every Options close regardless of
                // whether anything actually changed, and the resulting unconditional Stop()/reload was
                // traced to a native LibVLC hang. Only reload when the theme's splash path genuinely
                // changed, or playback isn't currently in a healthy running state.
                bool samePathAlreadyPlaying = _currentBootSplashPath == _viewModel.ThemeBootSplashVideoPath
                    && (BootSplashMediaPlayer.State == VLCState.Playing || BootSplashMediaPlayer.State == VLCState.Opening);

                if (samePathAlreadyPlaying) return;

                BootSplashMediaPlayer.Stop();
                using var media = new Media(_libVLC, _viewModel.ThemeBootSplashVideoPath, FromType.FromPath);
                media.AddOption(":input-repeat=65535");
                BootSplashMediaPlayer.Play(media);
                _currentBootSplashPath = _viewModel.ThemeBootSplashVideoPath;
            }
            else
            {
                BootSplashMediaPlayer.Stop();
                _currentBootSplashPath = null;
            }
        }

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

                _viewModel.SelectedGame = e.NewValue as Models.GameItem;
            }
        }

        // Stops any current playback and starts the video preview at the given path
        private void PlayVideoPreview(string path)
        {
            if (_libVLC == null || VlcMediaPlayer == null) return;

            VlcMediaPlayer.Stop();
            using var media = new Media(_libVLC, path, FromType.FromPath);
            media.AddOption(":input-repeat=65535");
            VlcMediaPlayer.Play(media);

            // Nudge a theme-binding refresh shortly after playback starts - both Save and Close on the
            // Options window do this incidentally and it fixes over-wide letterboxing, likely by forcing
            // MediaPanelBorder's clip geometry (bound to ThemeBorderCurve/ThemeBorderWidth) to recompute
            // and fully recomposite the video surface against its actual aspect ratio
            // _ = NudgeVideoAspectRatioAsync();
        //}

          // Waits briefly for LibVLC to finish parsing the video, then re-raises theme bindings to force a recomposite
          //   private async System.Threading.Tasks.Task NudgeVideoAspectRatioAsync()
          //   {
          //   await System.Threading.Tasks.Task.Delay(300);
          //  _viewModel.RefreshThemeBindings();
             }

        // Re-evaluates and restarts the video preview for the currently selected game (called after returning from a game launch)
        private void RefreshVideoPreview()
        {
            if (_viewModel.SelectedGame != null && !string.IsNullOrEmpty(_viewModel.VideoSourcePath))
            {
                MediaPanel.VideoPreview.Visibility = Visibility.Visible;
                PlayVideoPreview(_viewModel.VideoSourcePath);
            }
            else
            {
                MediaPanel.VideoPreview.Visibility = Visibility.Collapsed;
                VlcMediaPlayer?.Stop();
            }
        }

        // Sets the idle/boot-splash media state at startup (no game selected, no video playing)
        // Collapses and stops the video preview right as a spin begins - the RandomizerSpinMediaOverlay
        // Border can't visually hide it on its own, since LibVLC's VideoView is backed by a real HWND
        // that paints itself regardless of WPF Z-order (the same airspace issue noted elsewhere in this
        // file). VideoPreview's visibility is otherwise owned entirely by ViewModel_PropertyChanged/
        // RefreshVideoPreview/etc., so this only ever collapses it - the normal selection-change pipeline
        // is what reveals it again once a winner is set (or leaves it collapsed if the winner has none).
        private void StopVideoPreviewForSpin()
        {
            MediaPanel.VideoPreview.Visibility = Visibility.Collapsed;
            VlcMediaPlayer?.Stop();
        }

        private void ApplyDefaultMedia()
        {
            MediaPanel.VideoPreview.Visibility = Visibility.Collapsed;
            VlcMediaPlayer?.Stop();
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
                else if (rowContainer.DataContext is Models.TreeCategoryNode)
                {
                    e.Handled = true;
                    rowContainer.IsExpanded = !rowContainer.IsExpanded;
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

            var targetContainer = FindTreeViewItemContainer(GameTree, _viewModel.SelectedGame);
            if (targetContainer == null) return;

            _romCopyStatusTooltip = new System.Windows.Controls.ToolTip
            {
                Content = message,
                PlacementTarget = targetContainer,
                Placement = PlacementMode.Bottom,
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
            // TextElement.Foreground (ThemeContextMenuIconColor normally, ThemeContextMenuHoverColor on
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
            // -> VlcMediaPlayer.Stop() from there hangs the UI thread (same class of issue as the
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
            // touches VlcMediaPlayer.Stop().
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

        // [SECTION: Context Menu Text Size]
        // Shared by both FolderRowContextMenu's and GameRowContextMenu's "Text Size" submenus - each
        // percent item's Tag carries the multiplier (e.g. "-0.15", "0.05"), parsed with InvariantCulture
        // since ini/culture-formatted decimals could otherwise misparse the minus sign or decimal point.
        private void TextSizeNudge_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string tagStr &&
                double.TryParse(tagStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double percent))
            {
                _viewModel.NudgeFontSize(percent);
            }
        }

        private void TextSizeReset_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ResetFontSizeToSavedTheme();
        }
        // [END SECTION: Context Menu Text Size]

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

        // Pushes a manually-typed hex value back into the picker (and live-applies it), mirroring
        // ThemesTabControl's TxtXColorHex_LostFocus pattern. Invalid text is left alone.
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
            else
            {
                Dispatcher.BeginInvoke(new Action(() => _viewModel.SetCustomFolderColor(colorFolderName, hex)));
            }
        }

        // Parses a hex string into a Color, returning null instead of throwing on invalid input. Mirrors
        // ThemesTabControl.TryParseColor.
        private static Color? TryParseFolderColor(string hexText)
        {
            try
            {
                var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hexText);
                return brush.Color;
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

        // "Rename folder", "Choose folder color", and "Delete folder" are enabled only for custom folders
        // (playlist .cfg files) - Favorites, virtual/catver categories, and physical GAMES-bucket folders
        // are never renamable/recolorable/deletable this way. The resolved on-disk folder name is stashed
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

            // Choose Color is available for custom folders AND the two static play-history folders. The
            // Tag becomes the node's own HeaderText for the static folders, since they have no separate
            // "on-disk name" the way a custom folder's filename does.
            bool canRecolor = isCustomFolder || isRecentlyPlayed || isMostPlayed;
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
        // dismissal frame, and the tree rebuild this triggers touches VlcMediaPlayer.Stop().
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
            FolderNameInputPopup.HorizontalOffset = -20;
            FolderNameInputPopup.VerticalOffset = -18;
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

                VlcMediaPlayer?.Stop();
                MediaPanel.VideoPreview.Visibility = Visibility.Collapsed;
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

            if (e.Key == Key.F11)
            {
                e.Handled = true;
                if (WindowStyle != WindowStyle.None)
                {
                    WindowState = WindowState.Normal;
                    WindowStyle = WindowStyle.None;
                    ResizeMode = ResizeMode.NoResize;
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    WindowState = WindowState.Normal;
                    WindowStyle = WindowStyle.ThreeDBorderWindow;
                    ResizeMode = ResizeMode.CanResize;
                    Width = 1296;
                    Height = 759;
                    UpdateLayout();
                    UpdateGameListColumnWidth();
                }
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
                _viewModel.IsDevMode = !_viewModel.IsDevMode;

                if (_viewModel.IsDevMode)
                {
                    ResizeMode = ResizeMode.CanResize;
                }
                else
                {
                    ResizeMode = ResizeMode.CanResize;
                    WindowStyle = WindowStyle.ThreeDBorderWindow;
                    Width = 1296;
                    Height = 759;
                }
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
                }
                return;
            }

            // TEMPORARY - Phase 1 randomizer smoke test only, delete once the real die button exists.
            if (isCtrl && e.Key == Key.R)
            {
                e.Handled = true;
                _viewModel.DebugTestRandomizerSpin();
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
            // BootSplashMediaPlayer is intentionally left untouched here - no Pause() on open, no
            // restart on close. It was previously paused/resumed around the Options dialog, but the
            // resulting reload cascade (via RefreshThemeBindings() below re-raising IsBootSplashVideo)
            // was traced to a native LibVLC hang. Letting it keep playing uninterrupted avoids the
            // stop/restart calls entirely for the common case where nothing about it actually changed.
            VlcMediaPlayer?.Pause();

            _isOptionsWindowOpen = true;
            _viewModel.IsOptionsWindowOpen = true;

            var adjustmentsPanel = new Views.OptionsWindow(_viewModel) { Owner = this };

            if (_gamepadService != null)
            {
                adjustmentsPanel.WireLiveDiagnostics(_gamepadService);
                _gamepadService.TriggerDiagnosticsUpdate();
            }

            adjustmentsPanel.ShowDialog();

            _isOptionsWindowOpen = false;
            _viewModel.IsOptionsWindowOpen = false;

            // TEMP TEST: commented out to check whether this is still needed now that buttons were
            // rewired to their own plain Theme* properties, or whether it's the source of the residual
            // memory growth on Options close. Re-enable if anything stops updating live after this test.
            // _viewModel.RefreshThemeBindings();
            RefreshOptionsGearIconRestingColors();

            // Resume rather than reload - VlcMediaPlayer was paused (not stopped) on open, so Play() with
            // no arguments just continues from where it left off instead of restarting from the beginning
            VlcMediaPlayer?.Play();
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
        }
        // [END SECTION: Sorting Window]

        // [SECTION: Options Gear Icon Hover]
        // Swaps the gear badge's background between the games list background and games list hover
        // theme colors on mouse enter/leave for a simple hover affordance.
        private void OptionsGearIcon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            OptionsGearIcon.Foreground = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnColorHover);
            BtnOpenOptionsGear.Background = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnBgColorHover);
            DelayedTooltip_MouseEnter(sender, e);
        }

        private void OptionsGearIcon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RefreshOptionsGearIconRestingColors();
            DelayedTooltip_MouseLeave(sender, e);
        }

        // Applies the gear icon's resting-state colors (folder font color / games list background) - shared
        // by MouseLeave and by the Options-close path, so the button reflects a new theme immediately rather
        // than only refreshing on the next real hover
        private void RefreshOptionsGearIconRestingColors()
        {
            OptionsGearIcon.Foreground = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnColor);
            BtnOpenOptionsGear.Background = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnBgColor);
        }
        // [END SECTION: Options Gear Icon Hover]

        // [SECTION: Sort Gear Icon Hover]
        // Mirrors the Options gear icon's hover treatment exactly - same theme-color swap on
        // mouse enter/leave, same live-refresh-on-theme-load wiring via ThemeBindingsRefreshed.
        private void SortGearIcon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            SortGearIcon.Foreground = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnColorHover);
            BtnOpenSortingGear.Background = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnBgColorHover);
            DelayedTooltip_MouseEnter(sender, e);
        }

        private void SortGearIcon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RefreshSortGearIconRestingColors();
            DelayedTooltip_MouseLeave(sender, e);
        }

        // [SECTION: Play Random Icon Hover]
        // Same pattern as Sort/Options gear hover - handled in code-behind rather than XAML triggers
        // since GameColor/ArrowColor-style properties were moved off RelativeSource bindings for
        // performance, and this icon's colors follow that same direct-push convention. Subscribed to
        // ThemeBindingsRefreshed in the constructor so the resting color stays correct if the user
        // changes themes while not currently hovering this icon.
        private void PlayRandomIcon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            PlayRandomIcon.Foreground = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnColorHover);
            BtnPlayRandom.Background = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnBgColorHover);
            DelayedTooltip_MouseEnter(sender, e);
        }

        private void PlayRandomIcon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RefreshPlayRandomIconRestingColors();
            DelayedTooltip_MouseLeave(sender, e);
        }

        private void RefreshPlayRandomIconRestingColors()
        {
            PlayRandomIcon.Foreground = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnColor);
            BtnPlayRandom.Background = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnBgColor);
        }
        // [END SECTION: Play Random Icon Hover]

        // [SECTION: Randomizer Confirm Flyout]
        // Opens the small "Spin?" confirm popup anchored directly under the die button - guards against
        // accidental clicks (e.g. a miss-click intended for Sort or the search box) before any spin logic
        // runs.
        private void BtnPlayRandom_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Pool-size guard fires here, at die-click time, before the confirm popup even opens -
            // per the agreed design, rather than letting the user confirm first and find out after.
            if (!_viewModel.IsRandomizerPoolEligible())
            {
                MessageBox.Show(
                    "Not enough games match your current filter settings to spin (fewer than 10 eligible). Try loosening your filter in the Sorting window.",
                    "Play Random Game", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            RandomizerConfirmPopup.PlacementTarget = BtnPlayRandom;
            RandomizerConfirmPopup.Placement = PlacementMode.Bottom;
            CenterPopupOverTarget(RandomizerConfirmPopup, RandomizerConfirmContentBorder, BtnPlayRandom);
            RandomizerConfirmPopup.IsOpen = true;
        }

        private void RandomizerConfirmYesButton_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RandomizerConfirmPopup.IsOpen = false;
            StopVideoPreviewForSpin();
            _viewModel.StartRandomizerSpin();
        }

        private void RandomizerConfirmNoButton_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RandomizerConfirmPopup.IsOpen = false;
        }

        private void RandomizerConfirmPopup_Closed(object? sender, EventArgs e)
        {
            // No cleanup needed yet - placeholder to match the existing flyout Closed-handler pattern
            // (FolderNameInputPopup_Closed / FolderColorPickerPopup_Closed) in case Phase 3+ needs to
            // reset confirm-flyout state here later.
        }
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
            RepositionOpenPopup(RandomizerConfirmPopup);
            RepositionOpenPopup(RandomizerRevealPopup);
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
        private static void CenterPopupOverTarget(System.Windows.Controls.Primitives.Popup popup, FrameworkElement content, FrameworkElement target)
        {
            content.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            popup.HorizontalOffset = (target.ActualWidth - content.DesiredSize.Width) / 2;
        }
        // [END SECTION: Popup Centering]

        // [SECTION: Randomizer Reveal Flyout]
        // Launch reuses the exact same pipeline as the tree's own double-click launch - set SelectedGame
        // to the winner, then hand off to TriggerActiveSelectionLaunch. This gets play-history logging,
        // tree refresh, and video-preview handling for free, and confirms setting SelectedGame from code
        // does NOT force the TreeView's visual selection to jump/scroll (no two-way IsSelected binding
        // wired up) - consistent with the "no tree sync needed" design decision.
        private void RandomizerRevealLaunchButton_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RandomizerRevealPopup.IsOpen = false;
            _viewModel.IsRandomizerResultShowing = false;

            var winner = _viewModel.RandomizerCurrentFrameGame;
            if (winner != null)
            {
                _viewModel.SelectedGame = winner;
                TriggerActiveSelectionLaunch();
            }
        }

        private void RandomizerRevealRespinButton_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RandomizerRevealPopup.IsOpen = false;
            _viewModel.IsRandomizerResultShowing = false;
            StopVideoPreviewForSpin();
            _viewModel.StartRandomizerSpin();
        }

        private void RandomizerRevealCancelButton_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RandomizerRevealPopup.IsOpen = false;
            _viewModel.IsRandomizerResultShowing = false;
        }

        // Fires whenever the popup closes for ANY reason - explicit button clicks above already set
        // IsOpen=false themselves (which triggers this too, redundantly but harmlessly), and now also
        // an outside click (StaysOpen="False"), which otherwise would have left IsRandomizerResultShowing
        // stuck true with no equivalent cleanup, same gap Cancel's handler already covers.
        private void RandomizerRevealPopup_Closed(object? sender, EventArgs e)
        {
            _viewModel.IsRandomizerResultShowing = false;
        }
        // [END SECTION: Randomizer Reveal Flyout]

        // Applies the Sort gear icon's resting-state colors (folder font color / games list background) -
        // shared by MouseLeave and by the theme-refresh event, so the button reflects a new theme
        // immediately rather than only refreshing on the next real hover
        private void RefreshSortGearIconRestingColors()
        {
            SortGearIcon.Foreground = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnColor);
            BtnOpenSortingGear.Background = MainViewModel.SafeConvertToBrush(_viewModel.ThemeMainWinBtnBgColor);
        }
        // [END SECTION: Sort Gear Icon Hover]

        // [SECTION: Boot Splash Corner Clip Refresh]
        // MediaLayer0_Back's CornerRadius is evaluated inside the Viewbox's pre-scaled 1600x900 canvas, so
        // its rounded-corner render doesn't self-repaint on a theme change the way it does on a manual
        // window resize (which forces a full layout pass). Force that same invalidation here so the boot
        // splash image's corners stay correctly rounded immediately after a theme swap.
        private void RefreshBootSplashImageCornerClip()
        {
            MediaPanel.MediaLayer0_Back.InvalidateMeasure();
            MediaPanel.MediaLayer0_Back.InvalidateArrange();
            MediaPanel.MediaLayer0_Back.InvalidateVisual();
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
                    container.IsExpanded = false;
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

            foreach (var node in path)
            {
                node.IsNodeExpanded = true;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var winnerContainer = FindTreeViewItemContainer(GameTree, winner);
                if (winnerContainer != null)
                {
                    winnerContainer.IsSelected = true;
                    winnerContainer.BringIntoView();

                    // BringIntoView only guarantees the item is somewhere within the viewport (often
                    // landing at the nearest edge) - this second pass, deferred one more Loaded-priority
                    // dispatch so BringIntoView's own layout/scroll pass has already settled, manually
                    // recenters the winner in the middle of the visible list instead.
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var scrollViewer = FindVisualChild<ScrollViewer>(GameTree);
                        if (scrollViewer != null)
                        {
                            Point relativePosition = winnerContainer.TransformToAncestor(scrollViewer).Transform(new Point(0, 0));
                            double targetOffset = scrollViewer.VerticalOffset + relativePosition.Y
                                - (scrollViewer.ViewportHeight / 2) + (winnerContainer.ActualHeight / 2);
                            scrollViewer.ScrollToVerticalOffset(targetOffset);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        // [END SECTION: Randomizer Tree Jump]
    }
}
// [END SECTION: File Overrides]