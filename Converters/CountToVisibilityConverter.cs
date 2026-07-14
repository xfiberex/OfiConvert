using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using OfiConvert.Core;

namespace OfiConvert.Converters;

/// <summary>
/// Un contador → visibilidad. Por defecto se ve cuando el contador es <b>cero</b> (los estados vacíos:
/// «no hay archivos seleccionados»). Con <c>ConverterParameter=Invert</c>, al revés: se ve cuando hay
/// <b>algo que contar</b>.
/// </summary>
/// <remarks>
/// <b>El parámetro se ignoraba.</b> El XAML pasaba <c>Invert</c> al contador de reintentos de cada archivo
/// y este converter nunca lo miraba, así que el contador se veía cuando valía <b>0</b> —el inútil
/// <c>↻ 0</c> en todas las filas— y <b>se escondía justo cuando un archivo había reintentado</b>, que es el
/// único momento en que ese número le dice algo al usuario. La lógica vive en <see cref="VisibilityRules"/>
/// para poder probarla.
/// </remarks>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var count = value as int? ?? 0;
        var invert = VisibilityRules.IsInverted(parameter as string);

        return VisibilityRules.ShowForCount(count, invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
