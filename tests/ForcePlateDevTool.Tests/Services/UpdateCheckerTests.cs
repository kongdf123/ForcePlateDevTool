using System.Net;
using System.Net.Http;
using System.Text;
using ForcePlateDevTool.Services;
using Xunit;

namespace ForcePlateDevTool.Tests.Services;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.3", true)]
    [InlineData("1.2.3", true)]
    [InlineData("v1.2.3+abcdef0", true)]
    [InlineData("v1.2.3-rc.1", false)]
    [InlineData("0.1.0-dev", false)]
    [InlineData("v1.2", false)]
    [InlineData("latest", false)]
    [InlineData("", false)]
    public void TryParseStableVersion_ValidatesExpectedFormats(
        string input,
        bool expected)
    {
        bool result = UpdateChecker.TryParseStableVersion(
            input, out var version);

        Assert.Equal(expected, result);
        if (expected)
            Assert.NotNull(version);
    }

    [Fact]
    public void TryParseStableVersion_ReturnsCorrectVersion()
    {
        Assert.True(UpdateChecker.TryParseStableVersion("v1.2.3", out var v));
        Assert.Equal(new Version(1, 2, 3), v);
    }

    [Fact]
    public async Task CheckForUpdateAsync_NoUpdate_ReturnsNull()
    {
        var json = """
        {
            "tag_name": "v1.2.1",
            "html_url": "https://github.com/kongdf123/ForcePlateDevTool/releases/tag/v1.2.1",
            "body": "notes",
            "prerelease": false,
            "draft": false,
            "assets": []
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        var client = new HttpClient(handler);
        var checker = new UpdateChecker(client);

        var release = await checker.CheckForUpdateAsync("v1.2.1");

        Assert.Null(release);
    }

    [Fact]
    public async Task CheckForUpdateAsync_UpdateAvailable_ReturnsRelease()
    {
        var json = """
        {
            "tag_name": "v1.3.0",
            "html_url": "https://github.com/kongdf123/ForcePlateDevTool/releases/tag/v1.3.0",
            "body": "Bug fixes",
            "prerelease": false,
            "draft": false,
            "assets": [
                {
                    "name": "ForcePlateDevTool-Setup-v1.3.0.exe",
                    "browser_download_url": "https://example.com/setup.exe"
                }
            ]
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        var client = new HttpClient(handler);
        var checker = new UpdateChecker(client);

        var release = await checker.CheckForUpdateAsync("v1.2.1");

        Assert.NotNull(release);
        Assert.Equal("v1.3.0", release.TagName);
        Assert.Equal("1.3.0", release.Version);
        Assert.Equal("Bug fixes", release.ReleaseNotes);
        Assert.Equal("https://example.com/setup.exe", release.InstallerUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_DraftRelease_ReturnsNull()
    {
        var json = """
        {
            "tag_name": "v2.0.0",
            "html_url": "https://example.com",
            "body": "",
            "prerelease": false,
            "draft": true,
            "assets": []
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        var client = new HttpClient(handler);
        var checker = new UpdateChecker(client);

        var release = await checker.CheckForUpdateAsync("v1.2.1");

        Assert.Null(release);
    }

    [Fact]
    public async Task CheckForUpdateAsync_Prerelease_ReturnsNull()
    {
        var json = """
        {
            "tag_name": "v2.0.0",
            "html_url": "https://example.com",
            "body": "",
            "prerelease": true,
            "draft": false,
            "assets": []
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        var client = new HttpClient(handler);
        var checker = new UpdateChecker(client);

        var release = await checker.CheckForUpdateAsync("v1.2.1");

        Assert.Null(release);
    }

    [Fact]
    public async Task CheckForUpdateAsync_MissingInstallerAsset_ReturnsReleaseWithNullInstallerUrl()
    {
        var json = """
        {
            "tag_name": "v1.3.0",
            "html_url": "https://github.com/kongdf123/ForcePlateDevTool/releases/tag/v1.3.0",
            "body": "notes",
            "prerelease": false,
            "draft": false,
            "assets": [
                {
                    "name": "ForcePlateDevTool-v1.3.0-win-x64.zip",
                    "browser_download_url": "https://example.com/app.zip"
                }
            ]
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        var client = new HttpClient(handler);
        var checker = new UpdateChecker(client);

        var release = await checker.CheckForUpdateAsync("v1.2.1");

        Assert.NotNull(release);
        Assert.Null(release.InstallerUrl);
        Assert.Equal(
            "https://github.com/kongdf123/ForcePlateDevTool/releases/tag/v1.3.0",
            release.ReleaseUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_DevBuild_ReturnsNull()
    {
        var json = """
        {
            "tag_name": "v1.3.0",
            "html_url": "https://example.com",
            "body": "",
            "prerelease": false,
            "draft": false,
            "assets": []
        }
        """;

        var handler = new FakeHttpMessageHandler(json);
        var client = new HttpClient(handler);
        var checker = new UpdateChecker(client);

        var release = await checker.CheckForUpdateAsync("0.1.0-dev");

        Assert.Null(release);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ServerError_Throws()
    {
        var handler = new FakeHttpMessageHandler(
            "", HttpStatusCode.InternalServerError);
        var client = new HttpClient(handler);
        var checker = new UpdateChecker(client);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => checker.CheckForUpdateAsync("v1.2.1"));
    }

    [Fact]
    public async Task CheckForUpdateAsync_InvalidJson_Throws()
    {
        var handler = new FakeHttpMessageHandler("not json");
        var client = new HttpClient(handler);
        var checker = new UpdateChecker(client);

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(
            () => checker.CheckForUpdateAsync("v1.2.1"));
    }

    [Fact]
    public async Task CheckForUpdateAsync_NetworkFailure_Throws()
    {
        var handler = new ThrowingHttpMessageHandler();
        var client = new HttpClient(handler);
        var checker = new UpdateChecker(client);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => checker.CheckForUpdateAsync("v1.2.1"));
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _content;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(
            string content,
            HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _content = content;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Network unreachable");
        }
    }
}