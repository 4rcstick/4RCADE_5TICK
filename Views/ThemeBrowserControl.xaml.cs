using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ArcadeStick.Models;
using ArcadeStick.Services;

namespace ArcadeStick.Views
{
    // The THEME BROWSER tab's content: the scrolling catalog of installed themes, read from their saved zips by
    // ThemeCatalogReader, one two-column entry per theme (thumbnail left, name, resolutions, author and description right).
    // A resolution button asks the host to load that theme at that size (LoadRequested). Every color and size is a Global
    // resource, so the Browser restyles live; the layout numbers are in the constants block below.
    public partial class ThemeBrowserControl : UserControl
    {
        // [SECTION: Layout Constants]
        // Every entry layout number in one place, for tuning by eye
        private const double SidePadding = 15;               // left and right padding of the tab, as the Theme Builder tab content
        private const double InstructionLineBottomGap = 10;   // gap between the instruction line and the divider
        private const double DividerToListGap = 10;           // gap between the divider and the first entry (scrolls with the list)
        private const double RowPaddingVertical = 12;        // padding above and below each entry's content, around the dividers
        private const double ColumnGap = 16;                 // gap between the thumbnail column and the text column
        private const double LineGap = 4;                    // gap between the text lines of an entry
        private const double LabelToValueGap = 10;           // gap from "Resolutions:" to its buttons (as the Builder's row)
        private const double ResolutionLabelTopOffset = 4;   // moves "Resolutions:" down to line up with the first row of buttons
        private const double NameToGlyphGap = 4;             // gap between the theme name and the active check-circle
        private const double InlineFallbackGap = 4;          // gap between a label and its muted fallback text
        private const double FallbackOpacity = 0.6;          // the muted "no value" look of the Theme Info controls
        private const string ActiveGlyph = "\uE930";         // Segoe MDL2 Assets "Completed" (check in a circle)
        // [END SECTION: Layout Constants]

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

        // Raised by a left click (press and release on the same thumbnail) with that entry. ThemeBuilderWindow reads the
        // full-size thumbnail and opens the preview; this control holds no preview logic.
        public event Action<ThemeCatalogEntry>? PreviewRequested;

        // The thumbnail tile the left button went down on, so a click opens a preview only when released on the same tile
        private FrameworkElement? _pressedTile;

        // Shared converter for the hover overlay's IsMouseOver binding
        private static readonly BooleanToVisibilityConverter HoverVisibility = new();

        // What RefreshActiveMarks updates in place on one row: its entry, its check-circle and its size buttons
        private sealed class RowMarks
        {
            public ThemeCatalogEntry Entry { get; init; } = null!;
            public TextBlock Glyph { get; init; } = null!;
            public List<(Button Button, int Width, int Height)> Buttons { get; } = new();
        }

        // One record per shown row, rebuilt with the list
        private readonly List<RowMarks> _rows = new();

        public ThemeBrowserControl()
        {
            InitializeComponent();
            RootGrid.Margin = new Thickness(SidePadding);
            TxtInstruction.Margin = new Thickness(0, 0, 0, InstructionLineBottomGap);
            // The scroll region reaches out to the scrollbar baseline gutter from the inside of the right and bottom borders, and
            // the list pads back in by the same amount, so the text and the last entry still end SidePadding from the frame.
            // The scrollbar's extra corner clearance comes from ScrollbarClearance (themes.md 3.27).
            double reach = SidePadding - ScrollbarClearance.BaselineGutter;
            ScrollEntries.Margin = new Thickness(0, 0, -reach, -reach);
            PnlEntries.Margin = new Thickness(0, DividerToListGap, reach, reach);
            Unloaded += (_, _) => _savedOffset = ScrollEntries.VerticalOffset;
        }

        // Does the setup: keeps the settings the catalog reads the Themes folder from. RefreshCatalog does nothing before this.
        public void Initialize(ConfigurationSettings settings)
        {
            _settings = settings;
        }

