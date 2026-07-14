using System.Security.Cryptography;
using OfiConvert.Services;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Pruebas de la verificación del instalador: lo que decide si un ejecutable descargado de internet
/// se ejecuta o no.
///
/// Ejercen la DESCARGA COMPLETA contra un servidor HTTP local, no solo el cálculo del hash. Es
/// deliberado: en WingetUSoft, las pruebas cubrían el hash pero nunca la descarga, y por eso pasó
/// inadvertido el bug que dejó su auto-actualización muerta durante dos versiones (el archivo se
/// bloqueaba a sí mismo y la verificación no podía ni abrirlo).
/// </summary>
public sealed class GitHubUpdateServiceTests : IDisposable
{
    private readonly LocalHttpServer _server = new();
    private readonly string _destination =
        Path.Combine(Path.GetTempPath(), $"OfiConvert_Update_Test_{Guid.NewGuid():N}.exe");

    // Un instalador de mentira, con tamaño suficiente para que la descarga dé más de una vuelta al
    // buffer de 81920 bytes.
    private static byte[] FakeInstaller()
    {
        var bytes = new byte[200_000];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    private static string Sha256Of(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    public void Dispose()
    {
        _server.Dispose();
        if (File.Exists(_destination)) File.Delete(_destination);
    }

    [Fact]
    public async Task DownloadInstaller_WithMatchingChecksum_KeepsTheFile()
    {
        byte[] installer = FakeInstaller();
        string exeUrl = _server.Serve("/OfiConvert_Setup_9.9.9.exe", installer);
        // Formato sha256sum, que es el que escribe build-installer.ps1.
        string shaUrl = _server.Serve("/OfiConvert_Setup_9.9.9.exe.sha256",
            $"{Sha256Of(installer)} *OfiConvert_Setup_9.9.9.exe");

        string path = await GitHubUpdateService.DownloadInstallerAsync(
            exeUrl, shaUrl, destinationPath: _destination);

        Assert.Equal(_destination, path);
        Assert.True(File.Exists(path));
        Assert.Equal(installer, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task DownloadInstaller_WithTamperedContent_RejectsAndDeletesIt()
    {
        // El caso que esto existe para atrapar: el hash publicado es el del instalador legítimo, pero
        // lo que llega por el cable es OTRA COSA.
        byte[] legitimo = FakeInstaller();
        byte[] manipulado = FakeInstaller();

        string exeUrl = _server.Serve("/OfiConvert_Setup_9.9.9.exe", manipulado);
        string shaUrl = _server.Serve("/OfiConvert_Setup_9.9.9.exe.sha256", Sha256Of(legitimo));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GitHubUpdateService.DownloadInstallerAsync(exeUrl, shaUrl, destinationPath: _destination));

        // No basta con lanzar: el archivo en el que no se confía NO puede quedarse en el disco.
        Assert.False(File.Exists(_destination));
    }

    [Fact]
    public async Task DownloadInstaller_WithoutChecksumAsset_RefusesToTrustIt()
    {
        // Un release publicado sin su .sha256 (y sin firmar) NO es verificable: se rechaza.
        // Por eso release.ps1 aborta si el .sha256 no se generó.
        byte[] installer = FakeInstaller();
        string exeUrl = _server.Serve("/OfiConvert_Setup_9.9.9.exe", installer);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            GitHubUpdateService.DownloadInstallerAsync(exeUrl, checksumUrl: null, destinationPath: _destination));

        Assert.False(File.Exists(_destination));
    }

    /// <summary>
    /// <see cref="IProgress{T}"/> SÍNCRONO: recoge el valor en la misma llamada a <c>Report</c>.
    /// </summary>
    /// <remarks>
    /// Antes este test usaba <see cref="Progress{T}"/> con una <c>List&lt;double&gt;</c> y un
    /// <c>Task.Delay(200)</c> para "dar tiempo" a los callbacks. Eso era una carrera con dos filos:
    /// <see cref="Progress{T}"/> despacha cada callback al <c>SynchronizationContext</c> y, a falta de
    /// uno, <b>al thread pool</b> — así que los valores pueden llegar <b>desordenados</b>, desde varios
    /// hilos a la vez (y <c>List.Add</c> no es seguro ahí), y el <c>[^1]</c> no tenía por qué ser el
    /// último valor reportado. El test pasaba por suerte; se puso en rojo en cuanto la suite creció y
    /// metió presión en el thread pool. Aquí importa QUÉ reporta la descarga, no cómo decide despacharlo
    /// quien la llama.
    /// </remarks>
    private sealed class SyncProgress : IProgress<double>
    {
        private readonly List<double> _values = [];

        public IReadOnlyList<double> Values => _values;

        public void Report(double value) => _values.Add(value);
    }

    [Fact]
    public async Task DownloadInstaller_ReportsProgress()
    {
        byte[] installer = FakeInstaller();
        string exeUrl = _server.Serve("/OfiConvert_Setup_9.9.9.exe", installer);
        string shaUrl = _server.Serve("/OfiConvert_Setup_9.9.9.exe.sha256", Sha256Of(installer));

        var progress = new SyncProgress();

        await GitHubUpdateService.DownloadInstallerAsync(
            exeUrl, shaUrl, progress, destinationPath: _destination);

        Assert.NotEmpty(progress.Values);
        Assert.All(progress.Values, p => Assert.InRange(p, 0d, 1d));

        // La descarga termina al 100%: es lo que deja la barra de progreso llena en la UI.
        Assert.Equal(1d, progress.Values[^1], precision: 3);
    }

    [Fact]
    public async Task ComputeSha256_MatchesTheHashOfTheContent()
    {
        byte[] content = FakeInstaller();
        await File.WriteAllBytesAsync(_destination, content);

        Assert.Equal(Sha256Of(content), await GitHubUpdateService.ComputeSha256Async(_destination));
    }

    [Fact]
    public async Task VerifyAuthenticodeSignature_IsFalseForAnUnsignedFile()
    {
        // Los instaladores del proyecto se publican SIN firmar. Este test fija esa realidad: si algún
        // día devolviera true para un archivo sin firma, la verificación por hash se saltaría entera.
        await File.WriteAllBytesAsync(_destination, FakeInstaller());

        Assert.False(GitHubUpdateService.VerifyAuthenticodeSignature(_destination));
    }

    [Theory]
    [InlineData("2.1.0", "2.1.0.0")]
    [InlineData("2.1", "2.1.0.0")]
    [InlineData("2.1.0.0", "2.1.0.0")]
    public void NormalizeVersion_PadsTagsToFourParts(string tag, string expected)
    {
        Assert.Equal(expected, GitHubUpdateService.NormalizeVersion(tag));
    }

    [Fact]
    public void NormalizeVersion_MakesATagCompareEqualToItsAssemblyVersion()
    {
        // Sin normalizar, Version.Parse("2.1.0") tiene Revision = -1 y sale MENOR que 2.1.0.0.
        Assert.True(Version.Parse("2.1.0") < Version.Parse("2.1.0.0"));
        Assert.Equal(Version.Parse("2.1.0.0"), Version.Parse(GitHubUpdateService.NormalizeVersion("2.1.0")));
    }
}
