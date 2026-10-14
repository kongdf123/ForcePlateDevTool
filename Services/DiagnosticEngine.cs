using System.Net.NetworkInformation;
using System.Net.Sockets;

using KW.CommunicationLib.Configuration;
using KW.CommunicationLib.Decoder;
using KW.CommunicationLib.Linker;
using KW.Sensor.DataModel;

using ForcePlateDevTool.Models;

namespace ForcePlateDevTool.Services;

public class DiagnosticEngine : IAsyncDisposable, IDisposable
{
    private SocketLinker? _linker;
    private Decoder_76B_StartingBlock? _decoder;

    private readonly object _receiveLock = new();
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;

    private readonly object _bufferLock = new();
    private List<byte> _buffer = new();

    private bool _disposed;

    public DiagnosticConfig Config { get; private set; } = new();

    public event EventHandler<RawBytesEventArgs>? RawBytesReceived;
    public event EventHandler<FrameDecodedEventArgs>? FrameDecoded;
    public event EventHandler<LinkStateEventArgs>? LinkStateChanged;
    public event EventHandler<string>? LogMessage;

    public bool IsConnected => _linker?.IsOpen() ?? false;

    public int DataLength => _decoder?.DataLength ?? 0;

    public void Configure(DiagnosticConfig config)
    {
        Config = config;
    }

