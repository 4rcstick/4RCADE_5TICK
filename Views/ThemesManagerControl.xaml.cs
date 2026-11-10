using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ArcadeStick.Views
{
    public partial class ThemesManagerControl : UserControl
    {
        private ArcadeStick.Models.ConfigurationSettings _settings = null!;
        private ArcadeStick.ViewModels.MainViewModel _viewModel = null!;
        private Action? _persistToDisk;
        private Action? _refreshOptionsBindings;
        private Action? _themeSavedCallback;

        // Lets the hosting window react when Save Changes writes a theme (Theme Builder uses it to move
        // its close-without-saving restore point). Optional - nothing else needs to set it.
        public void SetThemeSavedCallback(Action callback) => _themeSavedCallback = callback;

        public ThemesManagerControl()
        {
            InitializeComponent();
        }

        // [SECTION: Constructor & Initialization]
        // Wires up theme dropdown/button events and populates the theme list. persistToDisk/refreshOptionsBindings
        // are callbacks from the hosting window so this row can save settings.json and force-refresh the parent
        // window's bindings independently of the main Save Adjustments button.
        public void Initialize(ArcadeStick.ViewModels.MainViewModel viewModel, ArcadeStick.Models.ConfigurationSettings settings, Action persistToDisk, Action refreshOptionsBindings)
        {
            _viewModel = viewModel;
            _settings = settings;
            _persistToDisk = persistToDisk;
            _refreshOptionsBindings = refreshOptionsBindings;

            BtnNewTheme.Click += BtnNewTheme_Click;
            BtnLoadTheme.Click += BtnLoadTheme_Click;
            BtnSaveTheme.Click += BtnSaveTheme_Click;
            BtnDeleteTheme.Click += BtnDeleteTheme_Click;

            RefreshThemeList();
        }

        // Rebuilds the theme list and re-selects the active theme. Used when the theme is switched from
        // outside this row (the main window's hamburger menu).
        public void ReloadFromSettings()
        {
            RefreshThemeList();
        }
        // [END SECTION: Constructor & Initialization]

        // [SECTION: Theme List Management]
        // Rebuilds the theme dropdown from *.zip files in the Themes folder and re-selects the currently active theme if it
        // still exists (matched case-insensitively, selecting the listed file name). With no active theme listed it stays empty.
        private void RefreshThemeList()
        {
            CboThemePresets.Items.Clear();
            foreach (string themeName in GetThemeNames())
            {
                CboThemePresets.Items.Add(themeName);
            }

            CboThemePresets.SelectedItem = string.IsNullOrWhiteSpace(_settings.ActiveThemeName) ? null
                : CboThemePresets.Items.OfType<string>().FirstOrDefault(name => string.Equals(name, _settings.ActiveThemeName, StringComparison.OrdinalIgnoreCase));
        }

        // Returns the name of every theme: the *.zip files directly in the Themes folder
        private List<string> GetThemeNames()
        {
            string themeDirectory = Path.Combine(_settings.GetArcadeStickFilesPath(), "Themes");
            if (!Directory.Exists(themeDirectory)) return new List<string>();

            return Directory.GetFiles(themeDirectory, "*.zip").Select(file => Path.GetFileNameWithoutExtension(file)).ToList();
        }
        // [END SECTION: Theme List Management]

        // [SECTION: Theme Serialization Logic]

        // [SUB-SECTION: Load Theme]
        // Set by the Theme Builder window: runs a theme load through the main window's theme switch rules for Smart
        // fullscreen (MainWindow.RunThemeSwitch). Null runs the load directly.
        public Action<Func<bool>>? ThemeLoadRunner;

        // Loads the selected theme zip through MainViewModel.LoadTheme (which maps theme.json onto _settings),
        // then refreshes the hosting window's bindings and persists settings.json, all through ThemeLoadRunner when set.
        private void BtnLoadTheme_Click(object sender, RoutedEventArgs e)
        {
            if (CboThemePresets.SelectedItem is string selectedTheme)
            {
                try
                {
                    Func<bool> load = () =>
                    {
                        if (!_viewModel.LoadTheme(selectedTheme)) return false;
                        _refreshOptionsBindings?.Invoke();
                        _persistToDisk?.Invoke();
                        return true;
                    };

                    if (ThemeLoadRunner != null) ThemeLoadRunner(load);
                    else load();
                }
                catch (ArcadeStick.Services.ThemeLoadException ex)
                {
                    MessageBox.Show(ex.Message, "Theme Not Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading theme properties: {ex.Message}", "Processing Failure", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        // [END SUB-SECTION: Load Theme]

        // [SECTION: Theme Archive Asset Bundling]
        // Resolves a theme asset's stored value to a real absolute path: a relative value is resolved against
        // the MAME root (the portable-friendly convention the old Basic tab browse buttons used), an absolute
        // value is returned as is.
        private string ResolveAssetAbsolutePath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return string.Empty;
            if (Path.IsPathRooted(storedPath)) return storedPath;
            return Path.Combine(_settings.GetMamePath(), storedPath);
        }

        // Copies one asset file into the staging assets/ folder (numbering the filename if it collides
        // with an asset already bundled from a different property) and returns the archive-relative path
        // the DTO should be rewritten to point at. A missing/empty source is left untouched and reported
        // back via missingAssets, rather than failing the whole save; LoadTheme leaves such a path as stored too.
        private string BundleThemeAsset(string storedPath, string stagingAssetsDir, List<string> missingAssets)
        {
            if (string.IsNullOrWhiteSpace(storedPath)) return storedPath;

            string absolutePath = ResolveAssetAbsolutePath(storedPath);
            if (!File.Exists(absolutePath))
            {
                missingAssets.Add(storedPath);
                return storedPath;
            }

            string fileName = Path.GetFileName(absolutePath);
            string destPath = Path.Combine(stagingAssetsDir, fileName);

            int counter = 1;
            while (File.Exists(destPath))
            {
                string nameOnly = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                destPath = Path.Combine(stagingAssetsDir, $"{nameOnly}_{counter}{ext}");
                counter++;
            }

            File.Copy(absolutePath, destPath);
            return $"assets/{Path.GetFileName(destPath)}";
        }

        // Staging subfolder holding a thumbnail recovered from the old zip until it is bundled into assets/
        private const string RecoveredThumbnailFolder = "recovered-thumbnail";

        // Copies assets/theme-thumbnail.png out of an existing theme zip into staging and returns its path. Empty when the
        // zip is missing, unreadable or has no thumbnail; a failure here never blocks the save. The zip is closed on return.
        private static string ExtractThumbnailFromZip(string themeFile, string stagingDir)
        {
            try
            {
                if (!File.Exists(themeFile)) return string.Empty;

                using var zip = ZipFile.OpenRead(themeFile);
                var entry = zip.GetEntry($"assets/{ArcadeStick.Services.ThemeThumbnailStore.FileName}");
                if (entry == null) return string.Empty;

                string folder = Path.Combine(stagingDir, RecoveredThumbnailFolder);
                Directory.CreateDirectory(folder);
                string filePath = Path.Combine(folder, ArcadeStick.Services.ThemeThumbnailStore.FileName);
                entry.ExtractToFile(filePath);
                return filePath;
            }
            catch
            {
                return string.Empty;
            }
        }

        // Removes the recovered thumbnail's staging subfolder, if any, so it does not end up in the new zip
        private static void DeleteRecoveredThumbnailFolder(string stagingDir)
        {
            string folder = Path.Combine(stagingDir, RecoveredThumbnailFolder);
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        // [END SECTION: Theme Archive Asset Bundling]

        // [SUB-SECTION: Save Theme]
        // Saves the selected theme: needs a selection and refuses an existing theme that is not the loaded one, then writes it
        private void BtnSaveTheme_Click(object sender, RoutedEventArgs e)
        {
            if (CboThemePresets.SelectedItem is not string themeName)
            {
                MessageBox.Show("Select a theme, or use New Theme.", "Theme Not Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // The zip name comes from the dropdown but everything written comes from memory, so an existing theme can only
            // be saved over by the theme that is actually loaded.
            string loadedTheme = _viewModel.LoadedThemeName;
            if (!string.Equals(themeName, loadedTheme, StringComparison.OrdinalIgnoreCase))
            {
                string mismatchMessage = string.IsNullOrWhiteSpace(loadedTheme)
                    ? $"The theme list shows '{themeName}' but no theme is loaded. Press Load on '{themeName}' first, so nothing gets overwritten."
                    : $"The theme list shows '{themeName}' but the loaded theme is '{loadedTheme}'. Press Load on '{themeName}' first, or select '{loadedTheme}', so nothing gets overwritten.";
                ArcadeStick.Services.ThemeLog.Write("SaveBlocked", themeName, themeName, loadedTheme, "blocked", "The selected theme is not the loaded theme.");
                MessageBox.Show(mismatchMessage, "Theme Not Saved", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveThemeCore(themeName, isNewTheme: false, themeName);
        }

        // What a save did: NotSaved (cancelled, or failed before the zip swap, so nothing on disk or in memory changed), Saved
        // (everything finished), or SavedIncomplete (the zip was swapped in, then a later step such as the settings.json
        // write failed: the theme file and memory hold the new theme, settings.json may not)
        public enum ThemeSaveOutcome { NotSaved, Saved, SavedIncomplete }

        // Writes memory to the named theme and returns how far it got (ThemeSaveOutcome): builds a ThemeSettings DTO from _settings,
        // bundles every layout entry's ImagePath into a staging assets/ folder (rewriting the DTO's copies of those paths to
        // archive-relative paths), asks before saving with missing files, then writes a temp zip, verifies it and swaps it in,
        // keeping the old zip as <name>.zip.bak. DANGER: any new flat theme property must be added to this DTO too (and to
        // ThemeSettings and LoadTheme). The info block's thumbnail is bundled the same way. Nothing live changes until the
        // swap succeeds, so a failed or cancelled save leaves everything as it was. The caller has already checked the name.
        private ThemeSaveOutcome SaveThemeCore(string themeName, bool isNewTheme, string dropdown)
        {
            string loadedTheme = _viewModel.LoadedThemeName;
            string stagingDir = Path.Combine(Path.GetTempPath(), $"4rcstick_theme_{Guid.NewGuid():N}");
            string? tempZip = null;
            bool zipWritten = false; // from here on the theme is saved, whatever a later refresh does
            bool completed = false; // every step after the swap finished too

            try
            {
                string themeFile = _settings.GetThemePath(themeName);
                string themesDir = Path.GetDirectoryName(themeFile) ?? throw new InvalidOperationException($"Invalid theme path: {themeFile}");
                string backupZip = ArcadeStick.Services.ThemePaths.Backup(themesDir, themeName);
                Directory.CreateDirectory(themesDir);
                ArcadeStick.Services.ThemeFileStore.CleanStaleTempZips(themesDir);

                // Create and populate the DTO
                var themeDto = new ArcadeStick.Models.ThemeSettings
                {
                    OptionsBg = _settings.OptionsBg,
                    ScrollTrackColor = _settings.ScrollTrackColor,
                    ScrollTrackHoverColor = _settings.ScrollTrackHoverColor,
                    ScrollThumbColor = _settings.ScrollThumbColor,
                    ScrollThumbHoverColor = _settings.ScrollThumbHoverColor,
                    ScrollThumbDragColor = _settings.ScrollThumbDragColor,
                    ScrollbarWidth = _settings.ScrollbarWidth,
                    SeparatorColorHex = _settings.SeparatorColorHex,
                    TabFontSize = _settings.TabFontSize,
                    TabColorHex = _settings.TabColorHex,
                    TabBgColorHex = _settings.TabBgColorHex,
                    TabActiveColorHex = _settings.TabActiveColorHex,
                    TabActiveBgColorHex = _settings.TabActiveBgColorHex,
                    HeaderFontSize = _settings.HeaderFontSize,
                    HeaderColorHex = _settings.HeaderColorHex,
                    SubHeaderFontSize = _settings.SubHeaderFontSize,
                    SubHeaderColorHex = _settings.SubHeaderColorHex,
                    StandardFontSize = _settings.StandardFontSize,
                    StandardColorHex = _settings.StandardColorHex,
                    ContextMenuFontColorHex = _settings.ContextMenuFontColorHex,
                    ContextMenuIconColorHex = _settings.ContextMenuIconColorHex,
                    ContextMenuBgColorHex = _settings.ContextMenuBgColorHex,
                    ContextMenuHoverColorHex = _settings.ContextMenuHoverColorHex,
                    ContextMenuHoverBgColorHex = _settings.ContextMenuHoverBgColorHex,
                    ContextMenuBorderColorHex = _settings.ContextMenuBorderColorHex,
                    SubTextFontSize = _settings.SubTextFontSize,
                    SubTextColorHex = _settings.SubTextColorHex,
                    OptionsMenuBgColorHex = _settings.OptionsMenuBgColorHex,
                    OptionsMenuBorderSize = _settings.OptionsMenuBorderSize,
                    OptionsMenuBorderColorHex = _settings.OptionsMenuBorderColorHex,
                    OptionsMenuBorderRadius = _settings.OptionsMenuBorderRadius,
                    TabColorHoverHex = _settings.TabColorHoverHex,
                    TabBgColorHoverHex = _settings.TabBgColorHoverHex,
                    OptionsBtnFontSize = _settings.OptionsBtnFontSize,
                    OptionsBtnColorHoverHex = _settings.OptionsBtnColorHoverHex,
                    OptionsBtnBorderSize = _settings.OptionsBtnBorderSize,
                    OptionsBtnBorderRadius = _settings.OptionsBtnBorderRadius,
                    BtnBgColorHex = _settings.BtnBgColorHex,
                    BtnBorderColorHex = _settings.BtnBorderColorHex,
                    BtnTextColorNormalHex = _settings.BtnTextColorNormalHex,
                    BtnBgColorHoverHex = _settings.BtnBgColorHoverHex
                };

                // Bundle every layout entry's ImagePath (below - wallpaper, logo, splash, fallback, shape and custom
                // layer files) into a staging assets/ folder, rewriting the DTO's copies of those paths to
                // archive-relative paths - the original absolute/relative
                // paths on THIS machine are meaningless to whoever imports the theme elsewhere.
                string stagingAssetsDir = Path.Combine(stagingDir, "assets");
                Directory.CreateDirectory(stagingAssetsDir);

                var missingAssets = new List<string>();

                // Theme info: a new theme keeps the author and description but never the previous theme's thumbnail file
                // or date (a pending capture goes under its own new GUID folder). Bundled before the layers, so a layer
                // image with the same name cannot rename the thumbnail. A pending capture goes straight into staging and
                // reaches the Thumbnails folder only after the swap. A missing file is recovered from the zip about to
                // be overwritten (the live path is left alone), otherwise it degrades to no thumbnail.
                var infoForTheme = _settings.Info.Clone();
                if (isNewTheme)
                {
                    infoForTheme.ThumbnailPath = "";
                    infoForTheme.ModifiedUtc = null;
                }
                if (_settings.PendingThemeThumbnail is System.Windows.Media.Imaging.BitmapSource pendingThumbnail)
                {
                    string fileName = ArcadeStick.Services.ThemeThumbnailStore.FileName;
                    ArcadeStick.Services.ThemeThumbnailStore.WritePng(pendingThumbnail, Path.Combine(stagingAssetsDir, fileName));
                    infoForTheme.ThumbnailPath = $"assets/{fileName}";
                }
                else
                {
                    string thumbnailSource = infoForTheme.ThumbnailPath;
                    if (!string.IsNullOrWhiteSpace(thumbnailSource) && !File.Exists(ResolveAssetAbsolutePath(thumbnailSource)))
                    {
                        thumbnailSource = isNewTheme ? "" : ExtractThumbnailFromZip(themeFile, stagingDir);
                    }
                    infoForTheme.ThumbnailPath = string.IsNullOrWhiteSpace(thumbnailSource)
                        ? ""
                        : BundleThemeAsset(thumbnailSource, stagingAssetsDir, missingAssets);
                    DeleteRecoveredThumbnailFolder(stagingDir); // everything left in staging goes into the zip
                }

                // The zip's theme.json must carry the date, so it is chosen here; the live block takes it only once the zip is written
                DateTime savedUtc = DateTime.UtcNow;
                infoForTheme.ModifiedUtc = savedUtc;
                themeDto.Info = infoForTheme;

                // Layouts: keep every saved resolution, and make sure the active resolution has an entry
                // (registry defaults fill in anything not customized yet) so a save always records it.
                int activeW = _settings.ActiveResolutionWidth;
                int activeH = _settings.ActiveResolutionHeight;
                var layoutsToSave = new List<ArcadeStick.Models.ThemeLayout>();
                ArcadeStick.Models.ThemeLayout? activeSaved = null;
                foreach (var layout in _settings.Layouts)
                {
                    if (layout.Width == activeW && layout.Height == activeH) activeSaved = layout;
                    else layoutsToSave.Add(layout);
                }
                var resolvedActive = ArcadeStick.Models.LayerRegistry.ResolveLayout(activeSaved, activeW, activeH);
                layoutsToSave.Add(resolvedActive);

                // The theme file gets its own copy of the layouts, with every custom layer's image bundled
                // into the zip and its path rewritten to archive-relative. The live layouts keep their real paths.
                var layoutsForTheme = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(layoutsToSave);
                var bundledCustomImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var customEntry in layoutsForTheme.SelectMany(l => l.Layers).Where(e => !string.IsNullOrWhiteSpace(e.ImagePath)))
                {
                    if (string.IsNullOrWhiteSpace(customEntry.ImagePath)) continue;

                    string imageKey = ResolveAssetAbsolutePath(customEntry.ImagePath);
                    if (!bundledCustomImages.TryGetValue(imageKey, out string? archivePath))
                    {
                        archivePath = BundleThemeAsset(customEntry.ImagePath, stagingAssetsDir, missingAssets);
                        bundledCustomImages[imageKey] = archivePath;
                    }
                    customEntry.ImagePath = archivePath;
                }
                themeDto.Layouts = layoutsForTheme;
                themeDto.ActiveWidth = activeW;
                themeDto.ActiveHeight = activeH;

                if (missingAssets.Count > 0 && !ConfirmSaveWithMissingAssets(missingAssets))
                {
                    ArcadeStick.Services.ThemeLog.Write("Save", themeName, dropdown, loadedTheme, "cancelled", $"Missing: {string.Join(", ", missingAssets)}");
                    return ThemeSaveOutcome.NotSaved;
                }

                var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string jsonString = System.Text.Json.JsonSerializer.Serialize(themeDto, jsonOptions);
                File.WriteAllText(Path.Combine(stagingDir, "theme.json"), jsonString);

                // Write and check the new zip beside the old one; the old zip is only replaced once the new one is good
                tempZip = ArcadeStick.Services.ThemePaths.TempZip(themesDir, themeName);
                ZipFile.CreateFromDirectory(stagingDir, tempZip);
                ArcadeStick.Services.ThemeFileStore.VerifyZip(tempZip, stagingDir);
                ArcadeStick.Services.ThemeFileStore.ReplaceZip(tempZip, themeFile, backupZip);
                zipWritten = true;

                // The live layout keeps its object, because the Advanced tab's _currentLayout points at it and adds and
                // removes custom layers through it; replacing it would lose those edits on the next save.
                if (activeSaved != null)
                {
                    activeSaved.Layers = resolvedActive.Layers;
                    layoutsToSave[layoutsToSave.Count - 1] = activeSaved;
                }
                _settings.Layouts = layoutsToSave;

                // Saving the theme moves the restore point to what was just written to the zip.
                _settings.SavedThemeLayouts = ArcadeStick.Models.ConfigurationSettings.CloneLayouts(layoutsToSave);
                _settings.SavedThemeWidth = activeW;
                _settings.SavedThemeHeight = activeH;

                // This is now the loaded and active theme, including under a new name
                _viewModel.LoadedThemeName = themeName;
                _settings.ActiveThemeName = themeName;

                // A new theme never keeps the previous theme's thumbnail file. The zip already holds the capture, so a
                // failed write to the Thumbnails folder only costs the live preview.
                if (isNewTheme) _settings.Info.ThumbnailPath = "";
                try
                {
                    ArcadeStick.Services.ThemeThumbnailStore.WritePending(_settings);
                }
                catch (Exception thumbnailEx)
                {
                    ArcadeStick.Services.ThemeLog.Write("Save", themeName, dropdown, themeName, "ok", $"Thumbnail not written to the Thumbnails folder: {thumbnailEx.Message}");
                }
                _settings.Info.ModifiedUtc = savedUtc;
                _settings.PendingThemeThumbnail = null;

                ArcadeStick.Services.ThemeLog.Write("Save", themeName, dropdown, themeName, "ok", missingAssets.Count > 0 ? $"Saved without: {string.Join(", ", missingAssets)}" : "");

                _persistToDisk?.Invoke();

                _viewModel.RefreshThemeBindings();
                _viewModel.RefreshFolderColorsLive();
                _viewModel.RefreshGameColorsLive();
                RefreshThemeList();
                CboThemePresets.SelectedItem = themeName;
                _persistToDisk?.Invoke();
                _themeSavedCallback?.Invoke();
                completed = true;
            }
            catch (Exception ex)
            {
                if (ex is ArcadeStick.Services.ThemePathException refusedSave)
                {
                    ArcadeStick.Services.ThemeLog.Write("PathRefused", themeName, dropdown, _viewModel.LoadedThemeName, "failed", $"Save: {refusedSave.Reason}");
                }
                else
                {
                    ArcadeStick.Services.ThemeLog.Write("Save", themeName, dropdown, _viewModel.LoadedThemeName, zipWritten ? "ok" : "failed", ex.Message);
                }

                if (zipWritten)
                {
                    MessageBox.Show($"'{themeName}' was saved, but the display did not finish updating. Restart the app if it looks wrong.\n\nDetails: {ex.Message}", "Theme Saved", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else if (ex is ArcadeStick.Services.ThemeFileException)
                {
                    MessageBox.Show(ex.Message, "Theme Not Saved", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show($"Couldn't save '{themeName}'. The existing theme file was not changed. Another program may be using it. Close it and try again.\n\nDetails: {ex.Message}", "Theme Not Saved", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                ArcadeStick.Services.ThemeFolderSwap.TryDeleteFolder(stagingDir);
                try
                {
                    if (tempZip != null && File.Exists(tempZip)) File.Delete(tempZip);
                }
                catch
                {
                }
            }

            if (!zipWritten) return ThemeSaveOutcome.NotSaved;
            return completed ? ThemeSaveOutcome.Saved : ThemeSaveOutcome.SavedIncomplete;
        }

        // [SECTION: New Size Auto-Save]
        // Returns why the loaded theme can't be saved by a size auto-save (no theme loaded, or its zip is gone or its name is
        // refused), ending with consequence (for example "the new size can't be created"), or null when it can. Never creates
        // a theme: the auto-save only writes over the loaded theme's existing zip.
        public string? GetLoadedThemeSaveProblem(string consequence)
        {
            string loadedTheme = _viewModel.LoadedThemeName;
            if (string.IsNullOrWhiteSpace(loadedTheme))
                return $"No saved theme is loaded, so {consequence}. Load a theme first.";

            try
            {
                if (!File.Exists(_settings.GetThemePath(loadedTheme)))
                    return $"The theme file for '{loadedTheme}' couldn't be found, so {consequence}. Load a theme first.";
            }
            catch (ArcadeStick.Services.ThemePathException ex)
            {
                return $"'{loadedTheme}' can't be saved, so {consequence}.\n\nDetails: {ex.Reason}";
            }
            return null;
        }

        // Does the size auto-save (a new or removed size): saves memory over the loaded theme through the normal Save path (SaveThemeCore: the
        // missing-files question, the safe zip swap, settings.json, the saved callback and its flash) and returns the outcome
        public ThemeSaveOutcome SaveLoadedTheme()
        {
            string loadedTheme = _viewModel.LoadedThemeName;
            return SaveThemeCore(loadedTheme, isNewTheme: false, loadedTheme);
        }
        // [END SECTION: New Size Auto-Save]

        // Asks whether to save without the files that could not be found (No is the default), listing at most 8 of them
        private static bool ConfirmSaveWithMissingAssets(List<string> missingAssets)
        {
            const int maxListed = 8;
            string list = string.Join("\n", missingAssets.Take(maxListed));
            if (missingAssets.Count > maxListed) list += $"\nand {missingAssets.Count - maxListed} more";

            return MessageBox.Show($"{missingAssets.Count} asset file(s) could not be found and will not be included:\n\n{list}\n\nSave anyway?", "Some Assets Missing", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
        }
        // [END SUB-SECTION: Save Theme]

        // [END SECTION: Theme Serialization Logic]

        // [SECTION: New Theme]
        // Set by the Theme Builder window: shows the Theme Saved flash and nothing else. A template creation changes nothing
        // live before its load, so it must not run _themeSavedCallback, which moves the restore points.
        public Action? ThemeCreatedFlash;

        // Opens the New Theme menu under the button: Copy Current Theme, then, if there are valid templates, a separator, a
        // Templates heading (BuilderMenuHeadingStyle: not clickable, never highlighted) and the templates. The Windows
        // template's light icon gutter is hidden (HideMenuGutter) so it cannot show at the left edge or between items.
        // It is built on every open, so a file added to the Templates folder appears with no code change.
        private void BtnNewTheme_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu
            {
                Style = (Style)FindResource("BuilderContextMenuStyle"),
                PlacementTarget = BtnNewTheme,
                Placement = PlacementMode.Bottom
            };
            menu.Loaded += (_, _) => HideMenuGutter(menu);
            menu.Items.Add(MakeNewThemeMenuItem("Copy Current Theme", CopyCurrentTheme));

            string templatesDir = Path.Combine(_settings.GetArcadeStickFilesPath(), ArcadeStick.Services.ThemeTemplateStore.FolderName);
            var templates = ArcadeStick.Services.ThemeTemplateStore.ListValid(templatesDir, _viewModel.LoadedThemeName);
            if (templates.Count > 0)
            {
                menu.Items.Add(new Separator { Style = (Style)FindResource("BuilderMenuSeparatorStyle") });
                menu.Items.Add(new MenuItem { Header = "Templates", Style = (Style)FindResource("BuilderMenuHeadingStyle") });
            }
            foreach (var template in templates)
            {
                menu.Items.Add(MakeNewThemeMenuItem(template.Name, () => CreateFromTemplate(template)));
            }

            menu.IsOpen = true;
        }

        // Does the gutter hide for this menu only: collapses the light icon gutter (three Rectangles beside the ItemsPresenter)
        // that the Windows ContextMenu template draws behind the items. It showed as a light line at the menu's left edge and,
        // where item edges fall on fractional pixels, as short stubs between items. Runs on Loaded, before the first frame; the
        // item layout is unchanged.
        private static void HideMenuGutter(ContextMenu menu)
        {
            if (menu.Template?.FindName("ItemsPresenter", menu) is not FrameworkElement presenter) return;
            if (System.Windows.Media.VisualTreeHelper.GetParent(presenter) is not Panel host) return;
            foreach (var gutterPart in host.Children.OfType<System.Windows.Shapes.Rectangle>())
            {
                gutterPart.Visibility = Visibility.Collapsed;
            }
        }

        // Builds one menu entry on the Theme Builder menu style. Its action runs after the menu has closed.
        private MenuItem MakeNewThemeMenuItem(string header, Action onClick)
        {
            var item = new MenuItem { Header = header, Style = (Style)FindResource("BuilderMenuItemStyle") };
            item.Click += (_, _) => Dispatcher.BeginInvoke(onClick);
            return item;
        }

        // Asks for a name, saves what is in memory under it through the safe Save path (unsaved edits included), then loads
        // the new theme so it runs from its own folder. Cancel, or a save that is declined or fails, changes nothing else.
        private void CopyCurrentTheme()
        {
            string? themeName = PromptForNewThemeName();
            if (themeName == null) return;

            string previousTheme = _viewModel.LoadedThemeName;
            string dropdown = CboThemePresets.SelectedItem as string ?? "";
            if (SaveThemeCore(themeName, isNewTheme: true, dropdown) == ThemeSaveOutcome.NotSaved) return;

            string copiedFrom = string.IsNullOrWhiteSpace(previousTheme) ? "-" : previousTheme;
            LoadCreatedTheme(
                themeName,
                $"'{themeName}' was created and saved, but couldn't be loaded. It is still using the previous theme's image files. Press Load to try again.",
                loaded => ArcadeStick.Services.ThemeLog.Write("Create", themeName, dropdown, _viewModel.LoadedThemeName, "ok", loaded ? $"Copied from {copiedFrom}" : "saved, load failed"));
        }

        // Asks for a name, re-reads the template and writes a new zip holding only its theme.json, shows the flash, then loads
        // the new theme. Never goes through SaveThemeCore: nothing live changes until the load succeeds. Unsaved edits on the
        // old theme are dropped, as with Load. Cancel, or a template or write failure, leaves everything as it was.
        private void CreateFromTemplate(ArcadeStick.Services.ThemeTemplate template)
        {
            string? themeName = PromptForNewThemeName();
            if (themeName == null) return;

            string previousTheme = _viewModel.LoadedThemeName;
            string dropdown = CboThemePresets.SelectedItem as string ?? "";

            try
            {
                string themeJson = ArcadeStick.Services.ThemeTemplateStore.BuildThemeJson(template.FilePath, previousTheme);
                WriteThemeOnlyZip(themeName, themeJson);
            }
            catch (ArcadeStick.Services.ThemeTemplateException ex)
            {
                ArcadeStick.Services.ThemeLog.Write("Create", themeName, dropdown, previousTheme, "failed", $"From template {template.Name}: {ex.Reason}");
                MessageBox.Show($"Couldn't read the template '{template.Name}'. Nothing was created.", "Theme Not Created", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (Exception ex)
            {
                if (ex is ArcadeStick.Services.ThemePathException refused)
                {
                    ArcadeStick.Services.ThemeLog.Write("PathRefused", themeName, dropdown, previousTheme, "failed", $"Create: {refused.Reason}");
                }
                else
                {
                    ArcadeStick.Services.ThemeLog.Write("Create", themeName, dropdown, previousTheme, "failed", $"From template {template.Name}: {ex.Message}");
                }

                string message = ex is ArcadeStick.Services.ThemeFileException
                    ? ex.Message
                    : $"Couldn't create '{themeName}'. Nothing was created. Another program may be using the Themes folder. Close it and try again.\n\nDetails: {ex.Message}";
                MessageBox.Show(message, "Theme Not Created", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ThemeCreatedFlash?.Invoke();

            string stillLoaded = string.IsNullOrWhiteSpace(previousTheme) ? "" : $" '{previousTheme}' is still the loaded theme.";
            LoadCreatedTheme(
                themeName,
                $"'{themeName}' was created, but couldn't be loaded.{stillLoaded} Press Load to try again.",
                loaded => ArcadeStick.Services.ThemeLog.Write("Create", themeName, dropdown, _viewModel.LoadedThemeName, "ok", loaded ? $"From template {template.Name}" : $"From template {template.Name}; created, load failed"));
        }

        // Writes a new theme zip holding only theme.json: staging in Temp, then the same temp zip, verify and swap steps as a save
        private void WriteThemeOnlyZip(string themeName, string themeJson)
        {
            string stagingDir = Path.Combine(Path.GetTempPath(), $"4rcstick_theme_{Guid.NewGuid():N}");
            string? tempZip = null;

            try
            {
                string themeFile = _settings.GetThemePath(themeName);
                string themesDir = Path.GetDirectoryName(themeFile) ?? throw new InvalidOperationException($"Invalid theme path: {themeFile}");
                string backupZip = ArcadeStick.Services.ThemePaths.Backup(themesDir, themeName);
                Directory.CreateDirectory(themesDir);
                ArcadeStick.Services.ThemeFileStore.CleanStaleTempZips(themesDir);

                Directory.CreateDirectory(stagingDir);
                File.WriteAllText(Path.Combine(stagingDir, "theme.json"), themeJson);

                tempZip = ArcadeStick.Services.ThemePaths.TempZip(themesDir, themeName);
                ZipFile.CreateFromDirectory(stagingDir, tempZip);
                ArcadeStick.Services.ThemeFileStore.VerifyZip(tempZip, stagingDir);
                ArcadeStick.Services.ThemeFileStore.ReplaceZip(tempZip, themeFile, backupZip);
            }
            finally
            {
                ArcadeStick.Services.ThemeFolderSwap.TryDeleteFolder(stagingDir);
                try
                {
                    if (tempZip != null && File.Exists(tempZip)) File.Delete(tempZip);
                }
                catch
                {
                }
            }
        }

        // Loads a just-created theme through ThemeLoadRunner, logs through writeLog (true when the load finished), shows
        // failureMessage if it did not, then refreshes the combo with the new name selected so Load can retry.
        private void LoadCreatedTheme(string themeName, string failureMessage, Action<bool> writeLog)
        {
            bool loaded = false;
            try
            {
                Func<bool> load = () =>
                {
                    loaded = _viewModel.LoadTheme(themeName);
                    if (!loaded) return false;
                    _refreshOptionsBindings?.Invoke();
                    _persistToDisk?.Invoke();
                    return true;
                };

                if (ThemeLoadRunner != null) ThemeLoadRunner(load);
                else load();
            }
            catch (Exception ex)
            {
                // A failure after the load itself finished means the theme is running from its own folder already
                if (loaded) MessageBox.Show($"'{themeName}' was created and loaded, but the display did not finish updating. Restart the app if it looks wrong.\n\nDetails: {ex.Message}", "Theme Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            writeLog(loaded);
            if (!loaded)
            {
                MessageBox.Show(failureMessage, "Theme Not Loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            RefreshThemeList();
            if (!loaded) CboThemePresets.SelectedItem = themeName; // the active theme may still be the old one
        }

        // Small name prompt built in code like the Advanced tab's (Create / Cancel, Enter creates, Esc cancels). Create closes it
        // only for an accepted name; otherwise the problem shows inline and it stays open. Returns the name, or null if cancelled.
        private string? PromptForNewThemeName()
        {
            var nameBox = new TextBox { Margin = new Thickness(0, 6, 0, 12), Style = (Style)FindResource("GLTextBoxStyle") };
            var createButton = MakePromptButton("Create");
            createButton.IsDefault = true;
            createButton.Margin = new Thickness(0, 0, 8, 0);
            var cancelButton = MakePromptButton("Cancel");
            cancelButton.IsCancel = true;

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(createButton);
            buttons.Children.Add(cancelButton);

            var labelText = new TextBlock { Text = "Theme Name" };
            labelText.SetResourceReference(TextBlock.ForegroundProperty, ArcadeStick.Services.GlobalThemeResources.FontStandardColor);
            labelText.SetResourceReference(TextBlock.FontSizeProperty, ArcadeStick.Services.GlobalThemeResources.FontStandardSize);

            var errorText = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, -6, 0, 12), Visibility = Visibility.Collapsed };
            errorText.SetResourceReference(TextBlock.ForegroundProperty, "ChromeError");
            errorText.SetResourceReference(TextBlock.FontSizeProperty, ArcadeStick.Services.GlobalThemeResources.FontStandardSize);

            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(labelText);
            panel.Children.Add(nameBox);
            panel.Children.Add(errorText);
            panel.Children.Add(buttons);

            var prompt = new Window
            {
                Title = "Create New Theme",
                Content = panel,
                Width = 320,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ShowInTaskbar = false
            };
            prompt.SetResourceReference(Window.BackgroundProperty, "ChromeWindowBg");

            createButton.Click += (s, args) =>
            {
                string? problem = FindNewThemeNameProblem(nameBox.Text.Trim());
                if (problem == null)
                {
                    prompt.DialogResult = true;
                    return;
                }

                errorText.Text = problem;
                errorText.Visibility = Visibility.Visible;
                nameBox.Focus();
                nameBox.SelectAll();
            };
            prompt.Loaded += (s, args) => nameBox.Focus();

            return prompt.ShowDialog() == true ? nameBox.Text.Trim() : null;
        }

        // Builds one name prompt button on the shared action button style (the Global button values come from the style)
        private Button MakePromptButton(string content) =>
            new Button { Content = content, Width = 70, Padding = new Thickness(0, 3, 0, 3), Style = (Style)FindResource("ThemedActionButtonStyle") };

        // Returns why a typed name cannot be used for a new theme (Check 1, then "default", then an existing theme in any case),
        // logging a SaveBlocked line, or null when it can. Only typed names come here, never existing themes' names.
        private string? FindNewThemeNameProblem(string name)
        {
            string reason;
            string message;
            if (!ArcadeStick.Services.ThemePaths.TryValidateNewName(name, out reason))
            {
                message = reason == "empty"
                    ? "Please enter a theme name."
                    : "Theme names can't contain \\ / : * ? \" < > | or \"..\", can't start with a dot, can't end with a dot or a space, can't be a reserved Windows name like CON, and can be at most 50 characters.";
            }
            else if (string.Equals(name, DefaultThemeName, StringComparison.OrdinalIgnoreCase))
            {
                reason = "the name of the launch fallback theme";
                message = $"The name '{DefaultThemeName}' is reserved. Choose another name.";
            }
            else if (GetThemeNames().Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
            {
                reason = "a theme with this name already exists";
                message = $"A theme named '{name}' already exists. Choose another name.";
            }
            else
            {
                return null;
            }

            ArcadeStick.Services.ThemeLog.Write("SaveBlocked", name, CboThemePresets.SelectedItem as string ?? "", _viewModel.LoadedThemeName, "blocked", reason);
            return message;
        }

        // The theme the app falls back to at launch, which a new theme may never take the name of
        private const string DefaultThemeName = "default";
        // [END SECTION: New Theme]

        // [SECTION: Delete Theme]
        // Prompts for confirmation (No is the default), then removes the selected theme through ThemeFileStore.DeleteTheme: the
        // zip moves to Themes\Deleted and the working folder is removed, or nothing changes. Deleting the loaded theme clears
        // the loaded and active names, so nothing keeps targeting it.
        private void BtnDeleteTheme_Click(object sender, RoutedEventArgs e)
        {
            if (CboThemePresets.SelectedItem is not string selectedTheme) return;

            string loadedTheme = _viewModel.LoadedThemeName;
            bool isLoadedTheme = string.Equals(selectedTheme, loadedTheme, StringComparison.OrdinalIgnoreCase);

            string prompt = $"Delete theme '{selectedTheme}'?\n\nThis removes the theme and its working folder from the app. Its zip is moved to the Themes\\Deleted folder, so it can be recovered by hand.";
            if (isLoadedTheme)
            {
                prompt += "\n\nThis is the theme currently loaded. Its images will be missing until you load another theme, and the app will start with the Default theme next time.";
            }

            if (MessageBox.Show(prompt, "Delete Theme", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                ArcadeStick.Services.ThemeLog.Write("Delete", selectedTheme, selectedTheme, loadedTheme, "cancelled");
                return;
            }

            try
            {
                string themesDir = Path.Combine(_settings.GetArcadeStickFilesPath(), "Themes");
                string themeFile = _settings.GetThemePath(selectedTheme);
                string extractDir = ArcadeStick.Services.ThemePaths.WorkingFolder(themesDir, selectedTheme);
                string stagingRoot = Path.Combine(themesDir, ArcadeStick.Services.ThemeFolderSwap.StagingFolderName);
                string deletedDir = Path.Combine(themesDir, ArcadeStick.Services.ThemeFileStore.DeletedFolderName);

                ArcadeStick.Services.ThemeFolderSwap.CleanStaleFolders(stagingRoot, Path.Combine(themesDir, "Extracted"));
                _viewModel.ReleaseThemeFiles();
                ArcadeStick.Services.ThemeFileStore.DeleteTheme(themeFile, extractDir, stagingRoot, deletedDir, selectedTheme);

                if (isLoadedTheme)
                {
                    _viewModel.LoadedThemeName = "";
                    _settings.ActiveThemeName = "";
                }
                ArcadeStick.Services.ThemeLog.Write("Delete", selectedTheme, selectedTheme, loadedTheme, "ok", isLoadedTheme ? "This was the loaded theme." : "");

                if (isLoadedTheme) _persistToDisk?.Invoke();
            }
            catch (Exception ex)
            {
                if (ex is ArcadeStick.Services.ThemePathException refusedDelete)
                {
                    ArcadeStick.Services.ThemeLog.Write("PathRefused", selectedTheme, selectedTheme, loadedTheme, "failed", $"Delete: {refusedDelete.Reason}");
                }
                else
                {
                    ArcadeStick.Services.ThemeLog.Write("Delete", selectedTheme, selectedTheme, loadedTheme, "failed", ex.Message);
                }

                string message = ex is ArcadeStick.Services.ThemeFileException
                    ? ex.Message
                    : $"Couldn't delete '{selectedTheme}'. Nothing was deleted.\n\nDetails: {ex.Message}";
                MessageBox.Show(message, "Theme Not Deleted", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // The release step let go of the current theme's files whatever happened, so show them again
            try
            {
                _viewModel.RefreshThemeBindings();
                RefreshThemeList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not refresh after a theme delete: {ex.Message}");
            }
        }
        // [END SECTION: Delete Theme]
    }
}