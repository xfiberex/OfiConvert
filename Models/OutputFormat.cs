namespace OfiConvert.Models;

/// <summary>
/// Formatos de salida. El mapeo (qué formato admite cada documento, con qué extensión) vive en
/// <c>Core/OutputFormats.cs</c>: aquí solo está el dato.
/// </summary>
public enum OutputFormat
{
    PDF,
    HTML,
    CSV,
    PNG,
    JPG
}
