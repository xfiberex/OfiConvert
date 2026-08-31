using System.Diagnostics;
using System.Runtime.InteropServices;
using OfiConvert.Models;
using OfiConvert.Services;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// El criterio de aceptación de TJ-01, ejecutado contra el PowerPoint de verdad.
/// </summary>
/// <remarks>
/// PowerPoint es una instancia COM <b>única</b>: <c>Activator.CreateInstance</c> devuelve el proceso que
/// ya está corriendo — <i>medido en esta máquina (Office 16 ClickToRun): dos activaciones dejan un solo
/// <c>POWERPNT.EXE</c>; Word y Excel dejan dos</i>. De ahí los dos desastres que estas pruebas vigilan:
/// <list type="number">
///   <item>Con paralelismo &gt; 1, N conversiones de <c>.pptx</c> conducían <b>la misma</b> aplicación, y
///   la primera en terminar llamaba a <c>Quit()</c> <b>matando a las demás</b>.</item>
///   <item>Si el usuario tenía PowerPoint abierto, la app se enganchaba a <b>su</b> sesión y se la cerraba
///   — con <c>DisplayAlerts = ppAlertsNone</c> puesto, o sea, <b>sin preguntar por lo no guardado</b>.</item>
/// </list>
/// Se omiten salvo <c>OFICONVERT_OFFICE_TESTS=1</c> (ver <see cref="OfficeFactAttribute"/>): abren
/// PowerPoint de verdad. <b>No las ejecutes con presentaciones tuyas abiertas.</b>
/// </remarks>
public sealed class PowerPointSharedInstanceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"OfiConvertPpt-{Guid.NewGuid():N}");

    public PowerPointSharedInstanceTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* limpieza best-effort */ }
    }

    private static int PowerPointProcesses()
    {
        var processes = Process.GetProcessesByName("POWERPNT");
        try { return processes.Length; }
        finally { foreach (var p in processes) p.Dispose(); }
    }

    /// <summary>La premisa del hallazgo, comprobada y no supuesta.</summary>
    [OfficeFact]
    public void PowerPoint_EsUnaInstanciaCompartida()
    {
        Assert.Equal(0, PowerPointProcesses());   // el test necesita partir de cero

        object? a = null, b = null;
        try
        {
            var type = Type.GetTypeFromProgID("PowerPoint.Application")!;
            a = Activator.CreateInstance(type)!;
            b = Activator.CreateInstance(type)!;

            Assert.Equal(1, PowerPointProcesses());   // DOS activaciones, UN proceso: de ahí todo el lío
        }
        finally
        {
            QuitQuietly(a);
            Release(b);
            EsperarACierre();
        }
    }

    /// <summary>
    /// EL CRITERIO DE TJ-01: con PowerPoint abierto y una presentación <b>sin guardar</b>, convertir un
    /// lote en paralelo deja las tres conversiones hechas, PowerPoint abierto y el trabajo del usuario
    /// intacto.
    /// </summary>
    [OfficeFact]
    public async Task LoteEnParalelo_NoMataLasConversiones_NiLaSesionDelUsuario()
    {
        Assert.Equal(0, PowerPointProcesses());

        object? userApp = null;
        try
        {
            // 1. Tres presentaciones de origen, creadas con el propio PowerPoint.
            var sources = CrearPresentaciones(3);

            // 2. El usuario tiene PowerPoint abierto con algo sin guardar.
            userApp = AbrirPowerPointDelUsuario();
            int suPresentacion = ContarPresentaciones(userApp);
            Assert.Equal(1, suPresentacion);

            // 3. La app convierte las tres A LA VEZ, como haría con MaxParallelConversions = 4.
            var service = new OfficeFileConversionService();
            var tasks = sources.Select(src => service.ConvertAsync(
                src,
                Path.Combine(_folder, Path.GetFileNameWithoutExtension(src) + ".pdf"),
                new ConversionOptions { OutputFormat = OutputFormat.PDF })).ToArray();

            var results = await Task.WhenAll(tasks);

            // 4. Las TRES salieron. Antes, la primera en terminar mataba a las otras dos.
            Assert.All(results, r => Assert.True(r.Success, $"Conversión fallida: {r.Error?.Key}"));
            Assert.All(results, r => Assert.True(File.Exists(r.OutputPath), $"No se generó {r.OutputPath}"));

            // 5. Y el PowerPoint del usuario sigue vivo, con su presentación sin guardar dentro.
            Assert.True(PowerPointProcesses() > 0, "La app cerró el PowerPoint del usuario.");
            Assert.Equal(1, ContarPresentaciones(userApp));
        }
        finally
        {
            QuitQuietly(userApp);
            EsperarACierre();
        }
    }

    /// <summary>
    /// La otra mitad de TJ-01: <b>sin</b> PowerPoint del usuario, la app es dueña de la instancia — pero
    /// sigue siendo <b>una sola</b> para las tres conversiones. Sin la puerta serializadora, la primera en
    /// terminar llamaba a <c>Quit()</c> y las otras dos morían a media conversión.
    /// </summary>
    [OfficeFact]
    public async Task LoteEnParalelo_SinSesionDelUsuario_ConvierteLasTres()
    {
        Assert.Equal(0, PowerPointProcesses());

        // Presentaciones grandes a propósito, para que las tres conversiones se solapen de verdad.
        //
        // AVISO HONESTO: con el código antiguo (sin puerta y cerrando siempre) esta prueba pasaba IGUAL en
        // la máquina donde se escribió — el "la primera en terminar mata a las demás" no llegó a
        // reproducirse aquí, seguramente porque Quit() sobre una instancia con otros clientes de
        // automatización enganchados no la termina. Lo que sí se reprodujo, y esta clase caza, es la otra
        // consecuencia: cerrarle la sesión al usuario. Esta prueba vigila el caso completo (las tres
        // salen, y la instancia PROPIA sí se cierra); la serialización en sí la cubre SerialGateTests.
        var sources = CrearPresentaciones(3, diapositivas: 40);
        var service = new OfficeFileConversionService();

        var results = await Task.WhenAll(sources.Select(src => service.ConvertAsync(
            src,
            Path.Combine(_folder, Path.GetFileNameWithoutExtension(src) + "-solo.pdf"),
            new ConversionOptions { OutputFormat = OutputFormat.PDF })));

        Assert.All(results, r => Assert.True(r.Success, $"Conversión fallida: {r.Error?.Key}"));
        Assert.All(results, r => Assert.True(File.Exists(r.OutputPath), $"No se generó {r.OutputPath}"));

        // Y la instancia que abrió la app se cierra: era suya, nadie más la estaba usando.
        EsperarACierre();
        Assert.Equal(0, PowerPointProcesses());
    }

    // ── Utilidades COM ────────────────────────────────────────────────────

    private string[] CrearPresentaciones(int cuantas, int diapositivas = 1)
    {
        var type = Type.GetTypeFromProgID("PowerPoint.Application")!;
        object app = Activator.CreateInstance(type)!;
        var paths = new string[cuantas];

        try
        {
            var presentations = Get(app, "Presentations")!;
            for (int i = 0; i < cuantas; i++)
            {
                object presentation = Invoke(presentations, "Add", [-1])!;   // msoTrue: con ventana
                var slides = Get(presentation, "Slides")!;
                for (int d = 1; d <= diapositivas; d++)
                    Invoke(slides, "Add", [d, 2]);                           // ppLayoutText

                paths[i] = Path.Combine(_folder, $"muestra{i}.pptx");
                Invoke(presentation, "SaveAs", [paths[i]]);
                Invoke(presentation, "Close", null);
                Release(presentation);
            }
        }
        finally
        {
            QuitQuietly(app);
            EsperarACierre();
        }

        Assert.All(paths, p => Assert.True(File.Exists(p)));
        return paths;
    }

    /// <summary>Un PowerPoint «del usuario»: abierto, visible y con una presentación SIN guardar.</summary>
    private static object AbrirPowerPointDelUsuario()
    {
        var type = Type.GetTypeFromProgID("PowerPoint.Application")!;
        object app = Activator.CreateInstance(type)!;

        var presentations = Get(app, "Presentations")!;
        object presentation = Invoke(presentations, "Add", [-1])!;
        var slides = Get(presentation, "Slides")!;
        Invoke(slides, "Add", [1, 2]);   // contenido que se perdería si alguien llamara a Quit()

        return app;
    }

    private static int ContarPresentaciones(object? app)
    {
        if (app is null) return 0;
        var presentations = Get(app, "Presentations");
        return presentations is null ? 0 : (int)Get(presentations, "Count")!;
    }

    private static object? Get(object target, string property)
        => target.GetType().InvokeMember(property, System.Reflection.BindingFlags.GetProperty, null, target, null);

    private static object? Invoke(object target, string method, object[]? args)
        => target.GetType().InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod, null, target, args);

    private static void QuitQuietly(object? app)
    {
        if (app is null) return;
        try { Invoke(app, "Quit", null); } catch { /* ya estaba cerrado */ }
        Release(app);
    }

    private static void Release(object? com)
    {
        if (com is null) return;
        try { Marshal.FinalReleaseComObject(com); } catch { /* ya soltado */ }
    }

    private static void EsperarACierre()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        for (int i = 0; i < 20 && PowerPointProcesses() > 0; i++)
            Thread.Sleep(250);
    }
}