    public async Task<string> PingAsync(string ip, int timeoutMs = 2000)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, timeoutMs);
            if (reply.Status == IPStatus.Success)
            {
                return $"Ping {ip} 成功: 往返 {reply.RoundtripTime} ms, TTL={reply.Options?.Ttl}";
            }
            return $"Ping {ip} 失败: {reply.Status}";
        }
        catch (Exception ex)
        {
            return $"Ping {ip} 异常: {ex.Message}";
        }
    }

    public async Task<bool> ConnectAsync()
    {
        // Avoid replacing _linker while a previous receive task is still active.
        await DisconnectAsync();

        try
        {
            var socketConfig = new SocketConfig
            {
                RemoteIP = Config.RemoteIP,
                RemotePort = Config.RemotePort,
                LocalIP = Config.LocalIP,
                LocalPort = Config.LocalPort,
                SocketMode = SocketMode.UDP,
                BindLocal = true
            };

            _linker = new SocketLinker(socketConfig);

            bool opened = await Task.Run(() => _linker.Open());
            if (opened)
            {
                BuildDecoder();
                LinkStateChanged?.Invoke(this, new LinkStateEventArgs(true, $"UDP socket 已就绪 {Config.RemoteIP}:{Config.RemotePort}"));
                return true;
            }

            LinkStateChanged?.Invoke(this, new LinkStateEventArgs(false, "UDP socket 初始化失败: Open() 返回 false"));
            return false;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"连接异常: {ex.Message}");
            LinkStateChanged?.Invoke(this, new LinkStateEventArgs(false, $"连接异常: {ex.Message}"));
            return false;
        }
    }

    private void BuildDecoder()
    {
        var decoderConfig = new DecoderConfig
        {
            DecoderClass = typeof(Decoder_76B_StartingBlock).FullName!,
            CollectionMode = Config.CollectionMode,
            StationAddress = Config.StationAddress,
            FakeAddress = Config.StationAddress,
            TableWidth = Config.TableWidth,
            TableHeight = Config.TableHeight,
            SensorWidth = Config.SensorWidth,
            SensorHeight = Config.SensorHeight,
            Az0 = Config.Az0,
            StartCMD = Config.CollectionMode switch
            {
                CollectionMode.Passive => KW_CMD.CMD48,
                CollectionMode.Initiative => KW_CMD.CMD49,
                CollectionMode.OnlyTrigger => KW_CMD.CMD58,
                _ => KW_CMD.CMD48
            },
            StopCMD = KW_CMD.CMD43
        };

        _decoder = new Decoder_76B_StartingBlock(decoderConfig);

        LogMessage?.Invoke(this,
            $"解码器就绪: {typeof(Decoder_76B_StartingBlock).Name}, " +
            $"帧头=0x{_decoder.StartBit[0]:X}, 帧长={_decoder.DataLength}B, " +
            $"帧尾=0x{_decoder.EndBit[0]:X} 0x{_decoder.EndBit[1]:X}, " +
            $"支持地址位={_decoder.IsSupportAddress}");
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _linker?.Close();

            await StopReceiveLoopAsync();

            _linker?.Dispose();
            _linker = null;

            _decoder = null;

            lock (_bufferLock)
            {
                _buffer.Clear();
            }

            LinkStateChanged?.Invoke(this, new LinkStateEventArgs(false, "已断开"));
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"断开异常: {ex.Message}");
        }
    }

    public bool StartReceiveLoop()
    {
        lock (_receiveLock)
        {
            if (_linker == null || !_linker.IsOpen())
            {
                LogMessage?.Invoke(this, "无法启动接收: UDP socket 未就绪");
                return false;
            }

            if (_receiveTask is { IsCompleted: false })
            {
                LogMessage?.Invoke(this, "接收循环已在运行，无需重复启动");
                return false;
            }

            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            var token = _cts.Token;
            _receiveTask = Task.Run(() => ReceiveLoop(token), token);

            return true;
        }
    }

    public async Task StopReceiveLoopAsync()
    {
        Task? task;
        CancellationTokenSource? cts;

        lock (_receiveLock)
        {
            cts = _cts;
            task = _receiveTask;
            cts?.Cancel();
        }

        if (task != null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"等待接收循环结束时发生异常: {ex.Message}");
            }
        }

        lock (_receiveLock)
        {
            if (ReferenceEquals(_cts, cts))
            {
                _cts = null;
                _receiveTask = null;
            }
        }

        cts?.Dispose();
    }

    private void ReceiveLoop(CancellationToken token)
    {
        LogMessage?.Invoke(this, "数据接收循环已启动");

        while (!token.IsCancellationRequested && _linker != null && _linker.IsOpen())
        {
            try
            {
                int n = _linker.Receive(out byte[] data, 100);
                if (n > 0 && data != null && data.Length > 0)
                {
                    var timestamp = DateTime.Now;
                    RawBytesReceived?.Invoke(this, new RawBytesEventArgs(data, timestamp));
                    ProcessBytes(data, timestamp);
                }
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke(this, $"接收异常: {ex.Message}");
                break;
            }
        }

        LogMessage?.Invoke(this, "数据接收循环已停止");
    }

    private void ProcessBytes(byte[] data, DateTime timestamp)
    {
        if (_decoder == null) return;

        lock (_bufferLock)
        {
            _buffer.AddRange(data);

            var localBuffer = _buffer;
            _decoder.GetMapping(ref localBuffer, Config.StationAddress, frame =>
            {
                DecodeFrame(frame, timestamp);
                return true;
            }, invalidBytes =>
            {
                if (invalidBytes.Length > 0)
                {
                    string snippet = BitConverter.ToString(invalidBytes).Replace("-", " ");
                    if (snippet.Length > 60)
                        snippet = snippet.Substring(0, 60);
                    LogMessage?.Invoke(this, $"丢弃 {invalidBytes.Length} 字节无效数据: {snippet}");
                }
            });

            _buffer = localBuffer;

            if (_buffer.Count > DataLength * 8 && DataLength > 0)
            {
                _buffer.RemoveRange(0, _buffer.Count - DataLength);
            }
        }
    }

    private void DecodeFrame(byte[] frame, DateTime timestamp)
    {
        if (_decoder == null) return;

        try
        {
            bool ok = _decoder.Decode(frame, out SensorData[] data);
            if (!ok)
            {
                LogMessage?.Invoke(this, $"解码失败 (长度={frame.Length}): {BitConverter.ToString(frame).Replace("-", " ")}");
                return;
            }

            var list = new List<object>();
            if (data != null)
            {
                foreach (var item in data)
                {
                    list.Add(item);
                }
            }
            FrameDecoded?.Invoke(this, new FrameDecodedEventArgs(frame, timestamp, list));
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"解码异常: {ex}");
        }
    }

    public async Task<CommandSendResult> SendCommandAsync(byte[] command)
    {
        if (_linker == null || !_linker.IsOpen())
        {
            const string message = "无法发送: UDP socket 未就绪";
            LogMessage?.Invoke(this, message);

            return new CommandSendResult(false, message, command);
        }

        try
        {
            await Task.Run(() => _linker.Send(command));

            string message =
                $"本地发送调用成功: " +
                $"{BitConverter.ToString(command).Replace("-", " ")}";

            LogMessage?.Invoke(this, message);

            return new CommandSendResult(true, message, command);
        }
        catch (Exception ex)
        {
            string message = $"发送失败: {ex.Message}";
            LogMessage?.Invoke(this, message);

            return new CommandSendResult(false, message, command);
        }
    }

    public Task<CommandSendResult> SendStartCommandAsync()
    {
        if (_decoder == null)
        {
            const string message = "无法发送开始指令: 解码器未就绪";

            LogMessage?.Invoke(this, message);

            return Task.FromResult(
                new CommandSendResult(false, message, Array.Empty<byte>()));
        }

        KW_CMD cmd = Config.CollectionMode switch
        {
            CollectionMode.Passive => KW_CMD.CMD48,
            CollectionMode.Initiative => KW_CMD.CMD49,
            CollectionMode.OnlyTrigger => KW_CMD.CMD58,
            _ => KW_CMD.CMD48
        };

        byte[] bytes = _decoder.GetCommand((int)cmd, Config.StationAddress);
        return SendCommandAsync(bytes);
    }

    public Task<CommandSendResult> SendStopCommandAsync()
    {
        if (_decoder == null)
        {
            const string message = "无法发送停止指令: 解码器未就绪";

            LogMessage?.Invoke(this, message);

            return Task.FromResult(
                new CommandSendResult(false, message, Array.Empty<byte>()));
        }

        byte[] bytes = _decoder.GetCommand((int)KW_CMD.CMD43, Config.StationAddress);
        return SendCommandAsync(bytes);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Run on a thread-pool thread so the awaits inside DisconnectAsync do not
        // try to resume on the caller's SynchronizationContext (e.g. the WPF UI
        // thread), which would deadlock when this method blocks on GetResult().
        try
        {
            Task.Run(() => DisconnectAsync()).GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort cleanup for the synchronous path.
        }

        _linker?.Dispose();
        _linker = null;
        _cts?.Dispose();
        _cts = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            await DisconnectAsync();
        }
        finally
        {
            _linker?.Dispose();
            _linker = null;
            _cts?.Dispose();
            _cts = null;
        }
    }
}