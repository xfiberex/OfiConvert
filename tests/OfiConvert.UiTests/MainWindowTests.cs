using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace OfiConvert.UiTests;

/// <summary>
/// La ventana abre y sus controles están donde deben. Suena a poco hasta que se recuerda que a
/// FormatDiskPro un publish sin el <c>.pri</c> le hacía <b>crashear al arrancar</b> en el equipo del
/// usuario, con un instalador que se generaba sin quejarse. Arrancar el <c>.exe</c> de verdad es lo único
/// que caza eso.
///
/// Ningún test de aquí convierte nada: no hace falta Office ni LibreOffice instalado para correrlos.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class MainWindowTests : IDisposable
{
    private readonly AppFixture _fixture;

    private Window Window => _fixture.MainWindow;

    public MainWindowTests(AppFixture fixture)
    {
        _fixture = fixture;

        // Cada test parte de la pestaña de conversión, la deje donde la deje el test anterior.
        Tabs.Select(Window, Tabs.Conversion, "btnConvert");
    }

    public void Dispose() { }

    [Fact]
    public void MainWindow_Opens()
    {
        Assert.False(Window.IsOffscreen);
        Assert.False(Window.BoundingRectangle.IsEmpty);
    }

    /// <summary>
    /// Los controles de la pestaña de conversión. <c>DropZone</c> no está en la lista a propósito: es un
    /// <c>Border</c>, y UI Automation no expone los bordes — no es un control, es decoración con un
    /// comportamiento de arrastre encima.
    /// </summary>
    [Theory]
    [InlineData("btnSelectFile")]
    [InlineData("btnConvert")]
    [InlineData("btnClear")]
    [InlineData("cmbFormat")]
    [InlineData("txtOutputFolder")]
    [InlineData("btnSelectFolder")]
    [InlineData("lvFiles")]
    public void ConversionTabControl_IsPresent(string automationId)
        => Assert.NotNull(Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)));

    [Fact]
    public void TheThreeTabs_ArePresent()
    {
        Assert.NotNull(Window.FindFirstDescendant(cf => cf.ByAutomationId(Tabs.Conversion)));
        Assert.NotNull(Window.FindFirstDescendant(cf => cf.ByAutomationId(Tabs.History)));
        Assert.NotNull(Window.FindFirstDescendant(cf => cf.ByAutomationId(Tabs.Settings)));
    }

    /// <summary>
    /// Pausar/Reanudar/Cancelar solo existen mientras se convierte (su <c>Visibility</c> cuelga de
    /// <c>IsConverting</c>/<c>IsPaused</c>). Con la app en reposo no deben verse: si aparecen, el binding
    /// de visibilidad se ha roto y el usuario tiene delante botones que no hacen nada.
    /// </summary>
    [Theory]
    [InlineData("btnPause")]
    [InlineData("btnResume")]
    [InlineData("btnCancel")]
    public void ConversionOnlyButton_IsHiddenWhenIdle(string automationId)
    {
        var button = Window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

        Assert.True(button is null || button.IsOffscreen, $"'{automationId}' se ve con la app en reposo.");
    }

    /// <summary>Los 5 formatos del enum <c>OutputFormat</c>, en el desplegable de verdad.</summary>
    [Fact]
    public void FormatComboBox_OffersTheFiveOutputFormats()
    {
        var combo = Window.FindFirstDescendant(cf => cf.ByAutomationId("cmbFormat"))?.AsComboBox();

        Assert.NotNull(combo);
        Assert.Equal(["PDF", "HTML", "CSV", "PNG", "JPG"], combo!.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public void FileList_StartsEmpty()
    {
        var list = Window.FindFirstDescendant(cf => cf.ByAutomationId("lvFiles"))?.AsListBox();

        Assert.NotNull(list);
        Assert.Empty(list!.Items);
    }

    /// <summary>La app arranca en estado "lista", no bloqueada.</summary>
    [Fact]
    public void ConvertButton_IsEnabledWhenIdle()
    {
        var button = Window.FindFirstDescendant(cf => cf.ByAutomationId("btnConvert"));

        Assert.NotNull(button);
        Assert.True(button!.IsEnabled);
        Assert.Equal(ControlType.Button, button.ControlType);
    }

    /// <summary>El historial exporta a CSV y a TXT: los dos botones existen y se pueden pulsar.</summary>
    [Fact]
    public void HistoryTab_HasItsExportButtons()
    {
        Tabs.Select(Window, Tabs.History, "btnExportCsv");

        Assert.NotNull(Window.FindFirstDescendant(cf => cf.ByAutomationId("btnExportCsv")));
        Assert.NotNull(Window.FindFirstDescendant(cf => cf.ByAutomationId("btnExportTxt")));
        Assert.NotNull(Window.FindFirstDescendant(cf => cf.ByAutomationId("btnClearHistory")));
    }
}
