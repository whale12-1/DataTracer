using System;
using System.Globalization;
using System.Windows.Data;

namespace DataTrace.UI.Converters
{
    public class EqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value) return parameter;
            return Binding.DoNothing;
        }
    }
}