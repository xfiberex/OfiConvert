using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace OfiConvert.Converters;

/// <summary>
/// Cabecera del panel de resultados: sin errores → tilde; con errores → aviso. El icono estaba FIJO a un
/// tilde verde, así que "Conversión finalizada con errores" salía encabezada por un check de éxito.
/// </summary>
public class ErrorsToResultIconConverter : IValueConverter
{
    private static readonly string CheckGlyph = ((char)0xE73E).ToString();     // Completed
    private static readonly string WarningGlyph = ((char)0xE7BA).ToString();   // Warning

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool hasErrors && hasErrors ? WarningGlyph : CheckGlyph;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>
/// Sin errores → verde; con errores → ámbar (aviso, no rojo total: parte del lote sí se convirtió).
/// Mismos tonos que Success/Warning de la app.
/// </summary>
public class ErrorsToResultColorConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessBrush = new(Windows.UI.Color.FromArgb(255, 16, 185, 129));   // #10B981
    private static readonly SolidColorBrush WarningBrush = new(Windows.UI.Color.FromArgb(255, 245, 158, 11));   // #F59E0B

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool hasErrors && hasErrors ? WarningBrush : SuccessBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
