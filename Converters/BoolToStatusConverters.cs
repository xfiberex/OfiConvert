using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using OfiConvert.Core;

namespace OfiConvert.Converters;

/// <summary>
/// Éxito → tilde; fallo → icono de error. El historial pintaba un tilde verde FIJO para todas las filas,
/// así que una conversión fallida se veía idéntica a una correcta. La decisión de glifo vive en
/// <see cref="HistoryStatus"/> (Core), que la prueba <c>HistoryStatusTests</c> cubre.
/// </summary>
public class BoolToStatusIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => HistoryStatus.Glyph(value is bool ok && ok);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>
/// Éxito → verde; fallo → rojo. Mismos tonos que StateToColorConverter, para que la cola y el historial
/// hablen el mismo idioma de color.
/// </summary>
public class BoolToStatusColorConverter : IValueConverter
{
    private static readonly SolidColorBrush SuccessBrush = new(Windows.UI.Color.FromArgb(255, 16, 124, 16));
    private static readonly SolidColorBrush ErrorBrush = new(Windows.UI.Color.FromArgb(255, 196, 43, 28));

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool ok && ok ? SuccessBrush : ErrorBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
