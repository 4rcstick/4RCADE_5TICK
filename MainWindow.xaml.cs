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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LibVLCSharp.Shared;
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
        private bool _isTogglingMouseSupport;
        private bool _isOptionsWindowOpen;
        private LibVLC? _libVLC;

        public WGIService? GamepadService => _gamepadService;
        public MediaPlayer? VlcMediaPlayer { get; private set; }
        public MediaPlayer? BootSplashMediaPlayer { get; private set; }

        // [SECTION: Constructor & LibVLC Setup]
        // Initializes LibVLC (portable path if bundled, else system install), wires media player events,
        // sets up the view model, and hooks window lifecycle + gamepad input.
        public MainWindow()
        {
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

            _viewModel.GameLaunchCompleted += RefreshVideoPreview;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

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
                UpdateMediaColumnWidth();
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

        // [SECTION: Dynamic Media Column Sizing]
        // Recalculates the right column's exact pixel width on every resize so the media panel's
        // Viewbox (fixed 1600x900 / 16:9 canvas) fits without letterboxing at any resolution.
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMediaColumnWidth();
        }

        private void UpdateMediaColumnWidth()
        {
            // RootLayoutGrid is the true client content area - unlike Window.ActualHeight, it excludes
            // the title bar/chrome, which was the actual source of the boot-size pillarboxing bug
            // (ActualHeight included ~39px of title bar that was never accounted for).
            double rootHeight = RootLayoutGrid.ActualHeight;
            if (rootHeight <= 0) return;

            // Root Row0 height = client area height minus the footer row's actual height (self-derived,
            // no hardcoded constant), minus the right column grid's own top+bottom margin.
            double footerHeight = FooterRowDef.ActualHeight;
            double rightColumnVerticalMargin = RightColumnGrid.Margin.Top + RightColumnGrid.Margin.Bottom;
            double availableHeight = rootHeight - footerHeight - rightColumnVerticalMargin;
            if (availableHeight <= 0) return;

            // Media row's share of availableHeight, derived from the live marquee/media star ratio
            // instead of an assumed 0.4/1 split - stays correct if those RowDefinitions ever change.
            double marqueeStars = ((GridLength)MarqueeRowDef.Height).Value;
            double mediaStars = ((GridLength)MediaRowDef.Height).Value;
            double totalStars = marqueeStars + mediaStars;
            if (totalStars <= 0) return;

            double mediaRowHeight = availableHeight * (mediaStars / totalStars);

            // The media panel Border's own BorderThickness is drawn inside its bounds, shrinking the
            // actual content area available to the Viewbox - account for it on both axes so the 16:9
            // fit is calculated against the true interior size, not the outer row/column bounds.
            double mediaBorderVertical = MediaPanelBorder.BorderThickness.Top + MediaPanelBorder.BorderThickness.Bottom;
            double mediaBorderHorizontal = MediaPanelBorder.BorderThickness.Left + MediaPanelBorder.BorderThickness.Right;

            double mediaContentHeight = mediaRowHeight - mediaBorderVertical;
            double neededContentWidth = mediaContentHeight * 16.0 / 9.0;

            // Width needed for a true 16:9 fit, plus the right column grid's own left+right margin
            // and the border's own horizontal thickness (added back since it's drawn inside the column).
            double rightColumnHorizontalMargin = RightColumnGrid.Margin.Left + RightColumnGrid.Margin.Right;
            double neededWidth = neededContentWidth + rightColumnHorizontalMargin + mediaBorderHorizontal;

            RightColumnDef.Width = new GridLength(neededWidth, GridUnitType.Pixel);
            LeftColumnDef.Width = new GridLength(1, GridUnitType.Star);

            // The full-bleed image layers live inside the Viewbox's fixed 1600x900 canvas, so their
            // CornerRadius gets scaled down along with the image when the Viewbox shrinks that canvas
            // to fit the panel - unlike MediaPanelBorder's own radius, which is real screen pixels and
            // never scales. Compensate by the inverse of the Viewbox's scale factor so the inner corner
            // curve lands at the same effective on-screen radius as the outer border, instead of coming
            // out tighter/sharper and poking past it.
            double viewboxScale = neededContentWidth / 1600.0;
            double compensatedCornerRadius = viewboxScale > 0 ? _viewModel.ThemeBorderCurve / viewboxScale : _viewModel.ThemeBorderCurve;
            MediaLayer0_Back.CornerRadius = new CornerRadius(compensatedCornerRadius);
            MediaLayer1_Back.CornerRadius = new CornerRadius(compensatedCornerRadius);

            // TEMP DEBUG - remove once the pillarboxing issue is confirmed fixed
            System.Diagnostics.Debug.WriteLine(
                $"[MediaColSizing] WindowActualHeight={ActualHeight:F1} FooterActualHeight={footerHeight:F1} " +
                $"RightColMargin=({RightColumnGrid.Margin.Left},{RightColumnGrid.Margin.Top},{RightColumnGrid.Margin.Right},{RightColumnGrid.Margin.Bottom}) " +
                $"availableHeight={availableHeight:F1} mediaRowHeight={mediaRowHeight:F1} " +
                $"neededWidth={neededWidth:F1} RightColumnDef.ActualWidth={RightColumnDef.ActualWidth:F1} " +
                $"RightColumnGrid.ActualHeight={RightColumnGrid.ActualHeight:F1} RightColumnGrid.ActualWidth={RightColumnGrid.ActualWidth:F1}");
        }
        // [END SECTION: Dynamic Media Column Sizing]

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
                    VideoPreview.Visibility = Visibility.Visible;
                    PlayVideoPreview(_viewModel.VideoSourcePath);
                }
                else
                {
                    VideoPreview.Visibility = Visibility.Collapsed;
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

                BootSplashMediaPlayer.Stop();
                using var media = new Media(_libVLC, _viewModel.ThemeBootSplashVideoPath, FromType.FromPath);
                media.AddOption(":input-repeat=65535");
                BootSplashMediaPlayer.Play(media);
            }
            else
            {
                BootSplashMediaPlayer.Stop();
            }
        }

        // Fired when the TreeView selection changes; just updates SelectedGame - video preview playback
        // is handled separately in ViewModel_PropertyChanged, once VideoSourcePath actually resolves.
        private void GameTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_viewModel != null)
            {
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
            _ = NudgeVideoAspectRatioAsync();
        }

        // Waits briefly for LibVLC to finish parsing the video, then re-raises theme bindings to force a recomposite
        private async System.Threading.Tasks.Task NudgeVideoAspectRatioAsync()
        {
            await System.Threading.Tasks.Task.Delay(300);
            _viewModel.RefreshThemeBindings();
        }

        // Re-evaluates and restarts the video preview for the currently selected game (called after returning from a game launch)
        private void RefreshVideoPreview()
        {
            if (_viewModel.SelectedGame != null && !string.IsNullOrEmpty(_viewModel.VideoSourcePath))
            {
                VideoPreview.Visibility = Visibility.Visible;
                PlayVideoPreview(_viewModel.VideoSourcePath);
            }
            else
            {
                VideoPreview.Visibility = Visibility.Collapsed;
                VlcMediaPlayer?.Stop();
            }
        }

        // Sets the idle/boot-splash media state at startup (no game selected, no video playing)
        private void ApplyDefaultMedia()
        {
            VideoPreview.Visibility = Visibility.Collapsed;
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
                else if (rowContainer.DataContext is ViewModels.TreeCategoryNode)
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
        // [END SECTION: Mouse Double-Click Launch]

        // Stops the video preview and hands off to the LaunchGameCommand for the currently selected game
        private void TriggerActiveSelectionLaunch()
        {
            if (_viewModel.SelectedGame != null && _viewModel.LaunchGameCommand.CanExecute(this))
            {
                VlcMediaPlayer?.Stop();
                VideoPreview.Visibility = Visibility.Collapsed;
                _viewModel.LaunchGameCommand.Execute(this);
            }
        }

        // [SECTION: Keyboard Shortcuts]
        // Global hotkey handling: Ctrl+O (Options), F11 (fullscreen toggle), Ctrl+F (favorite),
        // Ctrl+D (dev mode), Ctrl+C (copy ROM name), Escape (close), Ctrl+G (playlists), Ctrl+M (mouse support toggle).
        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

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
                    UpdateMediaColumnWidth();
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
                    ResizeMode = ResizeMode.NoResize;
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
        }
        // [END SECTION: Keyboard Shortcuts]

        // [SECTION: Options Window]
        // Opens the Options window modally, pauses video playback while it's open, wires live gamepad
        // diagnostics, and refreshes theme bindings + the video preview once it closes.
        private void OpenOptionsWindow()
        {
            VlcMediaPlayer?.Pause();
            BootSplashMediaPlayer.Pause();

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

            _viewModel.RefreshThemeBindings();
            RefreshOptionsGearIconRestingColors();

            // Resume rather than reload - both players were paused (not stopped) on open, so Play() with
            // no arguments just continues from where they left off instead of restarting from the beginning
            VlcMediaPlayer?.Play();
            BootSplashMediaPlayer?.Play();
        }

        // Footer gear icon click handler - opens the Options window
        private void BtnOpenOptionsGear_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenOptionsWindow();
        }
        // [END SECTION: Options Window]

        // [SECTION: Options Gear Icon Hover]
        // Swaps the gear badge's background between the games list background and games list hover
        // theme colors on mouse enter/leave for a simple hover affordance.
        private void OptionsGearIcon_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            OptionsGearIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_viewModel.ThemeGameSelectedColor));
            BtnOpenOptionsGear.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_viewModel.ThemeGameSelectedBgColor));
        }

        private void OptionsGearIcon_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            RefreshOptionsGearIconRestingColors();
        }

        // Applies the gear icon's resting-state colors (folder font color / games list background) - shared
        // by MouseLeave and by the Options-close path, so the button reflects a new theme immediately rather
        // than only refreshing on the next real hover
        private void RefreshOptionsGearIconRestingColors()
        {
            OptionsGearIcon.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_viewModel.ThemeFolderColor));
            BtnOpenOptionsGear.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_viewModel.ThemeGamesColor));
        }
        // [END SECTION: Options Gear Icon Hover]

        // [SECTION: TreeView Expand/Collapse Sync]
        // Enforces single-branch-open behavior: expanding a folder collapses sibling folders that
        // aren't ancestors of the expanded node, and scrolls the expanded node into view.
        private void GameTree_Expanded(object sender, RoutedEventArgs e)
        {
            if (_isTogglingMouseSupport) return;

            if (e.OriginalSource is TreeViewItem expandedContainer)
            {
                if (expandedContainer.DataContext is not ViewModels.TreeCategoryNode) return;

                CollapseSiblingsRecursive(GameTree.ItemContainerGenerator, GameTree.Items, expandedContainer.DataContext);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    expandedContainer.BringIntoView();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
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
            if (parentNode is ViewModels.TreeCategoryNode categoryNode)
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
    }
}
// [END SECTION: File Overrides]