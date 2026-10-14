using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ArcadeStick.Models;

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
        // Add Image Layer: asks for a file, then creates the layer at the CURRENT resolution only, sized to
        // the image's native pixels (fill-crop means a matching size is never scaled or cropped). At most
        // LayerRegistry.MaxCustomLayersPerResolution per resolution.
        private void BtnAddImageLayer_Click(object sender, RoutedEventArgs e)
        {
            int existingCount = _currentLayout.Layers.Count(l => LayerRegistry.IsCustomId(l.Id));
            if (existingCount >= LayerRegistry.MaxCustomLayersPerResolution)
            {
                MessageBox.Show($"This resolution already has the maximum of {LayerRegistry.MaxCustomLayersPerResolution} custom layers. Remove one to add another.",
                    "Add Image Layer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image and Video Files (*.png;*.jpg;*.jpeg;*.gif;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|Video Files (*.mp4)|*.mp4|All files (*.*)|*.*",
                Title = "Select Image for New Layer",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() != true) return;

            string selectedPath = dialog.FileName;

            double imageWidth;
            double imageHeight;
            try
            {
                if (System.IO.Path.GetExtension(selectedPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    if (!TryGetVideoSize(selectedPath, out imageWidth, out imageHeight))
                    {
                        MessageBox.Show("Could not read that video. It may use a codec this PC can't play (H.264, 8-bit yuv420p works on a stock Windows install).",
                            "Add Image Layer", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else
                {
                    var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                        new Uri(selectedPath, UriKind.Absolute),
                        System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation | System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                        System.Windows.Media.Imaging.BitmapCacheOption.None);
                    imageWidth = decoder.Frames[0].PixelWidth;
                    imageHeight = decoder.Frames[0].PixelHeight;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not read that image: {ex.Message}", "Add Image Layer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Same convention as the other theme assets: relative to the app folder when inside it, else absolute
            string storedPath = selectedPath;
            try
            {
                string relative = System.IO.Path.GetRelativePath(_settings.BaseDirectory, selectedPath);
                if (!relative.StartsWith("..") && !System.IO.Path.IsPathRooted(relative)) storedPath = relative;
            }
            catch { }

            int topZ = _currentLayout.Layers.Max(l => l.Z);
            var entry = new LayerLayoutEntry
            {
                Id = LayerRegistry.CustomIdPrefix + Guid.NewGuid().ToString("N"),
                Name = System.IO.Path.GetFileNameWithoutExtension(selectedPath),
                ImagePath = storedPath,
                X = 0,
                Y = 0,
                Width = imageWidth,
                Height = imageHeight,
                Z = topZ + 1,
                Visible = true
            };

            _currentLayout.Layers.Add(entry);
            NormalizeZ(GetAscendingOrder());
            RebuildLayerList(entry.Id);
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
        private static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private void LoadSelectedLayerIntoFields()
        {
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
            ChkLayerHidden.Visibility = positionable ? Visibility.Visible : Visibility.Collapsed;
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
            BtnRemoveLayer.Visibility = Visibility.Collapsed;
            BtnResetLayer.Visibility = Visibility.Visible;
            PnlGroupParts.Visibility = Visibility.Visible;

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
        private void ApplyPartField(TextBox? box)
        {
            if (_loading || box == null || box.Tag is not string partId) return;
            if (GetEntry(partId) is not LayerLayoutEntry entry || !_partControls.TryGetValue(partId, out var controls)) return;

            if (double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                if (box == controls.X) entry.X = value;
                else if (box == controls.Y) entry.Y = value;
                else if (box == controls.Width && value > 0) entry.Width = value;
                else if (box == controls.Height && value > 0) entry.Height = value;
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
                if (box == TxtLayerX) entry.X = value;
                else if (box == TxtLayerY) entry.Y = value;
                else if (box == TxtLayerWidth && value > 0) entry.Width = value;
                else if (box == TxtLayerHeight && value > 0) entry.Height = value;
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
                    Layers = _currentLayout.Layers.Where(l => !LayerRegistry.IsCustomId(l.Id)).Select(l => new LayerLayoutEntry
                    {
                        Id = l.Id,
                        X = l.X,
                        Y = l.Y,
                        Width = l.Width,
                        Height = l.Height,
                        Z = l.Z,
                        Visible = l.Visible,
                        ImagePath = l.ImagePath
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