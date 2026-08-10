using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YtDlpGui.Converters
{
    public class WidthToHeaderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WindowState state && parameter is string texts)
            {
                var parts = texts.Split('|');
                if (parts.Length == 2)
                {
                    return state == WindowState.Maximized ? parts[1] : parts[0];
                }
            }
            return parameter ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
