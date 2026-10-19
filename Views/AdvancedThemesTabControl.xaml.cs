using ArcadeStick.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    public partial class AdvancedThemesTabControl : UserControl
    {
        private ConfigurationSettings _settings = null!;
        private ThemeLayout _currentLayout = null!;
        private bool _loading;
        private bool _suppressGuideSync;

        // Group edit page state: which group's page is currently built, which part the alignment guides
        // follow (the part last clicked into or edited), and the controls built for each part.
        private string _builtGroupKey = "";
        private string _guidePartId = "";
        private readonly Dictionary<string, PartControls> _partControls = new();

        private class PartControls
        {
            public TextBox X = null!;
            public TextBox Y = null!;
            public TextBox Width = null!;
            public TextBox Height = null!;
            public CheckBox Hide = null!;
        }

        // Rect and color of the alignment guides for the selected layer (null = no guides).
        public record GuideInfo(double X, double Y, double Width, double Height, string ColorHex);

        // Raised whenever the guides should be redrawn or cleared. ThemeBuilderWindow forwards it to MainWindow.
        public event Action<GuideInfo?>? GuidesChanged;

        // Raised after any committed layout edit so the main window can re-apply the layout live.
        // Nothing is written to disk here - Save Adjustments / Save Changes still do that.
        public event Action? LayoutChanged;

        // Recomputes the guides from the selected layer's pending rect and the show/color preferences.
        public void RaiseGuides()
        {
            if (_settings == null) return;

            // Every committed edit, selection change, reorder, resolution switch and reset ends up here,
            // so this one call lets the main window re-apply the layout live.
            LayoutChanged?.Invoke();

            if (!_settings.ShowGuideLines
    || LstLayers.SelectedItem is not LayerListItem selected
    || IsBackground(selected.Id))
            {
                GuidesChanged?.Invoke(null);
                return;
            }

            // A group's guides follow the part last clicked into (first part by default)
            var unitEntries = GetUnitEntries(selected.Id);
            var entry = unitEntries.FirstOrDefault(e => e.Id == _guidePartId) ?? unitEntries.FirstOrDefault();
            if (entry == null)
            {
                GuidesChanged?.Invoke(null);
                return;
            }

            GuidesChanged?.Invoke(new GuideInfo(entry.X, entry.Y, entry.Width, entry.Height, _settings.GuideLineColorHex));
        }

        private class LayerListItem
        {
            public string Id { get; init; } = "";
            public string DisplayName { get; init; } = "";
        }

        public AdvancedThemesTabControl()
        {
            InitializeComponent();
        }

        // [SECTION: Initialization]
        // Fills the resolution combo box, then loads the active resolution's layout. Edits only touch
        // _settings.Layouts in memory - Save Adjustments persists to settings.json, and Save Changes on
        // the Basic Themes tab writes the layouts into the theme zip.
        public void Initialize(ConfigurationSettings settings)
        {
            _settings = settings;
            ReloadFromSettings();
        }

        // Re-reads the active resolution and layout from settings. Called on startup and after a theme
        // is loaded (which replaces _settings.Layouts).
        public void ReloadFromSettings()
        {
            _loading = true;

            _pendingSize = null; // a theme load or reload starts clean

            // No theme loaded or saved this session yet: the size the window opened at is the best
            // available "saved" size for Delete Size to return to.
            if (_settings.SavedThemeWidth <= 0 || _settings.SavedThemeHeight <= 0)
            {
                _settings.SavedThemeWidth = _settings.ActiveResolutionWidth;
                _settings.SavedThemeHeight = _settings.ActiveResolutionHeight;
            }

            var active = RebuildResolutionCombo(_settings.ActiveResolutionWidth, _settings.ActiveResolutionHeight);

            // Guide preferences (editor settings, not part of the theme)
            ChkShowGuides.IsChecked = _settings.ShowGuideLines;
            TxtGuideColorHex.Text = _settings.GuideLineColorHex;
            try
            {
                var guideColor = (Color)ColorConverter.ConvertFromString(_settings.GuideLineColorHex);
                _suppressGuideSync = true;
                GuideColorPicker.SelectedColor = guideColor;
                _suppressGuideSync = false;
            }
            catch { _suppressGuideSync = false; }

            _currentLayout = ResolveAndStore(active.Width, active.Height);
            _loading = false;

            RebuildLayerList(null);
        }
        // [END SECTION: Initialization]

        // [SECTION: Layout Helpers]
        // Returns a complete layout for the resolution (saved entries + registry defaults for anything
        // missing) and stores it back into _settings.Layouts, replacing any older entry for that size.
        private ThemeLayout ResolveAndStore(int width, int height)
        {
            var saved = _settings.Layouts.FirstOrDefault(l => l.Width == width && l.Height == height);
            var resolved = LayerRegistry.ResolveLayout(saved, width, height);
            StoreLayout(resolved);
            return resolved;
        }

        private void StoreLayout(ThemeLayout layout)
        {
            _settings.Layouts.RemoveAll(l => l.Width == layout.Width && l.Height == layout.Height);
            _settings.Layouts.Add(layout);
        }

        private LayerLayoutEntry? GetEntry(string id) => _currentLayout.Layers.FirstOrDefault(e => e.Id == id);

        private static bool IsBackground(string id) => LayerRegistry.GetDefinition(id)?.IsPositionable == false;

        // The key of a list row: the group's Id when the layer belongs to a group, otherwise the layer's own Id.
        private static string UnitKey(string id)
        {
            string groupId = LayerRegistry.GetDefinition(id)?.GroupId ?? "";
            return string.IsNullOrEmpty(groupId) ? id : groupId;
        }

        private static LayerGroupDefinition? GetGroup(string unitKey) => LayerRegistry.AllGroups.FirstOrDefault(g => g.Id == unitKey);

        // Every entry belonging to one list row: all parts of a group (in the group's page order), or the single layer.
        private List<LayerLayoutEntry> GetUnitEntries(string unitKey)
        {
            var group = GetGroup(unitKey);
            if (group == null) return _currentLayout.Layers.Where(e => e.Id == unitKey).ToList();

            var parts = new List<LayerLayoutEntry>();
            foreach (string partId in group.PartIds)
            {
                var part = GetEntry(partId);
                if (part != null) parts.Add(part);
            }
            return parts;
        }

        // Bottom-to-top order: background always first, then by Z, ties broken by registry order. The parts
        // of a group always stay together, ordered by the group's lowest Z.
        private List<LayerLayoutEntry> GetAscendingOrder()
        {
            var registry = LayerRegistry.AllLayers.Select((d, i) => (d.Id, i)).ToDictionary(x => x.Id, x => x.i);
            int RegistryIndex(string id) => registry.TryGetValue(id, out int idx) ? idx : int.MaxValue;

            var layers = _currentLayout.Layers;
            var unitZ = layers.GroupBy(e => UnitKey(e.Id)).ToDictionary(g => g.Key, g => g.Min(e => e.Z));
            var unitFirstIndex = layers.GroupBy(e => UnitKey(e.Id)).ToDictionary(g => g.Key, g => g.Min(e => RegistryIndex(e.Id)));

            return layers
                .OrderBy(e => IsBackground(e.Id) ? 0 : 1)
                .ThenBy(e => unitZ[UnitKey(e.Id)])
                .ThenBy(e => unitFirstIndex[UnitKey(e.Id)])
                .ThenBy(e => RegistryIndex(e.Id))
                .ToList();
        }

        // Rewrites every Z as a unique number from list position (bottom = 0), keeping the current order.
        // Every part of a group gets the same Z, since the group is one row in the list.
        private void NormalizeZ(List<LayerLayoutEntry> ascending)
        {
            int z = -1;
            string? lastKey = null;
            foreach (var entry in ascending)
            {
                string key = UnitKey(entry.Id);
                if (key != lastKey)
                {
                    z++;
                    lastKey = key;
                }
                entry.Z = z;
            }
        }
        // [END SECTION: Layout Helpers]

        // [SECTION: Layer List]
        // Shown frontmost-first (top of list = drawn on top). Optionally reselects a layer by Id.
        private void RebuildLayerList(string? selectId)
        {
            _loading = true;

            // One row per list unit (a group counts once). The row's Id is the group's Id for a group,
            // or the layer's own Id for a single layer.
            var display = GetAscendingOrder().Select(e => UnitKey(e.Id)).Distinct().Reverse()
                .Select(key =>
                {
                    var group = GetGroup(key);
                    var entry = GetEntry(key);
                    string name = group != null
                        ? group.DisplayName
                        : (entry != null && LayerRegistry.IsCustomId(key)
                            ? (string.IsNullOrWhiteSpace(entry.Name) ? "Custom Layer" : entry.Name)
                            : (LayerRegistry.GetDefinition(key)?.DisplayName ?? key));
                    return new LayerListItem { Id = key, DisplayName = name };
                })
                .ToList();
            LstLayers.ItemsSource = display;

            var toSelect = selectId != null ? display.FirstOrDefault(i => i.Id == selectId) : null;
            LstLayers.SelectedItem = toSelect;

            RebuildSavedSizes();

            _loading = false;
            LoadSelectedLayerIntoFields();
        }

        private void LstLayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            LoadSelectedLayerIntoFields();
        }

        private void BtnLayerUp_Click(object sender, RoutedEventArgs e) => MoveSelectedLayer(-1);
        private void BtnLayerDown_Click(object sender, RoutedEventArgs e) => MoveSelectedLayer(1);

        // Swaps the selected layer with its neighbor in the stacking order. The background stays pinned
        // at the bottom and can't be moved or passed.
        private void MoveSelectedLayer(int direction)
        {
            if (LstLayers.SelectedItem is not LayerListItem selected) return;
            if (IsBackground(selected.Id)) return;

            // Rows (a group counts once), top-to-bottom
            var display = GetAscendingOrder().Select(e => UnitKey(e.Id)).Distinct().Reverse().ToList();
            int index = display.IndexOf(selected.Id);
            int target = index + direction;
            if (index < 0 || target < 0 || target > display.Count - 2) return;

            (display[index], display[target]) = (display[target], display[index]);

            // display is top-to-bottom; Z counts up from the bottom. Every part of a group shares its row's Z.
            for (int i = 0; i < display.Count; i++)
            {
                foreach (var entry in GetUnitEntries(display[i])) entry.Z = display.Count - 1 - i;
            }

            RebuildLayerList(selected.Id);
        }
        // [SECTION: Custom Image Layers]
        // Reads a video's native pixel size by opening it in a MediaPlayer and waiting briefly for it to load
        // (BitmapDecoder can't read video). Returns false if the file can't be opened as video on this PC.
        private static bool TryGetVideoSize(string path, out double width, out double height)
        {
            width = 0;
            height = 0;

            var player = new System.Windows.Media.MediaPlayer();
            bool opened = false;
            bool failed = false;
            player.MediaOpened += (s, e) => opened = true;
            player.MediaFailed += (s, e) => failed = true;

            try
            {
                player.Open(new Uri(path, UriKind.Absolute));

                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!opened && !failed && DateTime.UtcNow < deadline)
                {
                    // Let the dispatcher deliver the MediaOpened / MediaFailed events
                    Application.Current.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                    System.Threading.Thread.Sleep(20);
                }

                if (opened)
                {
                    width = player.NaturalVideoWidth;
                    height = player.NaturalVideoHeight;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                player.Close();
            }

            return width > 0 && height > 0;
        }
        // Add Layer: asks for a name, then creates a box layer at the CURRENT resolution only, with the shape
        // layer defaults and no file yet (Browse on its Background tab sizes it to the file). At most
        // LayerRegistry.MaxCustomLayersPerResolution per resolution.
        private void BtnAddImageLayer_Click(object sender, RoutedEventArgs e)
        {
            int existingCount = _currentLayout.Layers.Count(l => LayerRegistry.IsCustomId(l.Id));
            if (existingCount >= LayerRegistry.MaxCustomLayersPerResolution)
            {
                MessageBox.Show($"This resolution already has the maximum of {LayerRegistry.MaxCustomLayersPerResolution} custom layers. Remove one to add another.",
                    "Add Layer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string? layerName = PromptForLayerName();
            if (layerName == null) return;

            int topZ = _currentLayout.Layers.Max(l => l.Z);
            var entry = new LayerLayoutEntry
            {
                Id = LayerRegistry.CustomIdPrefix + Guid.NewGuid().ToString("N"),
                Name = layerName,
                ImagePath = "",
                X = LayerRegistry.NewLayerX,
                Y = LayerRegistry.NewLayerY,
                Width = LayerRegistry.NewLayerWidth,
                Height = LayerRegistry.NewLayerHeight,
                Z = topZ + 1,
                Visible = true,
                VisibilityMode = LayerRegistry.DefaultVisibilityMode,
                BgColorHex = LayerRegistry.DefaultShapeBgColorHex,
                BorderSize = LayerRegistry.DefaultShapeBorderSize,
                BorderColorHex = LayerRegistry.DefaultShapeBorderColorHex,
                CornerRadius = LayerRegistry.DefaultShapeCornerRadius
            };

            _currentLayout.Layers.Add(entry);
            NormalizeZ(GetAscendingOrder());
            RebuildLayerList(entry.Id);
        }

        // Small name prompt built in code (OK / Cancel, Enter confirms, Esc cancels). Returns null if cancelled
        // or left empty.
        private string? PromptForLayerName()
        {
            var nameBox = new TextBox { Margin = new Thickness(0, 6, 0, 12), Padding = new Thickness(4), MaxLength = 40 };
            var okButton = new Button { Content = "OK", Width = 70, IsDefault = true, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(0, 3, 0, 3) };
            var cancelButton = new Button { Content = "Cancel", Width = 70, IsCancel = true, Padding = new Thickness(0, 3, 0, 3) };

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);

            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new TextBlock { Text = "Name for the new layer:" });
            panel.Children.Add(nameBox);
            panel.Children.Add(buttons);

            var prompt = new Window
            {
                Title = "Add Layer",
                Content = panel,
                Width = 320,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ShowInTaskbar = false
            };

            okButton.Click += (s, args) => prompt.DialogResult = true;
            prompt.Loaded += (s, args) => nameBox.Focus();

            if (prompt.ShowDialog() != true) return null;

            string name = nameBox.Text.Trim();
            return string.IsNullOrEmpty(name) ? null : name;
        }

        // Remove: deletes the selected custom layer at this resolution only (other resolutions are untouched).
        private void BtnRemoveLayer_Click(object sender, RoutedEventArgs e)
        {
            if (LstLayers.SelectedItem is not LayerListItem selected || !LayerRegistry.IsCustomId(selected.Id)) return;

            var result = MessageBox.Show(
                $"Remove the layer '{selected.DisplayName}' from the {_currentLayout.Width} x {_currentLayout.Height} layout?\n\nOther resolutions are not affected.",
                "Confirm Remove Layer", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            _currentLayout.Layers.RemoveAll(l => l.Id == selected.Id);
            NormalizeZ(GetAscendingOrder());
            RebuildLayerList(null);
        }
        // [END SECTION: Custom Image Layers]
        // [END SECTION: Layer List]

        // [SECTION: Layer Fields]
        // [SECTION: Layer Property Tabs]
        // Position is always the first tab. Each layer type lists the other tabs it shows; every other tab is
        // hidden. To give a layer type its own tabs: declare the TabItem in the XAML, add it to AllLayerTabs,
        // and list it for that layer in GetLayerTabs.
        private TabItem[] AllLayerTabs => new[] { TabGLFolders, TabGLGameTitles, TabSBSearch, TabSBButtons, TabShapeBackground, TabShapeBorder, TabMarqueeLogo };

        private TabItem[] GetLayerTabs(string? unitKey)
        {
            if (unitKey == LayerRegistry.GamesList) return new[] { TabGLFolders, TabGLGameTitles };

            // Search & Buttons group: label/box, shared button style, and the shared Border tab (mapped to the
            // Random Game Button entry by GetSelectedBorderEntry)
            if (unitKey == LayerRegistry.SearchAndButtonsGroup) return new[] { TabSBSearch, TabSBButtons, TabShapeBorder };

            // Marquee gets the Logo tab (the logo shown when no game is selected)
            if (unitKey == LayerRegistry.MarqueePanel) return new[] { TabMarqueeLogo };

            // Shape Layers 01 to 04 share one tab set
            if (unitKey != null && LayerRegistry.IsBoxId(unitKey)) return new[] { TabShapeBackground, TabShapeBorder };

            // Splash Image and Video Preview get the Border tab only
            if (unitKey == LayerRegistry.SplashImage || unitKey == LayerRegistry.VideoPreview) return new[] { TabShapeBorder };

            // Every other layer (custom image layers included) shows Position only for now.
            return Array.Empty<TabItem>();
        }

        // The layer whose tabs are showing. The selection returns to Position only when this changes, not on
        // every committed edit (edits reload the fields too).
        private string? _tabsUnitKey;

        private void ApplyLayerTabs(string? unitKey)
        {
            var wanted = GetLayerTabs(unitKey);
            foreach (var tab in AllLayerTabs)
            {
                tab.Visibility = wanted.Contains(tab) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (unitKey != _tabsUnitKey)
            {
                _tabsUnitKey = unitKey;
                TabsLayer.SelectedItem = TabPosition;
            }
        }
        // [END SECTION: Layer Property Tabs]

        private static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private void LoadSelectedLayerIntoFields()
        {
            // Tabs for the selected layer: Position always, the others per layer type. Selecting a different
            // layer returns to Position; edits to the same layer stay on the current tab.
            ApplyLayerTabs((LstLayers.SelectedItem as LayerListItem)?.Id);

            // Games List styling fields: filled from the entry when the Games List is selected
            LoadGamesListStyleFields();

            // Shape layer fields (visibility, background, border): filled when a Shape Layer is selected
            LoadShapeStyleFields();
            LoadBorderOnlyFields();

            // Search & Buttons fields (Search and Buttons tabs, Border tab hover row): filled when that group is selected
            LoadSearchButtonsStyleFields();

            // Marquee logo field: filled when the Marquee layer is selected
            if (LstLayers.SelectedItem is LayerListItem marqueeSel && marqueeSel.Id == LayerRegistry.MarqueePanel)
            {
                TxtMarqueeLogoFile.Text = GetEntry(LayerRegistry.MarqueePanel)?.ImagePath ?? "";
            }

            // A group row shows every part's fields at once on its own page
            if (LstLayers.SelectedItem is LayerListItem groupItem && GetGroup(groupItem.Id) is LayerGroupDefinition selectedGroup)
            {
                LoadGroupIntoFields(selectedGroup);
                return;
            }

            if (LstLayers.SelectedItem is not LayerListItem selected || GetEntry(selected.Id) is not LayerLayoutEntry entry)
            {
                PnlLayerFields.Visibility = Visibility.Collapsed;
                RaiseGuides();
                return;
            }

            // Single layer: undo anything the group page changed
            PnlGroupParts.Visibility = Visibility.Collapsed;
            ChkGroupMove.Visibility = Visibility.Collapsed;

            var def = LayerRegistry.GetDefinition(entry.Id);
            bool positionable = def?.IsPositionable ?? true;
            bool canHide = def?.CanHide ?? true;

            // File field: only for built-in layers that show a file
            PnlFileField.Visibility = (def?.HasFile ?? false) ? Visibility.Visible : Visibility.Collapsed;
            TxtLayerFile.Text = entry.ImagePath;

            _loading = true;
            PnlLayerFields.Visibility = Visibility.Visible;
            bool isCustom = LayerRegistry.IsCustomId(entry.Id);
            TxtLayerName.Text = isCustom
                ? (string.IsNullOrWhiteSpace(entry.Name) ? "Custom Layer" : entry.Name)
                : (def?.DisplayName ?? entry.Id);
            BtnRemoveLayer.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

            PnlRectFields.Visibility = positionable ? Visibility.Visible : Visibility.Collapsed;
            bool isShape = LayerRegistry.IsBoxId(entry.Id);
            ChkLayerHidden.Visibility = (positionable && canHide && !isShape) ? Visibility.Visible : Visibility.Collapsed;
            PnlVisibilityMode.Visibility = isShape ? Visibility.Visible : Visibility.Collapsed;
            BtnResetLayer.Visibility = (positionable && !isCustom) ? Visibility.Visible : Visibility.Collapsed;
            ChkLayerHidden.IsEnabled = canHide;

            TxtLayerX.Text = Fmt(entry.X);
            TxtLayerY.Text = Fmt(entry.Y);
            TxtLayerWidth.Text = Fmt(entry.Width);
            TxtLayerHeight.Text = Fmt(entry.Height);
            ChkLayerHidden.IsChecked = !entry.Visible;
            _loading = false;
            RaiseGuides();
        }

        // [SECTION: Group Edit Page]
        // Shows a grouped layer (Search & Buttons, Game Info): one section per part, all visible at once,
        // in the group's page order. Sections are built once per group and then only refreshed.
        private void LoadGroupIntoFields(LayerGroupDefinition group)
        {
            _loading = true;

            PnlLayerFields.Visibility = Visibility.Visible;
            TxtLayerName.Text = group.DisplayName;
            PnlRectFields.Visibility = Visibility.Collapsed;

            // The Random Game Display group has one file shared by both of its states, stored on its
            // "when a game is selected" part.
            bool hasSharedFile = group.Id == LayerRegistry.RandomGameGroup;
            PnlFileField.Visibility = hasSharedFile ? Visibility.Visible : Visibility.Collapsed;
            TxtLayerFile.Text = hasSharedFile ? (GetEntry(LayerRegistry.RandomGameDisplay)?.ImagePath ?? "") : "";

            ChkLayerHidden.Visibility = Visibility.Collapsed;
            PnlVisibilityMode.Visibility = Visibility.Collapsed;
            BtnRemoveLayer.Visibility = Visibility.Collapsed;
            BtnResetLayer.Visibility = Visibility.Visible;
            PnlGroupParts.Visibility = Visibility.Visible;
            ChkGroupMove.Visibility = group.AllowGroupMove ? Visibility.Visible : Visibility.Collapsed;

            if (_builtGroupKey != group.Id)
            {
                BuildGroupParts(group);
                _builtGroupKey = group.Id;
                _guidePartId = group.PartIds.FirstOrDefault() ?? "";
            }

            foreach (string partId in group.PartIds)
            {
                if (GetEntry(partId) is not LayerLayoutEntry entry || !_partControls.TryGetValue(partId, out var controls)) continue;

                controls.X.Text = Fmt(entry.X);
                controls.Y.Text = Fmt(entry.Y);
                controls.Width.Text = Fmt(entry.Width);
                controls.Height.Text = Fmt(entry.Height);
                controls.Hide.IsChecked = !entry.Visible;
            }

            _loading = false;
            RaiseGuides();
        }

        private void BuildGroupParts(LayerGroupDefinition group)
        {
            PnlGroupParts.Children.Clear();
            _partControls.Clear();

            var fieldBackground = (Brush)new BrushConverter().ConvertFromString("#1C1C1E")!;
            var fieldBorder = (Brush)new BrushConverter().ConvertFromString("#3A3A3C")!;

            TextBox MakeBox(string partId, int column, Grid host)
            {
                var box = new TextBox
                {
                    Tag = partId,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Background = fieldBackground,
                    Foreground = Brushes.White,
                    BorderBrush = fieldBorder,
                    Padding = new Thickness(4),
                    Margin = new Thickness(column == 0 ? 0 : 6, 0, 0, 0)
                };
                box.GotFocus += PartField_GotFocus;
                box.LostFocus += PartField_LostFocus;
                box.KeyDown += PartField_KeyDown;
                Grid.SetRow(box, 1);
                Grid.SetColumn(box, column);
                host.Children.Add(box);
                return box;
            }

            foreach (string partId in group.PartIds)
            {
                string partName = LayerRegistry.GetDefinition(partId)?.DisplayName ?? partId;

                var heading = new TextBlock { Text = partName, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 4, 0, 4) };
                heading.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("HeaderColorHex"));
                heading.SetBinding(TextBlock.FontSizeProperty, new System.Windows.Data.Binding("HeaderFontSize"));
                PnlGroupParts.Children.Add(heading);

                var host = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                for (int i = 0; i < 4; i++) host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                string[] labels = { "X:", "Y:", "Width:", "Height:" };
                for (int i = 0; i < 4; i++)
                {
                    var label = new TextBlock { Text = labels[i], Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 3) };
                    label.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("SubHeaderColorHex"));
                    label.SetBinding(TextBlock.FontSizeProperty, new System.Windows.Data.Binding("SubHeaderFontSize"));
                    Grid.SetRow(label, 0);
                    Grid.SetColumn(label, i);
                    host.Children.Add(label);
                }

                var controls = new PartControls
                {
                    X = MakeBox(partId, 0, host),
                    Y = MakeBox(partId, 1, host),
                    Width = MakeBox(partId, 2, host),
                    Height = MakeBox(partId, 3, host)
                };

                var hide = new CheckBox { Content = "Hide", Tag = partId, Margin = new Thickness(0, 6, 0, 14), VerticalContentAlignment = VerticalAlignment.Center };
                hide.SetBinding(CheckBox.ForegroundProperty, new System.Windows.Data.Binding("StandardColorHex"));
                hide.SetBinding(CheckBox.FontSizeProperty, new System.Windows.Data.Binding("StandardFontSize"));
                hide.Click += PartHide_Click;
                controls.Hide = hide;

                _partControls[partId] = controls;
                PnlGroupParts.Children.Add(host);
                PnlGroupParts.Children.Add(hide);
            }
        }

        // Clicking into a part's field points the alignment guides at that part
        private void PartField_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || sender is not TextBox box || box.Tag is not string partId) return;
            if (_guidePartId == partId) return;

            _guidePartId = partId;
            RaiseGuides();
        }

        private void PartField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyPartField(sender as TextBox);
        }

        private void PartField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyPartField(sender as TextBox);
        }

        // Applies one part field's value. Invalid input is discarded and the field shows the stored value again.
        // With "Move all parts together" on, an X or Y change moves every other part of the group by the same
        // amount; Width and Height never propagate.
        private void ApplyPartField(TextBox? box)
        {
            if (_loading || box == null || box.Tag is not string partId) return;
            if (GetEntry(partId) is not LayerLayoutEntry entry || !_partControls.TryGetValue(partId, out var controls)) return;

            if (double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                double dx = 0, dy = 0;
                if (box == controls.X) { dx = value - entry.X; entry.X = value; }
                else if (box == controls.Y) { dy = value - entry.Y; entry.Y = value; }
                else if (box == controls.Width && value > 0) entry.Width = value;
                else if (box == controls.Height && value > 0) entry.Height = value;

                if ((dx != 0 || dy != 0) && ChkGroupMove.IsChecked == true
                    && GetGroup(LayerRegistry.GetDefinition(partId)?.GroupId ?? "") is LayerGroupDefinition group && group.AllowGroupMove)
                {
                    foreach (string otherId in group.PartIds.Where(id => id != partId))
                    {
                        if (GetEntry(otherId) is not LayerLayoutEntry other) continue;
                        other.X += dx;
                        other.Y += dy;
                    }
                }
            }

            _guidePartId = partId;
            LoadSelectedLayerIntoFields();
        }

        private void PartHide_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || sender is not CheckBox box || box.Tag is not string partId) return;
            if (GetEntry(partId) is not LayerLayoutEntry entry) return;

            entry.Visible = box.IsChecked != true;
            LayoutChanged?.Invoke();
        }
        // [END SECTION: Group Edit Page]

        // Applies a field's value on Enter or focus loss. Invalid input is discarded and the field is
        // restored to the stored value.
        private void LayerField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyField(sender as TextBox);
        }

        private void LayerField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyField(sender as TextBox);
        }

        private void ApplyField(TextBox? box)
        {
            if (_loading || box == null) return;
            if (LstLayers.SelectedItem is not LayerListItem selected || GetEntry(selected.Id) is not LayerLayoutEntry entry) return;

            if (double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                // Ratio lock: when on, a changed Width or Height resizes the other one to keep the layer's
                // current proportions. The ratio is read from the stored size just before the edit, so
                // there is nothing to remember between edits.
                double oldWidth = entry.Width;
                double oldHeight = entry.Height;
                bool keepRatio = BtnRatioLock.IsChecked == true && oldWidth > 0 && oldHeight > 0;

                if (box == TxtLayerX) entry.X = value;
                else if (box == TxtLayerY) entry.Y = value;
                else if (box == TxtLayerWidth && value > 0)
                {
                    entry.Width = value;
                    if (keepRatio && value != oldWidth) entry.Height = value * oldHeight / oldWidth;
                }
                else if (box == TxtLayerHeight && value > 0)
                {
                    entry.Height = value;
                    if (keepRatio && value != oldHeight) entry.Width = value * oldWidth / oldHeight;
                }
            }

            // Re-display the stored value (normalizes formatting, and reverts invalid input)
            _loading = true;
            TxtLayerX.Text = Fmt(entry.X);
            TxtLayerY.Text = Fmt(entry.Y);
            TxtLayerWidth.Text = Fmt(entry.Width);
            TxtLayerHeight.Text = Fmt(entry.Height);
            _loading = false;
            LoadSelectedLayerIntoFields();
        }

        // File field (layers that show a file, e.g. Random Game Display): Browse stores the path in the layer's
        // ImagePath, same convention as Add Image Layer. The file isn't read here - the main window loads it.
        // The entry that holds a row's file path: the row's own entry, or for the Random Game Display group
        // the "when a game is selected" part (both of its states share that one file).
        private LayerLayoutEntry? GetFileEntry(string unitKey)
        {
            if (unitKey == LayerRegistry.RandomGameGroup) return GetEntry(LayerRegistry.RandomGameDisplay);
            return GetEntry(unitKey);
        }

        private void BtnBrowseLayerFile_Click(object sender, RoutedEventArgs e)
        {
            if (LstLayers.SelectedItem is not LayerListItem selected || GetFileEntry(selected.Id) is not LayerLayoutEntry entry) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image and Video Files (*.png;*.jpg;*.jpeg;*.gif;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|Video Files (*.mp4)|*.mp4|All files (*.*)|*.*",
                Title = "Select Image or Video for This Layer",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() != true) return;

            string storedPath = dialog.FileName;
            try
            {
                string relative = System.IO.Path.GetRelativePath(_settings.BaseDirectory, dialog.FileName);
                if (!relative.StartsWith("..") && !System.IO.Path.IsPathRooted(relative)) storedPath = relative;
            }
            catch { }

            entry.ImagePath = storedPath;
            LoadSelectedLayerIntoFields();
        }

        private void BtnClearLayerFile_Click(object sender, RoutedEventArgs e)
        {
            if (LstLayers.SelectedItem is not LayerListItem selected || GetFileEntry(selected.Id) is not LayerLayoutEntry entry) return;

            entry.ImagePath = "";
            LoadSelectedLayerIntoFields();
        }

        private void ChkLayerHidden_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (LstLayers.SelectedItem is not LayerListItem selected || GetEntry(selected.Id) is not LayerLayoutEntry entry) return;

            entry.Visible = ChkLayerHidden.IsChecked != true;
            LayoutChanged?.Invoke();
        }

        // [SECTION: Shape Layer Fields]
        // Shown while Shape Layer 01 to 04 is selected. Values live on the layer entry (per resolution) and apply
        // live; invalid input reverts to the stored value. Color pickers reuse _suppressGLColorSync.
        private static readonly (string Mode, string Label)[] VisibilityChoices =
        {
            (LayerRegistry.VisibilityAlways, "Always"),
            (LayerRegistry.VisibilityGameSelected, "Only when a game is selected"),
            (LayerRegistry.VisibilityNoGameSelected, "Only when no game is selected"),
            (LayerRegistry.VisibilityNever, "Never (hidden)")
        };

        private LayerLayoutEntry? GetSelectedShapeEntry()
        {
            if (LstLayers.SelectedItem is LayerListItem sel && LayerRegistry.IsBoxId(sel.Id)) return GetEntry(sel.Id);
            return null;
        }

        // Returns the entry the Border tab edits: the selected layer when it has a Border tab (shape layers, Splash
        // Image, Video Preview), or the Random Game Button entry (the shared button style) for Search & Buttons
        private LayerLayoutEntry? GetSelectedBorderEntry()
        {
            if (LstLayers.SelectedItem is not LayerListItem sel) return null;
            if (sel.Id == LayerRegistry.SearchAndButtonsGroup) return GetEntry(LayerRegistry.RandomButton);
            if (LayerRegistry.HasBorderTab(sel.Id)) return GetEntry(sel.Id);
            return null;
        }

        // Fills the Border tab for Splash Image, Video Preview and Search & Buttons (the shape layers are filled by LoadShapeStyleFields)
        private void LoadBorderOnlyFields()
        {
            if (GetSelectedShapeEntry() != null) return;
            var entry = GetSelectedBorderEntry();
            if (entry == null) return;

            _loading = true;

            string borderHex = entry.BorderColorHex ?? LayerRegistry.DefaultShapeBorderColorHex;
            TxtShapeBorderColorHex.Text = borderHex;
            try
            {
                _suppressGLColorSync = true;
                ShapeBorderColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(borderHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }

            TxtShapeBorderSize.Text = (entry.BorderSize ?? 0).ToString(CultureInfo.InvariantCulture);
            TxtShapeCornerRadius.Text = (entry.CornerRadius ?? LayerRegistry.DefaultShapeCornerRadius).ToString(CultureInfo.InvariantCulture);

            _loading = false;
        }

        private void LoadShapeStyleFields()
        {
            var entry = GetSelectedShapeEntry();
            if (entry == null) return;

            _loading = true;

            if (CboVisibilityMode.Items.Count == 0)
            {
                foreach (var choice in VisibilityChoices) CboVisibilityMode.Items.Add(choice.Label);
            }
            string mode = entry.VisibilityMode ?? LayerRegistry.DefaultVisibilityMode;
            int index = Array.FindIndex(VisibilityChoices, c => c.Mode == mode);
            CboVisibilityMode.SelectedIndex = index >= 0 ? index : 0;

            TxtShapeFile.Text = entry.ImagePath;
            TxtShapeOpacity.Text = (entry.FileOpacity ?? LayerRegistry.DefaultFileOpacity).ToString(CultureInfo.InvariantCulture);

            string bgHex = entry.BgColorHex ?? LayerRegistry.DefaultShapeBgColorHex;
            TxtShapeBgColorHex.Text = bgHex;
            try
            {
                _suppressGLColorSync = true;
                ShapeBgColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(bgHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }

            string borderHex = entry.BorderColorHex ?? LayerRegistry.DefaultShapeBorderColorHex;
            TxtShapeBorderColorHex.Text = borderHex;
            try
            {
                _suppressGLColorSync = true;
                ShapeBorderColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(borderHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }

            TxtShapeBorderSize.Text = (entry.BorderSize ?? LayerRegistry.DefaultShapeBorderSize).ToString(CultureInfo.InvariantCulture);
            TxtShapeCornerRadius.Text = (entry.CornerRadius ?? LayerRegistry.DefaultShapeCornerRadius).ToString(CultureInfo.InvariantCulture);

            _loading = false;
        }

        private void CboVisibilityMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;
            if (GetSelectedShapeEntry() is not LayerLayoutEntry entry) return;
            int index = CboVisibilityMode.SelectedIndex;
            if (index < 0 || index >= VisibilityChoices.Length) return;

            entry.VisibilityMode = VisibilityChoices[index].Mode;
            RaiseGuides();
        }
        // Browse stores the path in the layer's ImagePath, same convention as the other file fields.
        // Reads an image's or video's native pixel size (custom layer auto-resize). False if it can't be read.
        private static bool TryGetFileSize(string path, out double width, out double height)
        {
            width = 0;
            height = 0;

            try
            {
                if (System.IO.Path.GetExtension(path).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    return TryGetVideoSize(path, out width, out height);
                }

                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    new Uri(path, UriKind.Absolute),
                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation | System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.None);
                width = decoder.Frames[0].PixelWidth;
                height = decoder.Frames[0].PixelHeight;
                return width > 0 && height > 0;
            }
            catch
            {
                return false;
            }
        }

        private void BtnBrowseShapeFile_Click(object sender, RoutedEventArgs e)
        {
            if (GetSelectedShapeEntry() is not LayerLayoutEntry entry) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image and Video Files (*.png;*.jpg;*.jpeg;*.gif;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|Video Files (*.mp4)|*.mp4|All files (*.*)|*.*",
                Title = "Select Image or Video for This Layer",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() != true) return;

            string storedPath = dialog.FileName;
            try
            {
                string relative = System.IO.Path.GetRelativePath(_settings.BaseDirectory, dialog.FileName);
                if (!relative.StartsWith("..") && !System.IO.Path.IsPathRooted(relative)) storedPath = relative;
            }
            catch { }

            // Custom layers resize to the file's native size (position stays, ratio lock is ignored)
            if (LayerRegistry.IsCustomId(entry.Id) && TryGetFileSize(dialog.FileName, out double fileWidth, out double fileHeight))
            {
                entry.Width = fileWidth;
                entry.Height = fileHeight;
            }

            entry.ImagePath = storedPath;
            LoadSelectedLayerIntoFields();
        }

        // Marquee logo: Browse stores the path in the Marquee entry's ImagePath, same convention as the other file fields.
        // The main window reads it from there (see the logo lookup in MainViewModel).
        private void BtnBrowseMarqueeLogo_Click(object sender, RoutedEventArgs e)
        {
            if (GetEntry(LayerRegistry.MarqueePanel) is not LayerLayoutEntry entry) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All files (*.*)|*.*",
                Title = "Select Logo Image",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() != true) return;

            string storedPath = dialog.FileName;
            try
            {
                string relative = System.IO.Path.GetRelativePath(_settings.BaseDirectory, dialog.FileName);
                if (!relative.StartsWith("..") && !System.IO.Path.IsPathRooted(relative)) storedPath = relative;
            }
            catch { }

            entry.ImagePath = storedPath;
            LoadSelectedLayerIntoFields();
        }

        private void BtnClearMarqueeLogo_Click(object sender, RoutedEventArgs e)
        {
            if (GetEntry(LayerRegistry.MarqueePanel) is not LayerLayoutEntry entry) return;

            entry.ImagePath = "";
            LoadSelectedLayerIntoFields();
        }

        private void BtnClearShapeFile_Click(object sender, RoutedEventArgs e)
        {
            if (GetSelectedShapeEntry() is not LayerLayoutEntry entry) return;

            entry.ImagePath = "";
            LoadSelectedLayerIntoFields();
        }

        private void ShapeBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetSelectedShapeEntry() is not LayerLayoutEntry entry) return;

            var c = ShapeBgColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtShapeBgColorHex.Text = hex;
            entry.BgColorHex = hex;
            RaiseGuides();
        }

        private void TxtShapeBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetSelectedShapeEntry() is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtShapeBgColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                ShapeBgColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtShapeBgColorHex.Text = hex;
                entry.BgColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtShapeBgColorHex.Text = entry.BgColorHex ?? LayerRegistry.DefaultShapeBgColorHex;
            }
        }

        private void ShapeBorderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetSelectedBorderEntry() is not LayerLayoutEntry entry) return;

            var c = ShapeBorderColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtShapeBorderColorHex.Text = hex;
            entry.BorderColorHex = hex;
            RaiseGuides();
        }



        private void TxtShapeBorderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetSelectedBorderEntry() is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtShapeBorderColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                ShapeBorderColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtShapeBorderColorHex.Text = hex;
                entry.BorderColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtShapeBorderColorHex.Text = entry.BorderColorHex ?? LayerRegistry.DefaultShapeBorderColorHex;
            }
        }

        // File opacity (0 to 100 percent): Enter or focus loss, invalid input reverts.
        private void ShapeOpacityField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyShapeOpacity();
        }

        private void ShapeOpacityField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyShapeOpacity();
        }

        private void ApplyShapeOpacity()
        {
            if (_loading || _settings == null) return;
            if (GetSelectedShapeEntry() is not LayerLayoutEntry entry) return;

            if (int.TryParse(TxtShapeOpacity.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int opacity) && opacity >= 0 && opacity <= 100)
                entry.FileOpacity = opacity;

            LoadSelectedLayerIntoFields();
        }

        // Border size (0 to 50) and corner radius (0 to 200): Enter or focus loss, invalid input reverts.
        private void ShapeBorderField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyShapeBorderFields();
        }

        private void ShapeBorderField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyShapeBorderFields();
        }

        private void ApplyShapeBorderFields()
        {
            if (_loading || _settings == null) return;
            if (GetSelectedBorderEntry() is not LayerLayoutEntry entry) return;

            if (int.TryParse(TxtShapeBorderSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int size) && size >= 0 && size <= 50)
                entry.BorderSize = size;
            if (int.TryParse(TxtShapeCornerRadius.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int radius) && radius >= 0 && radius <= 200)
                entry.CornerRadius = radius;

            LoadSelectedLayerIntoFields();
        }
        // [END SECTION: Shape Layer Fields]

        // [SECTION: Games List Styling Fields]
        // Shown only while the Games List layer is selected. Values live on the layer entry (per resolution)
        // and apply live on Enter or focus loss; invalid input reverts to the stored value.
        private bool _suppressGLColorSync;

        // Fills the Folders and Game Titles tab fields from the Games List entry. Which tabs are visible is decided by ApplyLayerTabs.
        private void LoadGamesListStyleFields()
        {
            bool show = LstLayers.SelectedItem is LayerListItem sel && sel.Id == LayerRegistry.GamesList && GetEntry(LayerRegistry.GamesList) != null;
            if (!show) return;

            var entry = GetEntry(LayerRegistry.GamesList)!;
            string hex = entry.FolderColorHex ?? LayerRegistry.DefaultFolderColorHex;

            _loading = true;
            TxtGLFolderFontSize.Text = (entry.FolderFontSize ?? LayerRegistry.DefaultFolderFontSize).ToString(CultureInfo.InvariantCulture);
            TxtGLFolderColorHex.Text = hex;
            try
            {
                _suppressGLColorSync = true;
                GLFolderColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(hex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }

            string selHex = entry.FolderSelectedColorHex ?? LayerRegistry.DefaultFolderSelectedColorHex;
            TxtGLFolderSelectedColorHex.Text = selHex;
            try
            {
                _suppressGLColorSync = true;
                GLFolderSelectedColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(selHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }

            string selBgHex = entry.FolderSelectedBgColorHex ?? LayerRegistry.DefaultFolderSelectedBgColorHex;
            TxtGLFolderSelectedBgColorHex.Text = selBgHex;
            try
            {
                _suppressGLColorSync = true;
                GLFolderSelectedBgColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(selBgHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }

            // Font dropdown: filled once from the registry's font list, then set to the saved family
            if (CboGLFolderFont.Items.Count == 0)
            {
                foreach (string font in LayerRegistry.AvailableFonts) CboGLFolderFont.Items.Add(font);
            }
            string family = string.IsNullOrWhiteSpace(entry.FolderFontFamily) ? LayerRegistry.DefaultFontFamily : entry.FolderFontFamily;
            CboGLFolderFont.SelectedItem = LayerRegistry.AvailableFonts.Contains(family) ? family : LayerRegistry.DefaultFontFamily;
            ChkGLFolderBold.IsChecked = entry.FolderBold ?? LayerRegistry.DefaultFolderBold;

            string arrowHex = entry.ArrowColorHex ?? LayerRegistry.DefaultArrowColorHex;
            TxtGLArrowColorHex.Text = arrowHex;
            try
            {
                _suppressGLColorSync = true;
                GLArrowColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(arrowHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }
            TxtGLArrowNudge.Text = (entry.ArrowNudgeY ?? LayerRegistry.DefaultArrowNudgeY).ToString(CultureInfo.InvariantCulture);

            // Game title section
            TxtGLGameFontSize.Text = (entry.GameFontSize ?? LayerRegistry.DefaultGameFontSize).ToString(CultureInfo.InvariantCulture);

            string gameHex = entry.GameColorHex ?? LayerRegistry.DefaultGameColorHex;
            TxtGLGameColorHex.Text = gameHex;
            try
            {
                _suppressGLColorSync = true;
                GLGameColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(gameHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }

            if (CboGLGameFont.Items.Count == 0)
            {
                foreach (string font in LayerRegistry.AvailableFonts) CboGLGameFont.Items.Add(font);
            }
            string gameFamily = string.IsNullOrWhiteSpace(entry.GameFontFamily) ? LayerRegistry.DefaultFontFamily : entry.GameFontFamily;
            CboGLGameFont.SelectedItem = LayerRegistry.AvailableFonts.Contains(gameFamily) ? gameFamily : LayerRegistry.DefaultFontFamily;
            ChkGLGameBold.IsChecked = entry.GameBold ?? LayerRegistry.DefaultGameBold;

            string gameSelHex = entry.GameSelectedColorHex ?? LayerRegistry.DefaultGameSelectedColorHex;
            TxtGLGameSelectedColorHex.Text = gameSelHex;
            try
            {
                _suppressGLColorSync = true;
                GLGameSelectedColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(gameSelHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }

            string gameSelBgHex = entry.GameSelectedBgColorHex ?? LayerRegistry.DefaultGameSelectedBgColorHex;
            TxtGLGameSelectedBgColorHex.Text = gameSelBgHex;
            try
            {
                _suppressGLColorSync = true;
                GLGameSelectedBgColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(gameSelBgHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }

            string gameHoverHex = entry.GameHoverColorHex ?? LayerRegistry.DefaultGameHoverColorHex;
            TxtGLGameHoverColorHex.Text = gameHoverHex;
            try
            {
                _suppressGLColorSync = true;
                GLGameHoverColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(gameHoverHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }

            // Drop shadow controls
            ChkGLFolderShadow.IsChecked = entry.FolderShadowEnabled ?? LayerRegistry.DefaultShadowEnabled;
            string folderShadowHex = entry.FolderShadowColorHex ?? LayerRegistry.DefaultShadowColorHex;
            TxtGLFolderShadowColorHex.Text = folderShadowHex;
            try
            {
                _suppressGLColorSync = true;
                GLFolderShadowColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(folderShadowHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }
            TxtGLFolderShadowBlur.Text = (entry.FolderShadowBlur ?? LayerRegistry.DefaultShadowBlur).ToString(CultureInfo.InvariantCulture);
            TxtGLFolderShadowDepth.Text = (entry.FolderShadowDepth ?? LayerRegistry.DefaultShadowDepth).ToString(CultureInfo.InvariantCulture);

            ChkGLGameShadow.IsChecked = entry.GameShadowEnabled ?? LayerRegistry.DefaultShadowEnabled;
            string gameShadowHex = entry.GameShadowColorHex ?? LayerRegistry.DefaultShadowColorHex;
            TxtGLGameShadowColorHex.Text = gameShadowHex;
            try
            {
                _suppressGLColorSync = true;
                GLGameShadowColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(gameShadowHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }
            TxtGLGameShadowBlur.Text = (entry.GameShadowBlur ?? LayerRegistry.DefaultShadowBlur).ToString(CultureInfo.InvariantCulture);
            TxtGLGameShadowDepth.Text = (entry.GameShadowDepth ?? LayerRegistry.DefaultShadowDepth).ToString(CultureInfo.InvariantCulture);

            // Selected-row drop shadow controls
            ChkGLFolderSelShadow.IsChecked = entry.FolderSelectedShadowEnabled ?? LayerRegistry.DefaultShadowEnabled;
            string folderSelShadowHex = entry.FolderSelectedShadowColorHex ?? LayerRegistry.DefaultShadowColorHex;
            TxtGLFolderSelShadowColorHex.Text = folderSelShadowHex;
            try
            {
                _suppressGLColorSync = true;
                GLFolderSelShadowColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(folderSelShadowHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }
            TxtGLFolderSelShadowBlur.Text = (entry.FolderSelectedShadowBlur ?? LayerRegistry.DefaultShadowBlur).ToString(CultureInfo.InvariantCulture);
            TxtGLFolderSelShadowDepth.Text = (entry.FolderSelectedShadowDepth ?? LayerRegistry.DefaultShadowDepth).ToString(CultureInfo.InvariantCulture);

            ChkGLGameSelShadow.IsChecked = entry.GameSelectedShadowEnabled ?? LayerRegistry.DefaultShadowEnabled;
            string gameSelShadowHex = entry.GameSelectedShadowColorHex ?? LayerRegistry.DefaultShadowColorHex;
            TxtGLGameSelShadowColorHex.Text = gameSelShadowHex;
            try
            {
                _suppressGLColorSync = true;
                GLGameSelShadowColorPicker.SelectedColor = (Color)ColorConverter.ConvertFromString(gameSelShadowHex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }
            TxtGLGameSelShadowBlur.Text = (entry.GameSelectedShadowBlur ?? LayerRegistry.DefaultShadowBlur).ToString(CultureInfo.InvariantCulture);
            TxtGLGameSelShadowDepth.Text = (entry.GameSelectedShadowDepth ?? LayerRegistry.DefaultShadowDepth).ToString(CultureInfo.InvariantCulture);

            _loading = false;
        }

        private void GLStyleField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyGLFolderFontSize();
        }

        private void GLStyleField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyGLFolderFontSize();
        }

        private void ApplyGLFolderFontSize()
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            if (int.TryParse(TxtGLFolderFontSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int size) && size >= 6 && size <= 72)
            {
                entry.FolderFontSize = size;
            }

            // Re-display the stored value (reverts invalid input) and re-apply the layout, which repaints the rows
            LoadSelectedLayerIntoFields();
        }

        private void GLFolderColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLFolderColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLFolderColorHex.Text = hex;
            entry.FolderColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLFolderColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLFolderColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLFolderColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLFolderColorHex.Text = hex;
                entry.FolderColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLFolderColorHex.Text = entry.FolderColorHex ?? LayerRegistry.DefaultFolderColorHex;
            }
        }

        private void GLFolderSelectedColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLFolderSelectedColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLFolderSelectedColorHex.Text = hex;
            entry.FolderSelectedColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLFolderSelectedColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLFolderSelectedColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLFolderSelectedColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLFolderSelectedColorHex.Text = hex;
                entry.FolderSelectedColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLFolderSelectedColorHex.Text = entry.FolderSelectedColorHex ?? LayerRegistry.DefaultFolderSelectedColorHex;
            }
        }

        private void GLFolderSelectedBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLFolderSelectedBgColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLFolderSelectedBgColorHex.Text = hex;
            entry.FolderSelectedBgColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLFolderSelectedBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLFolderSelectedBgColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLFolderSelectedBgColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLFolderSelectedBgColorHex.Text = hex;
                entry.FolderSelectedBgColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLFolderSelectedBgColorHex.Text = entry.FolderSelectedBgColorHex ?? LayerRegistry.DefaultFolderSelectedBgColorHex;
            }
        }
        private void CboGLFolderFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (CboGLFolderFont.SelectedItem is not string family) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            entry.FolderFontFamily = family;
            RaiseGuides();
        }

        private void ChkGLFolderBold_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            entry.FolderBold = ChkGLFolderBold.IsChecked == true;
            RaiseGuides();
        }
        private void GLArrowColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLArrowColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLArrowColorHex.Text = hex;
            entry.ArrowColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLArrowColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLArrowColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLArrowColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLArrowColorHex.Text = hex;
                entry.ArrowColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLArrowColorHex.Text = entry.ArrowColorHex ?? LayerRegistry.DefaultArrowColorHex;
            }
        }

        private void GLArrowNudge_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyGLArrowNudge();
        }

        private void GLArrowNudge_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyGLArrowNudge();
        }

        private void ApplyGLArrowNudge()
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            if (int.TryParse(TxtGLArrowNudge.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int nudge) && nudge >= -10 && nudge <= 10)
            {
                entry.ArrowNudgeY = nudge;
            }

            // Re-display the stored value (reverts invalid input) and re-apply the layout
            LoadSelectedLayerIntoFields();
        }
        private void GLGameSizeField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyGLGameFontSize();
        }

        private void GLGameSizeField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyGLGameFontSize();
        }

        private void ApplyGLGameFontSize()
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            if (int.TryParse(TxtGLGameFontSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int size) && size >= 6 && size <= 72)
            {
                entry.GameFontSize = size;
            }

            // Re-display the stored value (reverts invalid input) and re-apply the layout, which repaints the rows
            LoadSelectedLayerIntoFields();
        }
        private void GLGameColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLGameColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLGameColorHex.Text = hex;
            entry.GameColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLGameColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLGameColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLGameColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLGameColorHex.Text = hex;
                entry.GameColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLGameColorHex.Text = entry.GameColorHex ?? LayerRegistry.DefaultGameColorHex;
            }
        }

        private void GLGameSelectedColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLGameSelectedColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLGameSelectedColorHex.Text = hex;
            entry.GameSelectedColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLGameSelectedColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLGameSelectedColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLGameSelectedColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLGameSelectedColorHex.Text = hex;
                entry.GameSelectedColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLGameSelectedColorHex.Text = entry.GameSelectedColorHex ?? LayerRegistry.DefaultGameSelectedColorHex;
            }
        }

        private void GLGameSelectedBgColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLGameSelectedBgColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLGameSelectedBgColorHex.Text = hex;
            entry.GameSelectedBgColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLGameSelectedBgColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLGameSelectedBgColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLGameSelectedBgColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLGameSelectedBgColorHex.Text = hex;
                entry.GameSelectedBgColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLGameSelectedBgColorHex.Text = entry.GameSelectedBgColorHex ?? LayerRegistry.DefaultGameSelectedBgColorHex;
            }
        }
        private void GLGameHoverColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLGameHoverColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLGameHoverColorHex.Text = hex;
            entry.GameHoverColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLGameHoverColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLGameHoverColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLGameHoverColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLGameHoverColorHex.Text = hex;
                entry.GameHoverColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLGameHoverColorHex.Text = entry.GameHoverColorHex ?? LayerRegistry.DefaultGameHoverColorHex;
            }
        }
        private void CboGLGameFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (CboGLGameFont.SelectedItem is not string family) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            entry.GameFontFamily = family;
            RaiseGuides();
        }

        private void ChkGLGameBold_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            entry.GameBold = ChkGLGameBold.IsChecked == true;
            RaiseGuides();
        }
        // Drop shadow controls (folder and game title). Checkbox and color apply immediately; blur and depth
        // apply on Enter or focus loss and revert invalid input to the stored value.
        private void ChkGLFolderShadow_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            entry.FolderShadowEnabled = ChkGLFolderShadow.IsChecked == true;
            RaiseGuides();
        }

        private void ChkGLGameShadow_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            entry.GameShadowEnabled = ChkGLGameShadow.IsChecked == true;
            RaiseGuides();
        }

        private void GLFolderShadowColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLFolderShadowColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLFolderShadowColorHex.Text = hex;
            entry.FolderShadowColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLFolderShadowColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLFolderShadowColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLFolderShadowColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLFolderShadowColorHex.Text = hex;
                entry.FolderShadowColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLFolderShadowColorHex.Text = entry.FolderShadowColorHex ?? LayerRegistry.DefaultShadowColorHex;
            }
        }

        private void GLGameShadowColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLGameShadowColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLGameShadowColorHex.Text = hex;
            entry.GameShadowColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLGameShadowColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLGameShadowColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLGameShadowColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLGameShadowColorHex.Text = hex;
                entry.GameShadowColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLGameShadowColorHex.Text = entry.GameShadowColorHex ?? LayerRegistry.DefaultShadowColorHex;
            }
        }

        // Selected-row drop shadow: checkbox and color apply immediately, same as the normal shadows above.
        private void ChkGLFolderSelShadow_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            entry.FolderSelectedShadowEnabled = ChkGLFolderSelShadow.IsChecked == true;
            RaiseGuides();
        }

        private void ChkGLGameSelShadow_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            entry.GameSelectedShadowEnabled = ChkGLGameSelShadow.IsChecked == true;
            RaiseGuides();
        }

        private void GLFolderSelShadowColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLFolderSelShadowColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLFolderSelShadowColorHex.Text = hex;
            entry.FolderSelectedShadowColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLFolderSelShadowColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLFolderSelShadowColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLFolderSelShadowColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLFolderSelShadowColorHex.Text = hex;
                entry.FolderSelectedShadowColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLFolderSelShadowColorHex.Text = entry.FolderSelectedShadowColorHex ?? LayerRegistry.DefaultShadowColorHex;
            }
        }

        private void GLGameSelShadowColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGLColorSync || _loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            var c = GLGameSelShadowColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGLGameSelShadowColorHex.Text = hex;
            entry.GameSelectedShadowColorHex = hex;
            RaiseGuides();
        }

        private void TxtGLGameSelShadowColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGLGameSelShadowColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                GLGameSelShadowColorPicker.SelectedColor = c;
                _suppressGLColorSync = false;
                TxtGLGameSelShadowColorHex.Text = hex;
                entry.GameSelectedShadowColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                TxtGLGameSelShadowColorHex.Text = entry.GameSelectedShadowColorHex ?? LayerRegistry.DefaultShadowColorHex;
            }
        }

        // One shared handler pair for all eight numeric shadow fields (blur/depth for folder, game, and both selected states).
        private void GLShadowField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyGLShadowFields();
        }

        private void GLShadowField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyGLShadowFields();
        }

        private void ApplyGLShadowFields()
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.GamesList) is not LayerLayoutEntry entry) return;

            // Blur 0-30, depth 0-20. Each field is checked on its own; an invalid one keeps its stored value.
            if (int.TryParse(TxtGLFolderShadowBlur.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fBlur) && fBlur >= 0 && fBlur <= 30)
                entry.FolderShadowBlur = fBlur;
            if (int.TryParse(TxtGLFolderShadowDepth.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fDepth) && fDepth >= 0 && fDepth <= 20)
                entry.FolderShadowDepth = fDepth;
            if (int.TryParse(TxtGLGameShadowBlur.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int gBlur) && gBlur >= 0 && gBlur <= 30)
                entry.GameShadowBlur = gBlur;
            if (int.TryParse(TxtGLGameShadowDepth.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int gDepth) && gDepth >= 0 && gDepth <= 20)
                entry.GameShadowDepth = gDepth;
            if (int.TryParse(TxtGLFolderSelShadowBlur.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fsBlur) && fsBlur >= 0 && fsBlur <= 30)
                entry.FolderSelectedShadowBlur = fsBlur;
            if (int.TryParse(TxtGLFolderSelShadowDepth.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fsDepth) && fsDepth >= 0 && fsDepth <= 20)
                entry.FolderSelectedShadowDepth = fsDepth;
            if (int.TryParse(TxtGLGameSelShadowBlur.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int gsBlur) && gsBlur >= 0 && gsBlur <= 30)
                entry.GameSelectedShadowBlur = gsBlur;
            if (int.TryParse(TxtGLGameSelShadowDepth.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int gsDepth) && gsDepth >= 0 && gsDepth <= 20)
                entry.GameSelectedShadowDepth = gsDepth;

            // Re-display the stored values (reverts invalid input) and re-apply the layout
            LoadSelectedLayerIntoFields();
        }
        
        // [END SECTION: Games List Styling Fields]

        // [SECTION: Search & Buttons Styling Fields]
        // Shown while the Search & Buttons group is selected. Label values live on the SearchLabel entry, box values
        // on SearchBox, and the style shared by the four buttons on RandomButton (all per resolution). Colors,
        // checkboxes and combos apply immediately; number fields apply on Enter or focus loss and revert invalid input.
        private static readonly string[] TextAlignChoices = { "Left", "Center", "Right" };

        private bool IsSearchButtonsSelected => LstLayers.SelectedItem is LayerListItem sel && sel.Id == LayerRegistry.SearchAndButtonsGroup;

        // Fills the Search and Buttons tabs and the Border tab's hover row from the three entries
        private void LoadSearchButtonsStyleFields()
        {
            PnlSBHoverBorder.Visibility = IsSearchButtonsSelected ? Visibility.Visible : Visibility.Collapsed;
            if (!IsSearchButtonsSelected) return;
            if (GetEntry(LayerRegistry.SearchLabel) is not LayerLayoutEntry label
                || GetEntry(LayerRegistry.SearchBox) is not LayerLayoutEntry box
                || GetEntry(LayerRegistry.RandomButton) is not LayerLayoutEntry button) return;

            _loading = true;

            if (CboSBLabelFont.Items.Count == 0)
            {
                foreach (string font in LayerRegistry.AvailableFonts) CboSBLabelFont.Items.Add(font);
            }
            string family = string.IsNullOrWhiteSpace(label.TextFontFamily) ? LayerRegistry.DefaultFontFamily : label.TextFontFamily;
            CboSBLabelFont.SelectedItem = LayerRegistry.AvailableFonts.Contains(family) ? family : LayerRegistry.DefaultFontFamily;
            ChkSBLabelBold.IsChecked = label.TextBold ?? LayerRegistry.DefaultSearchLabelBold;

            if (CboSBLabelAlign.Items.Count == 0)
            {
                foreach (string align in TextAlignChoices) CboSBLabelAlign.Items.Add(align);
            }
            string alignment = label.TextAlign ?? LayerRegistry.DefaultSearchLabelAlign;
            CboSBLabelAlign.SelectedItem = TextAlignChoices.Contains(alignment) ? alignment : LayerRegistry.DefaultSearchLabelAlign;
            TxtSBLabelNudge.Text = (label.TextNudgeY ?? LayerRegistry.DefaultSearchLabelNudgeY).ToString(CultureInfo.InvariantCulture);

            TxtSBLabelFontSize.Text = (label.TextFontSize ?? LayerRegistry.DefaultSearchLabelFontSize).ToString(CultureInfo.InvariantCulture);
            ShowColor(SBLabelColorPicker, TxtSBLabelColorHex, label.TextColorHex ?? LayerRegistry.DefaultSearchLabelColorHex);
            ShowColor(SBLabelBgColorPicker, TxtSBLabelBgColorHex, label.BgColorHex ?? LayerRegistry.DefaultSearchLabelBgColorHex);
            ChkSBLabelShadow.IsChecked = label.TextShadowEnabled ?? LayerRegistry.DefaultShadowEnabled;
            ShowColor(SBLabelShadowColorPicker, TxtSBLabelShadowColorHex, label.TextShadowColorHex ?? LayerRegistry.DefaultShadowColorHex);
            TxtSBLabelShadowBlur.Text = (label.TextShadowBlur ?? LayerRegistry.DefaultShadowBlur).ToString(CultureInfo.InvariantCulture);
            TxtSBLabelShadowDepth.Text = (label.TextShadowDepth ?? LayerRegistry.DefaultShadowDepth).ToString(CultureInfo.InvariantCulture);

            TxtSBBoxFontSize.Text = (box.TextFontSize ?? LayerRegistry.DefaultSearchBoxFontSize).ToString(CultureInfo.InvariantCulture);
            ShowColor(SBBoxColorPicker, TxtSBBoxColorHex, box.TextColorHex ?? LayerRegistry.DefaultSearchBoxColorHex);
            ShowColor(SBBoxBgColorPicker, TxtSBBoxBgColorHex, box.BgColorHex ?? LayerRegistry.DefaultSearchBoxBgColorHex);

            TxtSBButtonFontSize.Text = (button.TextFontSize ?? LayerRegistry.DefaultButtonFontSize).ToString(CultureInfo.InvariantCulture);
            ShowColor(SBButtonColorPicker, TxtSBButtonColorHex, button.TextColorHex ?? LayerRegistry.DefaultButtonColorHex);
            ShowColor(SBButtonBgColorPicker, TxtSBButtonBgColorHex, button.BgColorHex ?? LayerRegistry.DefaultButtonBgColorHex);
            ShowColor(SBButtonHoverColorPicker, TxtSBButtonHoverColorHex, button.HoverColorHex ?? LayerRegistry.DefaultButtonHoverColorHex);
            ShowColor(SBButtonHoverBgColorPicker, TxtSBButtonHoverBgColorHex, button.HoverBgColorHex ?? LayerRegistry.DefaultButtonHoverBgColorHex);
            ShowColor(SBHoverBorderColorPicker, TxtSBHoverBorderColorHex, button.HoverBorderColorHex ?? LayerRegistry.DefaultButtonHoverBorderColorHex);

            _loading = false;
        }

        // Shows a stored color in a picker and its hex box without firing the picker's change handler
        private void ShowColor(ColorPicker.PortableColorPicker picker, TextBox hexBox, string hex)
        {
            hexBox.Text = hex;
            try
            {
                _suppressGLColorSync = true;
                picker.SelectedColor = (Color)ColorConverter.ConvertFromString(hex);
            }
            catch { }
            finally { _suppressGLColorSync = false; }
        }

        // Stores a picker's color on an entry (through store) and mirrors it in the hex box
        private void ApplyPickerColor(ColorPicker.PortableColorPicker picker, TextBox hexBox, LayerLayoutEntry? entry, Action<LayerLayoutEntry, string> store)
        {
            if (_suppressGLColorSync || _loading || _settings == null || entry == null) return;

            var c = picker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            hexBox.Text = hex;
            store(entry, hex);
            RaiseGuides();
        }

        // Stores a typed hex color on an entry and syncs the picker; invalid input reverts to the stored value
        private void ApplyHexColor(ColorPicker.PortableColorPicker picker, TextBox hexBox, LayerLayoutEntry? entry, Func<LayerLayoutEntry, string> stored, Action<LayerLayoutEntry, string> store)
        {
            if (_loading || _settings == null || entry == null) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hexBox.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGLColorSync = true;
                picker.SelectedColor = c;
                _suppressGLColorSync = false;
                hexBox.Text = hex;
                store(entry, hex);
                RaiseGuides();
            }
            catch
            {
                _suppressGLColorSync = false;
                hexBox.Text = stored(entry);
            }
        }

        private LayerLayoutEntry? SBLabelEntry => IsSearchButtonsSelected ? GetEntry(LayerRegistry.SearchLabel) : null;
        private LayerLayoutEntry? SBBoxEntry => IsSearchButtonsSelected ? GetEntry(LayerRegistry.SearchBox) : null;
        private LayerLayoutEntry? SBButtonEntry => IsSearchButtonsSelected ? GetEntry(LayerRegistry.RandomButton) : null;

        // Label font, bold, alignment and shadow toggle
        private void CboSBLabelFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (CboSBLabelFont.SelectedItem is not string family || SBLabelEntry is not LayerLayoutEntry entry) return;

            entry.TextFontFamily = family;
            RaiseGuides();
        }

        private void ChkSBLabelBold_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null || SBLabelEntry is not LayerLayoutEntry entry) return;

            entry.TextBold = ChkSBLabelBold.IsChecked == true;
            RaiseGuides();
        }

        private void CboSBLabelAlign_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (CboSBLabelAlign.SelectedItem is not string align || SBLabelEntry is not LayerLayoutEntry entry) return;

            entry.TextAlign = align;
            RaiseGuides();
        }

        private void ChkSBLabelShadow_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null || SBLabelEntry is not LayerLayoutEntry entry) return;

            entry.TextShadowEnabled = ChkSBLabelShadow.IsChecked == true;
            RaiseGuides();
        }

        // Color pickers and hex boxes (label, box, buttons, hover border)
        private void SBLabelColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(SBLabelColorPicker, TxtSBLabelColorHex, SBLabelEntry, (en, hex) => en.TextColorHex = hex);
        private void TxtSBLabelColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(SBLabelColorPicker, TxtSBLabelColorHex, SBLabelEntry, en => en.TextColorHex ?? LayerRegistry.DefaultSearchLabelColorHex, (en, hex) => en.TextColorHex = hex);

        private void SBLabelBgColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(SBLabelBgColorPicker, TxtSBLabelBgColorHex, SBLabelEntry, (en, hex) => en.BgColorHex = hex);
        private void TxtSBLabelBgColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(SBLabelBgColorPicker, TxtSBLabelBgColorHex, SBLabelEntry, en => en.BgColorHex ?? LayerRegistry.DefaultSearchLabelBgColorHex, (en, hex) => en.BgColorHex = hex);

        private void SBLabelShadowColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(SBLabelShadowColorPicker, TxtSBLabelShadowColorHex, SBLabelEntry, (en, hex) => en.TextShadowColorHex = hex);
        private void TxtSBLabelShadowColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(SBLabelShadowColorPicker, TxtSBLabelShadowColorHex, SBLabelEntry, en => en.TextShadowColorHex ?? LayerRegistry.DefaultShadowColorHex, (en, hex) => en.TextShadowColorHex = hex);

        private void SBBoxColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(SBBoxColorPicker, TxtSBBoxColorHex, SBBoxEntry, (en, hex) => en.TextColorHex = hex);
        private void TxtSBBoxColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(SBBoxColorPicker, TxtSBBoxColorHex, SBBoxEntry, en => en.TextColorHex ?? LayerRegistry.DefaultSearchBoxColorHex, (en, hex) => en.TextColorHex = hex);

        private void SBBoxBgColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(SBBoxBgColorPicker, TxtSBBoxBgColorHex, SBBoxEntry, (en, hex) => en.BgColorHex = hex);
        private void TxtSBBoxBgColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(SBBoxBgColorPicker, TxtSBBoxBgColorHex, SBBoxEntry, en => en.BgColorHex ?? LayerRegistry.DefaultSearchBoxBgColorHex, (en, hex) => en.BgColorHex = hex);

        private void SBButtonColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(SBButtonColorPicker, TxtSBButtonColorHex, SBButtonEntry, (en, hex) => en.TextColorHex = hex);
        private void TxtSBButtonColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(SBButtonColorPicker, TxtSBButtonColorHex, SBButtonEntry, en => en.TextColorHex ?? LayerRegistry.DefaultButtonColorHex, (en, hex) => en.TextColorHex = hex);

        private void SBButtonBgColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(SBButtonBgColorPicker, TxtSBButtonBgColorHex, SBButtonEntry, (en, hex) => en.BgColorHex = hex);
        private void TxtSBButtonBgColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(SBButtonBgColorPicker, TxtSBButtonBgColorHex, SBButtonEntry, en => en.BgColorHex ?? LayerRegistry.DefaultButtonBgColorHex, (en, hex) => en.BgColorHex = hex);

        private void SBButtonHoverColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(SBButtonHoverColorPicker, TxtSBButtonHoverColorHex, SBButtonEntry, (en, hex) => en.HoverColorHex = hex);
        private void TxtSBButtonHoverColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(SBButtonHoverColorPicker, TxtSBButtonHoverColorHex, SBButtonEntry, en => en.HoverColorHex ?? LayerRegistry.DefaultButtonHoverColorHex, (en, hex) => en.HoverColorHex = hex);

        private void SBButtonHoverBgColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(SBButtonHoverBgColorPicker, TxtSBButtonHoverBgColorHex, SBButtonEntry, (en, hex) => en.HoverBgColorHex = hex);
        private void TxtSBButtonHoverBgColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(SBButtonHoverBgColorPicker, TxtSBButtonHoverBgColorHex, SBButtonEntry, en => en.HoverBgColorHex ?? LayerRegistry.DefaultButtonHoverBgColorHex, (en, hex) => en.HoverBgColorHex = hex);

        private void SBHoverBorderColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(SBHoverBorderColorPicker, TxtSBHoverBorderColorHex, SBButtonEntry, (en, hex) => en.HoverBorderColorHex = hex);
        private void TxtSBHoverBorderColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(SBHoverBorderColorPicker, TxtSBHoverBorderColorHex, SBButtonEntry, en => en.HoverBorderColorHex ?? LayerRegistry.DefaultButtonHoverBorderColorHex, (en, hex) => en.HoverBorderColorHex = hex);

        // One shared handler pair for the number fields: the three font sizes (6-72), the label's vertical nudge (-10 to 10) and the label shadow's blur (0-30) and depth (0-20)
        private void SBField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplySearchButtonsFields();
        }

        private void SBField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplySearchButtonsFields();
        }

        // Stores each valid number field on its entry; an invalid one keeps its stored value
        private void ApplySearchButtonsFields()
        {
            if (_loading || _settings == null) return;
            if (SBLabelEntry is not LayerLayoutEntry label || SBBoxEntry is not LayerLayoutEntry box || SBButtonEntry is not LayerLayoutEntry button) return;

            if (int.TryParse(TxtSBLabelFontSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int labelSize) && labelSize >= 6 && labelSize <= 72)
                label.TextFontSize = labelSize;
            // Vertical nudge: any whole number is accepted and clamped to -10..10; anything else reverts
            if (int.TryParse(TxtSBLabelNudge.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int nudge))
                label.TextNudgeY = Math.Clamp(nudge, LayerRegistry.MinTextNudgeY, LayerRegistry.MaxTextNudgeY);
            if (int.TryParse(TxtSBLabelShadowBlur.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int blur) && blur >= 0 && blur <= 30)
                label.TextShadowBlur = blur;
            if (int.TryParse(TxtSBLabelShadowDepth.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int depth) && depth >= 0 && depth <= 20)
                label.TextShadowDepth = depth;
            if (int.TryParse(TxtSBBoxFontSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int boxSize) && boxSize >= 6 && boxSize <= 72)
                box.TextFontSize = boxSize;
            if (int.TryParse(TxtSBButtonFontSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int buttonSize) && buttonSize >= 6 && buttonSize <= 72)
                button.TextFontSize = buttonSize;

            // Re-display the stored values (reverts invalid input) and re-apply the layout
            LoadSelectedLayerIntoFields();
        }
        // [END SECTION: Search & Buttons Styling Fields]
        // [END SECTION: Layer Fields]

        // [SECTION: Alignment Guide Preferences]
        private void ChkShowGuides_Click(object sender, RoutedEventArgs e)
        {
            _settings.ShowGuideLines = ChkShowGuides.IsChecked == true;
            RaiseGuides();
        }

        // Picker -> hex box and settings
        private void GuideColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGuideSync || _settings == null) return;

            var c = GuideColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            TxtGuideColorHex.Text = hex;
            _settings.GuideLineColorHex = hex;
            RaiseGuides();
        }

        // Hex box -> picker and settings (invalid text is reverted)
        private void TxtGuideColorHex_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_suppressGuideSync || _settings == null) return;

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(TxtGuideColorHex.Text.Trim());
                string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                _suppressGuideSync = true;
                GuideColorPicker.SelectedColor = c;
                _suppressGuideSync = false;
                TxtGuideColorHex.Text = hex;
                _settings.GuideLineColorHex = hex;
                RaiseGuides();
            }
            catch
            {
                _suppressGuideSync = false;
                TxtGuideColorHex.Text = _settings.GuideLineColorHex;
            }
        }
        // [END SECTION: Alignment Guide Preferences]

        // [SECTION: Resolution & Reset]
        // Switching resolution: a size with no saved layout starts as a copy of the layout that was just
        // being edited (background resized to fit); a size that already has one is loaded as-is.
        private void CboResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;

            // "Custom..." picked: reveal the entry row and put the combo back on the current size
            if (CboResolution.SelectedItem is string)
            {
                PnlCustomRes.Visibility = Visibility.Visible;
                TxtCustomWidth.Text = _currentLayout.Width.ToString();
                TxtCustomHeight.Text = _currentLayout.Height.ToString();

                _loading = true;
                CboResolution.SelectedItem = new ResolutionPreset(_currentLayout.Width, _currentLayout.Height);
                _loading = false;

                TxtCustomWidth.Focus();
                TxtCustomWidth.SelectAll();
                return;
            }

            if (CboResolution.SelectedItem is not ResolutionPreset preset) return;
            if (preset.Width == _currentLayout.Width && preset.Height == _currentLayout.Height) return;

            string? selectedId = (LstLayers.SelectedItem as LayerListItem)?.Id;

            var saved = _settings.Layouts.FirstOrDefault(l => l.Width == preset.Width && l.Height == preset.Height);
            if (saved == null)
            {
                saved = new ThemeLayout
                {
                    Width = preset.Width,
                    Height = preset.Height,
                    Layers = _currentLayout.Layers.Where(l => !LayerRegistry.IsCustomId(l.Id)).Select(l =>
                    {
                        var copy = new LayerLayoutEntry
                        {
                            Id = l.Id,
                            X = l.X,
                            Y = l.Y,
                            Width = l.Width,
                            Height = l.Height,
                            Z = l.Z,
                            Visible = l.Visible,
                            ImagePath = l.ImagePath
                        };
                        copy.CopyStyleFrom(l);
                        return copy;
                    }).ToList()
                };

                foreach (var entry in saved.Layers.Where(l => IsBackground(l.Id)))
                {
                    entry.X = 0; entry.Y = 0; entry.Width = preset.Width; entry.Height = preset.Height;
                }
            }

            _settings.ActiveResolutionWidth = preset.Width;
            _settings.ActiveResolutionHeight = preset.Height;
            _currentLayout = ResolveAndStoreFrom(saved, preset.Width, preset.Height);
            UpdateCustomResUi(preset.Width, preset.Height);

            // Leaving a pending custom size discards it, so it also leaves the combo box
            if (DropPendingSize(preset.Width, preset.Height))
            {
                _loading = true;
                RebuildResolutionCombo(preset.Width, preset.Height);
                _loading = false;
            }

            RebuildLayerList(selectedId);
        }

        // [SECTION: Custom Resolutions]
        // A custom size that was applied but not yet kept. It becomes permanent when Save Adjustments (or a
        // theme save) calls CommitPendingSize; switching to any other size before that discards it.
        private ResolutionPreset? _pendingSize;

        // Keeps the pending size: it joins the combo box from now on.
        public void CommitPendingSize()
        {
            if (_pendingSize == null || _currentLayout == null) return;
            _pendingSize = null;

            _loading = true;
            RebuildResolutionCombo(_currentLayout.Width, _currentLayout.Height);
            _loading = false;
        }

        // Discards the pending size (and its layout) unless it is the size being switched to.
        // Returns true if something was dropped, so the caller knows the combo needs rebuilding.
        private bool DropPendingSize(int keepWidth, int keepHeight)
        {
            if (_pendingSize == null) return false;
            if (_pendingSize.Width == keepWidth && _pendingSize.Height == keepHeight) return false;

            _settings.Layouts.RemoveAll(l => l.Width == _pendingSize.Width && l.Height == _pendingSize.Height);
            _pendingSize = null;
            return true;
        }
        private static bool IsPresetSize(int width, int height) =>
            LayerRegistry.ResolutionPresets.Any(p => p.Width == width && p.Height == height);

        // Fills the combo with the presets, every other size that has a saved layout (the custom ones), the
        // active size if it is somehow in neither, and a final "Custom..." entry. Returns the selected size.
        // Callers must have _loading set, since selecting an item raises SelectionChanged.
        private ResolutionPreset RebuildResolutionCombo(int activeWidth, int activeHeight)
        {
            var sizes = new List<ResolutionPreset>(LayerRegistry.ResolutionPresets);

            foreach (var l in _settings.Layouts.OrderBy(l => l.Width).ThenBy(l => l.Height))
            {
                var size = new ResolutionPreset(l.Width, l.Height);
                if (size == _pendingSize) continue; // a pending size stays out of the list until it is kept
                if (!sizes.Contains(size)) sizes.Add(size);
            }

            var active = new ResolutionPreset(activeWidth, activeHeight);
            bool activeIsPending = _pendingSize != null && active == _pendingSize;
            if (!activeIsPending && !sizes.Contains(active)) sizes.Add(active);

            CboResolution.Items.Clear();
            foreach (var s in sizes) CboResolution.Items.Add(s);
            CboResolution.Items.Add("Custom...");
            CboResolution.SelectedItem = activeIsPending ? "Custom..." : active;

            UpdateCustomResUi(activeWidth, activeHeight);
            return active;
        }

        // Hides the entry row, and shows Delete Size only while a custom (non-preset) size is active.
        private void UpdateCustomResUi(int activeWidth, int activeHeight)
        {
            // While the active size is pending, the entry row stays open so it can be adjusted further
            bool pendingActive = _pendingSize != null && _pendingSize.Width == activeWidth && _pendingSize.Height == activeHeight;
            PnlCustomRes.Visibility = pendingActive ? Visibility.Visible : Visibility.Collapsed;
            if (pendingActive)
            {
                TxtCustomWidth.Text = activeWidth.ToString();
                TxtCustomHeight.Text = activeHeight.ToString();
            }
            BtnDeleteCustomRes.Visibility = (IsPresetSize(activeWidth, activeHeight) || pendingActive) ? Visibility.Collapsed : Visibility.Visible;
        }

        // Switches to a size: loads its saved layout, or starts a new one as a copy of the layout being
        // edited (background resized to fit) - the same inherit rule the preset switch uses.
        private void SwitchToResolution(int width, int height)
        {
            string? selectedId = (LstLayers.SelectedItem as LayerListItem)?.Id;

            DropPendingSize(width, height);

            var saved = _settings.Layouts.FirstOrDefault(l => l.Width == width && l.Height == height);
            bool isNewSize = saved == null;
            if (saved == null)
            {
                saved = ConfigurationSettings.CloneLayouts(new[] { _currentLayout }).First();
                saved.Width = width;
                saved.Height = height;
                saved.Layers.RemoveAll(l => LayerRegistry.IsCustomId(l.Id));
                foreach (var entry in saved.Layers.Where(l => IsBackground(l.Id)))
                {
                    entry.X = 0; entry.Y = 0; entry.Width = width; entry.Height = height;
                }
            }

            _settings.ActiveResolutionWidth = width;
            _settings.ActiveResolutionHeight = height;
            _currentLayout = ResolveAndStoreFrom(saved, width, height);
            if (isNewSize && !IsPresetSize(width, height)) _pendingSize = new ResolutionPreset(width, height);

            bool onPending = _pendingSize != null && _pendingSize.Width == _currentLayout.Width && _pendingSize.Height == _currentLayout.Height;
            _loading = true;
            if (onPending) CboResolution.SelectedItem = "Custom...";
            else CboResolution.SelectedItem = new ResolutionPreset(_currentLayout.Width, _currentLayout.Height);
            _loading = false;

            RebuildLayerList(selectedId);
        }

        // Add: the only check is whole numbers greater than zero - anything else is up to the user.
        private void BtnAddCustomRes_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtCustomWidth.Text.Trim(), out int w) || !int.TryParse(TxtCustomHeight.Text.Trim(), out int h) || w <= 0 || h <= 0)
            {
                MessageBox.Show("Enter whole numbers greater than zero for the width and height.", "Custom Resolution", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (w == _currentLayout.Width && h == _currentLayout.Height)
            {
                bool stillPending = _pendingSize != null && _pendingSize.Width == w && _pendingSize.Height == h;
                if (!stillPending) PnlCustomRes.Visibility = Visibility.Collapsed;
                return;
            }

            SwitchToResolution(w, h);
        }

        // Keep: commits the pending size so it joins the combo box (in memory only - Save Adjustments and
        // Save Changes still write settings.json and the theme zip).
        private void BtnKeepCustomRes_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingSize == null)
            {
                PnlCustomRes.Visibility = Visibility.Collapsed;
                return;
            }

            CommitPendingSize();
        }

        private void CustomRes_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            BtnAddCustomRes_Click(sender, e);
        }

        // Delete Size: removes the active custom size's layout, then switches to the default preset.
        private void BtnDeleteCustomRes_Click(object sender, RoutedEventArgs e)
        {
            int w = _currentLayout.Width;
            int h = _currentLayout.Height;
            if (IsPresetSize(w, h)) return;

            var result = MessageBox.Show(
                $"Delete the custom {w} x {h} layout?\n\nIts layer positions are discarded, and the default {LayerRegistry.DefaultWidth} x {LayerRegistry.DefaultHeight} layout is shown instead.",
                "Confirm Delete Size", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            _settings.Layouts.RemoveAll(l => l.Width == w && l.Height == h);
            _pendingSize = null;

            // Back to the theme's saved size when known (and it isn't the one just deleted), else the default
            int backW = _settings.SavedThemeWidth;
            int backH = _settings.SavedThemeHeight;
            if (backW <= 0 || backH <= 0 || (backW == w && backH == h))
            {
                backW = LayerRegistry.DefaultWidth;
                backH = LayerRegistry.DefaultHeight;
            }
            SwitchToResolution(backW, backH);

            // Rebuild the list so the deleted size actually leaves the combo box
            _loading = true;
            RebuildResolutionCombo(backW, backH);
            _loading = false;
        }
        // [END SECTION: Custom Resolutions]

        // [SECTION: Theme Sizes Row]
        // One button per size saved in the theme file (SavedThemeLayouts), with the active size highlighted.
        // Pending or unsaved sizes never appear here until Save Changes writes them to the zip.
        public void RefreshSavedSizes() => RebuildSavedSizes();

        private void RebuildSavedSizes()
        {
            if (_settings == null || _currentLayout == null) return;

            PnlSavedSizes.Children.Clear();

            foreach (var layout in _settings.SavedThemeLayouts.OrderBy(l => l.Width).ThenBy(l => l.Height))
            {
                bool active = layout.Width == _currentLayout.Width && layout.Height == _currentLayout.Height;

                var button = new Button
                {
                    Content = $"{layout.Width} x {layout.Height}",
                    Tag = new ResolutionPreset(layout.Width, layout.Height),
                    Style = (Style)FindResource("ThemedActionButtonStyle"),
                    Padding = new Thickness(7, 3, 7, 3),
                    Margin = new Thickness(0, 0, 6, 4),
                    FontWeight = active ? FontWeights.Bold : FontWeights.Normal
                };
                button.SetBinding(Button.BackgroundProperty, new System.Windows.Data.Binding(active ? "BtnBgColorHoverHex" : "BtnBgColorHex"));
                button.SetBinding(Button.ForegroundProperty, new System.Windows.Data.Binding("BtnTextColorNormalHex"));
                button.SetBinding(Button.BorderBrushProperty, new System.Windows.Data.Binding("BtnBorderColorHex"));
                button.SetBinding(Button.BorderThicknessProperty, new System.Windows.Data.Binding("OptionsBtnBorderSize"));
                button.Click += BtnSavedSize_Click;

                PnlSavedSizes.Children.Add(button);
            }
        }

        // Same path as picking the size from the combo box: window resizes, layers move, and any pending
        // edits at the previous size stay in memory.
        private void BtnSavedSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not ResolutionPreset size) return;
            if (size.Width == _currentLayout.Width && size.Height == _currentLayout.Height) return;

            SwitchToResolution(size.Width, size.Height);

            _loading = true;
            RebuildResolutionCombo(size.Width, size.Height);
            _loading = false;
        }
        // [END SECTION: Theme Sizes Row]

        private ThemeLayout ResolveAndStoreFrom(ThemeLayout saved, int width, int height)
        {
            var resolved = LayerRegistry.ResolveLayout(saved, width, height);
            StoreLayout(resolved);
            return resolved;
        }

        // Restores one layer to registry defaults for the active resolution, then renumbers Z so the
        // stacking order stays unique.
        private void BtnResetLayer_Click(object sender, RoutedEventArgs e)
        {
            // A group resets every one of its parts
            if (LstLayers.SelectedItem is LayerListItem groupItem && GetGroup(groupItem.Id) is LayerGroupDefinition resetGroup)
            {
                var groupRestore = GetRestoreLayout(_currentLayout.Width, _currentLayout.Height);
                foreach (string partId in resetGroup.PartIds)
                {
                    var partEntry = GetEntry(partId);
                    var partDefault = groupRestore.Layers.FirstOrDefault(l => l.Id == partId);
                    if (partEntry == null || partDefault == null) continue;

                    partEntry.X = partDefault.X;
                    partEntry.Y = partDefault.Y;
                    partEntry.Width = partDefault.Width;
                    partEntry.Height = partDefault.Height;
                    partEntry.Visible = partDefault.Visible;

                    // Style values too (the Search & Buttons label, box and shared button style live on its parts)
                    partEntry.CopyStyleFrom(partDefault);

                    // The Random Game Display group's shared file lives on its first part
                    if (partId == LayerRegistry.RandomGameDisplay) partEntry.ImagePath = partDefault.ImagePath;
                }

                NormalizeZ(GetAscendingOrder());
                RebuildLayerList(groupItem.Id);
                return;
            }

            if (LstLayers.SelectedItem is not LayerListItem selected || GetEntry(selected.Id) is not LayerLayoutEntry entry) return;

            var restore = GetRestoreLayout(_currentLayout.Width, _currentLayout.Height);
            var defEntry = restore.Layers.FirstOrDefault(l => l.Id == entry.Id);
            if (defEntry == null) return;

            entry.X = defEntry.X;
            entry.Y = defEntry.Y;
            entry.Width = defEntry.Width;
            entry.Height = defEntry.Height;
            entry.Visible = defEntry.Visible;
            if (!LayerRegistry.IsCustomId(entry.Id)) entry.ImagePath = defEntry.ImagePath;
            entry.CopyStyleFrom(defEntry);

            NormalizeZ(GetAscendingOrder());
            RebuildLayerList(selected.Id);
        }

        // The restore point for the resets: the active theme's saved layout for this resolution (as of the
        // last theme load or Save Changes), with registry defaults filling anything the theme never saved.
        private ThemeLayout GetRestoreLayout(int width, int height)
        {
            var saved = _settings.SavedThemeLayouts.FirstOrDefault(l => l.Width == width && l.Height == height);
            var copy = saved != null ? ConfigurationSettings.CloneLayouts(new[] { saved }).First() : null;
            return LayerRegistry.ResolveLayout(copy, width, height);
        }

        // Reset Layout: every layer at this resolution back to the saved theme values (including order).
        private void BtnResetLayout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"Reset every layer at {_currentLayout.Width} x {_currentLayout.Height} to the saved theme values?\n\nThis discards your unsaved position, size, order, visibility and spacing changes for this resolution.",
                "Confirm Reset Layout", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            string? selectedId = (LstLayers.SelectedItem as LayerListItem)?.Id;

            _currentLayout = GetRestoreLayout(_currentLayout.Width, _currentLayout.Height);
            StoreLayout(_currentLayout);
            NormalizeZ(GetAscendingOrder());

            RebuildLayerList(selectedId);
        }

        // Restore Defaults: every layer at this resolution back to the built-in registry defaults.
        private void BtnRestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"Restore every layer at {_currentLayout.Width} x {_currentLayout.Height} to the built-in default layout?\n\nThis ignores the saved theme and discards your position, size, order, visibility and spacing changes for this resolution.",
                "Confirm Restore Defaults", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            string? selectedId = (LstLayers.SelectedItem as LayerListItem)?.Id;

            _currentLayout = LayerRegistry.BuildDefaultLayout(_currentLayout.Width, _currentLayout.Height);
            StoreLayout(_currentLayout);
            NormalizeZ(GetAscendingOrder());

            RebuildLayerList(selectedId);
        }
        // [END SECTION: Resolution & Reset]
    }
}