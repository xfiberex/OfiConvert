using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace OfiConvert.UiTests;

/// <summary>
/// El cambio de idioma EN CALIENTE, contra la app real.
/// </summary>
/// <remarks>
/// Los 8 diccionarios se parsean en runtime con <c>XDocument</c> y la UI se repinta por binding al indexer
/// (<c>{Binding [Clave], Source={StaticResource Loc}}</c>). El compilador no comprueba nada de eso: si el
/// binding se rompe, o si <c>LoadLanguage</c> falla —y se traga la excepción a propósito—, la app se queda
/// en español y <b>no pasa nada más</b>. Ningún test unitario lo vería: el fallo está en el repintado.
/// Este es el único test que mira lo que el usuario mira.
///
/// Cada test devuelve la app al español pase lo que pase: el resto espera textos en español, y
/// <see cref="SettingsBackup"/> protege el archivo del usuario, no el estado de la app en marcha.
/// </remarks>
[Collection(AppCollection.Name)]
public sealed class LocalizationUiTests(AppFixture fixture)
{
    // Índice en el desplegable y texto del ítem, que es estático (los idiomas se escriben en su propia
    // lengua y no se traducen).
    private const int Spanish = 0;
    private const int English = 1;
    private const int Japanese = 7;

    private const string SpanishItem = "Español";
    private const string EnglishItem = "English";
    private const string JapaneseItem = "日本語";

    private Window Window => fixture.MainWindow;

    [Fact]
    public void SwitchingLanguage_RepaintsTheButtonsAlreadyOnScreen()
    {
        try
        {
            SelectLanguage(English, EnglishItem);
            Assert.Equal("Convert", ConvertButtonText());

            SelectLanguage(Japanese, JapaneseItem);
            Assert.Equal("変換", ConvertButtonText());
        }
        finally
        {
            // Aunque el assert de arriba haya fallado: dejar la app en japonés estropearía a los demás.
            SelectLanguage(Spanish, SpanishItem);
        }

        Assert.Equal("Convertir", ConvertButtonText());
    }

    /// <summary>
    /// Los 8 idiomas se pueden elegir. Si alguien añade uno a <c>LocalizationService.SupportedLanguages</c>
    /// y olvida el XAML, el diccionario existe y el usuario no puede llegar a él.
    /// </summary>
    [Fact]
    public void LanguageComboBox_OffersTheEightLanguages()
    {
        Tabs.Select(Window, Tabs.Settings, "cmbLanguage");

        var combo = Window.FindFirstDescendant(cf => cf.ByAutomationId("cmbLanguage"))?.AsComboBox();

        Assert.NotNull(combo);
        Assert.Equal(8, combo!.Items.Length);
    }

    /// <summary>
    /// Vuelve a la pestaña de conversión y lee el botón. El Pivot descarga el contenido de la pestaña que
    /// no está delante, así que estando en Ajustes <c>btnConvert</c> ni siquiera existe en el árbol.
    /// </summary>
    private string ConvertButtonText()
    {
        Tabs.Select(Window, Tabs.Conversion, "btnConvert");

        var button = Window.FindFirstDescendant(cf => cf.ByAutomationId("btnConvert"));
        Assert.NotNull(button);

        return button!.Name;
    }

    /// <summary>
    /// Elige un idioma en el desplegable de Ajustes, <b>por teclado</b>.
    /// </summary>
    /// <remarks>
    /// Ni <c>ComboBox.Select(index)</c> ni abrir el Popup y hacer <c>SelectionItem.Select()</c> sobre el
    /// ítem cambian el idioma: la selección "ocurre" en el árbol de automatización y el
    /// <c>SelectionChanged</c> de la app <b>no se dispara</b>, así que <c>LoadLanguage</c> nunca llega a
    /// llamarse. Con el foco en el desplegable cerrado, en cambio, Inicio + Abajo mueven la selección de
    /// verdad — es el camino del teclado, el mismo que usa quien no puede usar el ratón, y el único que
    /// aquí atraviesa el evento real de la app.
    ///
    /// Se comprueba que el desplegable QUEDÓ en el idioma pedido antes de mirar los botones: si un día
    /// esto vuelve a no accionar nada, el test lo dirá aquí, en vez de dejar un "Convertir != Convert"
    /// que no explica nada.
    /// </remarks>
    private void SelectLanguage(int index, string expectedItemText)
    {
        Tabs.Select(Window, Tabs.Settings, "cmbLanguage");

        var combo = Window.FindFirstDescendant(cf => cf.ByAutomationId("cmbLanguage"))?.AsComboBox();
        Assert.NotNull(combo);

        combo!.Focus();
        Thread.Sleep(150);

        Keyboard.Press(VirtualKeyShort.HOME);
        Thread.Sleep(120);

        for (int i = 0; i < index; i++)
        {
            Keyboard.Press(VirtualKeyShort.DOWN);
            Thread.Sleep(80);
        }

        Thread.Sleep(400);

        Assert.Equal(expectedItemText, combo.SelectedItem?.Name);
    }
}
