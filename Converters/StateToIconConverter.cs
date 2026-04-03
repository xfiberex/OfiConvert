using Microsoft.UI.Xaml.Data;
using OfiConvert.Models;

namespace OfiConvert.Converters;

/// <summary>
/// Converts FileConversionState to a Segoe Fluent Icons glyph string.
/// </summary>
public class StateToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is FileConversionState state)
        {
            return state switch
            {
                FileConversionState.Pending => "\uE823",     // Clock
                FileConversionState.Validating => "\uEA18",  // Shield
                FileConversionState.Converting => "\uE895",  // Sync
                FileConversionState.Retrying => "\uE72C",    // Repeat
                FileConversionState.Paused => "\uE769",      // Pause
                FileConversionState.Completed => "\uE73E",   // Checkmark
                FileConversionState.Error => "\uEA39",       // Error
                FileConversionState.Skipped => "\uE711",     // Dismiss
                _ => "\uEA3A"                                // Circle
            };
        }
        return "\uEA3A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
