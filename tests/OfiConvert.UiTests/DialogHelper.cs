using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;

namespace OfiConvert.UiTests;

/// <summary>
/// Localiza y cierra los <c>ContentDialog</c> de WinUI.
/// </summary>
/// <remarks>
/// Un <c>ContentDialog</c> <b>no abre una ventana nueva</b> del escritorio: vive como descendiente de
/// MainWindow, expuesto como <c>ControlType.Window</c>. Y el árbol de MainWindow puede tener <b>más de
/// un</b> elemento Window a la vez — WinUI deja un proxy de Popup vacío rondando —, así que
/// <c>FindFirstDescendant(ByControlType(Window))</c> puede devolver el proxy en vez del diálogo. Por eso
/// se buscan TODOS los candidatos y se elige el que de verdad tiene botonera.
///
/// Cerrar siempre en un <c>finally</c>: WinUI solo admite UN ContentDialog a la vez. Si un assert falla y
/// deja el diálogo abierto, el siguiente test intenta abrir un segundo y <b>el proceso se muere</b>.
/// </remarks>
public static class DialogHelper
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static Window WaitForDialog(AppFixture fixture)
    {
        var result = Retry.WhileNull(
            () => FindDialog(fixture),
            timeout: Timeout,
            interval: TimeSpan.FromMilliseconds(200),
            ignoreException: true);

        Assert.True(result.Result is not null, "No se abrió ningún ContentDialog dentro del tiempo esperado.");
        return result.Result!;
    }

    private static Window? FindDialog(AppFixture fixture)
    {
        var candidates = fixture.MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Window));

        return candidates
            .Select(c => c.AsWindow())
            .FirstOrDefault(HasDialogButtons);
    }

    /// <summary>
    /// Todo diálogo de esta app fija <c>CloseButtonText</c> (o Primary/Secondary), y WinUI le da a esos
    /// botones el AutomationId de su plantilla. El proxy de Popup vacío no tiene ninguno.
    /// </summary>
    private static bool HasDialogButtons(AutomationElement dialog)
    {
        try
        {
            return dialog.FindFirstDescendant(cf => cf.ByAutomationId("CloseButton")) is not null
                || dialog.FindFirstDescendant(cf => cf.ByAutomationId("PrimaryButton")) is not null
                || dialog.FindFirstDescendant(cf => cf.ByAutomationId("SecondaryButton")) is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Lee el texto de un descendiente del diálogo, esperando a que aparezca.</summary>
    public static string ReadText(Window dialog, string automationId)
    {
        var element = Retry.WhileNull(
            () => dialog.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            timeout: Timeout,
            interval: TimeSpan.FromMilliseconds(150),
            ignoreException: true).Result;

        Assert.True(element is not null, $"No se encontró '{automationId}' dentro del diálogo.");
        return element!.Name;
    }

    /// <summary>Cierra el diálogo que haya abierto. Best-effort: nunca debe tapar el fallo real de un test.</summary>
    public static void SafeClose(AppFixture fixture)
    {
        try
        {
            var dialog = FindDialog(fixture);
            var close = dialog?.FindFirstDescendant(cf => cf.ByAutomationId("CloseButton"));

            if (close is not null && close.Patterns.Invoke.IsSupported)
                close.Patterns.Invoke.Pattern.Invoke();

            Thread.Sleep(300);
        }
        catch
        {
            // Si no se pudo cerrar, el test que venga detrás lo dirá con su propio fallo.
        }
    }
}
