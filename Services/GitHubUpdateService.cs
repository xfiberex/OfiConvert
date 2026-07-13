using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using OfiConvert.Helpers;

namespace OfiConvert.Services;

internal sealed record GitHubReleaseInfo(
    string TagName, string HtmlUrl, string Version, string DownloadUrl, string ChecksumUrl);

internal static class GitHubUpdateService
{
    private const string Owner = "xfiberex";
    private const string Repo = "OfiConvert";

    private static readonly Uri LatestReleaseUri =
        new($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
        DefaultRequestHeaders = { { "User-Agent", $"{Repo}/updater" } }
    };

    private static readonly HttpClient DownloadHttp = new()
    {
        Timeout = Timeout.InfiniteTimeSpan,
        DefaultRequestHeaders = { { "User-Agent", $"{Repo}/updater" } }
    };

    /// <summary>
    /// Compara la versión del ensamblado en ejecución con el último GitHub Release.
    /// Devuelve null si no hay versión más reciente o si la comprobación falla.
    /// </summary>
    public static async Task<GitHubReleaseInfo?> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        GitHubReleaseInfo? release = await GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
        if (release is null)
            return null;

        if (!Version.TryParse(NormalizeVersion(release.Version), out Version? remoteVersion))
            return null;

        Version currentVersion =
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

