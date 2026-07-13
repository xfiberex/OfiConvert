using System.Diagnostics;
using System.IO;
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
            return ConversionResult.Failed("LibreOffice no está instalado.");

        if (!File.Exists(sourcePath))
            return ConversionResult.Failed("El archivo de origen no existe.");

        var sw = Stopwatch.StartNew();

        try
        {
            var outputDir = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath();
            var formatArg = GetLibreOfficeFormatArg(options.OutputFormat);

            if (sourcePath.IndexOf('"') >= 0 || outputDir.IndexOf('"') >= 0)
                return ConversionResult.Failed("La ruta contiene caracteres no válidos.");

            Log.Information("LibreOffice: Convirtiendo {Source} a {Format}", sourcePath, formatArg);

            var psi = new ProcessStartInfo
            {
                FileName = loPath,
                Arguments = $"--headless --norestore --convert-to {formatArg} --outdir \"{outputDir}\" \"{sourcePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            await process.WaitForExitAsync(cancellationToken);
            sw.Stop();

            if (process.ExitCode == 0)
            {
                var expectedExt = OutputFormatHelper.GetFileExtension(options.OutputFormat);
                var expectedName = Path.ChangeExtension(Path.GetFileName(sourcePath), expectedExt);
                var expectedPath = Path.Combine(outputDir, expectedName);

                if (File.Exists(expectedPath) && !string.Equals(expectedPath, outputPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    File.Move(expectedPath, outputPath);
                }

                Log.Information("LibreOffice: Conversión exitosa en {Duration}ms", sw.ElapsedMilliseconds);
                return ConversionResult.Successful(outputPath, sw.Elapsed);
            }

            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            Log.Error("LibreOffice: Error de conversión - {Error}", error);
            return ConversionResult.Failed($"LibreOffice error (código {process.ExitCode}): {error}");
        }
        catch (OperationCanceledException)
        {
            return ConversionResult.Failed("Operación cancelada");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Error(ex, "LibreOffice: Error inesperado");
            return ConversionResult.Failed($"Error LibreOffice: {ex.Message}");
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
