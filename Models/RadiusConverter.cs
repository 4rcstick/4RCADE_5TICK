using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ArcadeStick.Models
{
    // Splits a single bound radius value across only two corners of a CornerRadius, based on
    // ConverterParameter ("Top" or "Bottom") - lets one theme property (e.g. OptionsMenuBorderRadius)
    // drive a "tabbed folder" look, where the tab strip rounds its top corners and the content area
    // below it rounds its bottom corners, meeting with square edges in between so they read as one
    // continuous shape. XAML can't embed a {Binding} inside a comma-separated CornerRadius string
    // directly, hence this converter.
    public class RadiusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double radius = 0;
            if (value is double d) radius = d;
            else if (value is int i) radius = i;
            else double.TryParse(value?.ToString(), out radius);

            string mode = parameter?.ToString() ?? "";

            return mode.Equals("Top", StringComparison.OrdinalIgnoreCase)
                ? new CornerRadius(radius, radius, 0, 0)
                : new CornerRadius(0, 0, radius, radius);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}