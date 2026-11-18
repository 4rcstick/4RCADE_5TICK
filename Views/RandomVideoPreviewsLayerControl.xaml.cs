using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    // [SECTION: Random Video Previews Layer]
    // The hover title and PLAY button style, built by MainWindow from the Random Video Previews entry. A record, so an unchanged
    // style (every ApplyLayout pushes it) is recognized and nothing is laid out again.
    public sealed record RandomPreviewOverlayStyle(
        string TitleFontFamily, int TitleFontWeight, double TitleMaxSize, double TitleMinSize, string TitleColorHex,
        bool TitleWrap, bool TitleScaleToFit, int TitleMaxLines, string TitleBgColorHex, double TitlePadding,
        string ButtonText, double ButtonHeightPercent, string ButtonTextColorHex, string ButtonBgColorHex, string ButtonBorderColorHex,
        double ButtonBorderSize, double ButtonCornerRadius, string ButtonHoverTextColorHex, string ButtonHoverBgColorHex, string ButtonHoverBorderColorHex);

    // Random Video Previews layer: the splash-only video surface inside the outer video frame, and on hover the current game's
    // title above a centered PLAY button plus the preview sound glyph. ApplyLayout places this control at the entry's X / Y (the
    // ring's outer corner), sized to the entry's Width / Height (the video area) plus 2 x BorderSize, the same border-ring rule as
    // the Video Preview. Everything is pushed in by MainWindow (ApplyBorder, ApplyBackground, ApplyOverlayStyle, SetCurrentTitle,
    // SetSoundEnabled, SetPreviewPlaying, ApplySoundGlyphSize); the control raises PlayRequested and SoundToggleRequested and
    // holds no playback logic. MainWindow.xaml.cs reaches PreviewVideoSurface through the public field.
    public partial class RandomVideoPreviewsLayerControl : UserControl
    {
        private double _borderSize;
        private double _borderRadius;

        // [SECTION: Random Video Previews Overlay Values]
        private const string SoundOnGlyph = "";    // Volume (the Video Preview's glyphs and sizes)
        private const string SoundOffGlyph = "";   // Mute
        private const double SoundGlyphHeightRatio = 0.08;
        private const double SoundGlyphMinSize = 16;
        private const double SoundGlyphMaxSize = 40;
        private const double SoundGlyphMargin = 8;
        private const double EdgeMargin = 8;              // keeps the title and button off the video's edges
        private const double MinButtonHeight = 20;        // below this the button would be unusable: no overlay (decision D7)
        private const double MaxButtonHeightRatio = 0.5;  // the button never takes more than half the video height
        private const double MinButtonWidth = 40;
        private const double ButtonFontRatio = 0.45;      // PLAY font size from the button height
        private const double ButtonSidePaddingRatio = 0.5;
        private const double MinTitleTextWidth = 24;
        private const double TitleSizeStep = 0.5;
        private const double GlyphClearance = 4;          // gap between the PLAY button's bottom and the sound glyph's top
        private const double NextIconRatio = 0.65;        // the NEXT shape's height in the glyph box, close to the speaker icon's ink
        private static readonly FontFamily GlyphFamily = new("Segoe MDL2 Assets");
        // [END SECTION: Random Video Previews Overlay Values]

        private double _contentRadius;     // the rounded clip's inner radius (ApplyClip); the hover fill follows it

        private RandomPreviewOverlayStyle? _style;
        private string? _title;            // the current game's title, null when nothing plays
        private bool _overlayFits;         // false when the video is too small for the PLAY button
        private bool _previewPlaying;
        private bool _playPressed;
        private bool _canSkip;             // another game can follow the one on screen (NEXT shows only then)
        private bool _nextPressed;

        // Raised when NEXT is clicked; MainWindow moves to the next game in the sequence
        public event EventHandler? NextRequested;

        // Raised when PLAY is clicked; MainWindow launches the game on screen
        public event EventHandler? PlayRequested;

        // Raised when the sound glyph is clicked; MainWindow owns the shared Preview sound setting
        public event EventHandler? SoundToggleRequested;

        // Does the control setup: the clip and overlay are laid out once loaded, and hover shows or hides the overlay
        public RandomVideoPreviewsLayerControl()
        {
            InitializeComponent();
            Loaded += (s, e) => { ApplyClip(); LayoutOverlay(); };
            MouseEnter += (s, e) => UpdateHoverVisibility();
            MouseLeave += (s, e) => UpdateHoverVisibility();
        }

        // Does the clip and overlay refresh when the media area changes size
        private void MediaSlotContent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyClip();
            LayoutOverlay();
        }

        // Does the overlay refresh when the window moves to a monitor with another DPI (text is measured in physical pixels)
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            LayoutOverlay();
        }

        // Does the outer video frame from the layer entry's border values (called from MainWindow.ApplyLayout)
        public void ApplyBorder(double size, string colorHex, double radius)
        {
            _borderSize = Math.Max(0, size);
            _borderRadius = Math.Max(0, radius);

            MediaSlotRing.BorderThickness = new Thickness(_borderSize);
            MediaSlotRing.CornerRadius = new CornerRadius(_borderRadius);
            MediaSlotRing.BorderBrush = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(colorHex);
            SoundGlyphButton.Margin = new Thickness(0, 0, _borderSize + SoundGlyphMargin, _borderSize + SoundGlyphMargin);
            NextGlyphButton.Margin = new Thickness(_borderSize + SoundGlyphMargin, 0, 0, _borderSize + SoundGlyphMargin);

            ApplyClip();
            LayoutOverlay();
        }

        // Does the letterbox color behind the video from the layer entry (called from MainWindow.ApplyLayout)
        public void ApplyBackground(string colorHex)
        {
            MediaSlot.Background = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(colorHex);
        }

        // Does the glyph size from the entry's Height (the video area), same rule as the Video Preview's glyph
        public void ApplySoundGlyphSize(double contentHeight)
        {
            double size = Math.Clamp(contentHeight * SoundGlyphHeightRatio, SoundGlyphMinSize, SoundGlyphMaxSize);
            if (SoundGlyphIcon.FontSize == size) return;
            SoundGlyphIcon.FontSize = size;

            // NEXT mirrors the speaker: a box one glyph wide and one glyph line tall, its shape centered in it
            NextGlyphButton.Width = size;
            NextGlyphButton.Height = Math.Ceiling(GlyphFamily.LineSpacing * size);
            NextGlyphIcon.Height = size * NextIconRatio;
            LayoutOverlay();
        }

        // Does the title and PLAY button style; an unchanged style lays nothing out again
        public void ApplyOverlayStyle(RandomPreviewOverlayStyle style)
        {
            if (style == _style) return;
            _style = style;
            ApplyButtonColors();
            LayoutOverlay();
        }

        // Does the current game's title (null when nothing plays): laid out at once, shown while hovered
        public void SetCurrentTitle(string? title)
        {
            if (title == _title) return;
            _title = title;
            LayoutOverlay();
            UpdateHoverVisibility();
        }

        // Shows the volume glyph when sound is on, the mute glyph when it is off
        public void SetSoundEnabled(bool enabled)
        {
            SoundGlyphIcon.Text = enabled ? SoundOnGlyph : SoundOffGlyph;
        }

        // MainWindow reports whether a video is actually playing (the glyph shows only then, as on the Video Preview)
        public void SetPreviewPlaying(bool playing)
        {
            _previewPlaying = playing;
            UpdateHoverVisibility();
        }

        // Does the hover state: the title and PLAY show while the pointer is over the layer and a game is on screen (and they
        // fit); the sound glyph while the pointer is over the layer and a video plays
        private void UpdateHoverVisibility()
        {
            bool hovered = IsMouseOver;
            HoverOverlay.Visibility = hovered && _title != null && _overlayFits ? Visibility.Visible : Visibility.Collapsed;
            SoundGlyphButton.Visibility = hovered && _previewPlaying ? Visibility.Visible : Visibility.Collapsed;
            NextGlyphButton.Visibility = hovered && _title != null && _canSkip ? Visibility.Visible : Visibility.Collapsed;
            if (!hovered)
            {
                _playPressed = false;
                _nextPressed = false;
            }
        }

        // MainWindow reports whether another game can follow the one on screen (none with zero or one eligible game)
        public void SetCanSkip(bool canSkip)
        {
            if (_canSkip == canSkip) return;
            _canSkip = canSkip;
            UpdateHoverVisibility();
        }

        // Does the NEXT press: remembered so only a press and release on the glyph advance
        private void NextGlyphButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _nextPressed = true;
        }

        // Does the NEXT click: a release on the glyph after a press on it asks MainWindow for the next game
        private void NextGlyphButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (!_nextPressed) return;
            _nextPressed = false;
            NextRequested?.Invoke(this, EventArgs.Empty);
        }

        // [SECTION: Random Video Previews Overlay Layout]
        // Does the overlay geometry inside the video area (MediaSlotContent): the translucent hover fill (the entry's TitleBgColorHex,
        // inset from every edge by TitlePadding; 0 fills the whole area, the rounded clip rounds it); the PLAY button horizontally
        // centered, its height a percent of the video height (at least 20, at most half), its width the text plus padding,
        // vertically centered unless the title at its full size needs more room above it, then moved down only that far, and never
        // with its bottom below the sound glyph's top edge minus a clearance; then the title in the space above it (the fit rules
        // shrink or trim it when the button cannot move further). A video too small for the button shows no overlay (decision D7).
        // Runs only when the size, the DPI, the style, the border or the title changes, never per frame.
        private void LayoutOverlay()
        {
            double w = MediaSlotContent.ActualWidth;
            double h = MediaSlotContent.ActualHeight;
            _overlayFits = false;
            if (_style is not RandomPreviewOverlayStyle style || w <= 0 || h <= 0)
            {
                UpdateHoverVisibility();
                return;
            }

            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double inset = Math.Max(0, style.TitlePadding);
            LayoutHoverFill(style, w, h, inset);

            // The lowest the button may reach: the glyph's top edge (its margin plus its line height up from the bottom) minus a gap
            double glyphTop = h - SoundGlyphMargin - Math.Ceiling(GlyphFamily.LineSpacing * SoundGlyphIcon.FontSize);
            double maxButtonBottom = glyphTop - GlyphClearance;

            // PLAY button
            double buttonHeight = Math.Min(Math.Max(Math.Round(h * Math.Clamp(style.ButtonHeightPercent, 0, 100) / 100.0), MinButtonHeight), h * MaxButtonHeightRatio);
            double maxButtonWidth = w - (2 * EdgeMargin);
            if (buttonHeight < MinButtonHeight || maxButtonWidth < Math.Max(MinButtonWidth, buttonHeight) || maxButtonBottom - buttonHeight < EdgeMargin)
            {
                UpdateHoverVisibility();
                return;
            }

            double border = Math.Max(0, Math.Min(style.ButtonBorderSize, buttonHeight / 4));
            double fontSize = Math.Max(1, buttonHeight * ButtonFontRatio);
            double sidePadding = buttonHeight * ButtonSidePaddingRatio;
            var buttonTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            double textWidth = MeasureWidth(style.ButtonText, buttonTypeface, fontSize, dip);
            double buttonWidth = Math.Min(maxButtonWidth, Math.Max(buttonHeight, textWidth + (2 * sidePadding) + (2 * border)));

            PlayButtonText.Text = style.ButtonText;
            PlayButtonText.FontSize = fontSize;
            PlayButtonText.MaxWidth = Math.Max(0, buttonWidth - (2 * border) - sidePadding);
            PlayButtonBox.Width = buttonWidth;
            PlayButtonBox.Height = buttonHeight;
            PlayButtonBox.BorderThickness = new Thickness(border);
            PlayButtonBox.CornerRadius = new CornerRadius(Math.Max(0, Math.Min(style.ButtonCornerRadius, buttonHeight / 2)));

            // Vertical place: centered, moved down only as far as the full-size title needs, capped above the glyph
            double titleMargin = EdgeMargin + inset;
            double gap = Math.Max(4, buttonHeight * 0.25);
            double titleNeed = TitleNaturalHeight(style, w - (2 * titleMargin), dip);
            double centeredTop = (h - buttonHeight) / 2;
            double wantedTop = titleNeed > 0 ? Math.Max(centeredTop, titleMargin + titleNeed + gap) : centeredTop;
            double buttonTop = Math.Min(wantedTop, maxButtonBottom - buttonHeight);
            Canvas.SetLeft(PlayButtonBox, (w - buttonWidth) / 2);
            Canvas.SetTop(PlayButtonBox, buttonTop);
            _overlayFits = true;

            LayoutTitle(style, w, titleMargin, buttonTop - gap, dip);
            UpdateHoverVisibility();
        }

        // Does the hover fill: the title background color over the video, inset by the same amount from every edge (0 fills the
        // whole area; the rounded clip rounds it, and an inset fill keeps a concentric corner). Never hit-testable, inside the clip,
        // under the title and button, and the sound glyph (outside the clip) stays above it.
        private void LayoutHoverFill(RandomPreviewOverlayStyle style, double w, double h, double inset)
        {
            double fillWidth = w - (2 * inset);
            double fillHeight = h - (2 * inset);
            if (fillWidth <= 0 || fillHeight <= 0)
            {
                HoverFill.Visibility = Visibility.Collapsed;
                return;
            }

            HoverFill.Visibility = Visibility.Visible;
            HoverFill.Width = fillWidth;
            HoverFill.Height = fillHeight;
            HoverFill.CornerRadius = new CornerRadius(Math.Max(0, _contentRadius - inset));
            HoverFill.Background = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(style.TitleBgColorHex);
            Canvas.SetLeft(HoverFill, inset);
            Canvas.SetTop(HoverFill, inset);
        }

        // Does the height the title takes at its maximum size within the width: its wrapped lines (at most the line cap) times the
        // line height; 0 without a title. The PLAY button moves down to make this room when it can.
        private double TitleNaturalHeight(RandomPreviewOverlayStyle style, double width, double dip)
        {
            if (string.IsNullOrWhiteSpace(_title) || width < MinTitleTextWidth) return 0;

            var (family, typeface) = TitleFont(style);
            double size = Math.Max(1, style.TitleMaxSize);
            double lineHeight = LineHeightFor(family, size);
            int lineCap = style.TitleWrap ? Math.Clamp(style.TitleMaxLines, 1, 5) : 1;
            var measured = MeasureTitle(_title, typeface, size, lineHeight, width, style.TitleWrap, dip);
            return Math.Min(measured.Lines, lineCap) * lineHeight;
        }

        // Does the title's font family and typeface from the style
        private static (FontFamily Family, Typeface Typeface) TitleFont(RandomPreviewOverlayStyle style)
        {
            var family = new FontFamily(string.IsNullOrWhiteSpace(style.TitleFontFamily) ? "Segoe UI" : style.TitleFontFamily);
            return (family, new Typeface(family, FontStyles.Normal, FontWeight.FromOpenTypeWeight(Math.Clamp(style.TitleFontWeight, 1, 999)), FontStretches.Normal));
        }

        // Does the title fit in the space from regionTop down to regionBottom (the side margins match regionTop): at the maximum
        // size, wrapping if allowed, shrinking (with Scale to fit) step by step to the minimum until it fits the width, the line cap
        // and the height; at the minimum it is capped to the lines that fit and the last one ends with an ellipsis. Measured in the
        // chosen font and weight. Hidden when even one line at the size it would use does not fit. The text sits directly over the
        // hover fill (no panel of its own), centered, its bottom at regionBottom.
        private void LayoutTitle(RandomPreviewOverlayStyle style, double w, double regionTop, double regionBottom, double dip)
        {
            TitlePanel.Visibility = Visibility.Collapsed;
            if (string.IsNullOrWhiteSpace(_title)) return;

            double textWidth = w - (2 * regionTop);
            double textHeight = regionBottom - regionTop;
            if (textWidth < MinTitleTextWidth || textHeight <= 0) return;

            var (family, typeface) = TitleFont(style);
            double maxSize = Math.Max(1, style.TitleMaxSize);
            double minSize = Math.Clamp(style.TitleMinSize, 1, maxSize);
            int lineCap = style.TitleWrap ? Math.Clamp(style.TitleMaxLines, 1, 5) : 1;

            double chosen = -1;
            (int Lines, double Width) fit = (0, 0);
            double lowest = style.TitleScaleToFit ? minSize : maxSize;
            for (double size = maxSize; size >= lowest - 0.001; size -= TitleSizeStep)
            {
                double lineHeight = LineHeightFor(family, size);
                int linesThatFit = (int)Math.Floor(textHeight / lineHeight);
                if (linesThatFit < 1) continue;
                var measured = MeasureTitle(_title, typeface, size, lineHeight, textWidth, style.TitleWrap, dip);
                if (measured.Lines <= Math.Min(lineCap, linesThatFit))
                {
                    chosen = size;
                    fit = measured;
                    break;
                }
            }

            // Nothing fits whole: the smallest allowed size, capped to the lines that fit, ending in an ellipsis on the full width
            bool trimmed = chosen < 0;
            if (trimmed) chosen = lowest;
            double finalLineHeight = LineHeightFor(family, chosen);
            int lines = trimmed ? Math.Min(lineCap, (int)Math.Floor(textHeight / finalLineHeight)) : fit.Lines;
            if (lines < 1) return;

            // Explicit sizes from the measurement (the widest line, the lines used), never a later Measure: the overlay is often
            // laid out while hidden, and the panel must be centered from the size the text really takes. One pixel of slack
            // keeps the TextBlock from wrapping a line that FormattedText fitted exactly.
            double contentWidth = trimmed ? textWidth : Math.Min(textWidth, Math.Ceiling(fit.Width) + 1);
            double contentHeight = lines * finalLineHeight;

            TitleText.Text = _title;
            TitleText.FontFamily = family;
            TitleText.FontWeight = typeface.Weight;
            TitleText.FontSize = chosen;
            TitleText.Foreground = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(style.TitleColorHex);
            TitleText.TextWrapping = style.TitleWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            TitleText.LineHeight = finalLineHeight;
            TitleText.Width = contentWidth;
            TitleText.Height = contentHeight;
            TitlePanel.Visibility = Visibility.Visible;

            Canvas.SetLeft(TitlePanel, Math.Max(regionTop, (w - contentWidth) / 2));
            Canvas.SetTop(TitlePanel, Math.Max(regionTop, regionBottom - contentHeight));
        }

        // Does the line height for a size: the family's own line spacing (ascent plus descent), rounded up so no glyph is clipped
        private static double LineHeightFor(FontFamily family, double size) => Math.Ceiling(family.LineSpacing * size);

        // Does the title measurement at a size and width: the line count (int.MaxValue when one line is wider than the width
        // without wrapping) and the widest line
        private static (int Lines, double Width) MeasureTitle(string text, Typeface typeface, double size, double lineHeight, double width, bool wrap, double dip)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, Brushes.White, dip);
            if (!wrap) return formatted.Width <= width ? (1, formatted.Width) : (int.MaxValue, width);
            formatted.MaxTextWidth = width;
            formatted.LineHeight = lineHeight;
            return ((int)Math.Round(formatted.Height / lineHeight), formatted.Width);
        }

        // Does the single-line width of a text in a typeface and size
        private static double MeasureWidth(string text, Typeface typeface, double size, double dip)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, Brushes.White, dip).WidthIncludingTrailingWhitespace;
        }
        // [END SECTION: Random Video Previews Overlay Layout]

        // Does the PLAY button colors for its state: hover colors while the pointer is over it, resting colors otherwise
        private void ApplyButtonColors()
        {
            if (_style is not RandomPreviewOverlayStyle style) return;
            bool hover = PlayButtonBox.IsMouseOver;
            PlayButtonText.Foreground = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(hover ? style.ButtonHoverTextColorHex : style.ButtonTextColorHex);
            PlayButtonBox.Background = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(hover ? style.ButtonHoverBgColorHex : style.ButtonBgColorHex);
            PlayButtonBox.BorderBrush = ArcadeStick.ViewModels.MainViewModel.SafeConvertToBrush(hover ? style.ButtonHoverBorderColorHex : style.ButtonBorderColorHex);
        }

        // Does the PLAY hover colors on
        private void PlayButtonBox_MouseEnter(object sender, MouseEventArgs e) => ApplyButtonColors();

        // Does the PLAY resting colors back and forgets a press that left the button
        private void PlayButtonBox_MouseLeave(object sender, MouseEventArgs e)
        {
            _playPressed = false;
            ApplyButtonColors();
        }

        // Does the PLAY press: remembered so only a press and release on the button launch
        private void PlayButtonBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _playPressed = true;
        }

        // Does the PLAY click: a release on the button after a press on it asks MainWindow to launch the game on screen
        private void PlayButtonBox_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (!_playPressed) return;
            _playPressed = false;
            PlayRequested?.Invoke(this, EventArgs.Empty);
        }

        // Passes a glyph click to MainWindow
        private void SoundGlyphButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SoundToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        // Does the inset and rounded clip, same as VideoPreviewLayerControl.ApplyClip: the media area pulls in by the border minus
        // 1px, so the frame covers only 1px of the picture, and its corner radius and clip shrink by the same inset (the hover
        // title and PLAY button live inside this clip, so they never leave the video area)
        private void ApplyClip()
        {
            double availableWidth = MediaSlotContent.ActualWidth;
            double availableHeight = MediaSlotContent.ActualHeight;
            if (availableWidth <= 0 || availableHeight <= 0) return;

            double inset = Math.Max(0, _borderSize - 1);
            double videoRadius = Math.Max(0, _borderRadius - inset);
            _contentRadius = videoRadius;

            MediaSlot.Margin = new Thickness(inset);
            MediaSlot.CornerRadius = new CornerRadius(videoRadius);
            MediaSlotContent.Clip = new RectangleGeometry(
                new Rect(0, 0, availableWidth, availableHeight), videoRadius, videoRadius);
        }
    }
    // [END SECTION: Random Video Previews Layer]
}
