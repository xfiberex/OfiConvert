using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using OfiConvert.Models;

namespace OfiConvert.Converters;

public class StateToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush PendingBrush = new(Windows.UI.Color.FromArgb(255, 102, 102, 102));
    private static readonly SolidColorBrush ActiveBrush = new(Windows.UI.Color.FromArgb(255, 0, 120, 212));
    private static readonly SolidColorBrush WarningBrush = new(Windows.UI.Color.FromArgb(255, 245, 158, 11));
    private static readonly SolidColorBrush PausedBrush = new(Windows.UI.Color.FromArgb(255, 234, 179, 8));
    private static readonly SolidColorBrush SuccessBrush = new(Windows.UI.Color.FromArgb(255, 16, 124, 16));
    private static readonly SolidColorBrush ErrorBrush = new(Windows.UI.Color.FromArgb(255, 196, 43, 28));
    private static readonly SolidColorBrush SkippedBrush = new(Windows.UI.Color.FromArgb(255, 153, 153, 153));

    public object Convert(object value, Type targetType, object parameter, string language)
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

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
