using OfiConvert.Services;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// La miniatura de un documento, y el rastro que no debe dejar (TJ-14).
/// </summary>
/// <remarks>
/// La versión anterior escribía un PNG en <c>%TEMP%</c>, se lo daba a <c>BitmapImage.UriSource</c> —carga
/// asíncrona— y lo borraba acto seguido. La carrera se perdía en los dos sentidos: o no cargaba la imagen,
/// o no se borraba el archivo y <c>%TEMP%</c> se llenaba de <c>oficonvert_thumb_*.png</c>. Aquí se
/// comprueba la mitad que no necesita hilo de UI: que hay imagen y que <b>no se toca el disco</b>.
/// </remarks>
public sealed class ThumbnailServiceTests : IDisposable
{
    private const string TempPattern = "oficonvert_thumb_*";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"OfiConvertThumb-{Guid.NewGuid():N}");

    public ThumbnailServiceTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* limpieza best-effort */ }
    }

    private string WriteDocument(string name = "informe.docx")
    {
        // Un archivo real, con extensión conocida: al shell le basta para dar el icono del tipo aunque el
        // documento no traiga miniatura propia.
        string path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, [0x50, 0x4B, 0x03, 0x04, .. new byte[128]]);
        return path;
    }

    private static int TempLeftovers() => Directory.GetFiles(Path.GetTempPath(), TempPattern).Length;

    [Fact]
    public async Task DeUnDocumento_SaleUnPng()
    {
        byte[]? png = await ThumbnailService.GetThumbnailBytesAsync(WriteDocument(), 48, 48);

        Assert.NotNull(png);
        Assert.True(png!.Length > 0);
        // Firma PNG: 89 50 4E 47. Que devuelva "algo" no basta; tiene que ser una imagen.
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], png.Take(4));
    }

    [Fact]
    public async Task DeUnArchivoQueNoExiste_NoHayMiniaturaNiExcepcion()
        => Assert.Null(await ThumbnailService.GetThumbnailBytesAsync(
            Path.Combine(_folder, "no-existe.docx"), 48, 48));

    /// <summary>
    /// EL RASTRO: encolar 50 archivos no puede dejar ni un PNG en <c>%TEMP%</c>. Antes quedaban ahí para
    /// siempre cada vez que la carrera la ganaba la carga de la imagen.
    /// </summary>
    [Fact]
    public async Task CincuentaMiniaturas_NoDejanNadaEnTemp()
    {
        int antes = TempLeftovers();

        var paths = Enumerable.Range(0, 50).Select(i => WriteDocument($"documento{i}.docx")).ToArray();
        await Task.WhenAll(paths.Select(p => ThumbnailService.GetThumbnailBytesAsync(p, 48, 48)));

        Assert.Equal(antes, TempLeftovers());
    }
}
