using System.ComponentModel;
using System.Xml.Linq;

namespace OfiConvert.Helpers;

/// <summary>
/// Traducciones en runtime, leídas de los diccionarios XAML de <c>Lang/</c>. Desde el código se accede por
/// el indexador del singleton; desde el XAML, con un binding al indexador contra el recurso <c>Loc</c>.
/// </summary>
/// <remarks>
/// <b>EL IDIOMA ES ESTADO ESTÁTICO, Y TIENE QUE SERLO — NO CONVERTIRLO EN ESTADO DE INSTANCIA.</b>
///
/// Hay <b>dos</b> instancias vivas y no se puede evitar: el código usa el singleton <see cref="Instance"/>,
/// mientras que <c>MainWindow.xaml</c> declara <c>&lt;helpers:LocalizationService x:Key="Loc"/&gt;</c>, que
/// construye <b>la suya</b> — es la que escuchan los ~40 bindings de la interfaz. (Registrar el singleton
/// como recurso desde código no es alternativa: WinUI no resuelve ese <c>{StaticResource}</c> desde los
/// recursos de la aplicación y la app <b>muere al arrancar</b>. Comprobado.)
///
/// Cuando el idioma era estado de instancia, cambiarlo llamaba a <c>LoadLanguage</c> sobre el singleton y
/// notificaba al singleton, mientras los bindings seguían escuchando al otro objeto, <b>que nadie tocaba
/// jamás</b>. Consecuencia: <b>los botones y las etiquetas se quedaban en español en los ocho idiomas</b>, y
/// ni siquiera reiniciar lo arreglaba (la instancia del XAML nace en español). Solo cambiaban de idioma los
/// textos que pasan por código — mensajes, estados y diálogos—, que sí van contra el singleton. El
/// <c>settings.json</c> guardaba el idioma elegido correctamente, así que desde fuera parecía funcionar.
///
/// Con el estado compartido, quien construya la instancia da igual: todas leen el mismo diccionario y todas
/// se enteran del cambio. Lo cubre <c>LocalizationUiTests</c>, contra la app real.
/// </remarks>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    /// <summary>
    /// Idiomas admitidos. Fuente única: cualquier código fuera de esta lista cae a español, y
    /// <see cref="Services.SettingsService"/> valida contra ella (antes solo aceptaba es/en, y los
    /// otros seis no sobrevivían a un reinicio).
    /// </summary>
    public static readonly string[] SupportedLanguages = ["es", "en", "pt", "fr", "de", "it", "zh", "ja"];

    public const string DefaultLanguage = "es";

    public static bool IsSupported(string? languageCode) =>
        languageCode is not null && SupportedLanguages.Contains(languageCode);

    // --- Estado COMPARTIDO por todas las instancias (ver el porqué en la nota de la clase) ---
    private static Dictionary<string, string> _strings = new();
    private static string _currentLanguage = DefaultLanguage;

    /// <summary>Cambio de idioma. Lo escuchan todas las instancias vivas, cada una para avisar a SUS bindings.</summary>
    private static event Action? LanguageChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationService()
    {
        LanguageChanged += NotifyBindings;

        // La primera instancia que aparezca deja el idioma por defecto cargado; las siguientes se
        // encuentran el diccionario ya puesto y no lo pisan (el XAML construye la suya DESPUÉS de que
        // MainWindow haya aplicado el idioma guardado del usuario: recargar aquí lo devolvería a español).
        if (_strings.Count == 0)
            LoadLanguage(DefaultLanguage);
    }

    public string this[string key] => _strings.TryGetValue(key, out var value) ? value : key;

    public string CurrentLanguage => _currentLanguage;

    public void LoadLanguage(string languageCode)
    {
        if (!IsSupported(languageCode))
            languageCode = DefaultLanguage;

        var cultureName = languageCode switch
        {
            "en" => "en-US",
            "pt" => "pt-BR",
            "fr" => "fr-FR",
            "de" => "de-DE",
            "it" => "it-IT",
            "zh" => "zh-CN",
            "ja" => "ja-JP",
            _ => "es-ES"
        };

        var filePath = Path.Combine(AppContext.BaseDirectory, "Lang", $"{cultureName}.xaml");
        if (!File.Exists(filePath))
            filePath = Path.Combine(AppContext.BaseDirectory, "Lang", "es-ES.xaml");

        if (!File.Exists(filePath)) return;

        try
        {
            var doc = XDocument.Load(filePath);
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

            var newStrings = new Dictionary<string, string>();
            foreach (var element in doc.Root?.Elements() ?? [])
            {
                var key = element.Attribute(x + "Key")?.Value;
                if (key is not null)
                    newStrings[key] = element.Value;
            }

            _strings = newStrings;
            _currentLanguage = languageCode;
        }
        catch
        {
            // Se conservan las cadenas anteriores: es preferible una UI en el idioma viejo a una vacía.
            return;
        }

        // Todas las instancias refrescan sus bindings: "Item[]" es el nombre que XAML entiende como
        // "han cambiado TODOS los valores del indexador".
        LanguageChanged?.Invoke();
    }

    private void NotifyBindings() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));

    public string Get(string key) => this[key];
}
