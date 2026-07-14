namespace OfiConvert.Core;

/// <summary>
/// Las reglas de "¿esto se ve o no?" que usan los converters del XAML, separadas de <c>Visibility</c> para
/// poder probarlas sin arrancar WinUI.
/// </summary>
/// <remarks>
/// No es ceremonia: <c>CountToVisibilityConverter</c> llevaba desde siempre <b>ignorando su
/// ConverterParameter</b>, y nada lo cazaba porque un converter mal escrito no rompe el build — solo
/// enseña, o esconde, lo que no debe.
/// </remarks>
public static class VisibilityRules
{
    /// <param name="parameter">El <c>ConverterParameter</c> del binding; solo <c>"Invert"</c> significa algo.</param>
    public static bool IsInverted(string? parameter) =>
        string.Equals(parameter, "Invert", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sin invertir: se ve cuando el contador es CERO (es el estado vacío — «no hay archivos»).
    /// Invertido: se ve cuando HAY algo que contar (el contador de reintentos de un archivo).
    /// </summary>
    public static bool ShowForCount(int count, bool invert) =>
        invert ? count > 0 : count == 0;
}
