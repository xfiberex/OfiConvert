using System.Reflection;

namespace OfiConvert.Core;

/// <summary>
/// Los textos legales, leídos de dentro del propio <c>.exe</c>.
/// </summary>
/// <remarks>
/// Van <b>embebidos</b> (ver los <c>EmbeddedResource</c> del <c>.csproj</c>) y no como archivos sueltos
/// junto al ejecutable: un archivo se borra, se queda atrás en una actualización o no llega al
/// instalador, y entonces la app dejaría de mostrar la atribución que sus licencias le <b>obligan</b> a
/// mostrar. Dentro del ensamblado no puede desaparecer.
///
/// Defensivo a propósito: si un recurso faltara, devuelve cadena vacía y la UI muestra "texto no
/// disponible" en vez de reventar. Que eso no pase lo comprueba <c>LegalTextTests</c>.
/// </remarks>
public static class LegalText
{
    /// <summary>Licencia de OfiConvert (MIT).</summary>
    public static string License() => Read("OfiConvert.LICENSE.txt");

    /// <summary>Avisos y atribuciones de los componentes de terceros que viajan en el instalador.</summary>
    public static string ThirdParty() => Read("OfiConvert.THIRD-PARTY-NOTICES.txt");

    /// <summary>Versión del ensamblado en ejecución ("2.4.0").</summary>
    public static string Version()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    private static string Read(string resourceName)
    {
        try
        {
            using var stream = typeof(LegalText).Assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return "";

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch
        {
            return "";
        }
    }
}
