using System;
using System.IO;
using System.Windows;

namespace ArcadeStick.Views
{
    public partial class ThemeBuilderWindow : Window
    {
        private readonly ArcadeStick.Models.ConfigurationSettings _settings;
        private readonly ArcadeStick.ViewModels.MainViewModel _viewModel;

        private System.Collections.Generic.List<ArcadeStick.Models.ThemeLayout> _layoutsAtLastSave = new();
        private int _resolutionWidthAtLastSave;
        private int _resolutionHeightAtLastSave;
        // The flat Global tab values at the same restore point (theme-wide, so the size switch never moves it)
        private ArcadeStick.Models.ThemeSettings _globalAtLastSave = new();
        // The theme info block at the same restore point. A pending thumbnail is always written by a save or dropped by a
        // load, so the restore point never holds one: closing without saving just discards it.
        private ArcadeStick.Models.ThemeInfo _infoAtLastSave = new();
        private System.Windows.Threading.DispatcherTimer? _themeSavedFlashTimer;

        // [SECTION: Constructor & Tab Initialization]
        // Anchors DataContext to the live ConfigurationSettings (same as OptionsWindow, so the window
        // inherits the exact same theme bindings), then hands the Themes Manager row the persist/refresh
        // callbacks it needs to save settings.json and re-evaluate this window's bindings independently.
        public ThemeBuilderWindow(ArcadeStick.ViewModels.MainViewModel viewModel)
        {
            InitializeComponent();
            ArcadeStick.Services.WindowFit.KeepInWorkArea(this); // never taller than the monitor's work area
            _viewModel = viewModel;
            _settings = viewModel.Configuration;

            this.DataContext = _settings;

            ThemesManager.Initialize(_viewModel, _settings, PersistSettingsToDisk, RefreshAfterThemeLoad);
            ThemesManager.SetThemeSavedCallback(() =>
            {
                AdvancedTab.RefreshSavedSizes();
                // Save Changes wrote the theme zip (which includes the current size), so a pending size is now kept
                AdvancedTab.CommitPendingSize();
                // what was just saved becomes the close restore point
                _layoutsAtLastSave = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_settings.Layouts);
                _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
                _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
                _globalAtLastSave = ArcadeStick.Services.GlobalThemeResources.CaptureSnapshot(_settings);
                _infoAtLastSave = _settings.Info.Clone();
                ArcadeStick.Services.ThemeThumbnailStore.RemoveUnusedFolders(_settings, _infoAtLastSave);
                AdvancedTab.RefreshThemeInfo();
                ShowThemeSavedFlash();
            });
            ThemesManager.ThemeCreatedFlash = ShowThemeSavedFlash;
            AdvancedTab.Initialize(_settings);
            AdvancedTab.ThumbnailCaptureRunner = () => (Owner as MainWindow)?.CaptureThemeThumbnail();
            GlobalTab.Initialize(_settings);
            ThemeBrowser.Initialize(_settings); // before the tab handler below can call RefreshCatalog
            ThemeBrowser.LoadRequested += LoadThemeFromBrowser;
            ThemeBrowser.PreviewRequested += ShowThemePreview;

            // The preview never outlives the window's focus: losing activation or closing dismisses it first
            Deactivated += (s, e) => PreviewOverlay.Hide();
            Closing += (s, e) => PreviewOverlay.Hide();

            // Alignment guides: forward the Advanced tab's guide info to the main window, but only while the
            // THEME BUILDER tab (ThemeBuilderPage) is selected. Cleared when switching to another tab or closing.
            AdvancedTab.GuidesChanged += info =>
            {
                if (!ReferenceEquals((ThemeBuilderTabControl.SelectedItem as System.Windows.Controls.TabItem)?.Content, ThemeBuilderPage)) info = null;
                (Owner as MainWindow)?.ShowGuides(info);
            };

            ThemeBuilderTabControl.SelectionChanged += (s, e) =>
            {
                // SelectionChanged bubbles up from the ListBox and ComboBox inside the tabs - only react to the tab strip itself
                if (!ReferenceEquals(e.OriginalSource, ThemeBuilderTabControl)) return;

                if (ReferenceEquals((ThemeBuilderTabControl.SelectedItem as System.Windows.Controls.TabItem)?.Content, ThemeBuilderPage))
                {
                    AdvancedTab.RaiseGuides();
                    AdvancedTab.RaiseGrid();
                }
                else
                {
                    (Owner as MainWindow)?.ShowGuides(null);
                    (Owner as MainWindow)?.SetBackgroundGrid(false, 0); // hidden, not unchecked: it returns with THEME BUILDER
                    AdvancedTab.StopPreviews(); // leaving THEME BUILDER turns the card and banner previews off

                    // The Browser refreshes once each time it is shown (ShowBrowserTab covers an already selected tab)
                    if (ReferenceEquals((ThemeBuilderTabControl.SelectedItem as System.Windows.Controls.TabItem)?.Content, ThemeBrowser))
                    {
                        ThemeBrowser.RefreshCatalog();
                    }
                }
            };

