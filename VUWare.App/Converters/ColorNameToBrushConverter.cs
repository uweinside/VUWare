using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VUWare.App.Converters
{
    /// <summary>
    /// Converts a color name string to a SolidColorBrush for visual display.
    /// </summary>
    public class ColorNameToBrushConverter : IValueConverter
    {
        private static readonly Dictionary<string, SolidColorBrush> ColorMap = new()
        {
            ["Red"] = new SolidColorBrush(Colors.Red),
            ["Green"] = new SolidColorBrush(Colors.Lime),  // Lime is brighter than Green
            ["Blue"] = new SolidColorBrush(Colors.Blue),
            ["Yellow"] = new SolidColorBrush(Colors.Yellow),
            ["Cyan"] = new SolidColorBrush(Colors.Cyan),
            ["Magenta"] = new SolidColorBrush(Colors.Magenta),
            ["Orange"] = new SolidColorBrush(Colors.Orange),
            ["Purple"] = new SolidColorBrush(Colors.Purple),
            ["Pink"] = new SolidColorBrush(Colors.Pink),
            ["White"] = new SolidColorBrush(Colors.White),
            ["Off"] = new SolidColorBrush(Color.FromRgb(64, 64, 64))  // Dark gray for "Off"
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorName && ColorMap.TryGetValue(colorName, out var brush))
            {
                return brush;
            }
            
            // Default to transparent if color not found
            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
