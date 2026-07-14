using System.IO;

namespace OfiConvert.Core;

/// <summary>
/// Decide dónde se escribe el resultado de una conversión. Extraído de <c>MainViewModel</c>.
/// </summary>
/// <remarks>
/// Sus dos garantías son de seguridad, y por eso vive aquí, aparte y con pruebas:
/// <list type="number">
///   <item>La salida <b>no puede escapar</b> de la carpeta destino.</item>
///   <item><b>Nunca se sobrescribe</b> un archivo existente: se renombra "archivo (1).pdf".</item>
/// </list>
/// La carpeta destino es la que eligió el usuario o, si no eligió ninguna, la del propio documento — pero
/// eso lo decide el ViewModel: aquí solo se garantiza que nada se salga de la carpeta que llegue.
/// </remarks>
public static class OutputPath
{
    /// <summary>Ruta de un ARCHIVO de salida, sin colisiones.</summary>
    /// <exception cref="InvalidOperationException">Si la ruta resultante se saldría de <paramref name="outputFolder"/>.</exception>
    public static string GetSafe(string outputFolder, string fileName)
    {
        var candidate = Confine(outputFolder, fileName, out var folder, out var safeName);

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

    /// <summary>
    /// Ruta de una SUBCARPETA de salida (una presentación a PNG/JPG son N imágenes, y van juntas).
    /// </summary>
    /// <remarks>
    /// A diferencia de <see cref="GetSafe"/>, aquí <b>no</b> se renombra si ya existe: convertir dos veces
    /// la misma presentación reescribe sus imágenes en la misma carpeta, que es lo que el usuario espera.
    /// La garantía que sí se mantiene es la de contención.
    /// </remarks>
    public static string GetSafeFolder(string outputFolder, string folderName)
        => Confine(outputFolder, folderName, out _, out _);

    /// <summary>
    /// Compone <paramref name="outputFolder"/> + <paramref name="name"/> y <b>rechaza</b> lo que se salga
    /// de la carpeta.
    /// </summary>
    private static string Confine(string outputFolder, string name, out string folder, out string safeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // GetFileName tira cualquier componente de directorio: "..\..\evil.pdf" se queda en "evil.pdf".
        safeName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(safeName))
            throw new InvalidOperationException($"Nombre de salida no válido: '{name}'.");

        folder = Path.GetFullPath(outputFolder);
        var candidate = Path.GetFullPath(Path.Combine(folder, safeName));

        // El prefijo se compara CON separador final. Sin él, "C:\Salida" daría por buena "C:\SalidaOtra\x".
        // Hoy no puede ocurrir (el candidato se construye sobre la propia carpeta), pero una comprobación
        // de seguridad no debe depender de eso para ser correcta.
        var prefix = folder.EndsWith(Path.DirectorySeparatorChar)
            ? folder
            : folder + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta de salida no válida.");

        return candidate;
    }
}