            // Background grid: forwarded like the guides, and only shown while THEME BUILDER is selected. Not part of
            // StopPreviews, so a layer, resolution or theme change never turns it off.
            AdvancedTab.GridChanged += (on, spacing, offsetX, offsetY) =>
            {
                bool onBuilder = ReferenceEquals((ThemeBuilderTabControl.SelectedItem as System.Windows.Controls.TabItem)?.Content, ThemeBuilderPage);
                (Owner as MainWindow)?.SetBackgroundGrid(on && onBuilder, spacing, offsetX, offsetY);
            };

            // Random Game Display card preview: forwarded to the main window like the guides
            AdvancedTab.RandomPreviewChanged += (on, fromSplash) => (Owner as MainWindow)?.SetRandomCardPreview(on, fromSplash);

            // Marquee banner preview: forwarded the same way
            AdvancedTab.BannerPreviewChanged += on => (Owner as MainWindow)?.SetBannerPreview(on);

            // Live layout: every committed Advanced edit re-applies the layout on the main window.
            // Nothing is written to disk here - only Save Adjustments / Save Changes do that.
            AdvancedTab.LayoutChanged += () => (Owner as MainWindow)?.ApplyLayoutNow();

            // Size switches (Resolutions buttons, size combo) follow the same Smart fullscreen pick rules as the hamburger Layout size
            AdvancedTab.SizePickRunner = run =>
            {
                if (Owner is MainWindow m) m.RunSizePick(run);
                else run();
            };

            // Load Theme follows the same Smart fullscreen theme switch rules as the hamburger theme switch
            ThemesManager.ThemeLoadRunner = load =>
            {
                if (Owner is MainWindow m) m.RunThemeSwitch(load);
                else load();
            };

            // Restore point for closing without saving: the layouts and Global values as of window open / last Save Adjustments.
            _layoutsAtLastSave = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_settings.Layouts);
            _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
            _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
            _globalAtLastSave = ArcadeStick.Services.GlobalThemeResources.CaptureSnapshot(_settings);
            _infoAtLastSave = _settings.Info.Clone();

