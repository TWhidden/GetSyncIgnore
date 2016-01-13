using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BitTorrentSyncIgnore.Converter
{
    public class InverseVisibilityConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {

            var boolVal = false;

            if (value is int || value is short || value is long || value is byte || value is decimal)
            {
                var boundValue = decimal.Parse(value.ToString());
                decimal alternateValue = 0;
                if (parameter != null && decimal.TryParse(parameter.ToString(), out alternateValue))
                {
                    boolVal = boundValue == alternateValue;
                }
                else
                {
                    boolVal = boundValue != 0;
                }

            }
            else if (value is string)
            {
                boolVal = !string.IsNullOrWhiteSpace(value.ToString());
            }

            else if (value is bool)
            {
                boolVal = (bool)value;
            }

            var visibility = (bool)boolVal;
            return visibility ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            Visibility visibility = (Visibility)value;
            return (visibility != Visibility.Visible);
        }
    }
}