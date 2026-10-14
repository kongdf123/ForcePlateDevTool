using System.Net;
using System.Net.Sockets;

using KW.CommunicationLib.Configuration;

using ForcePlateDevTool.Models;
using ForcePlateDevTool.Services;

namespace ForcePlateDevTool.Tests;

public class DiagnosticEngineLifecycleTests : IAsyncLifetime
{
    private DiagnosticEngine _engine = null!;

    public Task InitializeAsync()
    {
        _engine = new DiagnosticEngine();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _engine.DisconnectAsync();
        _engine.Dispose();
    }

    private static DiagnosticConfig LocalhostConfig(
        int localPort = 18886,
        int remotePort = 15152)
    {
        return new DiagnosticConfig
        {
            RemoteIP = "127.0.0.1",
            RemotePort = remotePort,
            LocalIP = "127.0.0.1",
            LocalPort = localPort,
            StationAddress = 0xAA,
            CollectionMode = CollectionMode.Passive
        };
    }

    [Fact]
    public void StartReceiveLoop_WithoutConnection_ReturnsFalse()
    {
        bool started = _engine.StartReceiveLoop();

        Assert.False(started);
    }

    [Fact]
    public async Task SendCommandAsync_WithoutConnection_ReturnsUnsuccessful()
    {
        var result = await _engine.SendCommandAsync(new byte[] { 0x48, 0xAA, 0x0D, 0x0A });

        Assert.False(result.Success);
        Assert.Equal(new byte[] { 0x48, 0xAA, 0x0D, 0x0A }, result.Command);
    }

    [Fact]
    public async Task SendStartCommandAsync_WithoutDecoder_ReturnsUnsuccessful()
    {
        var result = await _engine.SendStartCommandAsync();

        Assert.False(result.Success);
        Assert.Contains("解码器未就绪", result.Message);
    }

    [Fact]
    public async Task Connect_OpensUdpSocket()
    {
        _engine.Configure(LocalhostConfig());

        bool ok = await _engine.ConnectAsync();

        Assert.True(ok);
        Assert.True(_engine.IsConnected);
    }

    [Fact]
    public async Task StartReceiveLoop_Twice_SecondReturnsTrue()
    {
        _engine.Configure(LocalhostConfig());
        await _engine.ConnectAsync();

        bool first = _engine.StartReceiveLoop();
        bool second = _engine.StartReceiveLoop();

        Assert.True(first);
        Assert.True(second);
    }

    [Fact]
    public async Task StopReceiveLoopAsync_TerminatesTask_AndCanRestart()
    {
        _engine.Configure(LocalhostConfig(localPort: 18887));
        await _engine.ConnectAsync();

        Assert.True(_engine.StartReceiveLoop());

        await _engine.StopReceiveLoopAsync();

        // After a clean stop, starting again should succeed.
        bool restarted = _engine.StartReceiveLoop();
        Assert.True(restarted);
    }

    [Fact]
    public async Task DisconnectAsync_CompletesAndSetsDisconnected()
    {
        _engine.Configure(LocalhostConfig(localPort: 18888));
        await _engine.ConnectAsync();
        _engine.StartReceiveLoop();

        await _engine.DisconnectAsync();

        Assert.False(_engine.IsConnected);
    }

    [Fact]
    public async Task RepeatedConnectDisconnect_NoOrphanTasks()
    {
        for (int i = 0; i < 3; i++)
        {
            _engine.Configure(LocalhostConfig(localPort: 18890 + i));
            bool connected = await _engine.ConnectAsync();
            Assert.True(connected);

            bool started = _engine.StartReceiveLoop();
            Assert.True(started);

            await _engine.DisconnectAsync();
            Assert.False(_engine.IsConnected);
        }
    }

    [Fact]
    public async Task SendCommandAsync_WithOpenSocket_Succeeds()
    {
        _engine.Configure(LocalhostConfig(localPort: 18895));
        await _engine.ConnectAsync();

        var result = await _engine.SendCommandAsync(new byte[] { 0x48, 0xAA, 0x0D, 0x0A });

        Assert.True(result.Success);
        Assert.Equal(new byte[] { 0x48, 0xAA, 0x0D, 0x0A }, result.Command);
    }

    [Fact]
    public async Task SendStartCommandAsync_WithDecoder_ProducesExpectedBytes()
    {
        _engine.Configure(LocalhostConfig(localPort: 18896));
        await _engine.ConnectAsync();

        var result = await _engine.SendStartCommandAsync();

        Assert.True(result.Success);
        // Passive mode start command: 0x48 AA 0D 0A
        Assert.Equal(new byte[] { 0x48, 0xAA, 0x0D, 0x0A }, result.Command);
    }

    [Fact]
    public async Task Configure_WhileConnected_ThrowsInvalidOperationException()
    {
        _engine.Configure(LocalhostConfig(localPort: 18900));
        await _engine.ConnectAsync();

        Assert.Throws<InvalidOperationException>(
            () => _engine.Configure(LocalhostConfig(localPort: 18901)));
    }

    [Fact]
    public async Task Configure_AfterDisconnect_Succeeds()
    {
        _engine.Configure(LocalhostConfig(localPort: 18902));
        await _engine.ConnectAsync();
        await _engine.DisconnectAsync();

        // Configuration must be allowed again once disconnected.
        _engine.Configure(LocalhostConfig(localPort: 18903));

        Assert.Equal(18903, _engine.Config.LocalPort);
    }

    [Fact]
    public async Task StopThenRestartThenDisconnect_NoOrphanReceiveTask()
    {
        _engine.Configure(LocalhostConfig(localPort: 18904));
        await _engine.ConnectAsync();

        Assert.True(_engine.StartReceiveLoop());
        await _engine.StopReceiveLoopAsync();
        Assert.True(_engine.StartReceiveLoop());

        await _engine.DisconnectAsync();

        Assert.False(_engine.IsConnected);
    }

    [Fact]
    public async Task FailedConnect_LeavesDisconnected_AndAllowsRetry()
    {
        // An invalid local IP address causes the socket bind to fail.
        var badConfig = new DiagnosticConfig
        {
            RemoteIP = "127.0.0.1",
            RemotePort = 15152,
            LocalIP = "256.256.256.256",
            LocalPort = 18905,
            StationAddress = 0xAA,
            CollectionMode = CollectionMode.Passive
        };

        _engine.Configure(badConfig);
        bool ok = await _engine.ConnectAsync();

        Assert.False(ok);
        Assert.False(_engine.IsConnected);

        // A second attempt with a valid configuration must be permitted.
        _engine.Configure(LocalhostConfig(localPort: 18906));
        bool retryOk = await _engine.ConnectAsync();

        Assert.True(retryOk);
        Assert.True(_engine.IsConnected);
    }
}