            Closed += (s, e) =>
            {
                // Put back the last saved layouts so abandoned edits don't linger on the main window
                _settings.Layouts = _layoutsAtLastSave;
                _settings.ActiveResolutionWidth = _resolutionWidthAtLastSave;
                _settings.ActiveResolutionHeight = _resolutionHeightAtLastSave;

                // Same for the Global values, then republish their app resources
                ArcadeStick.Services.GlobalThemeResources.RestoreSnapshot(_globalAtLastSave, _settings, ArcadeStick.Services.GlobalArea.All);
                ArcadeStick.Services.GlobalThemeResources.Apply(_settings);

                // Same for the theme info: unsaved edits and any pending thumbnail are discarded
                _settings.Info = _infoAtLastSave;
                _settings.PendingThemeThumbnail = null;

                var main = Owner as MainWindow;
                main?.ShowGuides(null);
                main?.SetBackgroundGrid(false, 0); // closing hides the grid and gives the wallpaper back
                main?.SetRandomCardPreview(false, false); // closing turns the card and banner previews off
                main?.SetBannerPreview(false);
                main?.ApplyLayoutNow();

                // Closing can leave the launcher behind other programs' windows
                main?.BringToFrontAfterChildClose();
            };
        }

        // Passed to ThemesManager as its refresh callback. A theme load replaces _settings.Layouts and the Global
        // values, so the Advanced and Global tabs re-read them in addition to the normal binding refresh.
        // Save Adjustments deliberately calls RefreshWindowBindings only, so the Advanced tab keeps its
        // current layer selection when you save.
        private void RefreshAfterThemeLoad()
        {
            RefreshWindowBindings();

            // A pending thumbnail belonged to the previous theme. Cleared here, not in ReloadFromSettings: a size switch runs that too.
            _settings.PendingThemeThumbnail = null;

            AdvancedTab.ReloadFromSettings();
            GlobalTab.ReloadFromSettings();
            AdvancedTab.RefreshThemeInfo();

            // A theme load is a deliberate change: the loaded layouts and Global values become the restore point for closing
            _layoutsAtLastSave = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_settings.Layouts);
            _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
            _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
            _globalAtLastSave = ArcadeStick.Services.GlobalThemeResources.CaptureSnapshot(_settings);
            _infoAtLastSave = _settings.Info.Clone();
        }

        // Called by MainWindow after the theme is switched from the hamburger menu. Reloads the manager row and tabs and
        // moves the close-without-saving restore point to the newly loaded theme.
        public void RefreshAfterExternalThemeSwitch()
        {
            ThemesManager.ReloadFromSettings();
            RefreshAfterThemeLoad();
            ThemeBrowser.RefreshActiveMarks(); // the Browser's highlight and active marker follow the new theme and size
        }

        // Called by MainWindow when a real spin ended the card preview, so the checkbox unchecks
        public void RandomCardPreviewEndedBySpin()
        {
            AdvancedTab.ClearRandomPreview();
        }

        // Called by MainWindow when a real spin ended the banner preview, so the checkbox unchecks
        public void BannerPreviewEndedBySpin()
        {
            AdvancedTab.ClearBannerPreview();
        }

        // Called by MainWindow after the layout size is switched from the hamburger menu. Only the Advanced
        // tab is reloaded (Global values are not per size). Only the size restore point moves, so closing
        // won't snap the window back to the old size.
        public void RefreshAfterExternalSizeSwitch()
        {
            AdvancedTab.ReloadFromSettings();
            _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
            _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
            ThemeBrowser.RefreshActiveMarks(); // the Browser's highlight follows the new active size
        }

        // Called by MainWindow right after creating the builder when its open adopted Smart fullscreen's shown size as the
        // active size: closing without saving goes back to the size active before that adopt. Only the size restore point
        // moves; Save Adjustments, Save Changes and Load Theme still move it to the current active size.
        public void SetSizeRestorePoint(int width, int height)
        {
            _resolutionWidthAtLastSave = width;
            _resolutionHeightAtLastSave = height;
        }
        // [END SECTION: Constructor & Tab Initialization]

        // [SECTION: Tab Selection]
        // Does the switch to THEME BUILDER; an already selected tab needs nothing more (its selection logic already ran)
        public void ShowBuilderTab()
        {
            if (ReferenceEquals(ThemeBuilderTabControl.SelectedItem, TabThemeBuilder)) return;
            ThemeBuilderTabControl.SelectedItem = TabThemeBuilder;
        }

        // Does the switch to THEME BROWSER: selecting it lets the SelectionChanged handler refresh the catalog; an already
        // selected tab gets no SelectionChanged, so it refreshes here instead (never both)
        public void ShowBrowserTab()
        {
            if (ReferenceEquals(ThemeBuilderTabControl.SelectedItem, TabThemeBrowser))
            {
                ThemeBrowser.RefreshCatalog();
                return;
            }
            ThemeBuilderTabControl.SelectedItem = TabThemeBrowser;
        }
        // [END SECTION: Tab Selection]

        // [SECTION: Theme Browser Load]
        // Does a Browser load: the saved theme at the chosen size in one step (LoadTheme with a target size), through
        // RunThemeSwitch with explicitSize, so Smart fullscreen follows the size pick rules (an exact 1x fit goes fullscreen,
        // anything else windowed). Unsaved edits are
        // dropped without a prompt, as Load does. After the Builder refresh, settings.json is written once in its own
        // try/catch, so a failed write is reported and logged but still lets the fullscreen handling run. A failed load
        // saves nothing, keeps the previous theme, and refreshes the catalog so a vanished or broken theme drops out.
        private void LoadThemeFromBrowser(string themeName, int width, int height)
        {
            bool zipMissing = false;
            try
            {
                Func<bool> load = () =>
                {
                    if (!_viewModel.LoadTheme(themeName, (width, height)))
                    {
                        zipMissing = true;
                        return false;
                    }

                    RefreshAfterExternalThemeSwitch();

                    try
                    {
                        PersistSettingsToDisk();
                    }
                    catch (Exception persistEx)
                    {
                        ArcadeStick.Services.ThemeLog.Write("Load", themeName, "-", _viewModel.LoadedThemeName, "ok", $"Loaded, but settings.json could not be saved: {persistEx.Message}");
                        MessageBox.Show($"'{themeName}' was loaded, but the setting couldn't be saved. It will not be remembered the next time you start the app.\n\nDetails: {persistEx.Message}",
                            "Theme Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return true;
                };

                // explicitSize: the user picked this size, so the size pick rules decide fullscreen (not the theme switch rules)
                if (Owner is MainWindow m) m.RunThemeSwitch(load, explicitSize: true);
                else load();
            }
            catch (ArcadeStick.Services.ThemeLoadException ex)
            {
                MessageBox.Show(ex.Message, "Theme Not Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                ThemeBrowser.RefreshCatalog();
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading theme properties: {ex.Message}", "Processing Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                ThemeBrowser.RefreshCatalog();
                return;
            }

            if (zipMissing)
            {
                MessageBox.Show($"'{themeName}' couldn't be found. It may have been moved or deleted.", "Theme Not Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                ThemeBrowser.RefreshCatalog();
            }
        }
        // [END SECTION: Theme Browser Load]

        // [SECTION: Theme Browser Preview]
        // Does a thumbnail click: reads the theme's saved thumbnail at full size from its zip (no lock kept) and opens the
        // preview over the window. On any failure (zip gone, unreadable, no or oversized thumbnail) nothing opens, no message,
        // and the catalog refreshes so a vanished theme drops out (scroll kept). Loads and saves nothing.
        private void ShowThemePreview(ArcadeStick.Models.ThemeCatalogEntry entry)
        {
            var image = ArcadeStick.Services.ThemeCatalogReader.ReadFullThumbnail(entry.ZipPath);
            if (image == null)
            {
                ThemeBrowser.RefreshCatalog();
                return;
            }

            PreviewOverlay.Show(image);
        }
        // [END SECTION: Theme Browser Preview]

        // [SECTION: Theme Saved Flash]
        // Shows the "Theme Saved" text beside the tab headers for 2 seconds (a repeat save restarts the timer)
        private void ShowThemeSavedFlash()
        {
            BrdThemeSavedFlash.Visibility = Visibility.Visible;

            _themeSavedFlashTimer?.Stop();
            _themeSavedFlashTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _themeSavedFlashTimer.Tick += (s, e) =>
            {
                BrdThemeSavedFlash.Visibility = Visibility.Collapsed;
                _themeSavedFlashTimer?.Stop();
            };
            _themeSavedFlashTimer.Start();
        }
        // [END SECTION: Theme Saved Flash]

        // [SECTION: Window Binding Refresh]
        // Forces every binding in this window to re-evaluate by briefly clearing and restoring
        // DataContext. Used after a theme loads/saves so controls reflect the new values immediately.
        private void RefreshWindowBindings()
        {
            this.DataContext = null;
            this.DataContext = _settings;
        }
        // [END SECTION: Window Binding Refresh]

        // [SECTION: Settings Persistence]
        // Serializes the full ConfigurationSettings object to settings.json in 4rcade5tick_files.
        private void PersistSettingsToDisk()
        {
            string configFilePath = Path.Combine(_settings.GetArcadeStickFilesPath(), "settings.json");
            var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            string jsonString = System.Text.Json.JsonSerializer.Serialize(_settings, jsonOptions);
            File.WriteAllText(configFilePath, jsonString);
        }
        // [END SECTION: Settings Persistence]

        // [SECTION: Save / Close Handlers]
        // Save Adjustments: writes settings.json (the Advanced and Global tabs already edit _settings live),
        // moves the close-without-saving restore point and live-refreshes the main window's theme bindings and
        // folder/game colors. Does NOT write to a theme file - only Save Changes in the Themes Manager row does that.
        private void BtnSaveAdjustments_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // A pending thumbnail goes to disk first so settings.json records its path. ModifiedUtc is not stamped: only Save Changes does that.
                ArcadeStick.Services.ThemeThumbnailStore.WritePending(_settings);
                PersistSettingsToDisk();
                _settings.PendingThemeThumbnail = null;
                AdvancedTab.RefreshThemeInfo();

                // Saved: a pending custom size is now permanent, and this is the new restore point for closing
                AdvancedTab.CommitPendingSize();
                _layoutsAtLastSave = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_settings.Layouts);
                _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
                _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
                _globalAtLastSave = ArcadeStick.Services.GlobalThemeResources.CaptureSnapshot(_settings);
                _infoAtLastSave = _settings.Info.Clone();
                ArcadeStick.Services.ThemeThumbnailStore.RemoveUnusedFolders(_settings, _infoAtLastSave);

                _viewModel.RefreshThemeBindings();
                _viewModel.RefreshFolderColorsLive();
                _viewModel.RefreshGameColorsLive();

                RefreshWindowBindings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save local theme settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClosePanel_Click(object sender, RoutedEventArgs e) => this.Close();
        // [END SECTION: Save / Close Handlers]
    }
}