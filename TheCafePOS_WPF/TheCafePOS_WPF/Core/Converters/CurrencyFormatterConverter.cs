using System;
using System.Globalization;
using System.Windows.Data;

namespace TheCafePOS_WPF.Core.Converters
{
    public class CurrencyFormatterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal val)
            {
                return $"{val:N0}đ";
            }
            if (value is double dVal)
            {
                return $"{dVal:N0}đ";
            }
            if (value is int iVal)
            {
                return $"{iVal:N0}đ";
            }
            return "0đ";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
