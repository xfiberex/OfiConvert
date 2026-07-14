using FlaUI.Core.AutomationElements;

namespace OfiConvert.UiTests;

/// <summary>
/// El acceso del usuario a los textos legales (Ajustes → Acerca de).
/// </summary>
/// <remarks>
/// Lo que se comprueba no es que el diálogo abra, sino que <b>llegue con contenido</b>. <c>LegalText</c> es
/// defensivo: si el recurso embebido faltara, devolvería cadena vacía y el diálogo se abriría igual, con
/// un "Texto no disponible" — y la app dejaría de mostrar una atribución que las licencias de Serilog,
/// WebView2 y el Windows App SDK <b>obligan</b> a mostrar, sin que nada fallara. Leer el cuerpo real es lo
/// único que distingue un caso del otro desde fuera.
/// </remarks>
[Collection(AppCollection.Name)]
public sealed class LegalUiTests : IDisposable
{
    private readonly AppFixture _fixture;

    private Window Window => _fixture.MainWindow;

    public LegalUiTests(AppFixture fixture)
    {
        _fixture = fixture;
        Tabs.Select(Window, Tabs.Settings, "btnLicencia");
    }

    /// <summary>Ningún test puede dejar un ContentDialog abierto: WinUI solo admite uno, y el siguiente mataría el proceso.</summary>
    public void Dispose() => DialogHelper.SafeClose(_fixture);

    [Fact]
    public void AboutSection_ShowsTheAssemblyVersion()
    {
        var version = Window.FindFirstDescendant(cf => cf.ByAutomationId("txtAboutVersion"));

        Assert.NotNull(version);
        Assert.Matches(@"OfiConvert \d+\.\d+\.\d+", version!.Name);
    }

    [Fact]
    public void LicenseDialog_ShowsTheEmbeddedMitLicense()
    {
        Window.FindFirstDescendant(cf => cf.ByAutomationId("btnLicencia"))!.AsButton().Invoke();

        var dialog = DialogHelper.WaitForDialog(_fixture);
        var body = DialogHelper.ReadText(dialog, "txtLegalBody");

        Assert.Contains("MIT License", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no disponible", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Las dos licencias que NO son MIT tienen que llegar al usuario. Es el error que cometió el proyecto
    /// hermano —declarar el Windows App SDK como MIT— y el que este diálogo existe para no repetir.
    /// </summary>
    [Fact]
    public void ThirdPartyDialog_ShowsTheNoticesIncludingTheNonMitOnes()
    {
        Window.FindFirstDescendant(cf => cf.ByAutomationId("btnAvisosTerceros"))!.AsButton().Invoke();

        var dialog = DialogHelper.WaitForDialog(_fixture);
        var body = DialogHelper.ReadText(dialog, "txtLegalBody");

        Assert.DoesNotContain("no disponible", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Serilog", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Apache License 2.0", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MICROSOFT SOFTWARE LICENSE TERMS", body, StringComparison.OrdinalIgnoreCase);
    }
}
