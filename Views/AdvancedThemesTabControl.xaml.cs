using ArcadeStick.Models;
using ArcadeStick.Services;
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

        // Per-part ratio lock states (groups with AllowRatioLock), by part id. Session-only like BtnRatioLock, and kept
        // here rather than on the controls so they survive the group page being rebuilt for another group.
        private readonly Dictionary<string, bool> _partRatioLocks = new();

        private class PartControls
        {
            public TextBox X = null!;
            public TextBox Y = null!;
            public TextBox Width = null!;
            public TextBox Height = null!;
            public CheckBox Hide = null!;
            public System.Windows.Controls.Primitives.ToggleButton? RatioLock;
        }

        // Rect and color of the alignment guides for the selected layer, and which parts to draw: the layer
        // edges (ShowRect) and a cross at the rect's center (ShowCenter). Null = no guides.
        public record GuideInfo(double X, double Y, double Width, double Height, string ColorHex, bool ShowRect, bool ShowCenter);

        // Raised whenever the guides should be redrawn or cleared. ThemeBuilderWindow forwards it to MainWindow.
        public event Action<GuideInfo?>? GuidesChanged;

        // Raised when the Random Game Display card preview turns on or off (and with which state). ThemeBuilderWindow
        // forwards it to MainWindow.SetRandomCardPreview. Session-only, never stored.
        public event Action<bool, bool>? RandomPreviewChanged;

        // Raised when the Marquee banner preview turns on or off. ThemeBuilderWindow forwards it to
        // MainWindow.SetBannerPreview. Session-only, never stored.
        public event Action<bool>? BannerPreviewChanged;

        // Raised when the background grid turns on or off or its spacing or offsets change (on, spacing, offset X,
        // offset Y in px). ThemeBuilderWindow forwards it to MainWindow.SetBackgroundGrid. Session-only: the checkbox,
        // _gridSpacing and the offsets are never stored.
        public event Action<bool, double, double, double>? GridChanged;
        private double _gridSpacing = 20;
        private double _gridOffsetX;
        private double _gridOffsetY;

        // Raised after any committed layout edit so the main window can re-apply the layout live.
        // Nothing is written to disk here - Save Adjustments / Save Changes still do that.
        public event Action? LayoutChanged;

        // Recomputes the guides from the selected layer's pending rect and the show/center/color preferences.
        // Null only when there is nothing to draw: no usable selection, or both toggles off.
        public void RaiseGuides()
        {
            if (_settings == null) return;

            // Every committed edit, selection change, reorder, resolution switch and reset ends up here,
            // so this one call lets the main window re-apply the layout live.
            LayoutChanged?.Invoke();

            bool showRect = _settings.ShowGuideLines;
            bool showCenter = _settings.ShowCenterGuide;

            // Multi mode: one guide rect around the bounding box of every moving entry
            if (IsMultiSelect)
            {
                var moving = GetMultiMoveEntries();
                if ((!showRect && !showCenter) || moving.Count == 0)
                {
                    GuidesChanged?.Invoke(null);
                    return;
                }

                var rects = moving.Select(GetGuideRect).ToList();
                double minX = rects.Min(r => r.X);
                double minY = rects.Min(r => r.Y);
                double maxRight = rects.Max(r => r.Right);
                double maxBottom = rects.Max(r => r.Bottom);
                GuidesChanged?.Invoke(new GuideInfo(minX, minY, maxRight - minX, maxBottom - minY, _settings.GuideLineColorHex, showRect, showCenter));
                return;
            }

            if ((!showRect && !showCenter)
    || SingleSelectedLayer is not LayerListItem selected
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

            var guideRect = GetGuideRect(entry);
            GuidesChanged?.Invoke(new GuideInfo(guideRect.X, guideRect.Y, guideRect.Width, guideRect.Height, _settings.GuideLineColorHex, showRect, showCenter));
        }

        // Returns the rect the guides use for an entry: its own rect, or for the layers with a border ring
        // (LayerRegistry.HasOutsideBorder) the ring's outer box, from (X, Y) sized (W + 2B, H + 2B), so the guides frame the outer ring.
        private static Rect GetGuideRect(LayerLayoutEntry entry)
        {
            double border = LayerRegistry.HasOutsideBorder(entry.Id) ? Math.Max(0, entry.BorderSize ?? 0) : 0;
            return new Rect(entry.X, entry.Y, entry.Width + (border * 2), entry.Height + (border * 2));
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
        // _settings.Layouts in memory - Save Adjustments persists to settings.json, and Save Changes in
        // the Themes Manager row above this tab writes the layouts into the theme zip.
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
            StopPreviews();

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
            ChkCenterGuides.IsChecked = _settings.ShowCenterGuide;
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
                    string name = group != null ? group.DisplayName : GetLayerDisplayName(key);
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

        // [SECTION: Multi-Select]
        // Ctrl/Shift-click selects several rows ("multi mode"): the pane shows only the Move X / Move Y nudge boxes,
        // which shift every selected row's entries together. Single-selection code reads SingleSelectedLayer,
        // which is null in multi mode, so it stays inactive there. Nothing about the selection is saved.
        private const string MultiSelectHint = "Several layers selected. Type an amount to shift every selected layer together: positive X moves right, positive Y moves down. The boxes return to 0 after each move.";
        private const string MultiSelectNothingMovableHint = "None of the selected layers can be moved (the Main Window Background is not positionable).";

        // Does the multi-mode check: true while two or more rows are selected
        private bool IsMultiSelect => LstLayers.SelectedItems.Count > 1;

        // Does the single-selection lookup: the selected row, or null when none or several are selected
        private LayerListItem? SingleSelectedLayer => IsMultiSelect ? null : LstLayers.SelectedItem as LayerListItem;

        // Does the multi-mode entry list: every entry of every selected row (all parts of a group), without
        // the Main Window Background. Hidden layers are included.
        private List<LayerLayoutEntry> GetMultiMoveEntries() =>
            LstLayers.SelectedItems.OfType<LayerListItem>()
                .SelectMany(row => GetUnitEntries(row.Id))
                .Where(entry => !IsBackground(entry.Id))
                .Distinct()
                .ToList();

        // Does the multi-mode page: heading, nudge boxes reset to 0, single-layer tabs and buttons hidden
        private void LoadMultiSelection()
        {
            // Returning to one row then re-selects that layer's first tab
            _tabsUnitKey = null;
            StopPreviews();
            PnlRandomPreview.Visibility = Visibility.Collapsed;

            PnlLayerFields.Visibility = Visibility.Visible;
            TxtLayerName.Text = $"{LstLayers.SelectedItems.Count} layers selected";
            TabsLayer.Visibility = Visibility.Collapsed;
            PnlMultiSelect.Visibility = Visibility.Visible;

            BtnRenameLayer.Visibility = Visibility.Collapsed;
            BtnResetLayer.Visibility = Visibility.Collapsed;
            BtnRemoveLayer.Visibility = Visibility.Collapsed;
            BtnLayerUp.IsEnabled = false;
            BtnLayerDown.IsEnabled = false;

            bool canMove = GetMultiMoveEntries().Count > 0;
            _loading = true;
            TxtMultiX.Text = "0";
            TxtMultiY.Text = "0";
            _loading = false;
            TxtMultiX.IsEnabled = canMove;
            TxtMultiY.IsEnabled = canMove;
            TxtMultiHint.Text = canMove ? MultiSelectHint : MultiSelectNothingMovableHint;

            RaiseGuides();
        }

        // Does the undo of the multi-mode page before a single selection (or none) is shown
        private void EndMultiSelectionPage()
        {
            PnlMultiSelect.Visibility = Visibility.Collapsed;
            TabsLayer.Visibility = Visibility.Visible;
            BtnLayerUp.IsEnabled = true;
            BtnLayerDown.IsEnabled = true;
        }

        private void MultiNudge_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyMultiNudge(sender as TextBox);
        }

        private void MultiNudge_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyMultiNudge(sender as TextBox);
        }

        // Does the nudge: shifts every moving entry by the typed amount (X box: right is positive, Y box: down is
        // positive), then puts the box back to 0 so a later focus loss cannot apply the same amount twice.
        // Blank, invalid or 0 applies nothing.
        private void ApplyMultiNudge(TextBox? box)
        {
            if (_loading || box == null || !IsMultiSelect) return;

            if (double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double delta)
                && double.IsFinite(delta) && delta != 0)
            {
                foreach (var entry in GetMultiMoveEntries())
                {
                    if (box == TxtMultiX) entry.X += delta;
                    else if (box == TxtMultiY) entry.Y += delta;
                }
            }

            _loading = true;
            box.Text = "0";
            _loading = false;
            RaiseGuides();
        }
        // [END SECTION: Multi-Select]

        // [SECTION: Layer Copy and Paste]
        // Right-click menu on the layer list (decision 67). One layer selected: Copy items for what it has (position and
        // size: LayerRegistry.CanCopyRect; border: CanCopyBorder), plus Paste when something else is on the clipboard that it
        // can take. Several selected: Paste only. The values are the raw entry numbers the fields show (no border ring or
        // ratio lock conversion). A Random Game Display row copies from and pastes to the part last clicked into on the
        // Position page (_guidePartId), else its first part. The clipboard is session-only and lives with this control (one
        // per builder window); a new Copy replaces it, and a resolution switch, theme load or Reset Layer leaves it. A paste
        // is an ordinary working edit with no undo. The menu items are built each time the menu opens.
        [Flags]
        private enum LayerCopyKind
        {
            None = 0,
            X = 1,
            Y = 2,
            Width = 4,
            Height = 8,
            Border = 16,
            Position = X | Y,
            Size = Width | Height,
            Rect = Position | Size
        }

        // What was copied, the numbers, the source entry and the layout size it came from
        private sealed record LayerClipboard(LayerCopyKind Kind, string SourceId, int LayoutWidth, int LayoutHeight,
            double X, double Y, double Width, double Height, int BorderSize, string BorderColorHex, int CornerRadius);

        private LayerClipboard? _layerClipboard;

        // The Copy items in menu order; null marks a separator
        private static readonly LayerCopyKind?[] CopyMenuKinds =
        {
            LayerCopyKind.X, LayerCopyKind.Y, LayerCopyKind.Position, null,
            LayerCopyKind.Width, LayerCopyKind.Height, LayerCopyKind.Size, null,
            LayerCopyKind.Rect, null,
            LayerCopyKind.Border
        };

        // Does the menu label for a copy or paste kind ("X / Y", "Position + Size", ...)
        private static string CopyKindLabel(LayerCopyKind kind) => kind switch
        {
            LayerCopyKind.X => "X",
            LayerCopyKind.Y => "Y",
            LayerCopyKind.Position => "X / Y",
            LayerCopyKind.Width => "Width",
            LayerCopyKind.Height => "Height",
            LayerCopyKind.Size => "Width / Height",
            LayerCopyKind.Rect => "Position + Size",
            LayerCopyKind.Border => "Border",
            _ => kind.ToString()
        };

        // Does the copy and paste entry lookup for a list row: the layer's own Id, or for Random Game Display the part last
        // clicked into on the Position page (its first part when that is not one of its parts); null for the other groups
        private string? GetCopyEntryId(LayerListItem row)
        {
            if (row.Id == LayerRegistry.RandomGameGroup)
            {
                bool isPart = GetGroup(row.Id)?.PartIds.Contains(_guidePartId) == true;
                return isPart ? _guidePartId : LayerRegistry.RandomGameDisplay;
            }
            return GetGroup(row.Id) == null ? row.Id : null;
        }

        // Does the per-layer kind check: the kinds an entry can be copied from or pasted to
        private static LayerCopyKind GetCopyKinds(string entryId) =>
            (LayerRegistry.CanCopyRect(entryId) ? LayerCopyKind.Rect : LayerCopyKind.None)
            | (LayerRegistry.CanCopyBorder(entryId) ? LayerCopyKind.Border : LayerCopyKind.None);

        // Does the paste target check: the entry takes every kind on the clipboard and is not its source (same entry at
        // the same layout size); the same layer at another size can take it
        private bool CanPasteTo(string entryId, bool allowSource)
        {
            if (_layerClipboard is not LayerClipboard clip || GetEntry(entryId) == null) return false;
            if ((GetCopyKinds(entryId) & clip.Kind) != clip.Kind) return false;
            bool isSource = clip.SourceId == entryId && clip.LayoutWidth == _currentLayout.Width && clip.LayoutHeight == _currentLayout.Height;
            return allowSource || !isSource;
        }

        // Builds the menu for the current selection each time it opens, or cancels it: no row under the mouse (empty list
        // space), nothing to copy or paste, or an empty menu. A right-click has already selected the row (the ListBox
        // selects an unselected row and keeps a multi-selection that contains it); the menu key uses the selection as it is.
        private void LstLayers_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (LstLayers.ContextMenu is not ContextMenu menu) return;
            menu.Items.Clear();

            bool fromMouse = e.CursorLeft >= 0 && e.CursorTop >= 0;
            if (fromMouse)
            {
                var source = e.OriginalSource as DependencyObject;
                while (source != null && source is not ListBoxItem) source = VisualTreeHelper.GetParent(source);
                if (source == null)
                {
                    e.Handled = true;
                    return;
                }
            }

            if (!_loading && _settings != null)
            {
                if (IsMultiSelect) BuildMultiPasteMenu(menu);
                else if (SingleSelectedLayer is LayerListItem row) BuildSingleCopyMenu(menu, row);
            }

            if (menu.Items.Count == 0) e.Handled = true;
        }

        // Does the one-layer menu: the Copy items the layer supports, then Paste when the clipboard fits and this is not
        // its source. A Random Game Display row names its part in every item.
        private void BuildSingleCopyMenu(ContextMenu menu, LayerListItem row)
        {
            if (GetCopyEntryId(row) is not string entryId || GetEntry(entryId) == null) return;
            LayerCopyKind supported = GetCopyKinds(entryId);
            if (supported == LayerCopyKind.None) return;

            string suffix = row.Id == LayerRegistry.RandomGameGroup
                ? $" ({LayerRegistry.GetDefinition(entryId)?.DisplayName ?? entryId})"
                : "";

            bool pendingSeparator = false;
            foreach (var kind in CopyMenuKinds)
            {
                if (kind == null)
                {
                    pendingSeparator = menu.Items.Count > 0;
                    continue;
                }
                if ((supported & kind.Value) != kind.Value) continue;

                if (pendingSeparator) menu.Items.Add(MakeMenuSeparator());
                pendingSeparator = false;
                var copyKind = kind.Value;
                menu.Items.Add(MakeMenuItem($"Copy {CopyKindLabel(copyKind)}{suffix}", () => CopyLayerValues(entryId, copyKind)));
            }

            if (_layerClipboard is LayerClipboard clip && CanPasteTo(entryId, allowSource: false))
            {
                menu.Items.Add(MakeMenuSeparator());
                menu.Items.Add(MakeMenuItem($"Paste {CopyKindLabel(clip.Kind)}{suffix}", () => PasteLayerValues(new[] { entryId })));
            }
        }

        // Does the several-layers menu: Paste only, when at least one selected row can take the clipboard (rows that
        // cannot are skipped; the source, if selected, gets its own values back)
        private void BuildMultiPasteMenu(ContextMenu menu)
        {
            if (_layerClipboard is not LayerClipboard clip) return;

            var targets = LstLayers.SelectedItems.OfType<LayerListItem>()
                .Select(GetCopyEntryId)
                .OfType<string>()
                .Where(id => CanPasteTo(id, allowSource: true))
                .Distinct()
                .ToList();
            if (targets.Count == 0) return;

            menu.Items.Add(MakeMenuItem($"Paste {CopyKindLabel(clip.Kind)}", () => PasteLayerValues(targets)));
        }

        // Does a plain menu item on the Theme Builder menu style
        private MenuItem MakeMenuItem(string header, Action onClick)
        {
            var item = new MenuItem { Header = header, Style = (Style)FindResource("BuilderMenuItemStyle") };
            item.Click += (_, _) => onClick();
            return item;
        }

        // Does a separator on the Theme Builder menu style (set locally, or the menu would restyle it)
        private Separator MakeMenuSeparator() => new Separator { Style = (Style)FindResource("BuilderMenuSeparatorStyle") };

        // Does the copy: puts the entry's raw values, its Id and this layout size on the clipboard (replacing what was there)
        private void CopyLayerValues(string entryId, LayerCopyKind kind)
        {
            if (GetEntry(entryId) is not LayerLayoutEntry entry) return;

            // Same fallbacks as the Border tab loaders (the values are always filled by ResolveLayout anyway)
            int borderFallback = LayerRegistry.IsBoxId(entryId) ? LayerRegistry.DefaultShapeBorderSize : 0;
            _layerClipboard = new LayerClipboard(kind, entryId, _currentLayout.Width, _currentLayout.Height,
                entry.X, entry.Y, entry.Width, entry.Height,
                entry.BorderSize ?? borderFallback,
                entry.BorderColorHex ?? LayerRegistry.DefaultShapeBorderColorHex,
                entry.CornerRadius ?? LayerRegistry.DefaultShapeCornerRadius);
        }

        // Does the paste: writes the clipboard's kinds onto each target entry as raw numbers (no ratio lock, no group move,
        // no Z, visibility or file change), then repaints and refreshes the fields (multi mode: guides and main window only)
        private void PasteLayerValues(IReadOnlyCollection<string> entryIds)
        {
            if (_loading || _layerClipboard is not LayerClipboard clip) return;

            foreach (string entryId in entryIds)
            {
                if (!CanPasteTo(entryId, allowSource: true) || GetEntry(entryId) is not LayerLayoutEntry entry) continue;

                if (clip.Kind.HasFlag(LayerCopyKind.X)) entry.X = clip.X;
                if (clip.Kind.HasFlag(LayerCopyKind.Y)) entry.Y = clip.Y;
                if (clip.Kind.HasFlag(LayerCopyKind.Width)) entry.Width = clip.Width;
                if (clip.Kind.HasFlag(LayerCopyKind.Height)) entry.Height = clip.Height;
                if (clip.Kind.HasFlag(LayerCopyKind.Border))
                {
                    entry.BorderSize = clip.BorderSize;
                    entry.BorderColorHex = clip.BorderColorHex;
                    entry.CornerRadius = clip.CornerRadius;
                }
            }

            // LoadSelectedLayerIntoFields ends in RaiseGuides (LayoutChanged repaints the main window)
            if (IsMultiSelect) RaiseGuides();
            else LoadSelectedLayerIntoFields();
        }
        // [END SECTION: Layer Copy and Paste]

        // Does the name shown for a single layer (list row and heading). Custom layers: their own Name.
        // Shape layers: this resolution's Name, else the first Name set at any resolution, else the registry
        // name. Every other layer: the registry name.
        private string GetLayerDisplayName(string id)
        {
            if (LayerRegistry.IsCustomId(id))
            {
                string? customName = GetEntry(id)?.Name;
                return string.IsNullOrWhiteSpace(customName) ? "Custom Layer" : customName;
            }

            if (LayerRegistry.IsShapeId(id))
            {
                string? currentName = GetEntry(id)?.Name;
                if (!string.IsNullOrWhiteSpace(currentName)) return currentName;

                string? anyName = _settings.Layouts
                    .Select(layout => layout.Layers.FirstOrDefault(l => l.Id == id)?.Name)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
                if (anyName != null) return anyName;
            }

            return LayerRegistry.GetDefinition(id)?.DisplayName ?? id;
        }

        // [SECTION: Layer Rename]
        // Does the Rename button: renames the selected shape or custom layer
        private void BtnRenameLayer_Click(object sender, RoutedEventArgs e) => RenameSelectedLayer();

        // Does the double-click rename: only when the click landed on the selected, renameable row
        private void LstLayers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null && source is not ListBoxItem) source = VisualTreeHelper.GetParent(source);
            if (source is not ListBoxItem row) return;

            if (row.DataContext is not LayerListItem item || !ReferenceEquals(item, SingleSelectedLayer)) return;
            if (!LayerRegistry.IsBoxId(item.Id)) return;

            RenameSelectedLayer();
        }

        // Does the rename: custom layers at this resolution only (empty = cancel); shape layers at every
        // resolution (empty clears the name everywhere, back to the registry name)
        private void RenameSelectedLayer()
        {
            if (SingleSelectedLayer is not LayerListItem selected || !LayerRegistry.IsBoxId(selected.Id)) return;
            if (GetEntry(selected.Id) is not LayerLayoutEntry entry) return;

            bool isCustom = LayerRegistry.IsCustomId(selected.Id);
            string? newName = PromptForLayerName("Rename Layer", "Name for this layer:", GetLayerDisplayName(selected.Id), allowEmpty: !isCustom);
            if (newName == null) return;

            if (isCustom)
            {
                entry.Name = newName;
            }
            else
            {
                // _settings.Layouts holds the current layout too
                foreach (var layout in _settings.Layouts)
                {
                    foreach (var layer in layout.Layers.Where(l => l.Id == selected.Id)) layer.Name = newName;
                }
            }

            RebuildLayerList(selected.Id);
        }

        // Does the name capture for the layout resets: each shape layer's Name at this resolution
        private Dictionary<string, string> CaptureShapeNames() =>
            _currentLayout.Layers.Where(l => LayerRegistry.IsShapeId(l.Id)).ToDictionary(l => l.Id, l => l.Name);

        // Does the name restore after a layout reset: puts the captured shape layer names back
        private void RestoreShapeNames(Dictionary<string, string> names)
        {
            foreach (var layer in _currentLayout.Layers)
            {
                if (names.TryGetValue(layer.Id, out string? name)) layer.Name = name;
            }
        }
        // [END SECTION: Layer Rename]

        private void BtnLayerUp_Click(object sender, RoutedEventArgs e) => MoveSelectedLayer(-1);
        private void BtnLayerDown_Click(object sender, RoutedEventArgs e) => MoveSelectedLayer(1);

        // Swaps the selected layer with its neighbor in the stacking order. The background stays pinned
        // at the bottom and can't be moved or passed.
        private void MoveSelectedLayer(int direction)
        {
            if (SingleSelectedLayer is not LayerListItem selected) return;
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

            string? layerName = PromptForLayerName("Add Layer", "Name for the new layer:", "", allowEmpty: false);
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

        // Small name prompt built in code (OK / Cancel, Enter confirms, Esc cancels), pre-filled with initialText
        // (selected). Returns null if cancelled; an empty result is "" when allowEmpty, otherwise null. Chrome Palette
        // window and text box; the label and buttons use the Global font and button values.
        private string? PromptForLayerName(string title, string label, string initialText, bool allowEmpty)
        {
            var nameBox = new TextBox { Margin = new Thickness(0, 6, 0, 12), MaxLength = 24, Text = initialText, Style = (Style)FindResource("GLTextBoxStyle") };
            var okButton = MakePromptButton("OK");
            okButton.IsDefault = true;
            okButton.Margin = new Thickness(0, 0, 8, 0);
            var cancelButton = MakePromptButton("Cancel");
            cancelButton.IsCancel = true;

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);

            var labelText = new TextBlock { Text = label };
            labelText.SetResourceReference(TextBlock.ForegroundProperty, ArcadeStick.Services.GlobalThemeResources.FontStandardColor);
            labelText.SetResourceReference(TextBlock.FontSizeProperty, ArcadeStick.Services.GlobalThemeResources.FontStandardSize);

            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(labelText);
            panel.Children.Add(nameBox);
            panel.Children.Add(buttons);

            var prompt = new Window
            {
                Title = title,
                Content = panel,
                Width = 320,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ShowInTaskbar = false
            };
            prompt.SetResourceReference(Window.BackgroundProperty, "ChromeWindowBg");

            okButton.Click += (s, args) => prompt.DialogResult = true;
            prompt.Loaded += (s, args) =>
            {
                nameBox.Focus();
                nameBox.SelectAll();
            };

            if (prompt.ShowDialog() != true) return null;

            string name = nameBox.Text.Trim();
            return string.IsNullOrEmpty(name) && !allowEmpty ? null : name;
        }

        // Builds one name prompt button on the shared action button style (the Global button values come from the style)
        private Button MakePromptButton(string content) =>
            new Button { Content = content, Width = 70, Padding = new Thickness(0, 3, 0, 3), Style = (Style)FindResource("ThemedActionButtonStyle") };

        // "New size layout" prompt built in code like the name prompt (new-size-scaler.md): shows the sizes, the factors and,
        // when they differ, an aspect ratio warning, with one radio button per LayoutScaler choice. The remembered choice is
        // preselected unless the ratio is far off (then "keep"). Returns the choice, or null if cancelled. OK on a close ratio
        // remembers the choice in memory only (Save Adjustments writes settings.json); OK on a far-off ratio leaves it alone.
        private string? PromptForNewSizeScale(int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            var factors = LayoutScaler.GetFactors(oldWidth, oldHeight, newWidth, newHeight);
            bool farOff = LayoutScaler.IsFarOffRatio(oldWidth, oldHeight, newWidth, newHeight);
            string fx = LayoutScaler.FormatFactor(factors.Fx);
            string fy = LayoutScaler.FormatFactor(factors.Fy);

            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(MakePromptLine($"Adding {newWidth} x {newHeight} from {oldWidth} x {oldHeight}"));
            panel.Children.Add(MakePromptLine($"Width x{fx}, height x{fy}"));
            if (LayoutScaler.FactorsDiffer(factors))
                panel.Children.Add(MakePromptLine($"Aspect ratio differs: positions stretch to fill; text and borders follow x{LayoutScaler.FormatFactor(factors.U)}"));

            var choices = new (string Choice, string Label)[]
            {
                (LayoutScaler.ChoiceScaleAll, "Scale everything (positions, sizes, fonts, borders)"),
                (LayoutScaler.ChoicePositionsOnly, "Scale positions only"),
                (LayoutScaler.ChoiceKeep, "Keep the layout as is")
            };
            string remembered = LayoutScaler.IsChoice(_settings.NewSizeScaleChoice) ? _settings.NewSizeScaleChoice : LayoutScaler.ChoiceKeep;
            string preselected = farOff ? LayoutScaler.ChoiceKeep : remembered;

            var radios = new List<(RadioButton Radio, string Choice)>();
            var radioPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 12) };
            foreach (var (choice, label) in choices)
            {
                var radio = new RadioButton
                {
                    Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap },
                    GroupName = "NewSizeScale",
                    IsChecked = choice == preselected,
                    Margin = new Thickness(0, 3, 0, 3),
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                radio.SetResourceReference(Control.ForegroundProperty, ArcadeStick.Services.GlobalThemeResources.FontStandardColor);
                radio.SetResourceReference(Control.FontSizeProperty, ArcadeStick.Services.GlobalThemeResources.FontStandardSize);
                radios.Add((radio, choice));
                radioPanel.Children.Add(radio);
            }
            panel.Children.Add(radioPanel);

            var okButton = MakePromptButton("OK");
            okButton.IsDefault = true;
            okButton.Margin = new Thickness(0, 0, 8, 0);
            var cancelButton = MakePromptButton("Cancel");
            cancelButton.IsCancel = true;

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            panel.Children.Add(buttons);

            var prompt = new Window
            {
                Title = "New size layout",
                Content = panel,
                Width = 420,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ShowInTaskbar = false
            };
            prompt.SetResourceReference(Window.BackgroundProperty, "ChromeWindowBg");

            okButton.Click += (s, args) => prompt.DialogResult = true;
            prompt.Loaded += (s, args) => radios.First(r => r.Radio.IsChecked == true).Radio.Focus();

            if (prompt.ShowDialog() != true) return null;

            string picked = radios.First(r => r.Radio.IsChecked == true).Choice;
            if (!farOff) _settings.NewSizeScaleChoice = picked;
            return picked;
        }

        // Builds one wrapped text line for a code-built prompt in the Global Standard font
        private static TextBlock MakePromptLine(string text)
        {
            var line = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
            line.SetResourceReference(TextBlock.ForegroundProperty, ArcadeStick.Services.GlobalThemeResources.FontStandardColor);
            line.SetResourceReference(TextBlock.FontSizeProperty, ArcadeStick.Services.GlobalThemeResources.FontStandardSize);
            return line;
        }

        // Remove: deletes the selected custom layer at this resolution only (other resolutions are untouched).
        private void BtnRemoveLayer_Click(object sender, RoutedEventArgs e)
        {
            if (SingleSelectedLayer is not LayerListItem selected || !LayerRegistry.IsCustomId(selected.Id)) return;

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
        // Position is the first tab, shown for every layer except the fill layers that cannot be positioned (the Main
        // Window Background), where it would be empty. Each layer type lists the other tabs it shows; every other tab
        // is hidden. To give a layer type its own tabs: declare the TabItem in the XAML, add it to AllLayerTabs,
        // and list it for that layer in GetLayerTabs.
        private TabItem[] AllLayerTabs => new[] { TabGLFolders, TabGLGameTitles, TabSBSearch, TabSBButtons, TabGITitle, TabGIPublisher, TabGIInfo, TabGINav, TabGIPreviews, TabShapeBackground, TabSplashImage, TabShapeBorder, TabVideoBackground, TabVideoFallback, TabMarqueeLogo, TabMarqueeShadow, TabMainBgImage, TabRGText, TabRGBackground, TabMarqueeBanner };

        private TabItem[] GetLayerTabs(string? unitKey)
        {
            // Main Window Background gets the Image tab (wallpaper file and the Disable flag)
            if (unitKey == LayerRegistry.MainBackground) return new[] { TabMainBgImage };

            if (unitKey == LayerRegistry.GamesList) return new[] { TabGLFolders, TabGLGameTitles };

            // Search & Buttons group: label/box, shared button style, and the shared Border tab (mapped to the
            // Random Game Button entry by GetSelectedBorderEntry)
            if (unitKey == LayerRegistry.SearchAndButtonsGroup) return new[] { TabSBSearch, TabSBButtons, TabShapeBorder };

            // Game Info group: title, publisher block, headers + body, the two navs, and the image previews shadow
            if (unitKey == LayerRegistry.GameInfoGroup) return new[] { TabGITitle, TabGIPublisher, TabGIInfo, TabGINav, TabGIPreviews };

            // Random Game Display group: the spin card's text, background (color and file) and the shared Border tab
            // (mapped to the RandomGameDisplay entry by GetSelectedBorderEntry)
            if (unitKey == LayerRegistry.RandomGameGroup) return new[] { TabRGText, TabRGBackground, TabShapeBorder };

            // Marquee gets the Logo tab (the logo shown when no game is selected), the shared image shadow and the
            // no-art title card's Banner tab
            if (unitKey == LayerRegistry.MarqueePanel) return new[] { TabMarqueeLogo, TabMarqueeShadow, TabMarqueeBanner };

            // Shape Layers 01 to 04 share one tab set
            if (unitKey != null && LayerRegistry.IsBoxId(unitKey)) return new[] { TabShapeBackground, TabShapeBorder };

            // Splash Image: the splash file, then the shared Border tab
            if (unitKey == LayerRegistry.SplashImage) return new[] { TabSplashImage, TabShapeBorder };

            // Video Preview: the shared Border tab, the letterbox background color and the fallback image
            if (unitKey == LayerRegistry.VideoPreview) return new[] { TabShapeBorder, TabVideoBackground, TabVideoFallback };

            // Every other layer (custom image layers included) shows Position only for now.
            return Array.Empty<TabItem>();
        }

        // The layer whose tabs are showing. The selection returns to the first visible tab (Position, or the first
        // own tab when Position is hidden) only when this changes, not on every committed edit (edits reload the fields too).
        private string? _tabsUnitKey;

        // Shows the selected layer's tabs and, when the layer changed, selects its first visible tab
        private void ApplyLayerTabs(string? unitKey)
        {
            TabPosition.Visibility = unitKey != null && IsBackground(unitKey) ? Visibility.Collapsed : Visibility.Visible;

            var wanted = GetLayerTabs(unitKey);
            foreach (var tab in AllLayerTabs)
            {
                tab.Visibility = wanted.Contains(tab) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (unitKey != _tabsUnitKey)
            {
                _tabsUnitKey = unitKey;
                _rgStatePartId = LayerRegistry.RandomGameDisplay; // the State combo starts on the first state each time
                StopPreviews(); // the card and banner previews belong to the layer they were turned on for
                TabsLayer.SelectedItem = TabsLayer.Items.OfType<TabItem>().FirstOrDefault(t => t.Visibility == Visibility.Visible) ?? TabPosition;
            }
        }
        // [END SECTION: Layer Property Tabs]

        private static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private void LoadSelectedLayerIntoFields()
        {
            // Several rows selected: the nudge page only (none of the single-layer loaders below run)
            if (IsMultiSelect)
            {
                LoadMultiSelection();
                return;
            }
            EndMultiSelectionPage();

            // Tabs for the selected layer: Position always, the others per layer type. Selecting a different
            // layer returns to Position; edits to the same layer stay on the current tab.
            ApplyLayerTabs(SingleSelectedLayer?.Id);

            // Games List styling fields: filled from the entry when the Games List is selected
            LoadGamesListStyleFields();

            // Shape layer fields (visibility, background, border): filled when a Shape Layer is selected
            LoadShapeStyleFields();
            LoadBorderOnlyFields();

            // Search & Buttons fields (Search and Buttons tabs, Border tab hover row): filled when that group is selected
            LoadSearchButtonsStyleFields();

            // Game Info fields (Title, Publisher, Info and Nav tabs): filled when that group is selected
            LoadGameInfoStyleFields();

            // Random Game Display fields (Text and Background tabs): filled when that group is selected
            LoadRandomDisplayStyleFields();

            // Marquee logo field: filled when the Marquee layer is selected
            if (SingleSelectedLayer is LayerListItem marqueeSel && marqueeSel.Id == LayerRegistry.MarqueePanel)
            {
                TxtMarqueeLogoFile.Text = GetEntry(LayerRegistry.MarqueePanel)?.ImagePath ?? "";
            }

            // Marquee image shadow (SHADOW tab): filled when the Marquee layer is selected
            LoadMarqueeShadowFields();

            // Marquee no-art banner (BANNER tab): filled when the Marquee layer is selected
            LoadMarqueeBannerFields();

            // Splash Image file field: filled when the Splash Image layer is selected
            if (SingleSelectedLayer is LayerListItem splashSel && splashSel.Id == LayerRegistry.SplashImage)
            {
                TxtSplashFile.Text = GetEntry(LayerRegistry.SplashImage)?.ImagePath ?? "";
            }

            // Video Preview background color and fallback image: filled when the Video Preview layer is selected
            if (SingleSelectedLayer is LayerListItem videoSel && videoSel.Id == LayerRegistry.VideoPreview
                && GetEntry(LayerRegistry.VideoPreview) is LayerLayoutEntry videoEntry)
            {
                _loading = true;
                ShowColor(VPBgColorPicker, TxtVPBgColorHex, videoEntry.BgColorHex ?? LayerRegistry.DefaultVideoBgColorHex);
                TxtVPFallbackFile.Text = videoEntry.ImagePath ?? "";
                _loading = false;
            }

            // Main Window Background image fields: filled when that layer is selected
            if (SingleSelectedLayer is LayerListItem bgSel && bgSel.Id == LayerRegistry.MainBackground
                && GetEntry(LayerRegistry.MainBackground) is LayerLayoutEntry bgEntry)
            {
                _loading = true;
                ShowColor(MainBgColorPicker, TxtMainBgColorHex, bgEntry.BgColorHex ?? LayerRegistry.DefaultMainBackgroundColorHex);
                TxtMainBgFile.Text = bgEntry.ImagePath ?? "";
                ChkMainBgImageDisabled.IsChecked = bgEntry.ImageDisabled ?? LayerRegistry.DefaultImageDisabled;
                _loading = false;
            }

            // A group row shows every part's fields at once on its own page
            if (SingleSelectedLayer is LayerListItem groupItem && GetGroup(groupItem.Id) is LayerGroupDefinition selectedGroup)
            {
                BtnRenameLayer.Visibility = Visibility.Collapsed;
                LoadGroupIntoFields(selectedGroup);
                return;
            }

            if (SingleSelectedLayer is not LayerListItem selected || GetEntry(selected.Id) is not LayerLayoutEntry entry)
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

            _loading = true;
            PnlLayerFields.Visibility = Visibility.Visible;
            bool isCustom = LayerRegistry.IsCustomId(entry.Id);
            TxtLayerName.Text = GetLayerDisplayName(entry.Id);
            BtnRemoveLayer.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            // Rename: shape layers and custom layers only
            BtnRenameLayer.Visibility = LayerRegistry.IsBoxId(entry.Id) ? Visibility.Visible : Visibility.Collapsed;

            PnlRectFields.Visibility = positionable ? Visibility.Visible : Visibility.Collapsed;
            bool isShape = LayerRegistry.IsBoxId(entry.Id);
            ChkLayerHidden.Visibility = (positionable && canHide && !isShape) ? Visibility.Visible : Visibility.Collapsed;
            PnlVisibilityMode.Visibility = isShape ? Visibility.Visible : Visibility.Collapsed;
            // Reset Layer: every built-in layer, including the Main Window Background (it restores the wallpaper settings)
            BtnResetLayer.Visibility = !isCustom ? Visibility.Visible : Visibility.Collapsed;
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
        // Shows a grouped layer (Search & Buttons, Game Info, Random Game Display): one section per part, all visible
        // at once, in the group's page order. Sections are built once per group and then only refreshed.
        private void LoadGroupIntoFields(LayerGroupDefinition group)
        {
            _loading = true;

            PnlLayerFields.Visibility = Visibility.Visible;
            TxtLayerName.Text = group.DisplayName;
            PnlRectFields.Visibility = Visibility.Collapsed;

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
                if (controls.RatioLock != null) controls.RatioLock.IsChecked = _partRatioLocks.GetValueOrDefault(partId);
            }

            _loading = false;
            RaiseGuides();
        }

        // Builds one section per part: X / Y / Width / Height boxes (with a ratio lock between Width and Height when
        // the group allows it) and a Hide checkbox
        private void BuildGroupParts(LayerGroupDefinition group)
        {
            PnlGroupParts.Children.Clear();
            _partControls.Clear();

            var fieldStyle = (Style)FindResource("GLTextBoxStyle");

            TextBox MakeBox(string partId, int column, Grid host)
            {
                var box = new TextBox
                {
                    Tag = partId,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = fieldStyle,
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
                heading.SetResourceReference(TextBlock.ForegroundProperty, ArcadeStick.Services.GlobalThemeResources.FontHeaderColor);
                heading.SetResourceReference(TextBlock.FontSizeProperty, ArcadeStick.Services.GlobalThemeResources.FontHeaderSize);
                PnlGroupParts.Children.Add(heading);

                // Columns: X, Y, Width, [lock], Height. The narrow lock column is only added when the group allows it.
                var host = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                for (int i = 0; i < 3; i++) host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (group.AllowRatioLock) host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                int heightColumn = group.AllowRatioLock ? 4 : 3;

                string[] labels = { "X:", "Y:", "Width:", "Height:" };
                int[] labelColumns = { 0, 1, 2, heightColumn };
                for (int i = 0; i < 4; i++)
                {
                    var label = new TextBlock { Text = labels[i], Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 3) };
                    label.SetResourceReference(TextBlock.ForegroundProperty, ArcadeStick.Services.GlobalThemeResources.FontSubHeaderColor);
                    label.SetResourceReference(TextBlock.FontSizeProperty, ArcadeStick.Services.GlobalThemeResources.FontSubHeaderSize);
                    Grid.SetRow(label, 0);
                    Grid.SetColumn(label, labelColumns[i]);
                    host.Children.Add(label);
                }

                var controls = new PartControls
                {
                    X = MakeBox(partId, 0, host),
                    Y = MakeBox(partId, 1, host),
                    Width = MakeBox(partId, 2, host),
                    Height = MakeBox(partId, heightColumn, host)
                };

                // Ratio lock: same glyph toggle and behavior as BtnRatioLock, one per part
                if (group.AllowRatioLock)
                {
                    var ratioLock = new System.Windows.Controls.Primitives.ToggleButton
                    {
                        Tag = partId,
                        Width = 26,
                        Height = 26,
                        Margin = new Thickness(6, 0, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = "Lock aspect ratio",
                        Style = (Style)FindResource("GlyphToggleStyle"),
                        IsChecked = _partRatioLocks.GetValueOrDefault(partId)
                    };
                    ratioLock.Click += PartRatioLock_Click;
                    Grid.SetRow(ratioLock, 1);
                    Grid.SetColumn(ratioLock, 3);
                    host.Children.Add(ratioLock);
                    controls.RatioLock = ratioLock;
                }

                // GLCheckBoxStyle: palette box, label in the themed Standard font color and size
                var hide = new CheckBox { Content = "Hide", Tag = partId, Margin = new Thickness(0, 6, 0, 14), Style = (Style)FindResource("GLCheckBoxStyle") };
                hide.Click += PartHide_Click;
                controls.Hide = hide;

                _partControls[partId] = controls;
                PnlGroupParts.Children.Add(host);
                PnlGroupParts.Children.Add(hide);
            }
        }

        // Remembers a part's ratio lock state for the session
        private void PartRatioLock_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Primitives.ToggleButton toggle || toggle.Tag is not string partId) return;
            _partRatioLocks[partId] = toggle.IsChecked == true;
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
        // amount; Width and Height never propagate. With the part's ratio lock on, a Width or Height change resizes
        // the other one to keep the part's current proportions (read from the stored size just before the edit).
        private void ApplyPartField(TextBox? box)
        {
            if (_loading || box == null || box.Tag is not string partId) return;
            if (GetEntry(partId) is not LayerLayoutEntry entry || !_partControls.TryGetValue(partId, out var controls)) return;

            if (double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                double oldWidth = entry.Width;
                double oldHeight = entry.Height;
                bool keepRatio = controls.RatioLock?.IsChecked == true && oldWidth > 0 && oldHeight > 0;

                double dx = 0, dy = 0;
                if (box == controls.X) { dx = value - entry.X; entry.X = value; }
                else if (box == controls.Y) { dy = value - entry.Y; entry.Y = value; }
                else if (box == controls.Width && value > 0)
                {
                    entry.Width = value;
                    if (keepRatio && value != oldWidth) entry.Height = value * oldHeight / oldWidth;
                }
                else if (box == controls.Height && value > 0)
                {
                    entry.Height = value;
                    if (keepRatio && value != oldHeight) entry.Width = value * oldWidth / oldHeight;
                }

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
            if (SingleSelectedLayer is not LayerListItem selected || GetEntry(selected.Id) is not LayerLayoutEntry entry) return;

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

        private void ChkLayerHidden_Click(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            if (SingleSelectedLayer is not LayerListItem selected || GetEntry(selected.Id) is not LayerLayoutEntry entry) return;

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
            if (SingleSelectedLayer is LayerListItem sel && LayerRegistry.IsBoxId(sel.Id)) return GetEntry(sel.Id);
            return null;
        }

        // Returns the entry the Border tab edits: the selected layer when it has a Border tab (shape layers, Splash
        // Image, Video Preview), the Random Game Button entry (the shared button style) for Search & Buttons, or the
        // part the State combo picks for Random Game Display (each state has its own border)
        private LayerLayoutEntry? GetSelectedBorderEntry()
        {
            if (SingleSelectedLayer is not LayerListItem sel) return null;
            if (sel.Id == LayerRegistry.SearchAndButtonsGroup) return GetEntry(LayerRegistry.RandomButton);
            if (sel.Id == LayerRegistry.RandomGameGroup) return GetEntry(_rgStatePartId);
            if (LayerRegistry.HasBorderTab(sel.Id)) return GetEntry(sel.Id);
            return null;
        }

        // Fills the Border tab for Splash Image, Video Preview, Search & Buttons and Random Game Display (the shape layers are filled by LoadShapeStyleFields)
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

        // [SECTION: Splash Image and Video Preview Files]
        // Splash Image's Image tab and Video Preview's Background and Fallback Image tabs. Browse stores the file in
        // the layer entry's ImagePath (relative to the app folder when inside it); a cancelled dialog changes nothing.
        // Clear empties it, which brings back the built-in file. The main window picks both up through ApplyLayout's
        // _lastSplashPath / _lastVideoFallbackPath keys; the background color is pushed on every ApplyLayout.

        // Opens a file dialog and returns the chosen path in the stored convention, or null when cancelled
        private string? BrowseLayerFile(string filter, string title)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Title = title,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() != true) return null;

            string storedPath = dialog.FileName;
            try
            {
                string relative = System.IO.Path.GetRelativePath(_settings.BaseDirectory, dialog.FileName);
                if (!relative.StartsWith("..") && !System.IO.Path.IsPathRooted(relative)) storedPath = relative;
            }
            catch { }
            return storedPath;
        }

        // Picks the splash file: images, gif (animated) or mp4
        private void BtnBrowseSplashFile_Click(object sender, RoutedEventArgs e)
        {
            if (GetEntry(LayerRegistry.SplashImage) is not LayerLayoutEntry entry) return;

            string? storedPath = BrowseLayerFile(
                "Splash Files (*.png;*.jpg;*.jpeg;*.gif;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|Video Files (*.mp4)|*.mp4|All files (*.*)|*.*",
                "Select Splash Image or Video");
            if (storedPath == null) return;

            entry.ImagePath = storedPath;
            LoadSelectedLayerIntoFields();
        }

        // Empties the splash path so the built-in splash shows
        private void BtnClearSplashFile_Click(object sender, RoutedEventArgs e)
        {
            if (GetEntry(LayerRegistry.SplashImage) is not LayerLayoutEntry entry) return;

            entry.ImagePath = "";
            LoadSelectedLayerIntoFields();
        }

        // Picks the Video Preview fallback image (images only)
        private void BtnBrowseVPFallback_Click(object sender, RoutedEventArgs e)
        {
            if (GetEntry(LayerRegistry.VideoPreview) is not LayerLayoutEntry entry) return;

            string? storedPath = BrowseLayerFile(
                "Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All files (*.*)|*.*",
                "Select No Preview Available Image");
            if (storedPath == null) return;

            entry.ImagePath = storedPath;
            LoadSelectedLayerIntoFields();
        }

        // Empties the fallback path so the built-in no_preview.png shows
        private void BtnClearVPFallback_Click(object sender, RoutedEventArgs e)
        {
            if (GetEntry(LayerRegistry.VideoPreview) is not LayerLayoutEntry entry) return;

            entry.ImagePath = "";
            LoadSelectedLayerIntoFields();
        }

        // Video Preview background color picker and hex box (same helpers and revert rules as the other colors)
        private void VPBgColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(VPBgColorPicker, TxtVPBgColorHex, GetEntry(LayerRegistry.VideoPreview), (en, hex) => en.BgColorHex = hex);
        private void TxtVPBgColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(VPBgColorPicker, TxtVPBgColorHex, GetEntry(LayerRegistry.VideoPreview), en => en.BgColorHex ?? LayerRegistry.DefaultVideoBgColorHex, (en, hex) => en.BgColorHex = hex);
        // [END SECTION: Splash Image and Video Preview Files]

        // [SECTION: Main Window Background Image]
        // Browse stores the wallpaper in the Main Window Background entry's ImagePath (relative to the app folder when
        // inside it, same convention as the other file fields); a cancelled dialog changes nothing. Clear empties it,
        // which brings back the built-in default image. The main window picks both up through ApplyLayout's key.
        private void BtnBrowseMainBgFile_Click(object sender, RoutedEventArgs e)
        {
            if (GetEntry(LayerRegistry.MainBackground) is not LayerLayoutEntry entry) return;

            // Images plus mp4 (played by the main window's MediaElement; needs H.264, 8-bit yuv420p on a stock Windows install)
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Background Files (*.png;*.jpg;*.jpeg;*.bmp;*.mp4)|*.png;*.jpg;*.jpeg;*.bmp;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Video Files (*.mp4)|*.mp4|All files (*.*)|*.*",
                Title = "Select Main Window Background",
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

        // Empties the wallpaper path so the built-in default image shows
        private void BtnClearMainBgFile_Click(object sender, RoutedEventArgs e)
        {
            if (GetEntry(LayerRegistry.MainBackground) is not LayerLayoutEntry entry) return;

            entry.ImagePath = "";
            LoadSelectedLayerIntoFields();
        }

        // Turns the wallpaper off (solid Background Color only) or back on
        private void ChkMainBgImageDisabled_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (GetEntry(LayerRegistry.MainBackground) is not LayerLayoutEntry entry) return;

            entry.ImageDisabled = ChkMainBgImageDisabled.IsChecked == true;
            RaiseGuides();
        }

        // Background color picker and hex box (same helpers and revert rules as the Search & Buttons colors)
        private void MainBgColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(MainBgColorPicker, TxtMainBgColorHex, GetEntry(LayerRegistry.MainBackground), (en, hex) => en.BgColorHex = hex);
        private void TxtMainBgColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(MainBgColorPicker, TxtMainBgColorHex, GetEntry(LayerRegistry.MainBackground), en => en.BgColorHex ?? LayerRegistry.DefaultMainBackgroundColorHex, (en, hex) => en.BgColorHex = hex);
        // [END SECTION: Main Window Background Image]

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
            bool show = SingleSelectedLayer is LayerListItem sel && sel.Id == LayerRegistry.GamesList && GetEntry(LayerRegistry.GamesList) != null;
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

        private bool IsSearchButtonsSelected => SingleSelectedLayer is LayerListItem sel && sel.Id == LayerRegistry.SearchAndButtonsGroup;

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

        // [SECTION: Random Game Display Styling Fields]
        // Shown while the Random Game Display group is selected. The text style shared by both states (and the file)
        // lives on the RandomGameDisplay entry; each state's max text size, background color and border on its own
        // part, the latter two picked with the State combo on the Background and Border tabs. Colors, checkboxes and
        // combos apply immediately; the message and number fields apply on Enter or focus loss, and invalid numbers revert.
        private bool IsRandomDisplaySelected => SingleSelectedLayer is LayerListItem sel && sel.Id == LayerRegistry.RandomGameGroup;
        private LayerLayoutEntry? RGEntry => IsRandomDisplaySelected ? GetEntry(LayerRegistry.RandomGameDisplay) : null;
        private LayerLayoutEntry? RGSplashEntry => IsRandomDisplaySelected ? GetEntry(LayerRegistry.RandomGameDisplaySplash) : null;

        // The part the State combos pick (session-only; back to the first state whenever the group is selected again)
        private string _rgStatePartId = LayerRegistry.RandomGameDisplay;
        private LayerLayoutEntry? RGStateEntry => IsRandomDisplaySelected ? GetEntry(_rgStatePartId) : null;
        private static readonly (string PartId, string Label)[] RGStateChoices =
        {
            (LayerRegistry.RandomGameDisplay, "When a game is selected"),
            (LayerRegistry.RandomGameDisplaySplash, "When on the splash page")
        };

        // Fills the Text and Background tabs from the two parts' entries, and both State combos
        private void LoadRandomDisplayStyleFields()
        {
            var stateVisibility = IsRandomDisplaySelected ? Visibility.Visible : Visibility.Collapsed;
            PnlRGBorderState.Visibility = stateVisibility;
            PnlRGBgState.Visibility = stateVisibility;
            PnlRandomPreview.Visibility = stateVisibility;
            if (CboRandomPreviewState.Items.Count == 0)
            {
                bool wasLoading = _loading;
                _loading = true;
                foreach (var choice in RGStateChoices) CboRandomPreviewState.Items.Add(choice.Label);
                CboRandomPreviewState.SelectedIndex = 0;
                _loading = wasLoading;
            }
            if (RGEntry is not LayerLayoutEntry entry || RGSplashEntry is not LayerLayoutEntry splash || RGStateEntry is not LayerLayoutEntry stateEntry) return;

            _loading = true;

            foreach (var combo in new[] { CboRGBorderState, CboRGBgState })
            {
                if (combo.Items.Count == 0)
                {
                    foreach (var choice in RGStateChoices) combo.Items.Add(choice.Label);
                }
                combo.SelectedIndex = Math.Max(0, Array.FindIndex(RGStateChoices, c => c.PartId == _rgStatePartId));
            }

            TxtRGMessage.Text = entry.TextMessage ?? LayerRegistry.DefaultRandomDisplayMessage;

            if (CboRGFont.Items.Count == 0)
            {
                foreach (string font in LayerRegistry.AvailableFonts) CboRGFont.Items.Add(font);
            }
            string family = string.IsNullOrWhiteSpace(entry.TextFontFamily) ? LayerRegistry.DefaultFontFamily : entry.TextFontFamily;
            CboRGFont.SelectedItem = LayerRegistry.AvailableFonts.Contains(family) ? family : LayerRegistry.DefaultFontFamily;
            ChkRGBold.IsChecked = entry.TextBold ?? LayerRegistry.DefaultRandomDisplayBold;

            ShowColor(RGTextColorPicker, TxtRGTextColorHex, entry.TextColorHex ?? LayerRegistry.DefaultRandomDisplayColorHex);
            if (CboRGAlign.Items.Count == 0)
            {
                foreach (string align in TextAlignChoices) CboRGAlign.Items.Add(align);
            }
            string alignment = entry.TextAlign ?? LayerRegistry.DefaultRandomDisplayAlign;
            CboRGAlign.SelectedItem = TextAlignChoices.Contains(alignment) ? alignment : LayerRegistry.DefaultRandomDisplayAlign;
            ChkRGWrap.IsChecked = entry.TextWrap ?? LayerRegistry.DefaultRandomDisplayWrap;

            TxtRGFontSize.Text = (entry.TextFontSize ?? LayerRegistry.DefaultRandomDisplayFontSize).ToString(CultureInfo.InvariantCulture);
            TxtRGSplashFontSize.Text = (splash.TextFontSize ?? LayerRegistry.DefaultRandomDisplaySplashFontSize).ToString(CultureInfo.InvariantCulture);

            ChkRGShadow.IsChecked = entry.TextShadowEnabled ?? LayerRegistry.DefaultShadowEnabled;
            ShowColor(RGShadowColorPicker, TxtRGShadowColorHex, entry.TextShadowColorHex ?? LayerRegistry.DefaultShadowColorHex);
            TxtRGShadowBlur.Text = (entry.TextShadowBlur ?? LayerRegistry.DefaultShadowBlur).ToString(CultureInfo.InvariantCulture);
            TxtRGShadowDepth.Text = (entry.TextShadowDepth ?? LayerRegistry.DefaultShadowDepth).ToString(CultureInfo.InvariantCulture);

            ShowColor(RGBgColorPicker, TxtRGBgColorHex, stateEntry.BgColorHex ?? LayerRegistry.DefaultRandomDisplayBgColorHex);
            TxtRGFile.Text = entry.ImagePath ?? "";

            _loading = false;
        }

        // Card preview: the checkbox turns it on or off, the combo picks the state (live while on)
        private void ChkRandomPreview_Click(object sender, RoutedEventArgs e) => RaiseRandomPreview();

        private void CboRandomPreviewState_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || ChkRandomPreview.IsChecked != true) return;
            RaiseRandomPreview();
        }

        // Sends the checkbox and combo state to the main window (through ThemeBuilderWindow)
        private void RaiseRandomPreview()
        {
            bool fromSplash = CboRandomPreviewState.SelectedIndex == 1;
            RandomPreviewChanged?.Invoke(ChkRandomPreview.IsChecked == true, fromSplash);
        }

        // Turns both Theme Builder previews off (the Random Game Display card and the Marquee banner)
        public void StopPreviews()
        {
            StopRandomPreview();
            StopBannerPreview();
        }

        // Turns the card preview off (unchecks the box and tells the main window). Safe to call when it is already off.
        public void StopRandomPreview()
        {
            if (ChkRandomPreview.IsChecked != true) return;
            ChkRandomPreview.IsChecked = false;
            RandomPreviewChanged?.Invoke(false, false);
        }

        // Unchecks the box after the main window already ended the preview (a real spin started); raises nothing
        public void ClearRandomPreview()
        {
            ChkRandomPreview.IsChecked = false;
        }

        // Switches which state the Border tab and the Background tab's color row edit (both combos stay in step)
        private void CboRGState_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null || sender is not ComboBox combo) return;
            int index = combo.SelectedIndex;
            if (index < 0 || index >= RGStateChoices.Length) return;

            _rgStatePartId = RGStateChoices[index].PartId;
            LoadSelectedLayerIntoFields();
        }

        // Message: trimmed and capped at 60 characters; empty means no text (Reset Layer brings back the default)
        private void TxtRGMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyRandomDisplayMessage();
        }

        private void TxtRGMessage_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyRandomDisplayMessage();
        }

        // Stores the typed message on the RandomGameDisplay entry and re-applies the layout
        private void ApplyRandomDisplayMessage()
        {
            if (_loading || _settings == null || RGEntry is not LayerLayoutEntry entry) return;

            string message = TxtRGMessage.Text.Trim();
            if (message.Length > LayerRegistry.MaxRandomDisplayMessageLength) message = message.Substring(0, LayerRegistry.MaxRandomDisplayMessageLength);
            entry.TextMessage = message;
            LoadSelectedLayerIntoFields();
        }

        // Font, bold, alignment, wrap and shadow toggle
        private void CboRGFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (CboRGFont.SelectedItem is not string family || RGEntry is not LayerLayoutEntry entry) return;

            entry.TextFontFamily = family;
            RaiseGuides();
        }

        private void ChkRGBold_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null || RGEntry is not LayerLayoutEntry entry) return;

            entry.TextBold = ChkRGBold.IsChecked == true;
            RaiseGuides();
        }

        private void CboRGAlign_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (CboRGAlign.SelectedItem is not string align || RGEntry is not LayerLayoutEntry entry) return;

            entry.TextAlign = align;
            RaiseGuides();
        }

        private void ChkRGWrap_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null || RGEntry is not LayerLayoutEntry entry) return;

            entry.TextWrap = ChkRGWrap.IsChecked == true;
            RaiseGuides();
        }

        private void ChkRGShadow_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null || RGEntry is not LayerLayoutEntry entry) return;

            entry.TextShadowEnabled = ChkRGShadow.IsChecked == true;
            RaiseGuides();
        }

        // Color pickers and hex boxes (text and shadow on the shared entry, background on the State combo's part)
        private void RGTextColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(RGTextColorPicker, TxtRGTextColorHex, RGEntry, (en, hex) => en.TextColorHex = hex);
        private void TxtRGTextColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(RGTextColorPicker, TxtRGTextColorHex, RGEntry, en => en.TextColorHex ?? LayerRegistry.DefaultRandomDisplayColorHex, (en, hex) => en.TextColorHex = hex);

        private void RGShadowColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(RGShadowColorPicker, TxtRGShadowColorHex, RGEntry, (en, hex) => en.TextShadowColorHex = hex);
        private void TxtRGShadowColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(RGShadowColorPicker, TxtRGShadowColorHex, RGEntry, en => en.TextShadowColorHex ?? LayerRegistry.DefaultShadowColorHex, (en, hex) => en.TextShadowColorHex = hex);

        private void RGBgColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(RGBgColorPicker, TxtRGBgColorHex, RGStateEntry, (en, hex) => en.BgColorHex = hex);
        private void TxtRGBgColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(RGBgColorPicker, TxtRGBgColorHex, RGStateEntry, en => en.BgColorHex ?? LayerRegistry.DefaultRandomDisplayBgColorHex, (en, hex) => en.BgColorHex = hex);

        // One shared handler pair for the number fields: the two max text sizes (6-200) and the shadow's blur (0-30) and depth (0-20)
        private void RGField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyRandomDisplayFields();
        }

        private void RGField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyRandomDisplayFields();
        }

        // Stores each valid number field on its entry; an invalid one keeps its stored value
        private void ApplyRandomDisplayFields()
        {
            if (_loading || _settings == null) return;
            if (RGEntry is not LayerLayoutEntry entry || RGSplashEntry is not LayerLayoutEntry splash) return;

            if (int.TryParse(TxtRGFontSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int size)
                && size >= LayerRegistry.MinRandomDisplayFontSize && size <= LayerRegistry.MaxRandomDisplayFontSize)
                entry.TextFontSize = size;
            if (int.TryParse(TxtRGSplashFontSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int splashSize)
                && splashSize >= LayerRegistry.MinRandomDisplayFontSize && splashSize <= LayerRegistry.MaxRandomDisplayFontSize)
                splash.TextFontSize = splashSize;
            if (int.TryParse(TxtRGShadowBlur.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int blur) && blur >= 0 && blur <= 30)
                entry.TextShadowBlur = blur;
            if (int.TryParse(TxtRGShadowDepth.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int depth) && depth >= 0 && depth <= 20)
                entry.TextShadowDepth = depth;

            // Re-display the stored values (reverts invalid input) and re-apply the layout
            LoadSelectedLayerIntoFields();
        }

        // Picks the spin card's file (images, gif or mp4), stored on the RandomGameDisplay entry and shared by both states
        private void BtnBrowseRGFile_Click(object sender, RoutedEventArgs e)
        {
            if (RGEntry is not LayerLayoutEntry entry) return;

            string? storedPath = BrowseLayerFile(
                "Image and Video Files (*.png;*.jpg;*.jpeg;*.gif;*.mp4)|*.png;*.jpg;*.jpeg;*.gif;*.mp4|Image Files (*.png;*.jpg;*.jpeg;*.gif)|*.png;*.jpg;*.jpeg;*.gif|Video Files (*.mp4)|*.mp4|All files (*.*)|*.*",
                "Select Image or Video for the Random Game Display");
            if (storedPath == null) return;

            entry.ImagePath = storedPath;
            LoadSelectedLayerIntoFields();
        }

        // Empties the spin card's file (the background color and message still show)
        private void BtnClearRGFile_Click(object sender, RoutedEventArgs e)
        {
            if (RGEntry is not LayerLayoutEntry entry) return;

            entry.ImagePath = "";
            LoadSelectedLayerIntoFields();
        }
        // [END SECTION: Random Game Display Styling Fields]

        // [SECTION: Marquee Banner Styling Fields]
        // Shown while the Marquee layer is selected (BANNER tab). The no-art title card's style lives on the MarqueePanel
        // entry: font, bold, max size, color, wrap, the one-line text margin (BannerTextMargin), background, border and the
        // title's own shadow (BannerShadow*).
        // Colors, checkboxes and combos apply immediately; number fields apply on Enter or focus loss and revert invalid input.
        private LayerLayoutEntry? BannerEntry => SingleSelectedLayer is LayerListItem sel && sel.Id == LayerRegistry.MarqueePanel ? GetEntry(LayerRegistry.MarqueePanel) : null;

        // Fills the Banner tab from the Marquee entry
        private void LoadMarqueeBannerFields()
        {
            if (BannerEntry is not LayerLayoutEntry entry) return;

            _loading = true;

            if (CboBannerFont.Items.Count == 0)
            {
                foreach (string font in LayerRegistry.AvailableFonts) CboBannerFont.Items.Add(font);
            }
            string family = string.IsNullOrWhiteSpace(entry.TextFontFamily) ? LayerRegistry.DefaultFontFamily : entry.TextFontFamily;
            CboBannerFont.SelectedItem = LayerRegistry.AvailableFonts.Contains(family) ? family : LayerRegistry.DefaultFontFamily;
            ChkBannerBold.IsChecked = entry.TextBold ?? LayerRegistry.DefaultBannerBold;

            TxtBannerFontSize.Text = (entry.TextFontSize ?? LayerRegistry.DefaultBannerFontSize).ToString(CultureInfo.InvariantCulture);
            ShowColor(BannerColorPicker, TxtBannerColorHex, entry.TextColorHex ?? LayerRegistry.DefaultBannerColorHex);
            ChkBannerWrap.IsChecked = entry.TextWrap ?? LayerRegistry.DefaultBannerWrap;
            TxtBannerTextMargin.Text = (entry.BannerTextMargin ?? LayerRegistry.DefaultBannerTextMargin).ToString(CultureInfo.InvariantCulture);
            UpdateBannerTextMarginEnabled();
            ShowColor(BannerBgColorPicker, TxtBannerBgColorHex, entry.BgColorHex ?? LayerRegistry.DefaultBannerBgColorHex);

            ChkBannerShadow.IsChecked = entry.BannerShadowEnabled ?? LayerRegistry.DefaultShadowEnabled;
            ShowColor(BannerShadowColorPicker, TxtBannerShadowColorHex, entry.BannerShadowColorHex ?? LayerRegistry.DefaultShadowColorHex);
            TxtBannerShadowBlur.Text = (entry.BannerShadowBlur ?? LayerRegistry.DefaultShadowBlur).ToString(CultureInfo.InvariantCulture);
            TxtBannerShadowDepth.Text = (entry.BannerShadowDepth ?? LayerRegistry.DefaultShadowDepth).ToString(CultureInfo.InvariantCulture);

            ShowColor(BannerBorderColorPicker, TxtBannerBorderColorHex, entry.BorderColorHex ?? LayerRegistry.DefaultBannerBorderColorHex);
            TxtBannerBorderSize.Text = (entry.BorderSize ?? LayerRegistry.DefaultBannerBorderSize).ToString(CultureInfo.InvariantCulture);
            TxtBannerCornerRadius.Text = (entry.CornerRadius ?? LayerRegistry.DefaultBannerCornerRadius).ToString(CultureInfo.InvariantCulture);

            _loading = false;
        }

        // Banner preview: the checkbox turns it on or off on the main window (session-only)
        private void ChkBannerPreview_Click(object sender, RoutedEventArgs e) => BannerPreviewChanged?.Invoke(ChkBannerPreview.IsChecked == true);

        // Turns the banner preview off (unchecks the box and tells the main window). Safe to call when it is already off.
        public void StopBannerPreview()
        {
            if (ChkBannerPreview.IsChecked != true) return;
            ChkBannerPreview.IsChecked = false;
            BannerPreviewChanged?.Invoke(false);
        }

        // Unchecks the box after the main window already ended the preview (a real spin started); raises nothing
        public void ClearBannerPreview()
        {
            ChkBannerPreview.IsChecked = false;
        }

        // Font, bold, wrap and shadow toggle
        private void CboBannerFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if (CboBannerFont.SelectedItem is not string family || BannerEntry is not LayerLayoutEntry entry) return;

            entry.TextFontFamily = family;
            RaiseGuides();
        }

        private void ChkBannerBold_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null || BannerEntry is not LayerLayoutEntry entry) return;

            entry.TextBold = ChkBannerBold.IsChecked == true;
            RaiseGuides();
        }

        // Does the wrap toggle: stores it and enables the text margin box only for a one-line title
        private void ChkBannerWrap_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null || BannerEntry is not LayerLayoutEntry entry) return;

            entry.TextWrap = ChkBannerWrap.IsChecked == true;
            UpdateBannerTextMarginEnabled();
            RaiseGuides();
        }

        // Does the enable state of the text margin box: on only while Wrap text is off (the margin is one-line only)
        private void UpdateBannerTextMarginEnabled() => TxtBannerTextMargin.IsEnabled = ChkBannerWrap.IsChecked != true;

        private void ChkBannerShadow_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null || BannerEntry is not LayerLayoutEntry entry) return;

            entry.BannerShadowEnabled = ChkBannerShadow.IsChecked == true;
            RaiseGuides();
        }

        // Color pickers and hex boxes (text, background, text shadow, border)
        private void BannerColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(BannerColorPicker, TxtBannerColorHex, BannerEntry, (en, hex) => en.TextColorHex = hex);
        private void TxtBannerColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(BannerColorPicker, TxtBannerColorHex, BannerEntry, en => en.TextColorHex ?? LayerRegistry.DefaultBannerColorHex, (en, hex) => en.TextColorHex = hex);

        private void BannerBgColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(BannerBgColorPicker, TxtBannerBgColorHex, BannerEntry, (en, hex) => en.BgColorHex = hex);
        private void TxtBannerBgColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(BannerBgColorPicker, TxtBannerBgColorHex, BannerEntry, en => en.BgColorHex ?? LayerRegistry.DefaultBannerBgColorHex, (en, hex) => en.BgColorHex = hex);

        private void BannerShadowColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(BannerShadowColorPicker, TxtBannerShadowColorHex, BannerEntry, (en, hex) => en.BannerShadowColorHex = hex);
        private void TxtBannerShadowColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(BannerShadowColorPicker, TxtBannerShadowColorHex, BannerEntry, en => en.BannerShadowColorHex ?? LayerRegistry.DefaultShadowColorHex, (en, hex) => en.BannerShadowColorHex = hex);

        private void BannerBorderColorPicker_ColorChanged(object sender, RoutedEventArgs e) =>
            ApplyPickerColor(BannerBorderColorPicker, TxtBannerBorderColorHex, BannerEntry, (en, hex) => en.BorderColorHex = hex);
        private void TxtBannerBorderColorHex_LostFocus(object sender, RoutedEventArgs e) =>
            ApplyHexColor(BannerBorderColorPicker, TxtBannerBorderColorHex, BannerEntry, en => en.BorderColorHex ?? LayerRegistry.DefaultBannerBorderColorHex, (en, hex) => en.BorderColorHex = hex);

        // One shared handler pair for the number fields: max size (6-200), shadow blur (0-30) and depth (0-20), border size (0-50) and radius (0-200)
        private void BannerField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyBannerFields();
        }

        private void BannerField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyBannerFields();
        }

        // Stores each valid number field on the Marquee entry; an invalid one keeps its stored value
        private void ApplyBannerFields()
        {
            if (_loading || _settings == null || BannerEntry is not LayerLayoutEntry entry) return;

            if (int.TryParse(TxtBannerFontSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int size)
                && size >= LayerRegistry.MinBannerFontSize && size <= LayerRegistry.MaxBannerFontSize)
                entry.TextFontSize = size;
            if (int.TryParse(TxtBannerShadowBlur.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int blur) && blur >= 0 && blur <= 30)
                entry.BannerShadowBlur = blur;
            if (int.TryParse(TxtBannerShadowDepth.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int depth) && depth >= 0 && depth <= 20)
                entry.BannerShadowDepth = depth;
            if (int.TryParse(TxtBannerBorderSize.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int border) && border >= 0 && border <= 50)
                entry.BorderSize = border;
            if (int.TryParse(TxtBannerCornerRadius.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int radius) && radius >= 0 && radius <= 200)
                entry.CornerRadius = radius;
            if (int.TryParse(TxtBannerTextMargin.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int textMargin)
                && textMargin >= LayerRegistry.MinBannerTextMargin && textMargin <= LayerRegistry.MaxBannerTextMargin)
                entry.BannerTextMargin = textMargin;

            // Re-display the stored values (reverts invalid input) and re-apply the layout
            LoadSelectedLayerIntoFields();
        }
        // [END SECTION: Marquee Banner Styling Fields]

        // [SECTION: Game Info Styling Fields]
        // Six Game Info text blocks and two image-shadow blocks share one set of handlers: each control is named
        // GI{Block}{Control} and carries its block key in Tag, and GIBlocks maps the key to the entry, which field
        // set it uses (generic Text* or the body entry's SectionHeader*), its defaults and size range, and the list
        // row that must be selected for it (the Game Info group, or the Marquee layer for the Marquee block).
        // Colors, checkboxes and combos apply immediately; number fields apply on Enter or focus loss and revert
        // invalid input. The two navs have no font or bold controls (they are shapes); the image-shadow blocks
        // (Previews, Marquee) have only the shadow controls.

        // One block's storage: the entry it lives on, whether it uses the SectionHeader* fields, and its defaults.
        // SelectUnit is the list row that shows the block's tab; ShadowOnly blocks have no size or color controls.
        // DefaultAlign set = the block has a GI{Block}Align combo (Left / Center / Right, stored in Text* TextAlign).
        // HasShadowTarget = the block has a GI{Block}ShadowTarget combo (Both / Logo only / Marquees only, ShadowTarget).
        // DefaultRightMargin set = the block has a GI{Block}RightMargin box (text to scrollbar gap, stored in Text* TextRightMargin).
        private sealed record GIBlockDef(string EntryId, bool UsesSectionHeader, bool HasFont, string DefaultFamily, bool DefaultBold,
            int DefaultSize, int MinSize, int MaxSize, bool DefaultShadowEnabled, string DefaultShadowColorHex, int DefaultShadowBlur, int DefaultShadowDepth)
        {
            public string SelectUnit { get; init; } = LayerRegistry.GameInfoGroup;
            public bool ShadowOnly { get; init; }
            public string? DefaultAlign { get; init; }
            public bool HasShadowTarget { get; init; }
            public int? DefaultRightMargin { get; init; }
        }

        // The Marquee shadow target combo: stored value and the label shown for it, in combo order
        private static readonly (string Value, string Label)[] ShadowTargetChoices =
        {
            (LayerRegistry.ShadowTargetBoth, "Both"),
            (LayerRegistry.ShadowTargetLogo, "Logo only"),
            (LayerRegistry.ShadowTargetMarquees, "Marquees only")
        };

        private static readonly Dictionary<string, GIBlockDef> GIBlocks = new()
        {
            ["Title"] = new(LayerRegistry.GameTitle, false, true, LayerRegistry.DefaultFontFamily, LayerRegistry.DefaultTitleBold,
                LayerRegistry.DefaultTitleFontSize, 6, 72, LayerRegistry.DefaultTitleShadowEnabled, LayerRegistry.DefaultTitleShadowColorHex, LayerRegistry.DefaultTitleShadowBlur, LayerRegistry.DefaultTitleShadowDepth),
            ["Publisher"] = new(LayerRegistry.PublisherBlock, false, true, LayerRegistry.DefaultFontFamily, LayerRegistry.DefaultPublisherBold,
                LayerRegistry.DefaultPublisherFontSize, 6, 72, LayerRegistry.DefaultShadowEnabled, LayerRegistry.DefaultShadowColorHex, LayerRegistry.DefaultShadowBlur, LayerRegistry.DefaultShadowDepth) { DefaultAlign = LayerRegistry.DefaultPublisherAlign },
            ["InfoHeader"] = new(LayerRegistry.GameInfoBody, true, true, LayerRegistry.DefaultFontFamily, LayerRegistry.DefaultInfoHeaderBold,
                LayerRegistry.DefaultInfoHeaderFontSize, 6, 72, LayerRegistry.DefaultShadowEnabled, LayerRegistry.DefaultShadowColorHex, LayerRegistry.DefaultShadowBlur, LayerRegistry.DefaultShadowDepth),
            ["InfoBody"] = new(LayerRegistry.GameInfoBody, false, true, LayerRegistry.DefaultInfoBodyFontFamily, LayerRegistry.DefaultInfoBodyBold,
                LayerRegistry.DefaultInfoBodyFontSize, 6, 72, LayerRegistry.DefaultShadowEnabled, LayerRegistry.DefaultShadowColorHex, LayerRegistry.DefaultShadowBlur, LayerRegistry.DefaultShadowDepth) { DefaultRightMargin = LayerRegistry.DefaultInfoBodyRightMargin },
            ["Nav"] = new(LayerRegistry.GameInfoNav, false, false, LayerRegistry.DefaultFontFamily, false,
                LayerRegistry.DefaultNavSize, LayerRegistry.MinNavSize, LayerRegistry.MaxNavSize, LayerRegistry.DefaultShadowEnabled, LayerRegistry.DefaultShadowColorHex, LayerRegistry.DefaultShadowBlur, LayerRegistry.DefaultShadowDepth),
            ["PreviewsNav"] = new(LayerRegistry.ImagePreviewsNav, false, false, LayerRegistry.DefaultFontFamily, false,
                LayerRegistry.DefaultNavSize, LayerRegistry.MinNavSize, LayerRegistry.MaxNavSize, LayerRegistry.DefaultShadowEnabled, LayerRegistry.DefaultShadowColorHex, LayerRegistry.DefaultShadowBlur, LayerRegistry.DefaultShadowDepth),
            // Image shadows: the Image Previews (Game Info group, PREVIEWS tab) and the Marquee layer (its own SHADOW tab)
            ["Previews"] = new(LayerRegistry.ImagePreviewsBody, false, false, LayerRegistry.DefaultFontFamily, false,
                0, 0, 0, LayerRegistry.DefaultShadowEnabled, LayerRegistry.DefaultShadowColorHex, LayerRegistry.DefaultShadowBlur, LayerRegistry.DefaultShadowDepth) { ShadowOnly = true },
            ["Marquee"] = new(LayerRegistry.MarqueePanel, false, false, LayerRegistry.DefaultFontFamily, false,
                0, 0, 0, LayerRegistry.DefaultShadowEnabled, LayerRegistry.DefaultShadowColorHex, LayerRegistry.DefaultShadowBlur, LayerRegistry.DefaultShadowDepth) { ShadowOnly = true, SelectUnit = LayerRegistry.MarqueePanel, HasShadowTarget = true },
        };

        // Reads and writes one block's style on its entry, picking the Text* or SectionHeader* fields
        private sealed class GIStyle
        {
            private readonly LayerLayoutEntry _e;
            private readonly bool _h;
            public GIStyle(LayerLayoutEntry entry, bool usesSectionHeader) { _e = entry; _h = usesSectionHeader; }

            public string? FontFamily { get => _h ? _e.SectionHeaderFontFamily : _e.TextFontFamily; set { if (_h) _e.SectionHeaderFontFamily = value; else _e.TextFontFamily = value; } }
            public bool? Bold { get => _h ? _e.SectionHeaderBold : _e.TextBold; set { if (_h) _e.SectionHeaderBold = value; else _e.TextBold = value; } }
            public int? FontSize { get => _h ? _e.SectionHeaderFontSize : _e.TextFontSize; set { if (_h) _e.SectionHeaderFontSize = value; else _e.TextFontSize = value; } }
            public string? ColorHex { get => _h ? _e.SectionHeaderColorHex : _e.TextColorHex; set { if (_h) _e.SectionHeaderColorHex = value; else _e.TextColorHex = value; } }
            public bool? ShadowEnabled { get => _h ? _e.SectionHeaderShadowEnabled : _e.TextShadowEnabled; set { if (_h) _e.SectionHeaderShadowEnabled = value; else _e.TextShadowEnabled = value; } }
            public string? ShadowColorHex { get => _h ? _e.SectionHeaderShadowColorHex : _e.TextShadowColorHex; set { if (_h) _e.SectionHeaderShadowColorHex = value; else _e.TextShadowColorHex = value; } }
            public int? ShadowBlur { get => _h ? _e.SectionHeaderShadowBlur : _e.TextShadowBlur; set { if (_h) _e.SectionHeaderShadowBlur = value; else _e.TextShadowBlur = value; } }
            public int? ShadowDepth { get => _h ? _e.SectionHeaderShadowDepth : _e.TextShadowDepth; set { if (_h) _e.SectionHeaderShadowDepth = value; else _e.TextShadowDepth = value; } }
            // Text* only: there is no SectionHeader alignment field yet (add one before giving the headers an Align combo)
            public string? TextAlign { get => _e.TextAlign; set => _e.TextAlign = value; }
            // Marquee layer only: which images its shadow applies to
            public string? ShadowTarget { get => _e.ShadowTarget; set => _e.ShadowTarget = value; }
            // Text* only (the Info body): the gap between the text and the scrollbar
            public int? RightMargin { get => _e.TextRightMargin; set => _e.TextRightMargin = value; }
        }

        // Does the gating check: true while the block's list row (Game Info group or Marquee layer) is selected
        private bool IsBlockSelected(GIBlockDef def) => SingleSelectedLayer is LayerListItem sel && sel.Id == def.SelectUnit;

        // Returns the block's style accessor for the current layout (null when the block's row isn't selected)
        private GIStyle? GetGIStyle(string? block) =>
            block != null && GIBlocks.TryGetValue(block, out var def) && IsBlockSelected(def) && GetEntry(def.EntryId) is LayerLayoutEntry entry
                ? new GIStyle(entry, def.UsesSectionHeader)
                : null;

        // Finds a block's control by the GI{Block}{Control} naming convention (null for controls a block doesn't have)
        private T? GIControl<T>(string block, string control) where T : class => FindName($"GI{block}{control}") as T;

        private static string? GIBlockKey(object sender) => (sender as FrameworkElement)?.Tag as string;

        // Fills the Title, Publisher, Info, Nav and Previews tabs from the Game Info entries (group selected)
        private void LoadGameInfoStyleFields() => LoadGIBlockFields(LayerRegistry.GameInfoGroup);

        // Fills the Marquee layer's SHADOW tab (Marquee layer selected)
        private void LoadMarqueeShadowFields() => LoadGIBlockFields(LayerRegistry.MarqueePanel);

        // Does the fill for every block shown by one list row; does nothing unless that row is selected
        private void LoadGIBlockFields(string selectUnit)
        {
            _loading = true;
            foreach (var (block, def) in GIBlocks)
            {
                if (def.SelectUnit != selectUnit || GetGIStyle(block) is not GIStyle style) continue;

                if (def.HasFont && GIControl<ComboBox>(block, "Font") is ComboBox fontCombo && GIControl<CheckBox>(block, "Bold") is CheckBox boldCheck)
                {
                    if (fontCombo.Items.Count == 0)
                    {
                        foreach (string font in LayerRegistry.AvailableFonts) fontCombo.Items.Add(font);
                    }
                    string family = string.IsNullOrWhiteSpace(style.FontFamily) ? def.DefaultFamily : style.FontFamily;
                    fontCombo.SelectedItem = LayerRegistry.AvailableFonts.Contains(family) ? family : def.DefaultFamily;
                    boldCheck.IsChecked = style.Bold ?? def.DefaultBold;
                }

                if (def.DefaultAlign != null && GIControl<ComboBox>(block, "Align") is ComboBox alignCombo)
                {
                    if (alignCombo.Items.Count == 0)
                    {
                        foreach (string align in TextAlignChoices) alignCombo.Items.Add(align);
                    }
                    string alignment = style.TextAlign ?? def.DefaultAlign;
                    alignCombo.SelectedItem = TextAlignChoices.Contains(alignment) ? alignment : def.DefaultAlign;
                }

                if (def.HasShadowTarget && GIControl<ComboBox>(block, "ShadowTarget") is ComboBox targetCombo)
                {
                    if (targetCombo.Items.Count == 0)
                    {
                        foreach (var choice in ShadowTargetChoices) targetCombo.Items.Add(choice.Label);
                    }
                    int targetIndex = Array.FindIndex(ShadowTargetChoices, c => c.Value == style.ShadowTarget);
                    targetCombo.SelectedIndex = targetIndex >= 0 ? targetIndex : 0; // unknown or blank = Both
                }

                if (def.DefaultRightMargin is int defaultRightMargin && GIControl<TextBox>(block, "RightMargin") is TextBox rightMarginBox)
                    rightMarginBox.Text = (style.RightMargin ?? defaultRightMargin).ToString(CultureInfo.InvariantCulture);

                if (!def.ShadowOnly)
                {
                    GIControl<TextBox>(block, "Size")!.Text = (style.FontSize ?? def.DefaultSize).ToString(CultureInfo.InvariantCulture);
                    ShowColor(GIControl<ColorPicker.PortableColorPicker>(block, "ColorPicker")!, GIControl<TextBox>(block, "ColorHex")!,
                        style.ColorHex ?? LayerRegistry.DefaultGameInfoTextColorHex);
                }
                GIControl<CheckBox>(block, "Shadow")!.IsChecked = style.ShadowEnabled ?? def.DefaultShadowEnabled;
                ShowColor(GIControl<ColorPicker.PortableColorPicker>(block, "ShadowColorPicker")!, GIControl<TextBox>(block, "ShadowColorHex")!,
                    style.ShadowColorHex ?? def.DefaultShadowColorHex);
                GIControl<TextBox>(block, "ShadowBlur")!.Text = (style.ShadowBlur ?? def.DefaultShadowBlur).ToString(CultureInfo.InvariantCulture);
                GIControl<TextBox>(block, "ShadowDepth")!.Text = (style.ShadowDepth ?? def.DefaultShadowDepth).ToString(CultureInfo.InvariantCulture);
            }
            _loading = false;
        }

        // Font family, bold and shadow toggle (any block, resolved from the sender's Tag)
        private void GIFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if ((sender as ComboBox)?.SelectedItem is not string family || GetGIStyle(GIBlockKey(sender)) is not GIStyle style) return;

            style.FontFamily = family;
            RaiseGuides();
        }

        // Does the text alignment combo (any block with DefaultAlign, resolved from the sender's Tag)
        private void GIAlign_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null) return;
            if ((sender as ComboBox)?.SelectedItem is not string align || GetGIStyle(GIBlockKey(sender)) is not GIStyle style) return;

            style.TextAlign = align;
            RaiseGuides();
        }

        // Does the shadow target combo (any block with HasShadowTarget, resolved from the sender's Tag)
        private void GIShadowTarget_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _settings == null || GetGIStyle(GIBlockKey(sender)) is not GIStyle style) return;
            int index = (sender as ComboBox)?.SelectedIndex ?? -1;
            if (index < 0 || index >= ShadowTargetChoices.Length) return;

            style.ShadowTarget = ShadowTargetChoices[index].Value;
            RaiseGuides();
        }

        private void GIBold_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null || GetGIStyle(GIBlockKey(sender)) is not GIStyle style) return;

            style.Bold = (sender as CheckBox)?.IsChecked == true;
            RaiseGuides();
        }

        private void GIShadow_Click(object sender, RoutedEventArgs e)
        {
            if (_loading || _settings == null || GetGIStyle(GIBlockKey(sender)) is not GIStyle style) return;

            style.ShadowEnabled = (sender as CheckBox)?.IsChecked == true;
            RaiseGuides();
        }

        // Text color and shadow color pickers and hex boxes (any block). The entry passed to the shared helpers is
        // the block's own entry; the store/stored lambdas go through GIStyle so header colors land in SectionHeader*.
        private void GIColorPicker_ColorChanged(object sender, RoutedEventArgs e) => ApplyGIColor(GIBlockKey(sender), "", fromPicker: true);
        private void GIColorHex_LostFocus(object sender, RoutedEventArgs e) => ApplyGIColor(GIBlockKey(sender), "", fromPicker: false);
        private void GIShadowColorPicker_ColorChanged(object sender, RoutedEventArgs e) => ApplyGIColor(GIBlockKey(sender), "Shadow", fromPicker: true);
        private void GIShadowColorHex_LostFocus(object sender, RoutedEventArgs e) => ApplyGIColor(GIBlockKey(sender), "Shadow", fromPicker: false);

        // Applies one block's text color (prefix "") or shadow color (prefix "Shadow") from its picker or hex box
        private void ApplyGIColor(string? block, string prefix, bool fromPicker)
        {
            if (block == null || !GIBlocks.TryGetValue(block, out var def)) return;
            if (GIControl<ColorPicker.PortableColorPicker>(block, prefix + "ColorPicker") is not ColorPicker.PortableColorPicker picker
                || GIControl<TextBox>(block, prefix + "ColorHex") is not TextBox hexBox) return;

            LayerLayoutEntry? entry = IsBlockSelected(def) ? GetEntry(def.EntryId) : null;
            bool shadow = prefix == "Shadow";
            void Store(LayerLayoutEntry en, string hex)
            {
                var style = new GIStyle(en, def.UsesSectionHeader);
                if (shadow) style.ShadowColorHex = hex; else style.ColorHex = hex;
            }
            string Stored(LayerLayoutEntry en)
            {
                var style = new GIStyle(en, def.UsesSectionHeader);
                return shadow ? style.ShadowColorHex ?? def.DefaultShadowColorHex : style.ColorHex ?? LayerRegistry.DefaultGameInfoTextColorHex;
            }

            if (fromPicker) ApplyPickerColor(picker, hexBox, entry, Store);
            else ApplyHexColor(picker, hexBox, entry, Stored, Store);
        }

        // One shared handler pair for every block's number fields: size (6-72 for text, 4-40 for the navs),
        // shadow blur (0-30) and depth (0-20)
        private void GIField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyGameInfoFields();
        }

        private void GIField_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyGameInfoFields();
        }

        // Stores each valid number field of every block shown by the selected row on its entry; an invalid one keeps
        // its stored value
        private void ApplyGameInfoFields()
        {
            if (_loading || _settings == null || !GIBlocks.Values.Any(IsBlockSelected)) return;

            foreach (var (block, def) in GIBlocks)
            {
                if (GetGIStyle(block) is not GIStyle style) continue;

                if (!def.ShadowOnly && int.TryParse(GIControl<TextBox>(block, "Size")!.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int size) && size >= def.MinSize && size <= def.MaxSize)
                    style.FontSize = size;
                if (int.TryParse(GIControl<TextBox>(block, "ShadowBlur")!.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int blur) && blur >= 0 && blur <= 30)
                    style.ShadowBlur = blur;
                if (int.TryParse(GIControl<TextBox>(block, "ShadowDepth")!.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int depth) && depth >= 0 && depth <= 20)
                    style.ShadowDepth = depth;
                if (def.DefaultRightMargin != null && GIControl<TextBox>(block, "RightMargin") is TextBox rightMarginBox
                    && int.TryParse(rightMarginBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rightMargin)
                    && rightMargin >= LayerRegistry.MinInfoBodyRightMargin && rightMargin <= LayerRegistry.MaxInfoBodyRightMargin)
                    style.RightMargin = rightMargin;
            }

            // Re-display the stored values (reverts invalid input) and re-apply the layout
            LoadSelectedLayerIntoFields();
        }
        // [END SECTION: Game Info Styling Fields]
        // [END SECTION: Layer Fields]

        // [SECTION: Alignment Guide Preferences]
        private void ChkShowGuides_Click(object sender, RoutedEventArgs e)
        {
            _settings.ShowGuideLines = ChkShowGuides.IsChecked == true;
            RaiseGuides();
        }

        // Toggles the layer center cross (independent of Edge guides) and redraws
        private void ChkCenterGuides_Click(object sender, RoutedEventArgs e)
        {
            _settings.ShowCenterGuide = ChkCenterGuides.IsChecked == true;
            RaiseGuides();
        }

        // Stores the picked guide color in settings and redraws the guides
        private void GuideColorPicker_ColorChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressGuideSync || _settings == null) return;

            var c = GuideColorPicker.SelectedColor;
            string hex = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            _settings.GuideLineColorHex = hex;
            RaiseGuides();
        }
        // [END SECTION: Alignment Guide Preferences]

        // [SECTION: Background Grid]
        // Tells the main window the grid's state: on when the Grid box is checked, at the stored spacing and offsets
        public void RaiseGrid()
        {
            GridChanged?.Invoke(ChkBgGrid.IsChecked == true, _gridSpacing, _gridOffsetX, _gridOffsetY);
        }

        // Turns the background grid on or off
        private void ChkBgGrid_Click(object sender, RoutedEventArgs e)
        {
            RaiseGrid();
        }

        // Applies the spacing box on Enter
        private void TxtGridSpacing_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyGridSpacing();
        }

        // Applies the spacing box on focus loss
        private void TxtGridSpacing_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyGridSpacing();
        }

        // Stores a valid spacing (greater than 0, rounded to 2 decimals so it matches what the box shows), re-displays
        // the stored value (reverts invalid input) and redraws the grid
        private void ApplyGridSpacing()
        {
            if (_loading) return;

            if (double.TryParse(TxtGridSpacing.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                && double.IsFinite(value))
            {
                double rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                if (rounded > 0) _gridSpacing = rounded;
            }

            _loading = true;
            TxtGridSpacing.Text = Fmt(_gridSpacing);
            _loading = false;
            RaiseGrid();
        }

        // Applies an offset box on Enter
        private void TxtGridOffset_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            ApplyGridOffsets();
        }

        // Applies an offset box on focus loss
        private void TxtGridOffset_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyGridOffsets();
        }

        // Shows an offset box's muted X / Y letter only while the box is empty
        private void TxtGridOffset_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshGridOffsetPlaceholders();
        }

        // Stores both offsets (empty = 0, any finite number rounded to 2 decimals; invalid input keeps the stored
        // value), re-displays them (0 as an empty box) and redraws the grid
        private void ApplyGridOffsets()
        {
            if (_loading) return;

            _gridOffsetX = ParseGridOffset(TxtGridOffsetX.Text, _gridOffsetX);
            _gridOffsetY = ParseGridOffset(TxtGridOffsetY.Text, _gridOffsetY);

            _loading = true;
            TxtGridOffsetX.Text = _gridOffsetX == 0 ? string.Empty : Fmt(_gridOffsetX);
            TxtGridOffsetY.Text = _gridOffsetY == 0 ? string.Empty : Fmt(_gridOffsetY);
            _loading = false;
            RefreshGridOffsetPlaceholders();
            RaiseGrid();
        }

        // Returns an offset box's value: 0 when empty, the rounded number when valid, otherwise the stored value
        private static double ParseGridOffset(string text, double stored)
        {
            text = text.Trim();
            if (text.Length == 0) return 0;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value))
                return Math.Round(value, 2, MidpointRounding.AwayFromZero) + 0.0; // + 0.0 turns -0 into 0
            return stored;
        }

        // Shows each offset placeholder while its box is empty (skipped while the controls are still being created)
        private void RefreshGridOffsetPlaceholders()
        {
            if (PhGridOffsetX != null && TxtGridOffsetX != null)
                PhGridOffsetX.Visibility = string.IsNullOrEmpty(TxtGridOffsetX.Text) ? Visibility.Visible : Visibility.Collapsed;
            if (PhGridOffsetY != null && TxtGridOffsetY != null)
                PhGridOffsetY.Visibility = string.IsNullOrEmpty(TxtGridOffsetY.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
        // [END SECTION: Background Grid]

        // [SECTION: Resolution & Reset]
        // Set by the Theme Builder window: runs a size switch through the main window's Layout size pick rules for
        // Smart fullscreen (MainWindow.RunSizePick). Null runs the switch directly.
        public Action<Action>? SizePickRunner;

        // Does a size switch through SizePickRunner when set, otherwise directly
        private void RunSizePick(Action doSwitch)
        {
            if (SizePickRunner != null) SizePickRunner(doSwitch);
            else doSwitch();
        }

        // Does the combo pick: a different size goes through RunSizePick, anything else ("Custom...", same size) applies directly.
        // A size with no layout first asks the "New size layout" prompt (see BeginNewSizePick).
        private void CboResolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading) return;

            if (CboResolution.SelectedItem is ResolutionPreset preset
                && (preset.Width != _currentLayout.Width || preset.Height != _currentLayout.Height))
            {
                if (IsNewSize(preset.Width, preset.Height))
                {
                    BeginNewSizePick(preset, sender, e);
                    return;
                }

                RunSizePick(() => CboResolutionApply(sender, e));
                return;
            }

            CboResolutionApply(sender, e);
        }

        // [SECTION: New Size Prompt]
        // The scale choice for the new-size copy about to be made (set right before the switch, read and cleared by
        // ScaleNewSizeCopy). Null copies 1:1, as before the scaler.
        private string? _newSizeScaleChoice;

        // Returns true when no working layout exists at width x height, so switching there creates a new size
        private bool IsNewSize(int width, int height) =>
            !_settings.Layouts.Any(l => l.Width == width && l.Height == height);

        // Does a combo pick of a new size: puts the combo back on the current size at once, then, after the dropdown has
        // closed (a modal dialog over an open dropdown is avoided), asks the prompt; OK selects the size again under
        // _loading and runs the normal pick through RunSizePick, Cancel leaves the combo as it is.
        private void BeginNewSizePick(ResolutionPreset preset, object sender, SelectionChangedEventArgs e)
        {
            RestoreResolutionComboSelection();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_currentLayout == null || !IsNewSize(preset.Width, preset.Height)) return;
                if (preset.Width == _currentLayout.Width && preset.Height == _currentLayout.Height) return;

                string? choice = PromptForNewSizeScale(_currentLayout.Width, _currentLayout.Height, preset.Width, preset.Height);
                if (choice == null) return;

                _loading = true;
                CboResolution.SelectedItem = preset;
                _loading = false;

                RunNewSizeSwitch(choice, () => RunSizePick(() => CboResolutionApply(sender, e)));
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        // Does a switch that creates a new size with the given scale choice, clearing the choice afterwards
        private void RunNewSizeSwitch(string choice, Action doSwitch)
        {
            _newSizeScaleChoice = choice;
            try
            {
                doSwitch();
            }
            finally
            {
                _newSizeScaleChoice = null;
            }
        }

        // Does the combo restore after a cancelled or deferred new size: "Custom..." while a pending size is active,
        // otherwise the current layout's size, under _loading so it does not re-trigger the selection handler
        private void RestoreResolutionComboSelection()
        {
            bool onPending = _pendingSize != null && _pendingSize.Width == _currentLayout.Width && _pendingSize.Height == _currentLayout.Height;
            _loading = true;
            if (onPending) CboResolution.SelectedItem = "Custom...";
            else CboResolution.SelectedItem = new ResolutionPreset(_currentLayout.Width, _currentLayout.Height);
            _loading = false;
        }

        // Scales a fresh new-size copy (after its background was resized to fit) from the layout being edited, by the choice
        // the prompt set; the copy only, never _currentLayout or SavedThemeLayouts
        private void ScaleNewSizeCopy(ThemeLayout copy)
        {
            string? choice = _newSizeScaleChoice;
            _newSizeScaleChoice = null;
            LayoutScaler.Scale(copy.Layers, _currentLayout.Width, _currentLayout.Height, copy.Width, copy.Height, choice);
        }
        // [END SECTION: New Size Prompt]

        // Switching resolution: a size with no saved layout starts as a copy of the layout that was just
        // being edited (background resized to fit); a size that already has one is loaded as-is.
        private void CboResolutionApply(object sender, SelectionChangedEventArgs e)
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
                            Name = l.Name,
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
                ScaleNewSizeCopy(saved);
            }

            StopPreviews();
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
        // A new custom size not yet committed. Apply commits it at once; a Theme sizes button for a custom size removed but not
        // saved leaves it pending until Apply, Save Adjustments or a theme save calls CommitPendingSize; switching to any other
        // size before that discards it.
        private ResolutionPreset? _pendingSize;

        // Commits the pending size: it joins the combo box from now on.
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

        // Hides the entry row, and shows the remove button for any active size with a layout (not a pending one): "Delete Size"
        // on a custom size, "Remove Size" on a preset. Disabled, with the reason in its tooltip, when it is the only size left.
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
            bool hasLayout = _settings.Layouts.Any(l => l.Width == activeWidth && l.Height == activeHeight);
            BtnDeleteCustomRes.Visibility = (!hasLayout || pendingActive) ? Visibility.Collapsed : Visibility.Visible;
            BtnDeleteCustomRes.Content = IsPresetSize(activeWidth, activeHeight) ? "Remove Size" : "Delete Size";

            bool lastSize = CountRemovableSizes() <= 1;
            BtnDeleteCustomRes.IsEnabled = !lastSize;
            BtnDeleteCustomRes.ToolTip = lastSize
                ? "The theme needs at least one size, so the last one cannot be removed."
                : "Removes this size's layout from the theme (Save Changes writes it; closing without saving brings it back).";
        }

        // Returns how many sizes have a layout, not counting a pending (not yet kept) custom size
        private int CountRemovableSizes() =>
            _settings.Layouts
                .Select(l => new ResolutionPreset(l.Width, l.Height))
                .Distinct()
                .Count(size => size != _pendingSize);

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
                ScaleNewSizeCopy(saved);
            }

            StopPreviews();
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

        // Apply (the Add button, or Enter in the boxes): switches to the typed size and commits it at once, so it joins the combo
        // box and the entry row closes (in memory only - Save Adjustments and Save Changes still write settings.json and the
        // theme zip). The current size just commits a pending size and closes the row. The only check is whole numbers greater
        // than zero - anything else is up to the user.
        private void BtnAddCustomRes_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtCustomWidth.Text.Trim(), out int w) || !int.TryParse(TxtCustomHeight.Text.Trim(), out int h) || w <= 0 || h <= 0)
            {
                MessageBox.Show("Enter whole numbers greater than zero for the width and height.", "Custom Resolution", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (w == _currentLayout.Width && h == _currentLayout.Height)
            {
                CommitPendingSize();
                PnlCustomRes.Visibility = Visibility.Collapsed;
                return;
            }

            // A size with no layout asks the "New size layout" prompt first; Cancel leaves the entry row and any pending size as they were
            if (IsNewSize(w, h))
            {
                string? choice = PromptForNewSizeScale(_currentLayout.Width, _currentLayout.Height, w, h);
                if (choice == null) return;
                RunNewSizeSwitch(choice, () => SwitchToResolution(w, h));
            }
            else
            {
                SwitchToResolution(w, h);
            }

            CommitPendingSize();
            PnlCustomRes.Visibility = Visibility.Collapsed;
        }

        // Applies the custom size on Enter in either box
        private void CustomRes_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            BtnAddCustomRes_Click(sender, e);
        }

        // Delete Size / Remove Size: removes the active size's layout from the working layouts (any size, preset or custom,
        // except the last one), then switches to another size that has a layout. An ordinary working edit: Save Changes writes
        // it to the theme, and closing the builder without saving brings the layout back. A removed preset stays in the combo
        // and starts as a fresh copy of the layout being edited if picked again; a removed custom size leaves the combo.
        private void BtnDeleteCustomRes_Click(object sender, RoutedEventArgs e)
        {
            int w = _currentLayout.Width;
            int h = _currentLayout.Height;
            if (CountRemovableSizes() <= 1) return;

            bool preset = IsPresetSize(w, h);
            string verb = preset ? "Remove" : "Delete";
            var back = PickSizeAfterRemoval(w, h);
            if (back == null) return;

            var result = MessageBox.Show(
                $"{verb} the {w} x {h} layout from this theme?\n\nIts layer positions are discarded and the {back.Width} x {back.Height} layout is shown instead. "
                + "Save Changes writes this to the theme file; closing the Theme Builder without saving brings it back.",
                $"Confirm {verb} Size", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            _settings.Layouts.RemoveAll(l => l.Width == w && l.Height == h);
            _pendingSize = null;

            SwitchToResolution(back.Width, back.Height);

            // Rebuild the list so a removed custom size leaves the combo box, and the Theme sizes row's highlight moves
            _loading = true;
            RebuildResolutionCombo(back.Width, back.Height);
            _loading = false;
            RebuildSavedSizes();
        }

        // Returns the size to show after removing width x height: the theme's saved size when it still has a layout (the old
        // Delete Size rule), else the remaining size nearest to the removed one. Null when no other size has a layout.
        private ResolutionPreset? PickSizeAfterRemoval(int width, int height)
        {
            var remaining = _settings.Layouts
                .Select(l => new ResolutionPreset(l.Width, l.Height))
                .Distinct()
                .Where(size => size != _pendingSize && !(size.Width == width && size.Height == height))
                .ToList();
            if (remaining.Count == 0) return null;

            var themeSize = new ResolutionPreset(_settings.SavedThemeWidth, _settings.SavedThemeHeight);
            if (remaining.Contains(themeSize)) return themeSize;

            return remaining
                .OrderBy(size => Math.Abs(size.Width - width) + Math.Abs(size.Height - height))
                .ThenByDescending(size => size.Width * size.Height)
                .First();
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
                // The style gives the Global button look; the active size rests on the hover background
                if (active) button.SetResourceReference(Button.BackgroundProperty, ArcadeStick.Services.GlobalThemeResources.ButtonBgHover);
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

            Action doPick = () => RunSizePick(() =>
            {
                SwitchToResolution(size.Width, size.Height);

                _loading = true;
                RebuildResolutionCombo(size.Width, size.Height);
                _loading = false;
            });

            // A size removed but not saved has no working layout, so it is a new size: ask the "New size layout" prompt first
            if (IsNewSize(size.Width, size.Height))
            {
                string? choice = PromptForNewSizeScale(_currentLayout.Width, _currentLayout.Height, size.Width, size.Height);
                if (choice == null) return;
                RunNewSizeSwitch(choice, doPick);
                return;
            }

            doPick();
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
            if (SingleSelectedLayer is LayerListItem groupItem && GetGroup(groupItem.Id) is LayerGroupDefinition resetGroup)
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

                    // The Random Game Display group's shared file lives on its first part (its style, message included,
                    // comes back through CopyStyleFrom above)
                    if (partId == LayerRegistry.RandomGameDisplay) partEntry.ImagePath = partDefault.ImagePath;
                }

                NormalizeZ(GetAscendingOrder());
                RebuildLayerList(groupItem.Id);
                return;
            }

            if (SingleSelectedLayer is not LayerListItem selected || GetEntry(selected.Id) is not LayerLayoutEntry entry) return;

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
            var shapeNames = CaptureShapeNames(); // names are not style: they survive the reset

            _currentLayout = GetRestoreLayout(_currentLayout.Width, _currentLayout.Height);
            RestoreShapeNames(shapeNames);
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
            var shapeNames = CaptureShapeNames(); // names are not style: they survive the reset

            _currentLayout = LayerRegistry.BuildDefaultLayout(_currentLayout.Width, _currentLayout.Height);
            RestoreShapeNames(shapeNames);
            StoreLayout(_currentLayout);
            NormalizeZ(GetAscendingOrder());

            RebuildLayerList(selectedId);
        }
        // [END SECTION: Resolution & Reset]
    }
}