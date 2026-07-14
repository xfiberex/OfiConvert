using System.Text;

namespace OfiConvert.UiTests;

/// <summary>
/// Respalda los datos reales del usuario en <c>%AppData%\OfiConvert</c>, deja la app en un estado
/// CONOCIDO para las pruebas, y lo restaura todo al terminar.
/// </summary>
/// <remarks>
/// La app es <b>unpackaged</b>: no tiene almacenamiento aislado por prueba. Su <c>settings.json</c>, su
/// <c>queue.json</c> y su <c>history.json</c> viven en el MISMO sitio que usa la instalación real de quien
/// corre los tests. De ahí las dos mitades de esta clase:
///
/// 1. <b>Respaldar y restaurar</b>, para no dejarle al usuario el idioma cambiado ni la cola borrada.
/// 2. <b>Sembrar un estado conocido</b> antes de arrancar. Sin esto, las pruebas dependen de con qué se
///    encuentren: «el botón Convertir está apagado porque no hay archivos» fallaría en la máquina de
///    alguien que tuviera una cola pendiente — y la app no tendría ningún fallo. Un test que depende del
///    estado de quien lo corre no prueba nada.
/// </remarks>
public sealed class SettingsBackup
{
    private readonly Dictionary<string, byte[]?> _files;

    private SettingsBackup(Dictionary<string, byte[]?> files) => _files = files;

    private static string DataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OfiConvert");

    /// <summary>Guarda lo que haya y deja la app en el estado de partida de las pruebas.</summary>
    public static SettingsBackup CaptureAndReset()
    {
        var files = new Dictionary<string, byte[]?>();
        foreach (var name in new[] { "settings.json", "queue.json", "history.json" })
        {
            var path = Path.Combine(DataFolder, name);
            files[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        var backup = new SettingsBackup(files);
        backup.Reset();
        return backup;
    }

    /// <summary>Cola e historial VACÍOS, idioma español y tema del sistema: el punto de partida de todos los tests.</summary>
    private void Reset()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);

            File.WriteAllText(
                Path.Combine(DataFolder, "settings.json"),
                """
                {
                  "Theme": "System",
                  "Language": "es",
                  "MaxParallelConversions": 2,
                  "AutoRetryEnabled": true,
                  "MaxRetryCount": 3,
                  "MinimizeToTray": false,
                  "ShowNotifications": true,
                  "LastOutputFolder": "",
                  "DefaultOutputFormat": 0
                }
                """,
                Encoding.UTF8);

            File.Delete(Path.Combine(DataFolder, "queue.json"));
            File.Delete(Path.Combine(DataFolder, "history.json"));
        }
        catch (IOException)
        {
            // Si no se puede sembrar el estado, los tests lo dirán con su propio fallo — no se tapa aquí.
        }
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
