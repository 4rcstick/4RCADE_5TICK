using System;
using System.IO;
using System.Windows;

namespace ArcadeStick.Views
{
    public partial class ThemeBuilderWindow : Window
    {
        private readonly ArcadeStick.Models.ConfigurationSettings _settings;
        private readonly ArcadeStick.ViewModels.MainViewModel _viewModel;
        // The launcher window: the builder has no owner, so it reaches the guides, grid, previews, live layout, size picks,
        // theme switches and thumbnail capture through this reference
        private readonly MainWindow _main;

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
        // main is the launcher window (the builder is an independent window with no owner).
        public ThemeBuilderWindow(ArcadeStick.ViewModels.MainViewModel viewModel, MainWindow main)
        {
            InitializeComponent();
            ArcadeStick.Services.WindowFit.KeepInWorkArea(this); // never taller than the monitor's work area
            _main = main;
            _viewModel = viewModel;
            _settings = viewModel.Configuration;

            this.DataContext = _settings;

            ThemesManager.Initialize(_viewModel, _settings, PersistSettingsToDisk, RefreshAfterThemeLoad);
            ThemesManager.SetThemeSavedCallback(OnThemeSaved);
            ThemesManager.ThemeCreatedFlash = ShowThemeSavedFlash;
            AdvancedTab.Initialize(_settings);

            // A genuinely new size and a removed size save the loaded theme (themes.md 3.1): the Advanced tab asks these
            AdvancedTab.IsSavedSize = IsSavedSize;
            AdvancedTab.CanAutoSaveNewSize = CanAutoSaveNewSize;
            AdvancedTab.AutoSaveNewSizeRunner = RunNewSizeAutoSave;
            AdvancedTab.CanAutoSaveSizeRemoval = CanAutoSaveSizeRemoval;
            AdvancedTab.AutoSaveSizeRemovalRunner = RunSizeRemovalAutoSave;
            AdvancedTab.ThumbnailCaptureRunner = () => _main.CaptureThemeThumbnail();
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
                _main.ShowGuides(info);
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
                    _main.ShowGuides(null);
                    _main.SetBackgroundGrid(false, 0); // hidden, not unchecked: it returns with THEME BUILDER
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
                _main.SetBackgroundGrid(on && onBuilder, spacing, offsetX, offsetY);
            };

            // Random Game Display card preview: forwarded to the main window like the guides
            AdvancedTab.RandomPreviewChanged += (on, fromSplash) => _main.SetRandomCardPreview(on, fromSplash);

            // Marquee banner preview: forwarded the same way
            AdvancedTab.BannerPreviewChanged += on => _main.SetBannerPreview(on);

            // Live layout: every committed Advanced edit re-applies the layout on the main window.
            // Nothing is written to disk here - only Save Adjustments / Save Changes do that.
            AdvancedTab.LayoutChanged += () => _main.ApplyLayoutNow();

            // Size switches (Resolutions buttons, size combo) follow the same Smart fullscreen pick rules as the hamburger Layout size
            AdvancedTab.SizePickRunner = run => _main.RunSizePick(run);

            // Load Theme follows the same Smart fullscreen theme switch rules as the hamburger theme switch
            ThemesManager.ThemeLoadRunner = load => _main.RunThemeSwitch(load);

            // Restore point for closing without saving: the layouts and Global values as of window open / last Save Adjustments.
            _layoutsAtLastSave = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_settings.Layouts);
            _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
            _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
            _globalAtLastSave = ArcadeStick.Services.GlobalThemeResources.CaptureSnapshot(_settings);
            _infoAtLastSave = _settings.Info.Clone();

            // While open, saves that are not builder commits write the restore point instead of the unsaved edits
            _viewModel.PersistenceSnapshotProvider = BuildCommittedSettingsForPersistence;

