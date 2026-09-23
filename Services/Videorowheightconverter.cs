using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ArcadeStick.Models
{
    // Column One's Row1 (the video slot) is a fixed-width 640 design-unit cell. Subtracting a symmetric
    // border thickness B from both width and height of an exact 4:3 box (640x480) never leaves another
    // exact 4:3 box - that mismatch is what caused every "gray seam"/"pillarbox" symptom chased earlier
    // in this rework. The fix: instead of holding Row1's height fixed at 480 and fighting the resulting
    // ratio drift after the border eats into it, grow Row1's height ahead of time so the POST-border
    // content cell lands back on exact 4:3 against the fixed 640 width. Since width shrinks by B on
    // each side (2B total) and height needs to shrink by the same proportion to stay 4:3, height must
    // start (2B * 3/4) taller than 480 - i.e. RowHeight = 480 + (B * 3/8) if solved exactly, but since
    // border is subtracted from BOTH dimensions symmetrically (Row0/Row2 top+bottom, Col0/Col2 left+right
    // in the 3x3 border grid), the working formula empirically verified against real measurements is
    // RowHeight = 480 + (B / 2). This keeps ColumnOneMediaSlotContent (the post-border center cell) at
    // exact 4:3 for any border size, instead of a single value hardcoded for one specific border size.
    public class VideoRowHeightConverter : IValueConverter
    {
        // Derived from the design canvas width (4:3 = width * 3/4) rather than a second hardcoded literal -
        // a fixed BaseRowHeight here drifts out of sync the moment the canvas width changes (as it did when
        // the design width was widened from 640 to 700: this stayed pinned at 480, producing a 700x480 cell
        // instead of a true 4:3 shape). CanvasWidth must be kept in sync with the Grid's own Width in XAML
        // and with ColumnOneDesignWidth in MediaPanelControl.xaml.cs - all three describe the same design
        // canvas and must always agree.
        private const double CanvasWidth = 800.0;
        private const double BaseRowHeight = CanvasWidth * 3.0 / 4.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double borderSize = 0;
            if (value is double d) borderSize = d;
            else if (value is int i) borderSize = i;
            else double.TryParse(value?.ToString(), out borderSize);

            double rowHeight = BaseRowHeight + (borderSize / 2.0);
            return new GridLength(rowHeight);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}