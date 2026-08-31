namespace OfiConvert.Core;

/// <summary>
/// Un mensaje destinado al usuario, expresado como <b>clave de traducción</b> y sus argumentos.
/// </summary>
/// <param name="Key">Clave de <c>Lang/*.xaml</c> (p. ej. <c>MsgPasswordProtected</c>).</param>
/// <param name="Args">Valores para los huecos <c>{0}</c>, <c>{1}</c>… de la plantilla.</param>
/// <remarks>
/// Existe porque el proyecto ha escrito texto de interfaz en español dentro del código <b>cinco veces</b>
/// (TJ-06, 2026-08-31), la última con las traducciones ya escritas y sin usar: el literal
/// «El archivo no existe.» de <c>FileValidationService</c> era <b>idéntico</b> al valor de
/// <c>MsgFileNotFound</c> en los ocho idiomas.
///
/// La causa no es el descuido, es la forma: un servicio que devuelve <c>string</c> no tiene manera de
/// devolver algo traducible, porque <b>no sabe ni debe saber</b> en qué idioma está la app —y menos
/// cuando puede correr en un hilo de fondo—. Devolviendo una clave, la traducción ocurre en el borde de
/// la UI, que es el único sitio que conoce el idioma. `Core/` sigue sin depender de nada.
/// </remarks>
public sealed record UserMessage(string Key, params object[] Args)
{
    /// <summary>Atajo legible: <c>UserMessage.Of("MsgFileTooBig", nombre)</c>.</summary>
    public static UserMessage Of(string key, params object[] args) => new(key, args);
}
