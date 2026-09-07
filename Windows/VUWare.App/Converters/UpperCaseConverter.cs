using System;
using System.Globalization;
using System.Windows.Data;

namespace VUWare.App.Converters
{
    /// <summary>
    /// Converts a string to have the first character capitalized for display purposes.
    /// </summary>
    public class UpperCaseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                return char.ToUpper(str[0], culture) + str.Substring(1);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                return char.ToLower(str[0], culture) + str.Substring(1);
            }
            return value;
        }
    }
}
