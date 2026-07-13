using OfiConvert.Models;

namespace OfiConvert.Helpers;

/// <summary>
/// Extrae los archivos Office de una activación (menú contextual del Explorador, "Abrir con",
/// o una segunda instancia redirigida a la que ya está abierta).
/// </summary>
public static class ActivationArguments
{
    /// <summary>
    /// Convierte la línea de comandos de una activación en los archivos Office que contiene.
    /// </summary>
    /// <remarks>
    /// En una app <b>unpackaged</b>, <c>ILaunchActivatedEventArgs.Arguments</c> llega como una única
    /// cadena que <b>incluye la ruta del propio ejecutable</b> como primer token — a diferencia de la
    /// app empaquetada, donde solo vienen los argumentos. Por eso no se descarta el primer token a
    /// ciegas: se filtra por extensión y existencia, y el ".exe" se cae solo.
    /// </remarks>
    public static List<string> GetOfficeFiles(string? commandLine) =>
        string.IsNullOrWhiteSpace(commandLine)
            ? []
            : GetOfficeFiles(Tokenize(commandLine));

    /// <summary>
    /// Filtra los tokens que son archivos Office existentes, sin repetidos y conservando el orden.
    /// </summary>
    public static List<string> GetOfficeFiles(IEnumerable<string> tokens)
    {
        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
                continue;

            string path;
            try
            {
                path = Path.GetFullPath(token.Trim());
            }
            catch
            {
                continue; // Token que no es una ruta válida (una opción, basura…).
            }

            if (!OfficeFormats.IsSupported(Path.GetExtension(path)))
                continue;

            if (File.Exists(path) && seen.Add(path))
                files.Add(path);
        }

        return files;
    }

    /// <summary>
    /// Parte una línea de comandos en tokens respetando las comillas — las rutas del Explorador
    /// llegan entrecomilladas y casi siempre tienen espacios.
    /// </summary>
    public static List<string> Tokenize(string commandLine)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        foreach (char c in commandLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }
}
