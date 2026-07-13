using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await ConvertAsync(sourcePath, outputPath,
            new ConversionOptions { OutputFormat = OutputFormat.PDF }, progress, cancellationToken);
    }

    public async Task<ConversionResult> ConvertAsync(
        string sourcePath,
        string outputPath,
        ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            return ConversionResult.Failed("El archivo de origen no existe");

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();

        if (!IsValidOfficeFile(extension))
            return ConversionResult.Failed($"Extensi\u00f3n no soportada: {extension}");

        var sw = Stopwatch.StartNew();

        try
        {
            Log.Information("Office: Convirtiendo {Source} a {Format}", sourcePath, options.OutputFormat);

            await Task.Run(() =>
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

            sw.Stop();
            Log.Information("Office: Conversión exitosa en {Duration}ms", sw.ElapsedMilliseconds);
            return ConversionResult.Successful(outputPath, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ConversionResult.Failed("Operaci\u00f3n cancelada");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Error(ex, "Office: Error en conversión de {Source}", sourcePath);
            return ConversionResult.Failed(ex.Message);
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
        object? pptApp = null;
        object? presentation = null;

        try
        {
            pptApp = CreateOfficeApp("PowerPoint.Application", app =>
            {
                var appType = app.GetType();

                try
                {
                    appType.InvokeMember("Visible",
                        System.Reflection.BindingFlags.SetProperty,
                        null, app, [-1]); // msoTrue
                }
                catch { }

                try
                {
                    appType.InvokeMember("DisplayAlerts",
                        System.Reflection.BindingFlags.SetProperty,
                        null, app, [2]); // ppAlertsNone = 2
                }
                catch { }

                // Deshabilitar macros
                try
                {
                    appType.InvokeMember("AutomationSecurity",
                        System.Reflection.BindingFlags.SetProperty,
                        null, app, [3]);
                }
                catch { }
            });

            var pptType = pptApp.GetType();

            var presentations = pptType.InvokeMember("Presentations",
                System.Reflection.BindingFlags.GetProperty,
                null, pptApp, null)
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

            HidePowerPointWindows(presentation);

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
            CleanupComObject(presentation, closeMethod: "Close");
            CleanupOfficeApp(pptApp);
        }
    }

    private static void HidePowerPointWindows(object presentation)
    {
        try
        {
            var windows = presentation.GetType().InvokeMember("Windows",
                System.Reflection.BindingFlags.GetProperty,
                null, presentation, null);

            if (windows is null) return;

            var count = (int?)windows.GetType().InvokeMember("Count",
                System.Reflection.BindingFlags.GetProperty,
                null, windows, null) ?? 0;

            for (int i = 1; i <= count; i++)
            {
                try
                {
                    var window = windows.GetType().InvokeMember("Item",
                        System.Reflection.BindingFlags.InvokeMethod,
                        null, windows, [i]);

                    window?.GetType().InvokeMember("Visible",
                        System.Reflection.BindingFlags.SetProperty,
                        null, window, [-1]);

                    if (window is not null)
                        Marshal.ReleaseComObject(window);
                }
                catch { }
            }
        }
        catch { }
    }

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
        object? pptApp = null;
        object? presentation = null;

        try
        {
            pptApp = CreateOfficeApp("PowerPoint.Application", app =>
            {
                var appType = app.GetType();
                try { appType.InvokeMember("Visible", System.Reflection.BindingFlags.SetProperty, null, app, [-1]); } catch { }
                try { appType.InvokeMember("DisplayAlerts", System.Reflection.BindingFlags.SetProperty, null, app, [2]); } catch { }
                try { appType.InvokeMember("AutomationSecurity", System.Reflection.BindingFlags.SetProperty, null, app, [3]); } catch { }
            });

            var pptType = pptApp.GetType();
            var presentations = pptType.InvokeMember("Presentations",
                System.Reflection.BindingFlags.GetProperty,
                null, pptApp, null)
                ?? throw new InvalidOperationException("No se pudo acceder a las presentaciones");

            object[] openParams = [sourcePath, -1, 0, 0];
            presentation = presentations.GetType().InvokeMember("Open",
                System.Reflection.BindingFlags.InvokeMethod,
                null, presentations, openParams)
                ?? throw new InvalidOperationException("No se pudo abrir la presentaci\u00f3n");

            HidePowerPointWindows(presentation);

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
            CleanupComObject(presentation, closeMethod: "Close");
            CleanupOfficeApp(pptApp);
        }
    }

    #endregion

    #region Helper Methods

    private static object CreateOfficeApp(string progId, Action<object>? configure)
    {
        var appType = Type.GetTypeFromProgID(progId)
            ?? throw new InvalidOperationException($"No se pudo obtener el tipo de {progId}");

        var app = Activator.CreateInstance(appType)
            ?? throw new InvalidOperationException($"No se pudo crear la instancia de {progId}");

        try
        {
            app.GetType().InvokeMember("Visible",
                System.Reflection.BindingFlags.SetProperty,
                null, app, [false]);
        }
        catch { }

        configure?.Invoke(app);
        return app;
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

    private static void CleanupOfficeApp(object? app)
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