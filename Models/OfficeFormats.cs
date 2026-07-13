namespace OfiConvert.Models;

/// <summary>
/// Formatos de entrada admitidos. Fuente única: la usan los motores de conversión, el menú
/// contextual del Explorador y el filtro de los argumentos de activación.
/// </summary>
public static class OfficeFormats
{
    public static readonly string[] SupportedExtensions =
        [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"];

    /// <param name="extension">Extensión con punto (".docx"); no distingue mayúsculas.</param>
    public static bool IsSupported(string extension) =>
        SupportedExtensions.Contains(extension.ToLowerInvariant());
}
