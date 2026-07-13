using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using OfiConvert.Helpers;

namespace OfiConvert;

internal static class Program
{
    /// <summary>Clave de instancia única. Cualquier cadena estable sirve; solo debe coincidir consigo misma.</summary>
    private const string InstanceKey = "OfiConvert-main";

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();

            // Segunda instancia (p. ej. el menú contextual del Explorador con la app ya abierta):
            // se le pasa la activación a la primera y esta se cierra sin abrir ventana.
            if (TryRedirectToPrimaryInstance())
                return 0;

            Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App(args);
            });
            return 0;
        }
        catch (Exception ex)
        {
            AppPaths.WriteCrashLog(ex.ToString());
            return 1;
        }
    }

    private static bool TryRedirectToPrimaryInstance()
    {
        AppInstance primary = AppInstance.FindOrRegisterForKey(InstanceKey);
        if (primary.IsCurrent)
            return false;

        AppActivationArguments activation = AppInstance.GetCurrent().GetActivatedEventArgs();

        // RedirectActivationToAsync NO se puede esperar desde este hilo STA: la redirección necesita
        // bombear mensajes COM y el await se bloquearía contra sí mismo. Se despacha a un hilo del pool
        // y se espera con un semáforo (es el patrón del sample oficial del Windows App SDK).
        using var redirected = new SemaphoreSlim(0, 1);
        Task.Run(async () =>
        {
            try
            {
                await primary.RedirectActivationToAsync(activation);
            }
            finally
            {
                redirected.Release();
            }
        });
        redirected.Wait();

        return true;
    }
}