        return remoteVersion <= currentVersion ? null : release;
    }

    /// <summary>
    /// Devuelve el último release publicado <b>sin comparar versiones</b>. Null si la consulta falla.
    /// </summary>
    public static async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            GitHubReleaseResponse? release = await Http
                .GetFromJsonAsync<GitHubReleaseResponse>(LatestReleaseUri, cancellationToken)
                .ConfigureAwait(false);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
                return null;

            string version = release.TagName.TrimStart('v', 'V');

            string downloadUrl = release.Assets
                .FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl ?? release.HtmlUrl;

            // Lo publica release.ps1 junto al instalador. Ver VerifyInstallerAsync.
            string checksumUrl = release.Assets
                .FirstOrDefault(a => a.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl ?? "";

            return new GitHubReleaseInfo(release.TagName, release.HtmlUrl, version, downloadUrl, checksumUrl);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Normaliza a 4 tramos para comparar el tag ("2.1.0") con el AssemblyVersion ("2.1.0.0").
    /// </summary>
    /// <remarks>
    /// <see cref="Version"/> trata los tramos ausentes como -1, no como 0: <c>Version.Parse("2.1.0")</c>
    /// tiene Revision = -1 y por tanto es <b>menor</b> que <c>2.1.0.0</c>. Hoy eso no rompe nada —el
    /// error cae del lado seguro, "no hay actualización"—, pero es una comparación que no dice lo que
    /// parece decir, y bastaría cambiar el esquema de tags para que se tragara una actualización
    /// legítima en silencio.
    /// </remarks>
    internal static string NormalizeVersion(string version)
    {
        while (version.Count(c => c == '.') < 3)
            version += ".0";
        return version;
    }

    /// <summary>Ruta del instalador descargado. Se sobrescribe en cada intento (FileMode.Create).</summary>
    internal static string DefaultInstallerPath => Path.Combine(Path.GetTempPath(), $"{Repo}_Update.exe");

    /// <summary>
    /// Descarga el instalador y lo VERIFICA. Si no supera la verificación, lo borra y lanza excepción:
    /// nunca devuelve la ruta de un archivo en el que no se confía.
    /// </summary>
    /// <param name="destinationPath">Solo para pruebas: si es null, se usa <see cref="DefaultInstallerPath"/>.</param>
    public static async Task<string> DownloadInstallerAsync(
        string downloadUrl,
        string? checksumUrl = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        string? destinationPath = null)
    {
        string tempPath = destinationPath ?? DefaultInstallerPath;

        // La descarga va en su PROPIO método a propósito: así su FileStream (abierto con FileShare.None)
        // queda cerrado ANTES de verificar. Si el handle siguiera vivo —p. ej. declarando el
        // `await using` a nivel de este método—, tanto VerifyAuthenticodeSignature como
        // ComputeSha256Async fallarían al abrir el archivo con «lo está usando otro proceso» —y el
        // proceso somos nosotros mismos—, así que el instalador se rechazaría SIEMPRE. Le pasó a
        // WingetUSoft: dejó su auto-actualización muerta durante dos versiones.
        await DownloadToFileAsync(downloadUrl, tempPath, progress, cancellationToken).ConfigureAwait(false);

        try
        {
            await VerifyInstallerAsync(tempPath, checksumUrl, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryDeleteRejectedInstaller(tempPath);
            throw;
        }

        return tempPath;
    }

    private static async Task DownloadToFileAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await DownloadHttp
            .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        await using Stream contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using FileStream fileStream = new(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalRead += bytesRead;
            if (totalBytes > 0)
                progress?.Report((double)totalRead / totalBytes.Value);
        }
    }

    /// <summary>
    /// Borra el instalador rechazado. Si el borrado falla, se ignora: el error que importa es el que lo
    /// rechazó, y el próximo intento sobrescribe el archivo (FileMode.Create).
    /// </summary>
    private static void TryDeleteRejectedInstaller(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Comprueba que el instalador recién descargado es el que publicó el proyecto, ANTES de ejecutarlo.
    ///
    /// Se aceptan dos pruebas, en este orden:
    ///
    /// 1. <b>Firma Authenticode válida</b> (la más fuerte: la avala una CA en la que confía Windows).
    ///    Es la vía preferente y la que se usaría sola el día que el proyecto tenga certificado.
    /// 2. <b>SHA-256 publicado como asset del release</b> (<c>...exe.sha256</c>: lo genera
    ///    <c>installer/build-installer.ps1</c> y lo sube <c>release.ps1</c>). Hoy los instaladores se
    ///    publican SIN firmar, así que sin este segundo camino la auto-actualización estaría muerta:
    ///    rechazaría siempre su propio instalador.
    ///
    /// <b>Alcance honesto del hash:</b> el instalador y su .sha256 salen del mismo release, así que esto
    /// detecta corrupción y manipulación EN TRÁNSITO, pero NO protege frente a un compromiso de la
    /// cuenta de GitHub (quien pudiera sustituir el .exe podría sustituir también el hash). Es el
    /// compromiso habitual sin certificado; la firma sigue siendo el objetivo.
    ///
    /// Sin firma válida y sin .sha256, no se ejecuta nada: el instalador se borra.
    /// </summary>
    private static async Task VerifyInstallerAsync(
        string filePath, string? checksumUrl, CancellationToken cancellationToken)
    {
        if (VerifyAuthenticodeSignature(filePath))
            return;

        if (string.IsNullOrWhiteSpace(checksumUrl))
            throw new InvalidOperationException(LocalizationService.Instance["MsgUpdateUnverifiable"]);

        string published = await DownloadHttp
            .GetStringAsync(checksumUrl, cancellationToken)
            .ConfigureAwait(false);

        // Admite tanto "<hash>" a secas como el formato de sha256sum: "<hash> *OfiConvert_Setup_X.Y.Z.exe".
        string expected = published.Trim().Split((char[])[' ', '\t', '\r', '\n'], 2)[0];
        string actual = await ComputeSha256Async(filePath, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(LocalizationService.Instance["MsgUpdateChecksumMismatch"]);
    }

    internal static async Task<string> ComputeSha256Async(
        string filePath, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Comprueba que el archivo lleva una firma Authenticode válida y de confianza para Windows.
    /// Devuelve false si no está firmado, o si la firma está caducada o no es de confianza.
    /// </summary>
    internal static bool VerifyAuthenticodeSignature(string filePath)
    {
        var fileInfo = new NativeMethods.WINTRUST_FILE_INFO
        {
            cbStruct = (uint)Marshal.SizeOf<NativeMethods.WINTRUST_FILE_INFO>(),
            pcwszFilePath = filePath,
            hFile = IntPtr.Zero,
            pgKnownSubject = IntPtr.Zero
        };

        nint fileInfoPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.WINTRUST_FILE_INFO>());
        try
        {
            Marshal.StructureToPtr(fileInfo, fileInfoPtr, false);

            var trustData = new NativeMethods.WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<NativeMethods.WINTRUST_DATA>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = NativeMethods.WTD_UI_NONE,
                fdwRevocationChecks = NativeMethods.WTD_REVOKE_NONE,
                dwUnionChoice = NativeMethods.WTD_CHOICE_FILE,
                pUnion = fileInfoPtr,
                dwStateAction = NativeMethods.WTD_STATEACTION_IGNORE,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = null,
                dwProvFlags = NativeMethods.WTD_SAFER_FLAG,
                dwUIContext = 0
            };

            nint trustDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.WINTRUST_DATA>());
            try
            {
                Marshal.StructureToPtr(trustData, trustDataPtr, false);
                var actionId = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE"); // WINTRUST_ACTION_GENERIC_VERIFY_V2
                uint result = NativeMethods.WinVerifyTrust(IntPtr.Zero, ref actionId, trustDataPtr);
                return result == 0;
            }
            finally
            {
                Marshal.FreeHGlobal(trustDataPtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(fileInfoPtr);
        }
    }

    private static class NativeMethods
    {
        internal const uint WTD_UI_NONE = 2;
        internal const uint WTD_REVOKE_NONE = 0;
        internal const uint WTD_CHOICE_FILE = 1;
        internal const uint WTD_STATEACTION_IGNORE = 0;
        internal const uint WTD_SAFER_FLAG = 0x100;

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = false, CharSet = CharSet.Unicode)]
        internal static extern uint WinVerifyTrust(
            IntPtr hwnd,
            ref Guid pgActionID,
            IntPtr pWVTData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WINTRUST_FILE_INFO
        {
            public uint cbStruct;
            [MarshalAs(UnmanagedType.LPWStr)] public string? pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WINTRUST_DATA
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pUnion;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            [MarshalAs(UnmanagedType.LPWStr)] public string? pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
        }
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = "";

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; init; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = "";
    }
}
