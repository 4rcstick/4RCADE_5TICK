using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcadeStick.Models;
using ArcadeStick.Services;

namespace ArcadeStick.Views
{
    // The THEME BROWSER tab's content: a bare, scrollable list of the installed themes, read from their saved zips by
    // ThemeCatalogReader. Loading, the final layout and styling, and the pop-out come in later handoffs.
    public partial class ThemeBrowserControl : UserControl
    {
        private ConfigurationSettings? _settings;
        // The entries the list shows now, in order (the cache's shared instances)
        private List<ThemeCatalogEntry> _shownEntries = new();
        private bool _listBuilt;
        // The scroll offset when the tab was last left, re-applied on the next show
        private double _savedOffset;

        public ThemeBrowserControl()
        {
            InitializeComponent();
            Unloaded += (_, _) => _savedOffset = ScrollEntries.VerticalOffset;
        }

        // Does the setup: keeps the settings the catalog reads the Themes folder from. RefreshCatalog does nothing before this.
        public void Initialize(ConfigurationSettings settings)
        {
            _settings = settings;
        }

        // [SECTION: Catalog Refresh]
        // Does the catalog refresh each time the tab is shown: reads the catalog through the cache, updates the diagnostics
        // line, and rebuilds the list only when the entries changed (instances or order). The scroll position is kept either way.
        public void RefreshCatalog()
        {
            if (_settings == null) return;

            var entries = ThemeCatalogReader.BuildCatalog(_settings, out var stats);
            ShowDiagnostics(entries.Count, stats);

            // Shown already (ShowBrowserTab on a selected tab): the live offset; coming back to the tab: the one saved on leaving
            double offset = ScrollEntries.IsLoaded ? ScrollEntries.VerticalOffset : _savedOffset;

            if (!_listBuilt || !IsSameList(entries))
            {
                RebuildList(entries);
                _shownEntries = entries;
                _listBuilt = true;
            }

            RestoreScrollOffset(offset);
        }

        // Returns true when the entries are the same instances in the same order as the list shows
        private bool IsSameList(List<ThemeCatalogEntry> entries)
        {
            if (entries.Count != _shownEntries.Count) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!ReferenceEquals(entries[i], _shownEntries[i])) return false;
            }
            return true;
        }

        // Does the scroll restore once layout has run (clamped to the new extent by the ScrollViewer)
        private void RestoreScrollOffset(double offset)
        {
            _savedOffset = offset;
            Dispatcher.BeginInvoke(new Action(() => ScrollEntries.ScrollToVerticalOffset(Math.Min(offset, ScrollEntries.ScrollableHeight))),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // Does the TEMPORARY diagnostics line (removed in handoff 5)
        private void ShowDiagnostics(int shown, ThemeCatalogStats stats)
        {
            TxtDiagnostics.Text = $"{shown} themes shown | read {stats.Read}, from cache {stats.FromCache}, skipped {stats.Skipped}, hidden {stats.Hidden}";
        }
        // [END SECTION: Catalog Refresh]

        // [SECTION: Bare List]
        // Does the list rebuild: one bare row per entry, or "No themes found." when there are none
        private void RebuildList(List<ThemeCatalogEntry> entries)
        {
            PnlEntries.Children.Clear();

            if (entries.Count == 0)
            {
                PnlEntries.Children.Add(MakeText("No themes found."));
                return;
            }

            foreach (var entry in entries)
            {
                PnlEntries.Children.Add(MakeEntryRow(entry));
            }
        }

        // Does one bare row: the tile (or "No Image") on the left, then the name, the resolution buttons, the author and the description
        private UIElement MakeEntryRow(ThemeCatalogEntry entry)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ThemeThumbnailStore.BrowserMaxWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            UIElement thumbnail;
            if (entry.Tile != null)
            {
                var image = new Image
                {
                    Source = entry.Tile,
                    Stretch = Stretch.Uniform,
                    StretchDirection = StretchDirection.DownOnly,
                    MaxWidth = ThemeThumbnailStore.BrowserMaxWidth,
                    MaxHeight = ThemeThumbnailStore.BrowserMaxHeight,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.Fant);
                thumbnail = image;
            }
            else
            {
                thumbnail = MakeText(ThemeInfoFallbacks.NoImage);
            }
            Grid.SetColumn(thumbnail, 0);
            row.Children.Add(thumbnail);

            var details = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
            Grid.SetColumn(details, 1);
            row.Children.Add(details);

            var name = MakeText(entry.Name);
            name.FontWeight = FontWeights.Bold;
            details.Children.Add(name);

            // Resolutions: one plain button per saved size. INERT in this pass (no Click, no Tag, never disabled, since a
            // disabled button dims to 50%); handoff 4 wires them. A theme with no layouts gets the label and no buttons:
            // handoff 4 decides what such an entry offers.
            var sizesRow = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            var sizesLabel = MakeText("Resolutions:");
            sizesLabel.Margin = new Thickness(0, 3, 8, 0);
            DockPanel.SetDock(sizesLabel, Dock.Left);
            sizesRow.Children.Add(sizesLabel);
            var sizeButtons = new WrapPanel();
            foreach (var (width, height) in entry.Sizes)
            {
                sizeButtons.Children.Add(new Button
                {
                    Content = $"{width} x {height}",
                    Style = (Style)FindResource("ThemedActionButtonStyle"),
                    Padding = new Thickness(7, 3, 7, 3),
                    Margin = new Thickness(0, 0, 6, 4)
                });
            }
            sizesRow.Children.Add(sizeButtons);
            details.Children.Add(sizesRow);

            details.Children.Add(MakeText($"Author: {entry.Author}"));
            string description = string.IsNullOrWhiteSpace(entry.Description) ? ThemeInfoFallbacks.NoDescription : entry.Description;
            details.Children.Add(MakeText($"Description: {description}"));

            return row;
        }

        // Returns a wrapping TextBlock in the Global Standard Text size and color
        private static TextBlock MakeText(string text)
        {
            var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
            block.SetResourceReference(TextBlock.ForegroundProperty, GlobalThemeResources.FontStandardColor);
            block.SetResourceReference(TextBlock.FontSizeProperty, GlobalThemeResources.FontStandardSize);
            return block;
        }
        // [END SECTION: Bare List]
    }
}
