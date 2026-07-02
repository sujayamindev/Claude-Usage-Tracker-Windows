using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeUsageTracker.Windows.Services;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Error
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? LatestVersion = null, string? DownloadUrl = null, string? ErrorMessage = null)
{
    public static UpdateCheckResult UpToDate() => new(UpdateCheckStatus.UpToDate);
    public static UpdateCheckResult Available(string version, string downloadUrl) => new(UpdateCheckStatus.UpdateAvailable, version, downloadUrl);
    public static UpdateCheckResult Failed(string message) => new(UpdateCheckStatus.Error, ErrorMessage: message);
}

/// <summary>
/// Checks GitHub Releases for a newer version and downloads/installs it. Uses a plain HttpClient —
/// api.github.com is a separate public service, not behind the claude.ai Cloudflare bot-check that
/// requires WebView2ApiTransport (same reasoning as ClaudeStatusService).
/// </summary>
public sealed class UpdateService : IDisposable
{
    private const string RepoOwner = "sujayamindev";
    private const string RepoName = "Claude-Usage-Tracker-Windows";
    private static readonly Uri LatestReleaseUrl = new($"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest");

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public UpdateService()
    {
        // GitHub's API rejects requests with no User-Agent header.
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ClaudeUsageTracker.Windows", GetCurrentVersion().ToString()));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public static Version GetCurrentVersion() =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>Pure comparison, testable without HTTP. Malformed tags are treated as not-newer.</summary>
    public static bool IsNewerVersion(Version current, string latestTag)
    {
        var trimmed = latestTag.TrimStart('v', 'V');
        return Version.TryParse(trimmed, out var latest) && latest > current;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ReleaseResponse>(LatestReleaseUrl, cancellationToken);
            if (response is null)
                return UpdateCheckResult.Failed("Empty response from GitHub");

            var installerAsset = response.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            if (installerAsset is null)
                return UpdateCheckResult.Failed("No installer asset found in the latest release");

            var version = response.TagName.TrimStart('v', 'V');
            return IsNewerVersion(GetCurrentVersion(), response.TagName)
                ? UpdateCheckResult.Available(version, installerAsset.DownloadUrl)
                : UpdateCheckResult.UpToDate();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return UpdateCheckResult.Failed(ex.Message);
        }
    }

    /// <summary>Downloads the installer and launches it silently (/VERYSILENT), which needs no UAC
    /// prompt since the installer is per-user (installer/setup.iss). Caller must shut the app down
    /// afterward so the installer's [Run] relaunch entry can start the updated copy.</summary>
    public async Task DownloadAndInstallAsync(string downloadUrl, CancellationToken cancellationToken = default)
    {
        var updateDir = Path.Combine(Path.GetTempPath(), "ClaudeUsageTracker-Update");
        Directory.CreateDirectory(updateDir);
        var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        var installerPath = Path.Combine(updateDir, fileName);

        await using (var responseStream = await _httpClient.GetStreamAsync(downloadUrl, cancellationToken))
        await using (var fileStream = File.Create(installerPath))
        {
            await responseStream.CopyToAsync(fileStream, cancellationToken);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true
        });
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed record ReleaseResponse(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("assets")] List<ReleaseAsset> Assets);

    private sealed record ReleaseAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string DownloadUrl);
}
