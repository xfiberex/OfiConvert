using OfiConvert.Models;

namespace OfiConvert.Services;

public interface IFileConversionService
{
    Task<ConversionResult> ConvertToPdfAsync(
        string sourcePath,
        string outputPath,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ConversionResult> ConvertAsync(
        string sourcePath,
        string outputPath,
        ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default);

    bool IsOfficeInstalled();
    bool IsValidOfficeFile(string extension);
}