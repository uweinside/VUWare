using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VUWare.App.Converters
{
    /// <summary>
    /// Converts a color mode string to Visibility for showing/hiding color options.
    /// </summary>
    public class ColorModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string colorMode || parameter is not string expectedMode)
                return Visibility.Collapsed;

            return colorMode == expectedMode ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
