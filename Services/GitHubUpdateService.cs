using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace OfiConvert.Services;

internal sealed record GitHubReleaseInfo(string TagName, string HtmlUrl, string Version, string DownloadUrl);

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
        try
        {
            GitHubReleaseResponse? release = await Http
                .GetFromJsonAsync<GitHubReleaseResponse>(LatestReleaseUri, cancellationToken)
                .ConfigureAwait(false);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
                return null;

            string remoteTag = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(remoteTag, out Version? remoteVersion))
                return null;

            Version currentVersion =
                Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

            if (remoteVersion <= currentVersion)
                return null;

            string downloadUrl = release.Assets
                .FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl ?? release.HtmlUrl;
            return new GitHubReleaseInfo(release.TagName, release.HtmlUrl, remoteTag, downloadUrl);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string> DownloadInstallerAsync(
        string downloadUrl,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"{Repo}_Update.exe");

        using HttpResponseMessage response = await DownloadHttp
            .GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        await using Stream contentStream = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using FileStream fileStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

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

        return tempPath;
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
