using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace OfiConvert.UiTests;

/// <summary>
/// Tier G: la app era <b>muda</b> para un lector de pantalla.
/// </summary>
/// <remarks>
/// <c>AutomationProperties</c> no aparecía <b>ni una vez</b> en todo el XAML. Los botones que son solo un
/// icono —limpiar la carpeta de destino, quitar un archivo de la lista, cerrar el panel de resultados— no
/// tenían nombre accesible: NVDA o el Narrador anuncian «botón» y nada más, así que quien no ve la pantalla
/// no puede saber qué hace ninguno de ellos.
///
/// Que estos tests pasen ES el arreglo: leen el <b>nombre accesible</b>, el mismo dato que anuncia un lector
/// de pantalla. Un tooltip no bastaría — el ratón no lo usa quien navega con teclado.
///
/// El Tier J añadió la otra mitad (TJ-09): esta clase <b>solo miraba botones</b>, así que cuatro
/// <c>ComboBox</c> y dos <c>NumberBox</c> llevaban meses mudos con el test en verde.
/// </remarks>
[Collection(AppCollection.Name)]
public sealed class AccessibilityTests(AppFixture fixture)
{
    private Window Window => fixture.MainWindow;

    /// <summary>El único botón solo-icono que está siempre en pantalla y no depende de tener archivos.</summary>
    [Fact]
    public void IconOnlyButton_HasAnAccessibleName()
    {
        Tabs.Select(Window, Tabs.Conversion, "btnConvert");

        var button = Window.FindFirstDescendant(cf => cf.ByAutomationId("btnClearOutputFolder"));

        Assert.NotNull(button);
        Assert.False(
            string.IsNullOrWhiteSpace(button!.Name),
            "'btnClearOutputFolder' es un botón solo-icono SIN nombre accesible: un lector de pantalla dice 'botón' y nada más.");
    }

    /// <summary>
    /// Ningún botón visible puede quedarse sin nombre, en ninguna pestaña.
    /// </summary>
    /// <remarks>
    /// Los botones con texto heredan su nombre del Content; los que hay que nombrar a mano son los de solo
    /// icono… y los <c>ToggleSwitch</c>, que <b>UI Automation expone como botones</b> y cuya etiqueta es un
    /// TextBlock aparte que el lector de pantalla no asocia. Los tres interruptores de Ajustes anunciaban
    /// «botón, activado» sin decir <i>de qué</i> — los encontró este test.
    ///
    /// Se recorren las pestañas **explícitamente**: la primera versión miraba «lo que hubiera en el árbol»
    /// y por eso pasaba o fallaba según qué pestaña dejara abierta el test anterior.
    /// </remarks>
    [Theory]
    [InlineData(Tabs.Conversion, "btnConvert")]
    [InlineData(Tabs.Settings, "btnLicencia")]
    [InlineData(Tabs.History, "btnExportCsv")]
    public void NoVisibleButton_IsLeftWithoutAName(string tabId, string anchor)
    {
        Tabs.Select(Window, tabId, anchor);

        var nameless = Window
            .FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
            .Where(IsVisible)
            .Where(b => string.IsNullOrWhiteSpace(SafeName(b)))
            .Select(b => SafeId(b) is { Length: > 0 } id ? id : "(sin AutomationId)")
            .ToList();

        Assert.True(
            nameless.Count == 0,
            $"En '{tabId}' hay botones visibles sin nombre accesible (mudos para un lector de pantalla):\n  "
                + string.Join("\n  ", nameless));
    }

    /// <summary>
    /// Y lo mismo para los controles que <b>no</b> son botones: desplegables y campos numéricos.
    /// </summary>
    /// <remarks>
    /// El test de arriba filtraba por <c>ControlType.Button</c>, así que llevaba desde el Tier G pasando en
    /// verde sobre cuatro <c>ComboBox</c> y dos <c>NumberBox</c> mudos: el Narrador anunciaba «cuadro
    /// combinado, Sistema», «cuadro combinado, Español», «cuadro combinado, PDF» y «cuadro de número, 2»,
    /// sin decir nunca <i>qué</i> controlaban. Cazó los <c>ToggleSwitch</c> de puro rebote, porque UI
    /// Automation los expone como botones (TJ-09).
    ///
    /// <b>La etiqueta de al lado no cuenta:</b> es un <c>TextBlock</c> hermano, y UI Automation no asocia
    /// nada por proximidad visual. Hace falta <c>AutomationProperties.Name</c> (o <c>LabeledBy</c>).
    /// </remarks>
    [Theory]
    [InlineData(Tabs.Conversion, "btnConvert")]
    [InlineData(Tabs.Settings, "btnLicencia")]
    [InlineData(Tabs.History, "btnExportCsv")]
    public void NoVisibleInputControl_IsLeftWithoutAName(string tabId, string anchor)
    {
        Tabs.Select(Window, tabId, anchor);

        // ComboBox → ComboBox; NumberBox → Spinner, y su caja de texto interna → Edit. Se miran los tres
        // porque el reparto depende de la versión de WinUI, y de eso no puede depender la accesibilidad.
        ControlType[] tipos = [ControlType.ComboBox, ControlType.Spinner, ControlType.Edit];

        var nameless = tipos
            .SelectMany(tipo => Window.FindAllDescendants(cf => cf.ByControlType(tipo)))
            .Where(IsVisible)
            // La caja de texto interna de un Spinner hereda el nombre de su padre: si el padre lo tiene,
            // el lector de pantalla ya anuncia el control completo.
            .Where(e => string.IsNullOrWhiteSpace(SafeName(e)) && !ParentHasName(e))
            .Select(e => $"{SafeControlType(e)} {(SafeId(e) is { Length: > 0 } id ? id : "(sin AutomationId)")}")
            .Distinct()
            .ToList();

        Assert.True(
            nameless.Count == 0,
            $"En '{tabId}' hay controles visibles sin nombre accesible (el Narrador dice el tipo y el valor, "
                + $"nunca de qué son):\n  " + string.Join("\n  ", nameless));
    }

    private static bool ParentHasName(AutomationElement element)
    {
        try { return !string.IsNullOrWhiteSpace(SafeName(element.Parent)); } catch { return false; }
    }

    private static string SafeControlType(AutomationElement element)
    {
        try { return element.ControlType.ToString(); } catch { return "?"; }
    }

    // El árbol de WinUI deja elementos proxy que no soportan ni IsOffscreen ni Name: preguntarles lanza
    // PropertyNotSupportedException. No son botones reales de la app; se descartan en vez de reventar.
    private static bool IsVisible(AutomationElement element)
    {
        try { return !element.IsOffscreen; } catch { return false; }
    }

    private static string SafeName(AutomationElement element)
    {
        try { return element.Name; } catch { return ""; }
    }

    private static string SafeId(AutomationElement element)
    {
        try { return element.AutomationId; } catch { return ""; }
    }
}
