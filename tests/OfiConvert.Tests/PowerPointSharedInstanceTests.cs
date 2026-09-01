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
        ExigirMaquinaSinPowerPoint();   // el test necesita partir de cero

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
        ExigirMaquinaSinPowerPoint();

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
        ExigirMaquinaSinPowerPoint();

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
        var vivos = EsperarAQueSeCierren();
        Assert.True(vivos == 0,
            $"Quedaron {vivos} PowerPoint abierto(s) 15 s después de convertir. La instancia era de la "
                + "app —el usuario no tenía ninguna— así que tenía que cerrarla al terminar.");
    }

    /// <summary>
    /// Convertir no puede plantarle a nadie una ventana de PowerPoint encima de lo que esté haciendo.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>El código PEDÍA esa ventana.</b> <c>PowerPointSession.Open</c> hacía
    /// <c>Visible = msoTrue</c>, con un comentario que decía que PowerPoint «no admite trabajar oculto».
    /// Media verdad: <c>Visible = msoFalse</c> sí lanza («Hiding the application window is not allowed»),
    /// pero de eso no se sigue que haya que ponerlo a <b>true</b>.
    ///
    /// <b>Medido (Office 16 ClickToRun, 2026-08-31):</b> recién activado por COM, PowerPoint está en
    /// <c>Visible = msoFalse</c> y <b>sin ventana principal</b> (<c>MainWindowHandle = 0</c>), y abrir la
    /// presentación con <c>WithWindow:=False</c> lo deja igual. Es headless de fábrica; la línea que lo
    /// sacaba a pantalla era nuestra.
    ///
    /// Este test vigila la ventana <b>durante</b> la conversión, no al final: una ventana que aparece y
    /// se va sigue siendo una ventana que le salta al usuario encima.
    /// </remarks>
    [OfficeFact]
    public async Task Convertir_NoAbreNingunaVentanaDePowerPoint()
    {
        ExigirMaquinaSinPowerPoint();

        // Ojo: CrearPresentaciones usa Add(-1) —con ventana— a propósito, así que abre PowerPoint de
        // verdad. Se espera a que se cierre ANTES de empezar a vigilar, o el test se acusaría a sí mismo.
        var sources = CrearPresentaciones(1, diapositivas: 40);
        ExigirMaquinaSinPowerPoint();

        var vistas = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var vigilando = new CancellationTokenSource();

        var vigilante = Task.Run(async () =>
        {
            while (!vigilando.IsCancellationRequested)
            {
                foreach (var proc in Process.GetProcessesByName("POWERPNT"))
                {
                    try
                    {
                        proc.Refresh();
                        if (proc.MainWindowHandle != IntPtr.Zero)
                            vistas.Enqueue($"PID {proc.Id} con ventana '{proc.MainWindowTitle}'");
                    }
                    catch { /* el proceso puede morir entre el listado y la consulta */ }
                    finally { proc.Dispose(); }
                }

                try { await Task.Delay(50, vigilando.Token); } catch (OperationCanceledException) { return; }
            }
        });

        var service = new OfficeFileConversionService();
        var result = await service.ConvertAsync(
            sources[0],
            Path.Combine(_folder, "sin-ventana.pdf"),
            new ConversionOptions { OutputFormat = OutputFormat.PDF });

        vigilando.Cancel();
        await vigilante;

        // Que no se vea nada no vale de nada si encima no convierte.
        Assert.True(result.Success, $"Conversión fallida: {result.Error?.Key}");
        Assert.True(File.Exists(result.OutputPath), $"No se generó {result.OutputPath}");

        Assert.True(vistas.IsEmpty,
            "PowerPoint mostró su ventana durante la conversión, encima de lo que estuviera haciendo el "
                + $"usuario ({vistas.Count} muestras): " + string.Join(" · ", vistas.Take(3)));
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

    /// <summary>
    /// Espera a que no quede ningun <c>POWERPNT.EXE</c>. Devuelve cuantos quedan al agotar la espera.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>La espera corta era la causa de que esta clase fuese inestable.</b> Eran 5 s, y
    /// <c>POWERPNT.EXE</c> tarda a veces mas en desaparecer tras un <c>Quit()</c>: la prueba siguiente
    /// arrancaba viendo un proceso que ya estaba muriendose.
    ///
    /// <b>Medido:</b> ejecutando la clase entera,
    /// <c>LoteEnParalelo_SinSesionDelUsuario_ConvierteLasTres</c> fallo con «Expected: 0, Actual: 1» a los
    /// 8 ms, y paso en verde ejecutada sola y al repetir la clase. Una prueba que falla una de cada dos
    /// veces por su propia limpieza deja de ser un guardian: se acaba ignorando — y justo esta cubre el
    /// fallo mas grave del Tier J.
    ///
    /// Pasarse esperando no cuesta nada (se sale en cuanto llega a cero); quedarse corto cuesta un rojo
    /// falso.
    /// </remarks>
    private static int EsperarAQueSeCierren(int segundos = 15)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        for (int i = 0; i < segundos * 4 && PowerPointProcesses() > 0; i++)
            Thread.Sleep(250);

        return PowerPointProcesses();
    }

    private static void EsperarACierre() => EsperarAQueSeCierren();

    /// <summary>
    /// Punto de partida de las pruebas que necesitan la maquina sin PowerPoint: <b>espera</b> a que no
    /// quede ninguno, y solo entonces afirma.
    /// </summary>
    /// <remarks>
    /// El mensaje distingue los dos casos posibles, que piden cosas distintas: una fuga de la prueba
    /// anterior (se arregla aqui) o una sesion de verdad del usuario, que <b>si</b> debe parar la
    /// ejecucion — estas pruebas conducen el PowerPoint de la maquina.
    /// </remarks>
    private static void ExigirMaquinaSinPowerPoint()
    {
        var vivos = EsperarAQueSeCierren();

        Assert.True(vivos == 0,
            $"Hay {vivos} PowerPoint abierto(s) al EMPEZAR la prueba, tras esperar 15 s a que se cerraran. "
                + "O una prueba anterior no solto el suyo, o tienes PowerPoint abierto: estas pruebas "
                + "conducen el PowerPoint de la maquina y NO deben ejecutarse con trabajo tuyo dentro.");
    }
}
