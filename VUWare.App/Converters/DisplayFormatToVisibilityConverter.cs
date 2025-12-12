using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VUWare.App.Converters
{
    /// <summary>
    /// Converts a display format string to Visibility.
    /// Shows the control when display format is "value", hides it when "percentage".
    /// </summary>
    public class DisplayFormatToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string displayFormat)
                return Visibility.Collapsed;

            // Show when display format is "value" (case-insensitive)
            return displayFormat.Equals("value", StringComparison.OrdinalIgnoreCase) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