            Closed += (s, e) =>
            {
                // Live values go back to the restore point below, so the shared writer needs no snapshot any more
                _viewModel.PersistenceSnapshotProvider = null;

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

                _main.ShowGuides(null);
                _main.SetBackgroundGrid(false, 0); // closing hides the grid and gives the wallpaper back
                _main.SetRandomCardPreview(false, false); // closing turns the card and banner previews off
                _main.SetBannerPreview(false);
                _main.ApplyLayoutNow();

                // Focus after the close is MainWindow's conditional hand-back (TrackWorkspaceClose), not a forced activate
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

        // Called by MainWindow after the layout size is switched from the hamburger menu (or F11's entry-size restore). Only the
        // Advanced tab is reloaded (Global values are not per size). Only the size restore point moves, so closing won't snap
        // the window back to the old size and unsaved edits stay uncommitted. When the restore point has no layout at the new
        // size, it takes a clean copy (CopyLayoutForRestorePoint), so the active size never points at a missing layout.
        public void RefreshAfterExternalSizeSwitch()
        {
            AdvancedTab.ReloadFromSettings();
            _resolutionWidthAtLastSave = _settings.ActiveResolutionWidth;
            _resolutionHeightAtLastSave = _settings.ActiveResolutionHeight;
            if (!_layoutsAtLastSave.Any(l => l.Width == _resolutionWidthAtLastSave && l.Height == _resolutionHeightAtLastSave))
            {
                var copy = CopyLayoutForRestorePoint(_resolutionWidthAtLastSave, _resolutionHeightAtLastSave);
                if (copy != null) _layoutsAtLastSave.Add(copy);
            }
            ThemeBrowser.RefreshActiveMarks(); // the Browser's highlight follows the new active size
        }

        // Returns a deep copy of the layout at one size for the restore point: the theme's saved copy (SavedThemeLayouts, never
        // edited) when it has that size, otherwise the working copy (a size that exists only in the working layouts), or null
        private ArcadeStick.Models.ThemeLayout? CopyLayoutForRestorePoint(int width, int height)
        {
            var source = _settings.SavedThemeLayouts.FirstOrDefault(l => l.Width == width && l.Height == height)
                ?? _settings.Layouts.FirstOrDefault(l => l.Width == width && l.Height == height);
            return source == null ? null : ArcadeStick.Models.ConfigurationSettings.CloneLayouts(new[] { source }).First();
        }

        // Returns the settings the shared writer saves while the builder is open (MainViewModel.PersistenceSnapshotProvider): a
        // detached copy of the live settings with the restore point's layouts, active size, Global values and Theme Info, so
        // an Options, Sorting, sound or other unrelated save never writes unsaved builder edits. The restore point moves only
        // on Save Adjustments, Save Changes, a theme load and (size only) an outside size switch, exactly what the builder's
        // close-without-saving puts back. Nothing live is written to and no change notification fires.
        private ArcadeStick.Models.ConfigurationSettings BuildCommittedSettingsForPersistence()
        {
            var committed = _settings.CloneForPersistence();
            committed.Layouts = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_layoutsAtLastSave);
            committed.ActiveResolutionWidth = _resolutionWidthAtLastSave;
            committed.ActiveResolutionHeight = _resolutionHeightAtLastSave;

            // Safety net (RefreshAfterExternalSizeSwitch normally covers it): the active size always has a layout
            if (!committed.Layouts.Any(l => l.Width == _resolutionWidthAtLastSave && l.Height == _resolutionHeightAtLastSave))
            {
                var copy = CopyLayoutForRestorePoint(_resolutionWidthAtLastSave, _resolutionHeightAtLastSave);
                if (copy != null) committed.Layouts.Add(copy);
            }

            ArcadeStick.Services.GlobalThemeResources.RestoreSnapshot(_globalAtLastSave, committed, ArcadeStick.Services.GlobalArea.All);
            committed.Info = _infoAtLastSave.Clone();
            return committed;
        }

