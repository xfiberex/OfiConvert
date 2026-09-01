using System.IO;

namespace OfiConvert.Core;

/// <summary>
/// La línea de comandos de <c>soffice</c>, construida aparte para poder comprobarla <b>sin LibreOffice
/// instalado</b>.
/// </summary>
/// <remarks>
/// 🔴 <b>LibreOffice NO admite dos procesos headless sobre el mismo perfil de usuario</b> (TJ-25). El
/// segundo no arranca su propio motor: detecta el perfil ocupado y <b>se enchufa al primero</b> por IPC,
/// o falla. Con <c>MaxParallelConversions</c> llegando a 8, un lote convertido por LibreOffice degrada a
/// serie en el mejor caso y devuelve errores en el peor — y ninguno de los dos se parece a un fallo de
/// conversión, así que cuesta de diagnosticar.
///
/// La cura es darle a cada proceso <b>su propio perfil</b> con <c>-env:UserInstallation=</c>, que espera
/// una <b>URL de archivo</b>, no una ruta: <c>file:///C:/Users/…</c>, con barras normales. Pasarle una
/// ruta de Windows tal cual (<c>C:\Users\…</c>) no da error — LibreOffice la ignora y vuelve al perfil
/// compartido, con lo que el problema sigue ahí <b>en silencio</b>. Por eso la conversión a URL se prueba
/// carácter a carácter.
///
/// ⚠️ <b>PENDIENTE DE VERIFICACIÓN DE PUNTA A PUNTA:</b> en la máquina donde se escribió esto no hay
/// LibreOffice instalado, así que el criterio de aceptación de TJ-25 —un lote de 8 documentos con
/// paralelismo 4— <b>no se ha podido ejecutar</b>. Lo que sí está probado es la línea de comandos que se
/// construye. Ver <c>ROADMAP.md</c>.
/// </remarks>
public static class LibreOfficeCommand
{
    /// <summary>Convierte una ruta de Windows en la URL <c>file://</c> que espera <c>-env:</c>.</summary>
    /// <remarks>
    /// <c>Uri</c> haría casi esto, pero además percent-codifica: un perfil bajo
    /// <c>C:\Users\Ana Pérez\</c> se volvería <c>Ana%20P%C3%A9rez</c>. Aquí las carpetas las creamos
    /// nosotros con nombres ASCII sin espacios, así que basta —y se ve— con normalizar las barras.
    /// </remarks>
    public static string ToFileUrl(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return "file:///" + path.Replace('\\', '/').TrimStart('/');
    }

    /// <summary>Crea un perfil de usuario exclusivo para <b>un</b> proceso de <c>soffice</c>.</summary>
    public static string CreateProfileFolder(string? tempRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(tempRoot) ? Path.GetTempPath() : tempRoot;
        var folder = Path.Combine(root, $"OfiConvert-loprofile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Los argumentos completos de una conversión.</summary>
    /// <param name="formatArg">El destino tal y como lo nombra LibreOffice (<c>pdf</c>, <c>csv</c>…).</param>
    /// <param name="workFolder">Carpeta de salida exclusiva de esta conversión.</param>
    /// <param name="profileFolder">Perfil exclusivo de este proceso. Es lo que permite el paralelismo.</param>
    /// <param name="sourcePath">El documento de origen.</param>
    public static string BuildArguments(
        string formatArg, string workFolder, string profileFolder, string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formatArg);
        ArgumentException.ThrowIfNullOrWhiteSpace(workFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        // -env: va DELANTE. LibreOffice lee los parámetros de entorno antes de decidir si arranca motor
        // propio o se enchufa a uno existente; detrás de --convert-to llega tarde.
        return $"-env:UserInstallation={ToFileUrl(profileFolder)} " +
               $"--headless --norestore --convert-to {formatArg} " +
               $"--outdir \"{workFolder}\" \"{sourcePath}\"";
    }

    /// <summary>
    /// <c>true</c> si alguna ruta lleva comillas: no se pueden citar sin abrir un agujero de inyección.
    /// </summary>
    public static bool HasUnquotablePath(params string[] paths)
        => paths.Any(p => p is not null && p.Contains('"', StringComparison.Ordinal));
}
