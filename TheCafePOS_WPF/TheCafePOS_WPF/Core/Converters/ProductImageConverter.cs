using System;
using System.Globalization;
using System.Windows.Data;

namespace TheCafePOS_WPF.Core.Converters
{
    /// <summary>Turns a product image path into a bitmap, or null when there is no usable photo.</summary>
    public class ProductImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => ProductImages.Load(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
