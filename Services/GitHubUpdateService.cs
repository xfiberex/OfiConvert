using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace OfiConvert.Services;

internal sealed record GitHubReleaseInfo(string TagName, string HtmlUrl, string Version);

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

            return new GitHubReleaseInfo(release.TagName, release.HtmlUrl, remoteTag);
        }
        catch
        {
            return null;
        }
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; init; } = "";
    }
}
