using System;
using System.Globalization;
using System.Windows.Data;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// Returns true if value is non-null. Used to enable/disable detail panels when an item is selected.
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public static readonly NullToBoolConverter Instance = new();

        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
            => value != null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
