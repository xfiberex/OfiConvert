namespace OfiConvert.Core;

/// <summary>
/// Escapa un campo para el CSV del historial exportado.
/// </summary>
/// <remarks>
/// Además de doblar las comillas, <b>neutraliza la inyección de fórmulas</b>: Excel/LibreOffice/Sheets
/// evalúan una celda que empieza por <c>= + - @</c> TAB o CR al abrir el archivo, así que el nombre de un
/// archivo convertido —que lo elige quien lo creó, no esta app— podría ejecutar una fórmula en el equipo
/// de quien abra el historial. El prefijo <c>'</c> la convierte en texto.
/// </remarks>
public static class CsvField
{
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        // El disparador se busca en el valor ORIGINAL y las comillas se doblan después. Da exactamente el
        // mismo resultado que mirarlo sobre el valor ya escapado (doblar comillas no puede convertir el
        // primer carácter en disparador, ni al revés), pero así se lee sin tener que demostrarlo.
        var prefix = FormulaTriggers.Contains(value[0]) ? "'" : "";
        return prefix + value.Replace("\"", "\"\"");
    }
}
