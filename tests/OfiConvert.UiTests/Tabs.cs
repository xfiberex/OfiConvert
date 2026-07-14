using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace OfiConvert.UiTests;

/// <summary>
/// Cambia de pestaña y espera a que su contenido exista.
/// </summary>
/// <remarks>
/// <b>El Pivot de WinUI descarga el contenido de la pestaña que no está seleccionada.</b> Con Ajustes
/// delante, <c>btnConvert</c> <b>no está en el árbol de automatización</b> — no es que esté oculto: no
/// existe. De ahí dos reglas para todo test de este proyecto:
///
/// 1. Antes de buscar un control, hay que <b>estar en su pestaña</b>.
/// 2. Ningún test puede dar por buena la pestaña que dejó otro. Un test que solo pasa porque el anterior
///    dejó la app donde le convenía es una mina que estalla el día que cambia el orden de ejecución.
/// </remarks>
internal static class Tabs
{
    internal const string Conversion = "tabConversion";
    internal const string History = "tabHistory";
    internal const string Settings = "tabSettings";

    /// <summary>Selecciona la pestaña y espera a que aparezca <paramref name="expectedChild"/>.</summary>
    internal static void Select(Window window, string tabAutomationId, string expectedChild)
    {
        var tab = window.FindFirstDescendant(cf => cf.ByAutomationId(tabAutomationId));
        Assert.True(tab is not null, $"No se encontró la pestaña '{tabAutomationId}'.");

        // El peer del PivotItem expone SelectionItem (es un TabItem para UIA): es el mismo patrón que
        // usan el teclado y los lectores de pantalla, no un clic por coordenadas.
        Assert.True(
            tab!.Patterns.SelectionItem.IsSupported,
            $"La pestaña '{tabAutomationId}' no expone SelectionItem: no se puede seleccionar sin ratón.");

        tab.Patterns.SelectionItem.Pattern.Select();

        var child = Retry.WhileNull(
            () => window.FindFirstDescendant(cf => cf.ByAutomationId(expectedChild)),
            timeout: TimeSpan.FromSeconds(10),
            interval: TimeSpan.FromMilliseconds(150),
            ignoreException: true).Result;

        Assert.True(
            child is not null,
            $"Se seleccionó '{tabAutomationId}' pero su contenido no apareció: no se encontró '{expectedChild}'.");
    }
}
