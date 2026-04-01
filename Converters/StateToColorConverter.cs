using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using OfiConvert.Models;

namespace OfiConvert.Converters
{
    public class StateToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush PendingBrush = CreateFrozen(102, 102, 102);
        private static readonly SolidColorBrush ActiveBrush = CreateFrozen(0, 120, 212);
        private static readonly SolidColorBrush WarningBrush = CreateFrozen(245, 158, 11);
        private static readonly SolidColorBrush PausedBrush = CreateFrozen(234, 179, 8);
        private static readonly SolidColorBrush SuccessBrush = CreateFrozen(16, 124, 16);
        private static readonly SolidColorBrush ErrorBrush = CreateFrozen(196, 43, 28);
        private static readonly SolidColorBrush SkippedBrush = CreateFrozen(153, 153, 153);

        private static SolidColorBrush CreateFrozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FileConversionState state)
            {
                return state switch
                {
                    FileConversionState.Pending => PendingBrush,
                    FileConversionState.Validating => ActiveBrush,
                    FileConversionState.Converting => ActiveBrush,
                    FileConversionState.Retrying => WarningBrush,
                    FileConversionState.Paused => PausedBrush,
                    FileConversionState.Completed => SuccessBrush,
                    FileConversionState.Error => ErrorBrush,
                    FileConversionState.Skipped => SkippedBrush,
                    _ => PendingBrush
                };
            }
            return PendingBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