        // [SECTION: Catalog Refresh]
        // Does the catalog refresh each time the tab is shown: reads the catalog through the cache and rebuilds the list only
        // when the entries changed (instances or order). The scroll position is kept either way, and the active marks are
        // refreshed every time (a skipped rebuild would otherwise leave them stale after a theme swap).
        public void RefreshCatalog()
        {
            if (_settings == null) return;

            var entries = ThemeCatalogReader.BuildCatalog(_settings, out _);

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
        // [END SECTION: Catalog Refresh]

        // [SECTION: Active Marks]
        // Does the active marks in place, with no zip read, no rebuild and no scroll change: the button for the active theme at
        // the active size gets the selected look, and the active theme's name shows the check-circle right after it. The
        // active theme is matched by ActiveThemeName in any case; one hidden from the Browser or deleted is simply not marked.
        // Safe before the list has been shown.
        public void RefreshActiveMarks()
        {
            if (_settings == null) return;

            foreach (var row in _rows)
            {
                bool activeTheme = IsActiveTheme(row.Entry.Name);
                row.Glyph.Visibility = activeTheme ? Visibility.Visible : Visibility.Collapsed;

                foreach (var (button, width, height) in row.Buttons)
                {
                    ApplySelectedLook(button, activeTheme && IsActiveSize(width, height));
                }
            }
        }

        // Does the selected look exactly as the Theme Builder's saved-size buttons (RebuildSavedSizes): the active one rests on
        // the hover pair (Background and Foreground, the one allowed exception to the ThemedActionButtonStyle rule) and shows
        // the Bold layer of its two-layer label (MakeSizeLabel); the others clear the pair and show the Normal layer. The
        // button itself stays Normal, so its size never changes. Never disabled.
        private static void ApplySelectedLook(Button button, bool selected)
        {
            if (selected)
            {
                button.SetResourceReference(Button.BackgroundProperty, GlobalThemeResources.ButtonBgHover);
                button.SetResourceReference(Button.ForegroundProperty, GlobalThemeResources.ButtonTextHover);
            }
            else
            {
                button.ClearValue(Button.BackgroundProperty);
                button.ClearValue(Button.ForegroundProperty);
            }

            if (button.Content is Grid label && label.Children.Count == 2)
            {
                label.Children[0].Visibility = selected ? Visibility.Hidden : Visibility.Visible;
                label.Children[1].Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // Returns the two-layer label of a resolution button (a matching copy is in AdvancedThemesTabControl): the Normal
        // layer is the only one that sizes the button (Hidden, not Collapsed, while selected, so it keeps its space); the Bold
        // layer has a -1000 margin on every side, so it adds no width or height, and is centered on the Normal layer, spilling
        // into the button's padding without clipping. Neither layer sets Foreground or FontSize, so the hover color and the
        // Global Button font size are inherited.
        private static Grid MakeSizeLabel(string text, bool selected)
        {
            var label = new Grid();
            label.Children.Add(new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Normal,
                Visibility = selected ? Visibility.Hidden : Visibility.Visible
            });
            label.Children.Add(new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(-1000),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = selected ? Visibility.Visible : Visibility.Collapsed
            });
            return label;
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

        // [SECTION: Entry List]
        // Does the list rebuild: one entry per theme with a Global divider between entries (none above the first or below the
        // last), or "No themes found." when there are none
        private void RebuildList(List<ThemeCatalogEntry> entries)
        {
            PnlEntries.Children.Clear();
            _rows.Clear();

            if (entries.Count == 0)
            {
                PnlEntries.Children.Add(MakeText("No themes found.", GlobalThemeResources.FontStandardColor, GlobalThemeResources.FontStandardSize));
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                {
                    var divider = new Separator { Margin = new Thickness(0) };
                    divider.SetResourceReference(Separator.BackgroundProperty, GlobalThemeResources.OptionsSeparator);
                    PnlEntries.Children.Add(divider);
                }

                var row = MakeEntryRow(entries[i]);
                row.Margin = new Thickness(0, i == 0 ? 0 : RowPaddingVertical, 0, i == entries.Count - 1 ? 0 : RowPaddingVertical);
                PnlEntries.Children.Add(row);
            }
        }

        // Does one entry: the thumbnail centered in a column every entry shares (so the text column starts at one x), then
        // the name with its check-circle, the Resolutions row, the author and the description
        private FrameworkElement MakeEntryRow(ThemeCatalogEntry entry)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Thumb" });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColumnGap) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var thumbnail = MakeThumbnail(entry);
            Grid.SetColumn(thumbnail, 0);
            row.Children.Add(thumbnail);

            var details = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(details, 2);
            row.Children.Add(details);

            var glyph = MakeNameLine(entry, details);
            var marks = new RowMarks { Entry = entry, Glyph = glyph };
            _rows.Add(marks);

            details.Children.Add(MakeResolutionsRow(entry, marks));
            details.Children.Add(MakeAuthorLine(entry));
            details.Children.Add(MakeDescriptionLine(entry));

            return row;
        }

        // Does the thumbnail cell: the cached tile in the shared ThemeThumbnailControl (fit inside 160 x 90, never upscaled)
        // with its hover and click layer (MakePreviewTile) inside a square-cornered Global Options border drawn outside the
        // image, or the control's own "No Image" box with no extra border, no hover and no click. Centered both ways; it reads
        // no file (ShowImage only).
        private FrameworkElement MakeThumbnail(ThemeCatalogEntry entry)
        {
            var view = new ThemeThumbnailControl
            {
                DisplayHeight = ThemeThumbnailStore.BrowserMaxHeight,
                MaxDisplayWidth = ThemeThumbnailStore.BrowserMaxWidth,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            view.ShowImage(entry.Tile);
            if (entry.Tile == null) return view;

            var frame = new Border
            {
                Child = MakePreviewTile(view, entry),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            };
            frame.SetResourceReference(Border.BorderBrushProperty, GlobalThemeResources.OptionsBorderColor);
            frame.SetResourceReference(Border.BorderThicknessProperty, GlobalThemeResources.OptionsBorderSize);
            return frame;
        }

        // Does the hover and click layer over a thumbnail image: a Grid sized by the control (which is exactly the displayed
        // image, inside the Options border) holding the control and, on top, the hover overlay (Chrome black at 60% with the
        // centered off-white magnifier-plus, ZoomIn E8A3). The overlay follows the tile's IsMouseOver through a binding, so
        // WPF keeps it right after the preview closes (no stuck hover); no animation. The glyph hides on an image smaller than
        // its box. Hand cursor; a left press and release on the same tile raises PreviewRequested.
        private FrameworkElement MakePreviewTile(ThemeThumbnailControl view, ThemeCatalogEntry entry)
        {
            // The transparent background makes the whole image area hit-testable for IsMouseOver and the click
            var tile = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand };
            tile.Children.Add(view);

            var hover = new Grid { IsHitTestVisible = false };
            hover.SetResourceReference(Panel.BackgroundProperty, "ChromeBrowserThumbnailHover");
            hover.SetBinding(VisibilityProperty, new Binding(nameof(IsMouseOver)) { Source = tile, Converter = HoverVisibility });

            var glyph = new TextBlock
            {
                Text = "\uE8A3",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = ThemePreviewOverlay.HoverGlyphSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "ChromeBrowserThumbnailGlyph");
            hover.Children.Add(glyph);
            tile.Children.Add(hover);

            tile.SizeChanged += (_, _) =>
                glyph.Visibility = tile.ActualWidth < ThemePreviewOverlay.HoverGlyphSize || tile.ActualHeight < ThemePreviewOverlay.HoverGlyphSize
                    ? Visibility.Collapsed : Visibility.Visible;

            tile.MouseLeftButtonDown += (_, _) => _pressedTile = tile;
            tile.MouseLeave += (_, _) => { if (ReferenceEquals(_pressedTile, tile)) _pressedTile = null; };
            tile.MouseLeftButtonUp += (_, _) =>
            {
                if (!ReferenceEquals(_pressedTile, tile)) return;
                _pressedTile = null;
                PreviewRequested?.Invoke(entry);
            };

            return tile;
        }

        // Does the name line: the name in capitals for display only (the real name is what loads), Bold in the Global Header
        // color and size, on one line with an ellipsis when too long, and the check-circle right after it (shown only for the
        // active theme). The Left-aligned DockPanel sizes to the name, so the glyph sits after the last letter, and the name
        // is trimmed first when space runs out, so the glyph always stays visible. Returns the glyph for RefreshActiveMarks.
        private static TextBlock MakeNameLine(ThemeCatalogEntry entry, Panel details)
        {
            var line = new DockPanel { HorizontalAlignment = HorizontalAlignment.Left };

            var glyph = new TextBlock
            {
                Text = ActiveGlyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Margin = new Thickness(NameToGlyphGap, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            glyph.SetResourceReference(TextBlock.ForegroundProperty, GlobalThemeResources.FontHeaderColor);
            glyph.SetResourceReference(TextBlock.FontSizeProperty, GlobalThemeResources.FontHeaderSize);
            DockPanel.SetDock(glyph, Dock.Right);
            line.Children.Add(glyph);

            var name = new TextBlock
            {
                Text = entry.Name.ToUpper(CultureInfo.InvariantCulture),
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            name.SetResourceReference(TextBlock.ForegroundProperty, GlobalThemeResources.FontHeaderColor);
            name.SetResourceReference(TextBlock.FontSizeProperty, GlobalThemeResources.FontHeaderSize);
            line.Children.Add(name);

            details.Children.Add(line);
            return glyph;
        }

        // Does the Resolutions row as the Theme Builder's: the label in an Auto column, the buttons in a WrapPanel in the next
        // column so wrapped buttons start under the first one. A legacy theme with no saved layouts shows the muted
        // "No saved resolutions" instead and is not loadable from the Browser (decision 83).
        private FrameworkElement MakeResolutionsRow(ThemeCatalogEntry entry, RowMarks marks)
        {
            var grid = new Grid { Margin = new Thickness(0, LineGap, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = MakeText("Resolutions:", GlobalThemeResources.FontSubHeaderColor, GlobalThemeResources.FontSubHeaderSize);
            label.VerticalAlignment = VerticalAlignment.Top;
            label.Margin = new Thickness(0, ResolutionLabelTopOffset, LabelToValueGap, 0);
            grid.Children.Add(label);

            FrameworkElement content;
            if (entry.Sizes.Count == 0)
            {
                var noSizes = MakeFallbackText("No saved resolutions");
                noSizes.VerticalAlignment = VerticalAlignment.Top;
                noSizes.Margin = new Thickness(0, ResolutionLabelTopOffset, 0, 0);
                content = noSizes;
            }
            else
            {
                var sizeButtons = new WrapPanel();
                foreach (var (width, height) in entry.Sizes)
                {
                    // The Builder's saved-size button values (RebuildSavedSizes): style, padding, margin, the two-layer label
                    // (sized by its Normal layer; RefreshActiveMarks shows the Bold one) and the button always Normal. The
                    // automation name keeps the plain "W x H" text that string content used to give.
                    string text = $"{width} x {height}";
                    var button = new Button
                    {
                        Content = MakeSizeLabel(text, false),
                        Style = (Style)FindResource("ThemedActionButtonStyle"),
                        Padding = new Thickness(7, 3, 7, 3),
                        Margin = new Thickness(0, 0, 6, 4),
                        FontWeight = FontWeights.Normal
                    };
                    AutomationProperties.SetName(button, text);
                    string themeName = entry.Name;
                    int buttonWidth = width;
                    int buttonHeight = height;
                    button.Click += (_, _) => SizeButton_Click(themeName, buttonWidth, buttonHeight);
                    sizeButtons.Children.Add(button);
                    marks.Buttons.Add((button, width, height));
                }
                content = sizeButtons;
            }
            Grid.SetColumn(content, 1);
            grid.Children.Add(content);
            return grid;
        }

        // Does the author line: "Author:" (Sub-Header) and the value (Standard) inline, wrapping; an empty author shows
        // nothing after the label, as the Theme Info author control does
        private static FrameworkElement MakeAuthorLine(ThemeCatalogEntry entry)
        {
            var line = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, LineGap, 0, 0) };
            line.Inlines.Add(MakeRun("Author:", GlobalThemeResources.FontSubHeaderColor, GlobalThemeResources.FontSubHeaderSize));
            if (!string.IsNullOrWhiteSpace(entry.Author))
            {
                line.Inlines.Add(MakeRun(" " + entry.Author, GlobalThemeResources.FontStandardColor, GlobalThemeResources.FontStandardSize));
            }
            return line;
        }

        // Does the description line: "Description:" (Sub-Header) and the text (Standard) inline in one wrapping TextBlock, so
        // later lines wrap naturally. An empty description shows the label and the muted "No description" side by side.
        private static FrameworkElement MakeDescriptionLine(ThemeCatalogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Description))
            {
                var empty = new WrapPanel { Margin = new Thickness(0, LineGap, 0, 0) };
                var label = MakeText("Description:", GlobalThemeResources.FontSubHeaderColor, GlobalThemeResources.FontSubHeaderSize);
                label.VerticalAlignment = VerticalAlignment.Center;
                empty.Children.Add(label);
                var fallback = MakeFallbackText(ThemeInfoFallbacks.NoDescription);
                fallback.VerticalAlignment = VerticalAlignment.Center;
                fallback.Margin = new Thickness(InlineFallbackGap, 0, 0, 0);
                empty.Children.Add(fallback);
                return empty;
            }

            var line = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, LineGap, 0, 0) };
            line.Inlines.Add(MakeRun("Description:", GlobalThemeResources.FontSubHeaderColor, GlobalThemeResources.FontSubHeaderSize));
            line.Inlines.Add(MakeRun(" " + entry.Description, GlobalThemeResources.FontStandardColor, GlobalThemeResources.FontStandardSize));
            return line;
        }

        // Returns a wrapping TextBlock with the given Global color and size resources
        private static TextBlock MakeText(string text, string colorKey, string sizeKey)
        {
            var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
            block.SetResourceReference(TextBlock.ForegroundProperty, colorKey);
            block.SetResourceReference(TextBlock.FontSizeProperty, sizeKey);
            return block;
        }

        // Returns the muted "no value" text of the Theme Info controls: Global Sub-Text color and size at 60% opacity
        private static TextBlock MakeFallbackText(string text)
        {
            var block = MakeText(text, GlobalThemeResources.FontSubTextColor, GlobalThemeResources.FontSubTextSize);
            block.Opacity = FallbackOpacity;
            return block;
        }

        // Returns a Run with the given Global color and size resources (live, like the elements)
        private static Run MakeRun(string text, string colorKey, string sizeKey)
        {
            var run = new Run(text);
            run.SetResourceReference(TextElement.ForegroundProperty, colorKey);
            run.SetResourceReference(TextElement.FontSizeProperty, sizeKey);
            return run;
        }
        // [END SECTION: Entry List]
    }
}
