using System;
using System.Windows.Data;

namespace BitTorrentSyncIgnore.Converter
{
    public class InverseBooleanConverter : IValueConverter 
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {


            if(!(targetType == typeof(bool) || targetType == typeof(bool?)))
                throw new InvalidOperationException("The target must be a boolean");
            //if(value.GetType() != typeof(bool))
            //    throw new InvalidOperationException("The source must be a boolean");

            bool boolVal = false;

            if (value is int || value is byte || value is long || value is short)
            {
                boolVal = (long.Parse(value.ToString())) != 0;
            }else if(value is bool)
            {
                boolVal = (bool) value;
            }

            return !boolVal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");
            if (value.GetType() != typeof(bool))
                throw new InvalidOperationException("The source must be a boolean");
            return !(bool)value;
        }
    }
}
