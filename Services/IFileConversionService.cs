using OfiConvert.Models;

namespace OfiConvert.Services;

/// <summary>
/// Un motor de conversión: Office por COM o LibreOffice por línea de comandos.
/// </summary>
/// <remarks>
/// <b>Aquí NO hay <c>IProgress&lt;ConversionProgress&gt;</c>, y es una decisión, no un olvido (TJ-19).</b>
///
/// Lo hubo: atravesaba las dos firmas, las dos implementaciones y el ViewModel, que construía un
/// <c>Progress&lt;&gt;</c> con el mensaje «Convirtiendo 3/7». <b>No se ejecutó nunca</b> — ningún motor
/// llamaba a <c>Report</c>. Se quitó entero en vez de implementarlo, y el motivo es que <b>no hay
/// progreso que reportar</b>:
///
/// <list type="bullet">
///   <item>Word y Excel convierten con <b>una</b> llamada COM (<c>ExportAsFixedFormat</c>, <c>SaveAs</c>)
///   que no admite devolución de llamada: se entra, se sale, no hay puntos intermedios.</item>
///   <item>LibreOffice es <b>un proceso externo</b> que no informa de nada por la salida estándar.</item>
///   <item>Solo PPT→imágenes conoce el número de diapositivas, y aun así la exportación es un único
///   <c>Export</c>. Reportar en 1 de 6 caminos daría una barra que se mueve para un formato y se queda
///   quieta para los demás: peor que no tenerla.</item>
/// </list>
///
/// El progreso que el usuario sí ve —cuántos archivos del lote van— lo lleva el ViewModel, que es quien
/// tiene esa información. <b>Una API que promete lo que no puede cumplir es peor que una API pequeña.</b>
/// </remarks>
public interface IFileConversionService
{
    Task<ConversionResult> ConvertToPdfAsync(
        string sourcePath,
        string outputPath,
        CancellationToken cancellationToken = default);

    Task<ConversionResult> ConvertAsync(
        string sourcePath,
        string outputPath,
        ConversionOptions options,
        CancellationToken cancellationToken = default);

    bool IsOfficeInstalled();
    bool IsValidOfficeFile(string extension);
}