using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ArcadeStick.Views
{
    // [SECTION: Background Grid Layer]
    // Theme Builder editor aid: a full-window grid of 1px lines drawn behind every layer (MainWindow's
    // BackgroundGridLayer, driven by MainWindow.SetBackgroundGrid). Every line is snapped to a device pixel,
    // at round((start + k * spacing) * scale) / scale and 1 / scale thick, so it stays one sharp screen pixel at any
    // spacing (whole or decimal) and any display scaling. start is the offset reduced into one cell, so the lines pass
    // through the offset point and still fill the whole window on both sides of it. Each line is its own
    // DrawRectangle with one frozen opaque brush: a single combined geometry (thousands of overlapping crossings)
    // broke up into missing-line wedges in the hardware renderer at spacings of 10 and below. The snapped positions
    // are cached and rebuilt only when the size, spacing, offsets or DPI change; a color change only swaps the frozen
    // brush.
    public class BackgroundGridLayer : FrameworkElement
    {
        // Below this spacing in screen pixels the lines would touch, so the layer is a solid line-color fill
        private const double MinSpacingDevicePixels = 2;

        private double _spacing = 20;
        private double _offsetX;
        private double _offsetY;
        private Color _lineColor;
        private SolidColorBrush? _lineBrush;

        // Snapped line positions (DIPs) and the size and scale they were built for; null = rebuild on next render
        private List<double>? _lineXs;
        private List<double>? _lineYs;
        private double _builtScaleX;
        private double _builtScaleY;

        // Sets aliased edges so the snapped lines are never smoothed
        public BackgroundGridLayer()
        {
            RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        }

        // Sets the spacing and offsets (DIPs, from the window's top-left) and the solid line color; redraws only when
        // one changed
        public void SetGrid(double spacing, double offsetX, double offsetY, Color lineColor)
        {
            bool changed = false;
            if (spacing != _spacing || offsetX != _offsetX || offsetY != _offsetY)
            {
                _spacing = spacing;
                _offsetX = offsetX;
                _offsetY = offsetY;
                _lineXs = null;
                changed = true;
            }
            if (_lineBrush == null || lineColor != _lineColor)
            {
                _lineColor = lineColor;
                var brush = new SolidColorBrush(lineColor);
                brush.Freeze();
                _lineBrush = brush;
                changed = true;
            }
            if (changed) InvalidateVisual();
        }

        // Rebuilds the line positions when the layer resizes (window resize, fullscreen on or off)
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            _lineXs = null;
            InvalidateVisual();
        }

        // Rebuilds the line positions when the window moves to a monitor with a different scaling
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            _lineXs = null;
            InvalidateVisual();
        }

        // Draws each cached line as its own rectangle (vertical full height, horizontal full width), or a solid
        // line-color fill when the spacing is under 2 screen pixels
        protected override void OnRender(DrawingContext dc)
        {
            if (_lineBrush == null || !(_spacing > 0) || ActualWidth <= 0 || ActualHeight <= 0) return;

            var dpi = VisualTreeHelper.GetDpi(this);
            if (_spacing * dpi.DpiScaleX < MinSpacingDevicePixels || _spacing * dpi.DpiScaleY < MinSpacingDevicePixels)
            {
                dc.DrawRectangle(_lineBrush, null, new Rect(RenderSize));
                return;
            }

            if (_lineXs == null || _lineYs == null || _builtScaleX != dpi.DpiScaleX || _builtScaleY != dpi.DpiScaleY)
            {
                _lineXs = BuildPositions(ActualWidth, _spacing, _offsetX, dpi.DpiScaleX);
                _lineYs = BuildPositions(ActualHeight, _spacing, _offsetY, dpi.DpiScaleY);
                _builtScaleX = dpi.DpiScaleX;
                _builtScaleY = dpi.DpiScaleY;
            }

            double lineWidth = 1 / dpi.DpiScaleX;
            double lineHeight = 1 / dpi.DpiScaleY;
            foreach (double x in _lineXs)
                dc.DrawRectangle(_lineBrush, null, new Rect(x, 0, lineWidth, ActualHeight));
            foreach (double y in _lineYs)
                dc.DrawRectangle(_lineBrush, null, new Rect(0, y, ActualWidth, lineHeight));
        }

        // Returns the device-pixel-snapped line positions every spacing DIPs through the offset, from 0 up to length.
        // The offset is first reduced into [0, spacing) so a huge or negative offset never walks millions of lines. The
        // line one cell before start is kept when it snaps to 0, which covers a reduced offset that lands a hair under
        // spacing through floating-point error (its line belongs at 0, not one cell later).
        private static List<double> BuildPositions(double length, double spacing, double offset, double scale)
        {
            double start = ((offset % spacing) + spacing) % spacing;
            var positions = new List<double>();
            for (int k = -1; start + k * spacing < length; k++)
            {
                // Ties always round up (not to even), so a whole spacing with a half-pixel offset keeps even gaps
                double snapped = Math.Floor((start + k * spacing) * scale + 0.5) / scale;
                if (snapped >= 0) positions.Add(snapped);
            }
            return positions;
        }
    }
    // [END SECTION: Background Grid Layer]
}
