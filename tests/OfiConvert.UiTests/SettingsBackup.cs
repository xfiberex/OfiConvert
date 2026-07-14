namespace OfiConvert.UiTests;

/// <summary>
/// Copia y restaura los datos reales del usuario en <c>%AppData%\OfiConvert</c>.
/// </summary>
/// <remarks>
/// La app es <b>unpackaged</b>: no tiene almacenamiento aislado por prueba. Su <c>settings.json</c>,
/// su <c>queue.json</c> y su <c>history.json</c> viven en el MISMO sitio que usa la instalación real de
/// quien corre los tests. Sin este respaldo, una prueba que cambia el idioma a japonés se lo dejaría
/// cambiado, y una que toca la cola le borraría los archivos que tuviera pendientes.
/// </remarks>
public sealed class SettingsBackup
{
    private readonly Dictionary<string, byte[]?> _files;

    private SettingsBackup(Dictionary<string, byte[]?> files) => _files = files;

    public static SettingsBackup Capture()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OfiConvert");

        var files = new Dictionary<string, byte[]?>();
        foreach (var name in new[] { "settings.json", "queue.json", "history.json" })
        {
            var path = Path.Combine(folder, name);
            files[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        return new SettingsBackup(files);
    }

    public void Restore()
    {
        foreach (var (path, original) in _files)
        {
            try
            {
                if (original is null)
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                else
                {
                    File.WriteAllBytes(path, original);
                }
            }
            catch
            {
                // Mejor esfuerzo: no tapar el resultado real de las pruebas con un fallo de limpieza.
            }
        }
    }
}
