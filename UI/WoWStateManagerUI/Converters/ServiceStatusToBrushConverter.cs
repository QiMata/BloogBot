using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.Converters
{
    public class ServiceStatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Green = new(Color.FromRgb(76, 175, 80));
        private static readonly SolidColorBrush Red = new(Color.FromRgb(244, 67, 54));
        private static readonly SolidColorBrush Gray = new(Color.FromRgb(158, 158, 158));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ServiceStatus status)
            {
                return status switch
                {
                    ServiceStatus.Up => Green,
                    ServiceStatus.Down => Red,
                    _ => Gray,
                };
            }
            return Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
