using Microsoft.UI.Xaml.Data;
using OfiConvert.Helpers;

namespace OfiConvert.Converters;

/// <summary>
/// Segundos → texto con unidad ("2,4 s"). El historial mostraba el double crudo ("2.4"), un número
/// desnudo flotando en la fila. El separador decimal sale en la cultura del usuario ("2,4" en es).
/// </summary>
public class DurationToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        double seconds = value is double d ? d : 0;
        return $"{seconds:0.0} {LocalizationService.Instance["UnitSeconds"]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
