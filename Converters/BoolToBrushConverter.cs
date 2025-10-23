using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RimWorldModTranslate.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) 
            => value is true ? Brushes.OrangeRed : Brushes.Gray;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}