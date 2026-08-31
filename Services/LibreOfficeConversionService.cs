using System.Diagnostics;
using System.IO;
using OfiConvert.Core;
using OfiConvert.Models;
using Serilog;

namespace OfiConvert.Services;

public class LibreOfficeConversionService : IFileConversionService
{
    private string? _libreOfficePath;

    public bool IsOfficeInstalled() => GetLibreOfficePath() is not null;

    public bool IsValidOfficeFile(string extension) => OfficeFormats.IsSupported(extension);

    public async Task<ConversionResult> ConvertToPdfAsync(
        string sourcePath, string outputPath,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await ConvertAsync(sourcePath, outputPath,
            new ConversionOptions { OutputFormat = OutputFormat.PDF }, progress, cancellationToken);
    }

    public async Task<ConversionResult> ConvertAsync(
        string sourcePath, string outputPath, ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var loPath = GetLibreOfficePath();
        if (loPath is null)
            return ConversionResult.Failed("MsgLibreOfficeNotInstalled");

        if (!File.Exists(sourcePath))
            return ConversionResult.Failed("MsgFileNotFound");

        var sw = Stopwatch.StartNew();
        string? workFolder = null;

        try
        {
            var formatArg = GetLibreOfficeFormatArg(options.OutputFormat);

            // NUNCA se le da a LibreOffice la carpeta del usuario como --outdir. Escribe con el nombre del
            // original y PISA lo que encuentre: con un informe.pdf ya presente, lo sobrescribía y el
            // File.Move de después se llevaba el nuevo a "informe (1).pdf", así que el anterior
            // desaparecía. Se convierte en una carpeta temporal exclusiva y desde ahí se mueve (TJ-03).
            workFolder = LibreOfficeOutput.CreateWorkFolder();

            if (sourcePath.IndexOf('"') >= 0 || workFolder.IndexOf('"') >= 0)
                return ConversionResult.Failed("MsgInvalidPathCharacters");

            Log.Information("LibreOffice: Convirtiendo {Source} a {Format}", sourcePath, formatArg);

            var psi = new ProcessStartInfo
            {
                FileName = loPath,
                Arguments = $"--headless --norestore --convert-to {formatArg} --outdir \"{workFolder}\" \"{sourcePath}\""
            };

            // ProcessRunner lee los dos flujos ANTES de esperar al proceso. Con stdout y stderr
            // redirigidos y sin leer, el búfer de la tubería (~4 KB) se llena, soffice se BLOQUEA
            // escribiendo y la espera no vuelve nunca: la conversión se congelaba para siempre ocupando
            // una plaza del semáforo. Basta un documento que arrastre unos avisos de fuentes. (TJ-02.)
            var run = await ProcessRunner.RunAsync(psi, cancellationToken);
            sw.Stop();

            if (run.ExitCode != 0)
            {
                Log.Error("LibreOffice: Error de conversión - {Error}", run.StandardError);
                return ConversionResult.Failed(new UserMessage("MsgLibreOfficeError", run.ExitCode, run.StandardError));
            }

            var expectedName = LibreOfficeOutput.ExpectedFileName(
                sourcePath, OutputFormatHelper.GetFileExtension(options.OutputFormat));
            var produced = LibreOfficeOutput.PickProduced(Directory.GetFiles(workFolder), expectedName);

            if (produced is null)
            {
                // Código 0 y sin resultado: pasa con formatos que el filtro no soporta para ese documento.
                Log.Error("LibreOffice: terminó en 0 sin producir {Expected}. stdout: {Out} stderr: {Err}",
                    expectedName, run.StandardOutput, run.StandardError);
                return ConversionResult.Failed("MsgLibreOfficeNoOutput");
            }

            var finalPath = LibreOfficeOutput.MoveToFinal(produced, outputPath);

            Log.Information("LibreOffice: Conversión exitosa en {Duration}ms", sw.ElapsedMilliseconds);
            return ConversionResult.Successful(finalPath, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return ConversionResult.Failed("MsgConversionCancelled");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Error(ex, "LibreOffice: Error inesperado");
            return ConversionResult.Failed(new UserMessage("MsgLibreOfficeUnexpected", ex.Message));
        }
        finally
        {
            // La carpeta de trabajo es de esta conversión y de nadie más: se va con ella, pase lo que pase.
            // Si no se puede borrar (antivirus, soffice aún soltando el archivo), no es motivo para fallar
            // una conversión que salió bien: queda en %TEMP%, que es de donde Windows la barre.
            if (workFolder is not null)
            {
                try { Directory.Delete(workFolder, recursive: true); }
                catch (Exception ex) { Log.Warning(ex, "LibreOffice: no se pudo borrar {Folder}", workFolder); }
            }
        }
    }

    private static string GetLibreOfficeFormatArg(OutputFormat format)
    {
        return format switch
        {
            OutputFormat.PDF => "pdf",
            OutputFormat.HTML => "html",
            OutputFormat.CSV => "csv",
            OutputFormat.PNG => "png",
            OutputFormat.JPG => "jpg",
            _ => "pdf"
        };
    }

    private string? GetLibreOfficePath()
    {
        if (_libreOfficePath is not null) return _libreOfficePath;

        string[] possiblePaths =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe"),
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
        ];

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _libreOfficePath = path;
                Log.Information("LibreOffice encontrado en: {Path}", path);
                return path;
            }
        }

        return null;
    }
}
