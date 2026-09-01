using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using OfiConvert.Core;
using OfiConvert.Models;
using Serilog;

namespace OfiConvert.Services;

public class OfficeFileConversionService : IFileConversionService
{
    public bool IsOfficeInstalled()
    {
        try
        {
            string[] registryPaths =
            [
                @"SOFTWARE\Microsoft\Office\ClickToRun\Configuration",
                @"SOFTWARE\Microsoft\Office\16.0\Word\InstallRoot",
                @"SOFTWARE\Microsoft\Office\15.0\Word\InstallRoot",
                @"SOFTWARE\Microsoft\Office\14.0\Word\InstallRoot",
            ];

            foreach (var path in registryPaths)
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(path);
                if (key is not null)
                    return true;
            }

            return Type.GetTypeFromProgID("Word.Application") is not null;
        }
        catch
        {
            return false;
        }
    }

    public bool IsValidOfficeFile(string extension) => OfficeFormats.IsSupported(extension);

    public async Task<ConversionResult> ConvertToPdfAsync(
        string sourcePath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        return await ConvertAsync(sourcePath, outputPath,
            new ConversionOptions { OutputFormat = OutputFormat.PDF }, cancellationToken);
    }

    public async Task<ConversionResult> ConvertAsync(
        string sourcePath,
        string outputPath,
        ConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            return ConversionResult.Failed("MsgFileNotFound");

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();

        if (!IsValidOfficeFile(extension))
            return ConversionResult.Failed(new UserMessage("MsgUnsupportedExtension", extension));

        var sw = Stopwatch.StartNew();

        try
        {
            Log.Information("Office: Convirtiendo {Source} a {Format}", sourcePath, options.OutputFormat);

            // PowerPoint es una instancia COM ÚNICA: activarlo devuelve el que ya corre, no uno nuevo. Dos
            // conversiones de .pptx en paralelo conducían LA MISMA aplicación y la primera en terminar
            // llamaba a Quit(), matando a la otra a media conversión. Word y Excel sí crean un proceso por
            // activación, así que esos siguen yendo en paralelo. (TJ-01.)
            var esPowerPoint = extension is ".ppt" or ".pptx";

            Task Convertir() => Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isWord = extension is ".doc" or ".docx";
                var isExcel = extension is ".xls" or ".xlsx";
                var isPpt = extension is ".ppt" or ".pptx";

                if (isWord && options.OutputFormat == OutputFormat.HTML)
                    ConvertWordToHtml(sourcePath, outputPath);
                else if (isWord)
                    ConvertWordToPdf(sourcePath, outputPath);
                else if (isExcel && options.OutputFormat == OutputFormat.CSV)
                    ConvertExcelToCsv(sourcePath, outputPath, options.SheetNames);
                else if (isExcel)
                    ConvertExcelToPdf(sourcePath, outputPath);
                else if (isPpt && options.OutputFormat is OutputFormat.PNG or OutputFormat.JPG)
                    ConvertPowerPointToImages(sourcePath, outputPath, options);
                else if (isPpt)
                    ConvertPowerPointToPdf(sourcePath, outputPath);
                else
                    throw new NotSupportedException($"Formato no soportado: {extension}");

            }, cancellationToken);

            if (esPowerPoint)
                await SerialGate.PowerPoint.RunAsync(Convertir, cancellationToken);
            else
                await Convertir();

            sw.Stop();
            Log.Information("Office: Conversión exitosa en {Duration}ms", sw.ElapsedMilliseconds);
            return ConversionResult.Successful(outputPath, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ConversionResult.Failed("MsgConversionCancelled");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Error(ex, "Office: Error en conversión de {Source}", sourcePath);
            return ConversionResult.Failed(new UserMessage("MsgOfficeError", ex.Message));
        }
    }

    #region Word Conversion

    private static void ConvertWordToPdf(string sourcePath, string pdfPath)
    {
        object? wordApp = null;
        object? doc = null;

        try
        {
            wordApp = CreateOfficeApp("Word.Application", app =>
            {
                var appType = app.GetType();
                // Deshabilitar alertas
                appType.InvokeMember("DisplayAlerts",
                    System.Reflection.BindingFlags.SetProperty,
                    null, app, [0]); // wdAlertsNone = 0

                // Deshabilitar macros: AutomationSecurity = msoAutomationSecurityForceDisable (3)
                appType.InvokeMember("AutomationSecurity",
                    System.Reflection.BindingFlags.SetProperty,
                    null, app, [3]);
            });

            var wordType = wordApp.GetType();

            var documents = wordType.InvokeMember("Documents",
                System.Reflection.BindingFlags.GetProperty,
                null, wordApp, null)
                ?? throw new InvalidOperationException("No se pudo acceder a la colecci\u00f3n de documentos");

            // Open(FileName, ReadOnly:=True, AddToRecentFiles:=False)
            object[] openParams = [sourcePath, Type.Missing, true, false];
            doc = documents.GetType().InvokeMember("Open",
                System.Reflection.BindingFlags.InvokeMethod,
                null, documents, openParams)
                ?? throw new InvalidOperationException("No se pudo abrir el documento");

            doc.GetType().InvokeMember("ExportAsFixedFormat",
                System.Reflection.BindingFlags.InvokeMethod,
                null, doc, [pdfPath, 17]); // wdExportFormatPDF = 17
        }
        finally
        {
            CleanupComObject(doc, closeMethod: "Close", closeParams: [false]);
            CleanupOfficeApp(wordApp);
        }
    }

    #endregion

    #region Excel Conversion

    private static void ConvertExcelToPdf(string sourcePath, string pdfPath)
    {
        object? excelApp = null;
        object? workbook = null;

        try
        {
            excelApp = CreateOfficeApp("Excel.Application", app =>
            {
                var appType = app.GetType();
                appType.InvokeMember("DisplayAlerts",
                    System.Reflection.BindingFlags.SetProperty,
                    null, app, [false]);

                // Deshabilitar macros
                appType.InvokeMember("AutomationSecurity",
                    System.Reflection.BindingFlags.SetProperty,
                    null, app, [3]);
            });

            var excelType = excelApp.GetType();

            var workbooks = excelType.InvokeMember("Workbooks",
                System.Reflection.BindingFlags.GetProperty,
                null, excelApp, null)
                ?? throw new InvalidOperationException("No se pudo acceder a la colecci\u00f3n de libros");

            // Open(Filename, ReadOnly:=True)
            object[] openParams = [sourcePath, Type.Missing, true];
            workbook = workbooks.GetType().InvokeMember("Open",
                System.Reflection.BindingFlags.InvokeMethod,
                null, workbooks, openParams)
                ?? throw new InvalidOperationException("No se pudo abrir el libro");

            workbook.GetType().InvokeMember("ExportAsFixedFormat",
                System.Reflection.BindingFlags.InvokeMethod,
                null, workbook, [0, pdfPath]); // xlTypePDF = 0
        }
        finally
        {
            CleanupComObject(workbook, closeMethod: "Close", closeParams: [false]);
            CleanupOfficeApp(excelApp);
        }
    }

    #endregion

    #region PowerPoint Conversion

    private static void ConvertPowerPointToPdf(string sourcePath, string pdfPath)
    {
        // La sesión decide si la instancia es NUESTRA o del usuario, y solo cierra la nuestra (TJ-01).
        using var session = PowerPointSession.Open();
        object? presentation = null;

        try
        {
            var pptType = session.App.GetType();

            var presentations = pptType.InvokeMember("Presentations",
                System.Reflection.BindingFlags.GetProperty,
                null, session.App, null)
                ?? throw new InvalidOperationException("No se pudo acceder a la colecci\u00f3n de presentaciones");

            // Open(FileName, ReadOnly:=True, Untitled, WithWindow:=False)
            object[] openParams =
            [
                sourcePath,
                -1,  // ReadOnly = msoTrue
                0,   // Untitled = msoFalse
                0    // WithWindow = msoFalse
            ];

            presentation = presentations.GetType().InvokeMember("Open",
                System.Reflection.BindingFlags.InvokeMethod,
                null, presentations, openParams)
                ?? throw new InvalidOperationException("No se pudo abrir la presentaci\u00f3n");


            try
            {
                object[] exportParams =
                [
                    pdfPath,
                    2,    // ppFixedFormatTypePDF
                    1,    // ppFixedFormatIntentPrint
                    Type.Missing, Type.Missing,
                    1,    // ppPrintOutputSlides
                    Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                    Type.Missing, Type.Missing, Type.Missing, Type.Missing,
                    Type.Missing, Type.Missing
                ];

                presentation.GetType().InvokeMember("ExportAsFixedFormat",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null, presentation, exportParams);
            }
            catch
            {
                // Fallback: SaveAs con formato PDF
                presentation.GetType().InvokeMember("SaveAs",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null, presentation, [pdfPath, 32]); // ppSaveAsPDF = 32
            }
        }
        finally
        {
            // Se cierra LA PRESENTACIÓN, siempre: es la que hemos abierto nosotros. De la aplicación se
            // encarga la sesión al soltarse, que es quien sabe si era nuestra o prestada.
            CleanupComObject(presentation, closeMethod: "Close");
        }
    }

    // 🔴 Aquí vivía HidePowerPointWindows, y era código muerto QUE ADEMÁS MENTÍA (TJ-21).
    //
    // Recorría presentation.Windows poniendo Visible = -1 en cada una. Eso es msoTrue: MOSTRAR, justo lo
    // contrario de lo que decía su nombre. Y no se notaba porque no llegaba a ejecutarse nunca: las
    // presentaciones se abren con WithWindow:=False y entonces Windows.Count es 0 — medido, no supuesto.
    //
    // Se borra en vez de "arreglarse" a msoFalse: sin ventanas que ocultar no había nada que arreglar, y
    // dejar una función que no se ejecuta es dejar una trampa cargada para quien algún día abra con
    // ventana. Lo que SÍ había que arreglar era el Visible = msoTrue de la aplicación, en
    // PowerPointSession.Open.

    #endregion

    #region Word HTML Conversion

    private static void ConvertWordToHtml(string sourcePath, string htmlPath)
    {
        object? wordApp = null;
        object? doc = null;

        try
        {
            wordApp = CreateOfficeApp("Word.Application", app =>
            {
                var appType = app.GetType();
                appType.InvokeMember("DisplayAlerts",
                    System.Reflection.BindingFlags.SetProperty,
                    null, app, [0]);
                appType.InvokeMember("AutomationSecurity",
                    System.Reflection.BindingFlags.SetProperty,
                    null, app, [3]);
            });

            var wordType = wordApp.GetType();
            var documents = wordType.InvokeMember("Documents",
                System.Reflection.BindingFlags.GetProperty,
                null, wordApp, null)
                ?? throw new InvalidOperationException("No se pudo acceder a la colecci\u00f3n de documentos");

            object[] openParams = [sourcePath, Type.Missing, true, false];
            doc = documents.GetType().InvokeMember("Open",
                System.Reflection.BindingFlags.InvokeMethod,
                null, documents, openParams)
                ?? throw new InvalidOperationException("No se pudo abrir el documento");

            // wdFormatFilteredHTML = 10
            doc.GetType().InvokeMember("SaveAs2",
                System.Reflection.BindingFlags.InvokeMethod,
                null, doc, [htmlPath, 10]);
        }
        finally
        {
            CleanupComObject(doc, closeMethod: "Close", closeParams: [false]);
            CleanupOfficeApp(wordApp);
        }
    }

    #endregion

    #region Excel CSV Conversion

    private static void ConvertExcelToCsv(string sourcePath, string csvPath, string sheetNames)
    {
        object? excelApp = null;
        object? workbook = null;

        try
        {
            excelApp = CreateOfficeApp("Excel.Application", app =>
            {
                var appType = app.GetType();
                appType.InvokeMember("DisplayAlerts",
                    System.Reflection.BindingFlags.SetProperty,
                    null, app, [false]);
                appType.InvokeMember("AutomationSecurity",
                    System.Reflection.BindingFlags.SetProperty,
                    null, app, [3]);
            });

            var excelType = excelApp.GetType();
            var workbooks = excelType.InvokeMember("Workbooks",
                System.Reflection.BindingFlags.GetProperty,
                null, excelApp, null)
                ?? throw new InvalidOperationException("No se pudo acceder a la colecci\u00f3n de libros");

            object[] openParams = [sourcePath, Type.Missing, true];
            workbook = workbooks.GetType().InvokeMember("Open",
                System.Reflection.BindingFlags.InvokeMethod,
                null, workbooks, openParams)
                ?? throw new InvalidOperationException("No se pudo abrir el libro");

            // Activate specific sheet if requested
            if (!string.IsNullOrWhiteSpace(sheetNames))
            {
                try
                {
                    var sheets = workbook.GetType().InvokeMember("Worksheets",
                        System.Reflection.BindingFlags.GetProperty,
                        null, workbook, null);
                    var sheet = sheets?.GetType().InvokeMember("Item",
                        System.Reflection.BindingFlags.GetProperty,
                        null, sheets, [sheetNames.Trim()]);
                    sheet?.GetType().InvokeMember("Activate",
                        System.Reflection.BindingFlags.InvokeMethod,
                        null, sheet, null);
                }
                catch { /* If sheet not found, export active sheet */ }
            }

            // xlCSV = 6
            workbook.GetType().InvokeMember("SaveAs",
                System.Reflection.BindingFlags.InvokeMethod,
                null, workbook, [csvPath, 6]);
        }
        finally
        {
            CleanupComObject(workbook, closeMethod: "Close", closeParams: [false]);
            CleanupOfficeApp(excelApp);
        }
    }

    #endregion

    #region PowerPoint Image Conversion

    private static void ConvertPowerPointToImages(string sourcePath, string outputPath, ConversionOptions options)
    {
        using var session = PowerPointSession.Open();
        object? presentation = null;

        try
        {
            var pptType = session.App.GetType();
            var presentations = pptType.InvokeMember("Presentations",
                System.Reflection.BindingFlags.GetProperty,
                null, session.App, null)
                ?? throw new InvalidOperationException("No se pudo acceder a las presentaciones");

            object[] openParams = [sourcePath, -1, 0, 0];
            presentation = presentations.GetType().InvokeMember("Open",
                System.Reflection.BindingFlags.InvokeMethod,
                null, presentations, openParams)
                ?? throw new InvalidOperationException("No se pudo abrir la presentaci\u00f3n");


            var format = options.OutputFormat == OutputFormat.JPG ? "JPG" : "PNG";
            var width = (int)(options.ImageDpi * 13.333); // ~10 inches wide at given DPI
            var height = (int)(width * 0.5625); // 16:9

            // Create output directory for slides
            var outputDir = outputPath;
            Directory.CreateDirectory(outputDir);

            // Export all slides as images
            presentation.GetType().InvokeMember("Export",
                System.Reflection.BindingFlags.InvokeMethod,
                null, presentation, [outputDir, format, width, height]);
        }
        finally
        {
            // Se cierra LA PRESENTACIÓN, siempre: es la que hemos abierto nosotros. De la aplicación se
            // encarga la sesión al soltarse, que es quien sabe si era nuestra o prestada.
            CleanupComObject(presentation, closeMethod: "Close");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// La instancia de PowerPoint que usa una conversión, y <b>de quién es</b>.
    /// </summary>
    /// <remarks>
    /// PowerPoint no se puede instanciar dos veces: activarlo devuelve <b>el que ya está corriendo</b>, que
    /// puede ser el del usuario, con su presentación a medias sin guardar. La app le ponía
    /// <c>DisplayAlerts = ppAlertsNone</c> y al terminar llamaba a <c>Quit()</c>: le cerraba su PowerPoint
    /// <b>sin preguntar por lo no guardado</b>. (TJ-01, 2026-08-31.)
    ///
    /// De ahí las dos reglas de esta clase: <b>solo se cierra lo que se ha abierto</b>, y lo que se usa
    /// prestado <b>se devuelve como estaba</b>. Ante cualquier duda —no se puede mirar la lista de
    /// procesos, no se puede leer un ajuste— se asume que la instancia es del usuario: cerrar de más le
    /// cuesta su trabajo; no cerrar solo deja un proceso abierto.
    /// </remarks>
    private sealed class PowerPointSession : IDisposable
    {
        private const string ProcessName = "POWERPNT";

        public object App { get; }

        private readonly bool _preexisting;
        private readonly object? _previousAlerts;
        private readonly object? _previousSecurity;

        private PowerPointSession(object app, bool preexisting, object? previousAlerts, object? previousSecurity)
        {
            App = app;
            _preexisting = preexisting;
            _previousAlerts = previousAlerts;
            _previousSecurity = previousSecurity;
        }

        public static PowerPointSession Open()
        {
            // Se mira ANTES de activar: después ya no hay forma de saber quién trajo el proceso.
            bool preexisting = IsRunning();

            var app = CreateOfficeApp("PowerPoint.Application", null);
            var type = app.GetType();

            // Lo anterior solo importa si la instancia es prestada: la nuestra muere con nosotros.
            object? previousAlerts = preexisting ? TryGet(type, app, "DisplayAlerts") : null;
            object? previousSecurity = preexisting ? TryGet(type, app, "AutomationSecurity") : null;

            // 🔴 NO SE TOCA Visible. Medido en esta maquina (Office 16 ClickToRun, 2026-08-31):
            //
            //   - Recien activado por COM, PowerPoint arranca en Visible = msoFalse y SIN ventana
            //     principal (MainWindowHandle = 0), y abrir la presentacion con WithWindow:=False lo deja
            //     igual. O sea: headless de fabrica, sin pedirlo.
            //   - Poner Visible = msoTrue —lo que hacia este codigo— es precisamente LO QUE HACE APARECER
            //     la ventana de PowerPoint encima de lo que el usuario este haciendo, durante todo el lote.
            //   - Y poner Visible = msoFalse no es alternativa: LANZA
            //     «Hiding the application window is not allowed». De ahi venia la confusion — se creia que
            //     habia que elegir entre msoTrue y una excepcion, cuando la respuesta es no tocarlo.
            //
            // Sobre una instancia prestada, ademas, cambiar esto seria manipular la ventana del usuario.
            TrySet(type, app, "DisplayAlerts", 2);          // ppAlertsNone
            TrySet(type, app, "AutomationSecurity", 3);     // msoAutomationSecurityForceDisable

            Log.Information("PowerPoint: instancia {Origen}",
                preexisting ? "PREEXISTENTE del usuario (no se cerrará)" : "creada por la app");

            return new PowerPointSession(app, preexisting, previousAlerts, previousSecurity);
        }

        public void Dispose()
        {
            if (!_preexisting)
            {
                CleanupOfficeApp(App);   // nuestra: Quit() y a soltar
                return;
            }

            var type = App.GetType();
            if (_previousAlerts is not null) TrySet(type, App, "DisplayAlerts", _previousAlerts);
            if (_previousSecurity is not null) TrySet(type, App, "AutomationSecurity", _previousSecurity);

            try
            {
                // ReleaseComObject y NO FinalReleaseComObject: el RCW de una aplicación COM es COMPARTIDO
                // dentro del proceso, así que "Final" no suelta NUESTRA referencia, sino TODAS —incluidas
                // las de quien más la tenga—. Sobre una instancia prestada eso deja a PowerPoint sin
                // clientes de automatización y CIERRA LAS PRESENTACIONES que se abrieron por esa vía: el
                // proceso sigue vivo y aun así el usuario pierde lo que tenía. Medido con la prueba
                // PowerPointSharedInstanceTests, que fallaba exactamente ahí. Se devuelve UNA referencia,
                // que es la que se pidió.
                Marshal.ReleaseComObject(App);
            }
            catch (Exception ex)
            {
                Log.Warning("PowerPoint: error al soltar la instancia prestada: {Message}", ex.Message);
            }
        }

        /// <summary>¿Hay ya un PowerPoint corriendo? Ante la duda, sí: cerrar de más es lo caro.</summary>
        private static bool IsRunning()
        {
            Process[] processes;
            try { processes = Process.GetProcessesByName(ProcessName); }
            catch (Exception ex)
            {
                Log.Warning("PowerPoint: no se pudo mirar la lista de procesos ({Message}); se asume que la instancia es del usuario.", ex.Message);
                return true;
            }

            try { return processes.Length > 0; }
            finally { foreach (var p in processes) p.Dispose(); }
        }

        private static object? TryGet(Type type, object app, string property)
        {
            try { return type.InvokeMember(property, System.Reflection.BindingFlags.GetProperty, null, app, null); }
            catch { return null; }
        }

        private static void TrySet(Type type, object app, string property, object value)
        {
            try { type.InvokeMember(property, System.Reflection.BindingFlags.SetProperty, null, app, [value]); }
            catch { }
        }
    }

    /// <summary>
    /// Activa una aplicación de Office y la deja configurada. <b>Si algo falla por el camino, la cierra
    /// antes de propagar</b>.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>El fallo que cierra (TJ-20).</b> Antes, <c>configure(app)</c> se llamaba fuera de todo
    /// <c>try</c>. Si lanzaba —un <c>InvokeMember</c> que esa versión de Office no admite—, el método
    /// propagaba <b>sin haber devuelto el objeto</b>: la variable del llamante seguía en <c>null</c>, su
    /// <c>finally</c> no tenía a quién llamarle <c>Quit()</c>, y quedaba un <c>WINWORD.EXE</c> o
    /// <c>EXCEL.EXE</c> vivo <b>por cada intento</b>. Un lote de 50 documentos contra una versión de
    /// Office que no admitiera una de estas propiedades dejaba 50 procesos.
    ///
    /// Es el riesgo que <c>CONTEXT.md</c> señala como el principal de la app: quien abre por COM es
    /// responsable de cerrar <b>también cuando el camino se tuerce</b>. La ventana peligrosa es
    /// exactamente la que va entre «ya existe el proceso» y «el llamante tiene la referencia».
    /// </remarks>
    internal static object CreateOfficeApp(string progId, Action<object>? configure)
    {
        var appType = Type.GetTypeFromProgID(progId)
            ?? throw new InvalidOperationException($"No se pudo obtener el tipo de {progId}");

        var app = Activator.CreateInstance(appType)
            ?? throw new InvalidOperationException($"No se pudo crear la instancia de {progId}");

        try
        {
            try
            {
                app.GetType().InvokeMember("Visible",
                    System.Reflection.BindingFlags.SetProperty,
                    null, app, [false]);
            }
            catch { /* PowerPoint no lo admite; se trata en PowerPointSession */ }

            configure?.Invoke(app);
            return app;
        }
        catch
        {
            // Nadie más va a poder cerrarlo: el llamante todavía no tiene la referencia.
            CleanupOfficeApp(app);
            throw;
        }
    }

    private static void CleanupComObject(object? obj, string? closeMethod = null, object[]? closeParams = null)
    {
        if (obj is null) return;

        try
        {
            if (closeMethod is not null)
            {
                obj.GetType().InvokeMember(closeMethod,
                    System.Reflection.BindingFlags.InvokeMethod,
                    null, obj, closeParams);
            }

            Marshal.FinalReleaseComObject(obj);
        }
        catch (Exception ex)
        {
            Log.Warning("Error al limpiar objeto COM: {Message}", ex.Message);
        }
    }

    internal static void CleanupOfficeApp(object? app)
    {
        if (app is null) return;

        try
        {
            app.GetType().InvokeMember("Quit",
                System.Reflection.BindingFlags.InvokeMethod,
                null, app, null);

            Marshal.FinalReleaseComObject(app);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        catch (Exception ex)
        {
            Log.Warning("Error al cerrar aplicación Office: {Message}", ex.Message);
        }
    }

    #endregion
}