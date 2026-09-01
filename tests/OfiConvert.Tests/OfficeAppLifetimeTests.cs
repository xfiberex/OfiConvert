using System.Diagnostics;
using OfiConvert.Services;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Quien abre un proceso de Office por COM tiene que cerrarlo <b>también cuando el camino se tuerce</b>.
/// </summary>
/// <remarks>
/// <c>CONTEXT.md</c> señala los procesos de Office huérfanos como el riesgo principal de la app, y este
/// es el agujero por el que se colaban (TJ-20): <c>CreateOfficeApp</c> activaba la aplicación y llamaba
/// a <c>configure(app)</c> <b>fuera de todo <c>try</c></b>. Si esa configuración lanzaba, el método
/// propagaba sin haber devuelto el objeto: el <c>finally</c> del llamante recibía <c>null</c>, no
/// llamaba a <c>Quit()</c>, y quedaba un proceso vivo <b>por cada intento</b>.
///
/// Se prueba con Word, no con PowerPoint, precisamente porque Word <b>sí</b> arranca un proceso propio:
/// contar procesos dice la verdad. Con PowerPoint —instancia única— el recuento no distinguiría el
/// nuestro del que ya hubiera. Ver <c>OfficeAutomation</c> en los tests de PowerPoint.
/// </remarks>
public sealed class OfficeAppLifetimeTests
{
    private static int Procesos(string nombre)
    {
        var encontrados = Process.GetProcessesByName(nombre);
        try { return encontrados.Length; }
        finally { foreach (var p in encontrados) p.Dispose(); }
    }

    /// <summary>Espera a que no quede ninguno; devuelve cuántos quedan al agotar la espera.</summary>
    private static int EsperarAQueSeCierren(string nombre, int segundos = 15)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        for (int i = 0; i < segundos * 4 && Procesos(nombre) > 0; i++)
            Thread.Sleep(250);

        return Procesos(nombre);
    }

    /// <summary>EL CRITERIO DE TJ-20: la configuración revienta y no queda nada detrás.</summary>
    [OfficeFact]
    public void SiLaConfiguracionFalla_NoQuedaNingunProcesoDeOffice()
    {
        Assert.True(EsperarAQueSeCierren("WINWORD") == 0,
            "Hay Word abierto al empezar. Este test cuenta procesos WINWORD.EXE, así que no puede "
                + "ejecutarse con Word abierto: cierra tus documentos o quita OFICONVERT_OFFICE_TESTS.");

        var explota = new InvalidOperationException("la versión de Office no admite esta propiedad");

        var lanzada = Assert.Throws<InvalidOperationException>(() =>
            OfficeFileConversionService.CreateOfficeApp("Word.Application", _ => throw explota));

        // La excepción original llega intacta: cerrar el proceso no puede tragarse el motivo del fallo.
        Assert.Same(explota, lanzada);

        var huerfanos = EsperarAQueSeCierren("WINWORD");
        Assert.True(huerfanos == 0,
            $"Quedaron {huerfanos} WINWORD.EXE tras fallar la configuración. Cada intento deja uno: un "
                + "lote de 50 documentos contra una versión de Office que no admita alguna de estas "
                + "propiedades dejaría 50 procesos vivos.");
    }

    /// <summary>Y el camino bueno sigue devolviendo la aplicación, no vaya a cerrarse de más.</summary>
    [OfficeFact]
    public void SiLaConfiguracionVaBien_DevuelveLaAplicacionViva()
    {
        Assert.True(EsperarAQueSeCierren("WINWORD") == 0, "Hay Word abierto al empezar.");

        object? app = null;
        var configurada = false;
        try
        {
            app = OfficeFileConversionService.CreateOfficeApp("Word.Application", _ => configurada = true);

            Assert.True(configurada);
            Assert.NotNull(app);
            Assert.True(Procesos("WINWORD") > 0, "No se activó ningún Word.");
        }
        finally
        {
            if (app is not null)
                OfficeFileConversionService.CleanupOfficeApp(app);
        }

        Assert.Equal(0, EsperarAQueSeCierren("WINWORD"));
    }
}
