using System.IO;

namespace OfiConvert.Core;

/// <summary>
/// Decide dónde se escribe el resultado de una conversión. Extraído de <c>MainViewModel</c>.
/// </summary>
/// <remarks>
/// Sus dos garantías son de seguridad, y por eso vive aquí, aparte y con pruebas:
/// <list type="number">
///   <item>La salida <b>no puede escapar</b> de la carpeta destino que eligió el usuario.</item>
///   <item><b>Nunca se sobrescribe</b> un archivo existente: se renombra "archivo (1).pdf".</item>
/// </list>
/// </remarks>
public static class OutputPath
{
    /// <exception cref="InvalidOperationException">Si la ruta resultante se saldría de <paramref name="outputFolder"/>.</exception>
    public static string GetSafe(string outputFolder, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        // GetFileName tira cualquier componente de directorio: "..\..\evil.pdf" se queda en "evil.pdf".
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
            throw new InvalidOperationException($"Nombre de archivo de salida no válido: '{fileName}'.");

        var folder = Path.GetFullPath(outputFolder);
        var candidate = Path.GetFullPath(Path.Combine(folder, safeName));

        // El prefijo se compara CON separador final. Sin él, "C:\Salida" daría por buena "C:\SalidaOtra\x.pdf".
        // Hoy no puede ocurrir (el candidato se construye sobre la propia carpeta), pero una comprobación
        // de seguridad no debe depender de eso para ser correcta.
        var prefix = folder.EndsWith(Path.DirectorySeparatorChar)
            ? folder
            : folder + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta de salida no válida.");

        if (!File.Exists(candidate))
            return candidate;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
        var ext = Path.GetExtension(safeName);

        for (int counter = 1; ; counter++)
        {
            candidate = Path.Combine(folder, $"{nameWithoutExt} ({counter}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }
}
