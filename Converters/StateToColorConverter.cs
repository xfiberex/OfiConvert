using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OfiConvert.Models;

namespace OfiConvert.Converters
{
    public class StateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FileConversionState state)
            {
                return state switch
                {
                    FileConversionState.Pending => new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    FileConversionState.Converting => new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                    FileConversionState.Completed => new SolidColorBrush(Color.FromRgb(16, 124, 16)),
                    FileConversionState.Error => new SolidColorBrush(Color.FromRgb(196, 43, 28)),
                    FileConversionState.Skipped => new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                    _ => new SolidColorBrush(Color.FromRgb(102, 102, 102))
                };
            }
            return new SolidColorBrush(Color.FromRgb(102, 102, 102));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
