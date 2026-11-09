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
    // ThemeCatalogReader. A resolution button asks the host to load that theme at that size (LoadRequested); the final
    // layout and styling and the pop-out come in later handoffs.
    public partial class ThemeBrowserControl : UserControl
    {
        private ConfigurationSettings? _settings;
        // The entries the list shows now, in order (the cache's shared instances)
        private List<ThemeCatalogEntry> _shownEntries = new();
        private bool _listBuilt;
        // The scroll offset when the tab was last left, re-applied on the next show
        private double _savedOffset;

        // Raised by a resolution button with the theme name and the size to load. ThemeBuilderWindow does the load; this
        // control holds no load logic.
        public event Action<string, int, int>? LoadRequested;

        // True from a load request until the dispatcher is idle again, so clicks queued during the (blocking) load are ignored
        private bool _loadBusy;

        // What RefreshActiveMarks updates in place on one row: its entry, its name text and its size buttons
        private sealed class RowMarks
        {
            public ThemeCatalogEntry Entry { get; init; } = null!;
            public TextBlock NameText { get; init; } = null!;
            public List<(Button Button, int Width, int Height)> Buttons { get; } = new();
        }

        // One record per shown row, rebuilt with the list
        private readonly List<RowMarks> _rows = new();

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
        // line, and rebuilds the list only when the entries changed (instances or order). The scroll position is kept either way,
        // and the active marks are refreshed every time (a skipped rebuild would otherwise leave them stale after a theme swap).
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

            RefreshActiveMarks();
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

        // [SECTION: Active Marks]
        // Does the active marks in place, with no zip read, no rebuild and no scroll change: the button for the active theme at
        // the active size gets the selected look, and the active theme's name gets the TEMPORARY " (active)" text (handoff 5
        // replaces it with the check-circle glyph). The active theme is matched by ActiveThemeName in any case; one hidden
        // from the Browser or deleted is simply not marked. Safe before the list has been shown.
        public void RefreshActiveMarks()
        {
            if (_settings == null) return;

            foreach (var row in _rows)
            {
                bool activeTheme = IsActiveTheme(row.Entry.Name);
                row.NameText.Text = activeTheme ? $"{row.Entry.Name} (active)" : row.Entry.Name;

                foreach (var (button, width, height) in row.Buttons)
                {
                    ApplySelectedLook(button, activeTheme && IsActiveSize(width, height));
                }
            }
        }

        // Does the selected look exactly as the Theme Builder's active saved-size button (RebuildSavedSizes): the hover pair
        // as Background and Foreground plus Bold, the one allowed exception to the ThemedActionButtonStyle rule. Off clears
        // the three values so the style applies again. Never disabled.
        private static void ApplySelectedLook(Button button, bool selected)
        {
            if (selected)
            {
                button.SetResourceReference(Button.BackgroundProperty, GlobalThemeResources.ButtonBgHover);
                button.SetResourceReference(Button.ForegroundProperty, GlobalThemeResources.ButtonTextHover);
                button.FontWeight = FontWeights.Bold;
            }
            else
            {
                button.ClearValue(Button.BackgroundProperty);
                button.ClearValue(Button.ForegroundProperty);
                button.ClearValue(Button.FontWeightProperty);
            }
        }

        // Returns true when the name is the active theme (ActiveThemeName, any case)
        private bool IsActiveTheme(string themeName) =>
            _settings != null && string.Equals(themeName, _settings.ActiveThemeName, StringComparison.OrdinalIgnoreCase);

        // Returns true when the size is the active resolution
        private bool IsActiveSize(int width, int height) =>
            _settings != null && width == _settings.ActiveResolutionWidth && height == _settings.ActiveResolutionHeight;
        // [END SECTION: Active Marks]

        // [SECTION: Load Requests]
        // Does a resolution button click. The active theme at the active size does nothing at all (checked at click time: no
        // load, no discard, no save, no log). Otherwise, unless a load is running or just finished, it asks the host to load
        // that theme at that size; the busy flag clears only once the dispatcher is idle, so clicks queued while the load
        // blocked the UI thread are ignored, and the finally makes sure the Browser is never left locked.
        private void SizeButton_Click(string themeName, int width, int height)
        {
            if (IsActiveTheme(themeName) && IsActiveSize(width, height)) return;
            if (_loadBusy) return;

            _loadBusy = true;
            try
            {
                LoadRequested?.Invoke(themeName, width, height);
            }
            finally
            {
                Dispatcher.BeginInvoke(new Action(() => _loadBusy = false), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
        // [END SECTION: Load Requests]

        // [SECTION: Bare List]
        // Does the list rebuild: one bare row per entry, or "No themes found." when there are none
        private void RebuildList(List<ThemeCatalogEntry> entries)
        {
            PnlEntries.Children.Clear();
            _rows.Clear();

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

            var marks = new RowMarks { Entry = entry, NameText = name };
            _rows.Add(marks);

            // Resolutions: one plain button per saved size; a click loads the theme at that size (SizeButton_Click). Never
            // disabled (a disabled button dims to 50%); the active one only gets the selected look (RefreshActiveMarks).
            var sizesRow = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
            var sizesLabel = MakeText("Resolutions:");
            sizesLabel.Margin = new Thickness(0, 3, 8, 0);
            DockPanel.SetDock(sizesLabel, Dock.Left);
            sizesRow.Children.Add(sizesLabel);
            if (entry.Sizes.Count == 0)
            {
                // A legacy theme with no saved layouts is listed but not loadable from the Browser (decision 83): there is no
                // size to load, highlight or keep inert. It still loads from the Builder's Load button and the hamburger.
                var noSizes = MakeText("No saved resolutions");
                noSizes.Margin = new Thickness(0, 3, 0, 0);
                sizesRow.Children.Add(noSizes);
            }
            else
            {
                var sizeButtons = new WrapPanel();
                foreach (var (width, height) in entry.Sizes)
                {
                    var button = new Button
                    {
                        Content = $"{width} x {height}",
                        Style = (Style)FindResource("ThemedActionButtonStyle"),
                        Padding = new Thickness(7, 3, 7, 3),
                        Margin = new Thickness(0, 0, 6, 4)
                    };
                    string themeName = entry.Name;
                    int buttonWidth = width;
                    int buttonHeight = height;
                    button.Click += (_, _) => SizeButton_Click(themeName, buttonWidth, buttonHeight);
                    sizeButtons.Children.Add(button);
                    marks.Buttons.Add((button, width, height));
                }
                sizesRow.Children.Add(sizeButtons);
            }
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