        // Does the work after a theme save (the Themes Manager's saved callback): the Theme sizes row, a pending size kept, the
        // close restore point moved to what was just saved, the Theme Info view and the "Theme Saved" flash
        private void OnThemeSaved()
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
        }

        // [SECTION: New Size Auto-Save]
        // Returns true when width x height is a size already saved: in the close restore point (Save Adjustments included) or
        // in the theme file. Such a size, removed but not saved, is restored the old way, with no auto-save.
        private bool IsSavedSize(int width, int height) =>
            _layoutsAtLastSave.Any(l => l.Width == width && l.Height == height)
            || _settings.SavedThemeLayouts.Any(l => l.Width == width && l.Height == height);

        // Does the check before the New size layout prompt: true when the loaded theme can be saved; otherwise says why the
        // size can't be created and returns false (nothing has changed)
        private bool CanAutoSaveNewSize() => CanAutoSaveSizeChange("the new size can't be created", "Size Not Created");

        // Does the check before the Remove Resolution popup: true when the loaded theme can be saved; otherwise says why the
        // size can't be removed and returns false (nothing has changed)
        private bool CanAutoSaveSizeRemoval() => CanAutoSaveSizeChange("the size can't be removed", "Size Not Removed");

        // Does the loaded-theme check shared by creation and removal: shows the problem under title and returns false, or true
        private bool CanAutoSaveSizeChange(string consequence, string title)
        {
            string? problem = ThemesManager.GetLoadedThemeSaveProblem(consequence);
            if (problem == null) return true;

            MessageBox.Show(problem, title, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        // Does a genuinely new size (newWidth x newHeight) as a theme save: the current size must have a saved layout (the
        // restore point, else the theme file), then RunAutoSavedSizeChange discards to the restore point, runs createSize (the
        // Advanced tab's copy and scale from that restored layout) and saves. Returns true when the theme file now holds the new size.
        private bool RunNewSizeAutoSave(int newWidth, int newHeight, Action createSize)
        {
            int sourceWidth = _settings.ActiveResolutionWidth;
            int sourceHeight = _settings.ActiveResolutionHeight;

            // The restored layouts, checked before anything changes: the source size must have a saved layout
            var restoredLayouts = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_layoutsAtLastSave);
            if (!restoredLayouts.Any(l => l.Width == sourceWidth && l.Height == sourceHeight))
            {
                var themeCopy = _settings.SavedThemeLayouts.FirstOrDefault(l => l.Width == sourceWidth && l.Height == sourceHeight);
                if (themeCopy == null)
                {
                    MessageBox.Show($"The {sourceWidth} x {sourceHeight} layout has never been saved, so it can't be the source of a new size. Save the theme first, or switch to a saved size.",
                        "Size Not Created", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }
                restoredLayouts.Add(ArcadeStick.Models.ConfigurationSettings.CloneLayouts(new[] { themeCopy }).First());
            }

            return RunAutoSavedSizeChange(restoredLayouts, () => { createSize(); return true; },
                "Size Not Created", $"The {newWidth} x {newHeight} size was not created.");
        }

        // Does the removal of the active size (width x height) as a theme save (window management Pass 4.2): the restored
        // layouts must keep another size to show, then RunAutoSavedSizeChange discards to the restore point, runs removeSize
        // (the Advanced tab's removal and switch, against the restored layouts) and saves. The active size missing from the
        // restore point takes a copy (theme file, else working), so the reload finds it; the removal drops it anyway. Returns
        // true when the theme file no longer holds the size.
        private bool RunSizeRemovalAutoSave(int width, int height, Func<bool> removeSize)
        {
            var restoredLayouts = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(_layoutsAtLastSave);
            if (!restoredLayouts.Any(l => l.Width == width && l.Height == height))
            {
                var copy = CopyLayoutForRestorePoint(width, height);
                if (copy != null) restoredLayouts.Add(copy);
            }

            // Refused before anything changes: the last saved state has no other size to switch to
            if (!restoredLayouts.Any(l => l.Width != width || l.Height != height))
            {
                MessageBox.Show($"The {width} x {height} size can't be removed, because the last saved theme has no other size to show. Save the theme first.",
                    "Size Not Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            return RunAutoSavedSizeChange(restoredLayouts, removeSize,
                "Size Not Removed", $"The {width} x {height} size was not removed.");
        }

        // Does a size change as a theme save, shared by creation and removal: keeps an undo copy of the editing session, puts
        // back the close restore point (restoredLayouts, Global values, Theme Info; the pending thumbnail dropped) at the
        // current size, runs change (false = refused, nothing saved), then saves the loaded theme. Saved: the saved callback
        // has moved the restore point. Saved but incomplete (the zip was swapped in, a later step such as the settings.json
        // write failed): memory already matches the new zip, so the restore point moves to it too and nothing is rolled back.
        // Not saved (refused, missing-files No, or a failure before the swap, so disk is unchanged): the undo copy goes back
        // and notDoneText is shown. Returns true when the theme file now holds the change.
        private bool RunAutoSavedSizeChange(System.Collections.Generic.List<ArcadeStick.Models.ThemeLayout> restoredLayouts,
            Func<bool> change, string notDoneTitle, string notDoneText)
        {
            int sourceWidth = _settings.ActiveResolutionWidth;
            int sourceHeight = _settings.ActiveResolutionHeight;

            // Undo copy: discarding replaces these objects without changing them, so keeping the references is enough
            var undoLayouts = _settings.Layouts;
            var undoGlobal = ArcadeStick.Services.GlobalThemeResources.CaptureSnapshot(_settings);
            var undoInfo = _settings.Info;
            var undoThumbnail = _settings.PendingThemeThumbnail;
            var undoEditState = AdvancedTab.CaptureNewSizeEditState();

            // Discard the unsaved edits: the restore point at the current size (the active size stays the source)
            _settings.Layouts = restoredLayouts;
            ArcadeStick.Services.GlobalThemeResources.RestoreSnapshot(_globalAtLastSave, _settings, ArcadeStick.Services.GlobalArea.All);
            ArcadeStick.Services.GlobalThemeResources.Apply(_settings);
            _settings.Info = _infoAtLastSave.Clone();
            _settings.PendingThemeThumbnail = null;
            AdvancedTab.ReloadForNewSizeAutoSave(undoEditState with { PendingSize = null });
            GlobalTab.ReloadFromSettings();
            AdvancedTab.RefreshThemeInfo();

            var outcome = ThemesManagerControl.ThemeSaveOutcome.NotSaved;
            string? error = null;
            try
            {
                if (change()) outcome = ThemesManager.SaveLoadedTheme();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            if (outcome == ThemesManagerControl.ThemeSaveOutcome.Saved) return true;

            if (outcome == ThemesManagerControl.ThemeSaveOutcome.SavedIncomplete)
            {
                // The save already reported it; keep the restore point with the zip so later saves and a close agree with it
                try
                {
                    OnThemeSaved();
                }
                catch
                {
                }
                return true;
            }

            // Not saved: nothing on disk changed, so the editing session goes back exactly as it was
            _settings.Layouts = undoLayouts;
            _settings.ActiveResolutionWidth = sourceWidth;
            _settings.ActiveResolutionHeight = sourceHeight;
            ArcadeStick.Services.GlobalThemeResources.RestoreSnapshot(undoGlobal, _settings, ArcadeStick.Services.GlobalArea.All);
            ArcadeStick.Services.GlobalThemeResources.Apply(_settings);
            _settings.Info = undoInfo;
            _settings.PendingThemeThumbnail = undoThumbnail;
            AdvancedTab.ReloadForNewSizeAutoSave(undoEditState);
            GlobalTab.ReloadFromSettings();
            AdvancedTab.RefreshThemeInfo();
            _main.ApplyLayoutNow();

            string details = error != null ? $"\n\nDetails: {error}" : "";
            MessageBox.Show($"{notDoneText} The theme file was not changed, and your unsaved changes are back as they were.{details}",
                notDoneTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        // [END SECTION: New Size Auto-Save]

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
                _main.RunThemeSwitch(load, explicitSize: true);
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
        // Serializes the full live ConfigurationSettings object to settings.json in 4rcade5tick_files. Used only for the
        // builder's own commits (Save Adjustments, the Themes Manager's load / save / create / delete, Browser loads), which
        // write the live state on purpose; every other save goes through MainViewModel.WriteSettingsFile and its snapshot.
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