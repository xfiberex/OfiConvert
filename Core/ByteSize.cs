namespace OfiConvert.Core;

/// <summary>
/// Formatea un tamaño en bytes para mostrarlo al usuario ("1,5 MB").
/// </summary>
/// <remarks>
/// Fuente única. Antes existía DUPLICADO, y las dos copias ya no coincidían:
/// <c>MainViewModel.FormatFileSize</c> llegaba hasta TB y <c>ConversionHistoryService.FormatSize</c> se
/// quedaba en GB, así que un archivo de 2 TB salía como "2 TB" en la cola y como "2048 GB" en el mismo
/// historial exportado.
/// </remarks>
public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <param name="bytes">Tamaño en bytes. Negativo o cero se muestran como "0 B": no existe un archivo de tamaño negativo.</param>
    public static string Format(long bytes)
    {
        if (bytes <= 0) return "0 B";

        double size = bytes;
        int unit = 0;

        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        // Cultura actual a propósito: es texto que lee el usuario, junto al resto de la UI traducida.
        return $"{size:0.##} {Units[unit]}";
    }
}
