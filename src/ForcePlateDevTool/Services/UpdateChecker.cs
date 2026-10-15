using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ForcePlateDevTool.Services;

public sealed record UpdateRelease(
    string TagName,
    string Version,
    string ReleaseUrl,
    string ReleaseNotes,
    string? InstallerUrl);

public sealed class UpdateChecker
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/kongdf123/ForcePlateDevTool/releases/latest";

    private static readonly HttpClient SharedClient = CreateClient();
    private readonly HttpClient _httpClient;

    public UpdateChecker(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedClient;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ForcePlateDevTool", "1.0"));

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return client;
    }

    public async Task<UpdateRelease?> CheckForUpdateAsync(
        string installedVersion,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            LatestReleaseApi, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var json = await JsonDocument.ParseAsync(
            stream, cancellationToken: cancellationToken);

        var root = json.RootElement;

        if (root.TryGetProperty("draft", out var draft) &&
            draft.GetBoolean())
        {
            return null;
        }

        if (root.TryGetProperty("prerelease", out var prerelease) &&
            prerelease.GetBoolean())
        {
            return null;
        }

        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!TryParseStableVersion(tag, out var latestVersion))
            return null;

        if (!TryParseStableVersion(installedVersion, out var currentVersion))
            return null;

        if (latestVersion <= currentVersion)
            return null;

        string releaseUrl =
            root.GetProperty("html_url").GetString()
            ?? "https://github.com/kongdf123/ForcePlateDevTool/releases";

        string notes = root.TryGetProperty("body", out var body)
            ? body.GetString() ?? ""
            : "";

        string? installerUrl = null;

        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";

                if (name.Equals(
                    $"ForcePlateDevTool-Setup-{tag}.exe",
                    StringComparison.OrdinalIgnoreCase))
                {
                    installerUrl =
                        asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
        }

        return new UpdateRelease(
            tag,
            latestVersion.ToString(3),
            releaseUrl,
            notes,
            installerUrl);
    }

    public static bool TryParseStableVersion(
        string value,
        out Version version)
    {
        version = new Version(0, 0, 0);

        var match = Regex.Match(
            value.Trim(),
            @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:\+[0-9A-Za-z.-]+)?$");

        if (!match.Success)
            return false;

        if (Version.TryParse(
                $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}",
                out var parsed) &&
            parsed is not null)
        {
            version = parsed;
            return true;
        }

        return false;
    }
}