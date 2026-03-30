using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using OfiConvert.Models;

namespace OfiConvert.Services;

public class OfficeFileConversionService : IFileConversionService
{
    private static readonly string[] SupportedExtensions =
        [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx"];

    public bool IsOfficeInstalled()
    {
        try
        {
            var wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType == null)
                return false;

            var wordApp = Activator.CreateInstance(wordType);
            if (wordApp is not null)
            {
                Marshal.ReleaseComObject(wordApp);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool IsValidOfficeFile(string extension)
    {
        return SupportedExtensions.Contains(extension.ToLowerInvariant());
    }

    public async Task<ConversionResult> ConvertToPdfAsync(
        string sourcePath,
        string outputPath,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
            return ConversionResult.Failed("El archivo de origen no existe");

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();

        if (!IsValidOfficeFile(extension))
            return ConversionResult.Failed($"Extensi\u00f3n no soportada: {extension}");

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (extension)
                {
                    case ".doc":
                    case ".docx":
                        ConvertWordToPdf(sourcePath, outputPath);
                        break;

                    case ".xls":
                    case ".xlsx":
                        ConvertExcelToPdf(sourcePath, outputPath);
                        break;

                    case ".ppt":
                    case ".pptx":
                        ConvertPowerPointToPdf(sourcePath, outputPath);
                        break;

                    default:
                        throw new NotSupportedException($"Formato no soportado: {extension}");
                }
            }, cancellationToken);

            return ConversionResult.Successful(outputPath);
        }
        catch (OperationCanceledException)
        {
            return ConversionResult.Failed("Operaci\u00f3n cancelada");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error en conversi\u00f3n: {ex}");
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
            Debug.WriteLine($"Error al limpiar objeto COM: {ex.Message}");
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
            Debug.WriteLine($"Error al cerrar aplicaci\u00f3n Office: {ex.Message}");
        }
    }

    #endregion
}