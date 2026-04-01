using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;
using OfiConvert.Models;

namespace OfiConvert.Converters
{
    public class StateToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FileConversionState state)
            {
                return state switch
                {
                    FileConversionState.Pending => SymbolRegular.Clock24,
                    FileConversionState.Validating => SymbolRegular.ShieldCheckmark24,
                    FileConversionState.Converting => SymbolRegular.ArrowSync24,
                    FileConversionState.Retrying => SymbolRegular.ArrowRepeatAll24,
                    FileConversionState.Paused => SymbolRegular.Pause24,
                    FileConversionState.Completed => SymbolRegular.CheckmarkCircle24,
                    FileConversionState.Error => SymbolRegular.ErrorCircle24,
                    FileConversionState.Skipped => SymbolRegular.DismissCircle24,
                    _ => SymbolRegular.Circle24
                };
            }
            return SymbolRegular.Circle24;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
