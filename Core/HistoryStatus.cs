namespace OfiConvert.Core;

/// <summary>
/// Que icono corresponde al resultado de una conversion en el historial. El historial pintaba un tilde
/// verde FIJO para todas las filas, asi que un fallo se veia identico a un exito. La decision vive aqui,
/// en logica pura (devuelve una cadena de glifo Segoe Fluent Icons), para poder probarla sin arrancar la
/// UI: lo unico que importa es que exito y fallo NO compartan glifo.
/// </summary>
public static class HistoryStatus
{
    public static readonly string SuccessGlyph = ((char)0xE73E).ToString();   // Completed
    public static readonly string ErrorGlyph = ((char)0xEA39).ToString();     // Error

    public static string Glyph(bool success) => success ? SuccessGlyph : ErrorGlyph;
}