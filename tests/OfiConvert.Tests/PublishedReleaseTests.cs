using OfiConvert.Services;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Verifica el release REAL publicado en GitHub, no uno de mentira.
///
/// Cierra la costura que ninguna prueba con un servidor local puede cerrar: que el <c>.sha256</c> que
/// escribe <c>installer/build-installer.ps1</c> —y que sube <c>release.ps1</c>— sea exactamente lo que
/// el verificador de la app sabe leer. Las dos mitades viven en repos distintos del cerebro (PowerShell
/// y C#) y pueden divergir en silencio: bastaría un cambio de formato del archivo de hash para que
/// TODA actualización pasara a rechazarse, y no se sabría hasta el siguiente corte.
///
/// Descarga ~58 MB, así que se OMITE por defecto (ver <see cref="NetworkFactAttribute"/>).
/// </summary>
public sealed class PublishedReleaseTests
{
    [NetworkFact]
    public async Task LatestRelease_IsDownloadableAndVerifiable()
    {
        // GetLatestReleaseAsync y no CheckForUpdateAsync: esta última compara versiones y devolvería
        // null precisamente cuando el repo está al día —que es lo normal justo después de un corte—,
        // así que el test no verificaría nada nunca.
        GitHubReleaseInfo? release = await GitHubUpdateService.GetLatestReleaseAsync();

        Assert.NotNull(release);
        Assert.False(string.IsNullOrWhiteSpace(release!.DownloadUrl), "El release no publica un instalador (.exe).");
        Assert.False(string.IsNullOrWhiteSpace(release.ChecksumUrl),
            "El release no publica su .sha256: la app RECHAZARÁ esta actualización. release.ps1 debe subir los dos assets.");

        string destination = Path.Combine(Path.GetTempPath(), $"OfiConvert_RealRelease_{Guid.NewGuid():N}.exe");
        try
        {
            // Si el hash publicado no cuadrase con el instalador publicado, esto lanzaría.
            string path = await GitHubUpdateService.DownloadInstallerAsync(
                release.DownloadUrl, release.ChecksumUrl, destinationPath: destination);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 1_000_000, "El instalador descargado es sospechosamente pequeño.");
        }
        finally
        {
            if (File.Exists(destination)) File.Delete(destination);
        }
    }
}
