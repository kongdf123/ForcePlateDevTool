using System.Net.NetworkInformation;
using System.Net.Sockets;

using KW.CommunicationLib.Configuration;
using KW.CommunicationLib.Decoder;
using KW.CommunicationLib.Linker;
using KW.Sensor.DataModel;

using ForcePlateDevTool.Models;

namespace ForcePlateDevTool.Services;

public class DiagnosticEngine : IDisposable
{
    private SocketLinker? _linker;
    private Decoder_76B_StartingBlock? _decoder;
    private CancellationTokenSource? _cts;
    private readonly object _bufferLock = new();
    private List<byte> _buffer = new();

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
                LinkStateChanged?.Invoke(this, new LinkStateEventArgs(true, $"已连接 {Config.RemoteIP}:{Config.RemotePort}"));
                return true;
            }

            LinkStateChanged?.Invoke(this, new LinkStateEventArgs(false, "连接失败: Open() 返回 false"));
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

    public void Disconnect()
    {
        try
        {
            StopReceiveLoop();
            _linker?.Close();
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"断开异常: {ex.Message}");
        }
        finally
        {
            LinkStateChanged?.Invoke(this, new LinkStateEventArgs(false, "已断开连接"));
        }
    }

    public void StartReceiveLoop()
    {
        if (_linker == null || !_linker.IsOpen())
        {
            LogMessage?.Invoke(this, "无法启动接收: 连接未建立");
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(() => ReceiveLoop(token), token);
    }

    public void StopReceiveLoop()
    {
        try
        {
            _cts?.Cancel();
        }
        catch { }
        _cts = null;
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
                    LogMessage?.Invoke(this, $"丢弃 {invalidBytes.Length} 字节无效数据: {BitConverter.ToString(invalidBytes).Replace("-", " ").Substring(0, Math.Min(60, invalidBytes.Length * 3))}");
                }
            });

            _buffer = localBuffer;

            if (_buffer.Count > DataLength * 8)
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

    public async Task SendCommandAsync(byte[] command)
    {
        if (_linker == null || !_linker.IsOpen())
        {
            LogMessage?.Invoke(this, "无法发送: 连接未建立");
            return;
        }

        try
        {
            await Task.Run(() => _linker.Send(command));
            LogMessage?.Invoke(this, $"已发送: {BitConverter.ToString(command).Replace("-", " ")}");
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"发送异常: {ex.Message}");
        }
    }

    public Task SendStartCommandAsync()
    {
        if (_decoder == null)
        {
            LogMessage?.Invoke(this, "无法发送开始指令: 解码器未就绪");
            return Task.CompletedTask;
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

    public Task SendStopCommandAsync()
    {
        if (_decoder == null)
        {
            LogMessage?.Invoke(this, "无法发送停止指令: 解码器未就绪");
            return Task.CompletedTask;
        }

        byte[] bytes = _decoder.GetCommand((int)KW_CMD.CMD43, Config.StationAddress);
        return SendCommandAsync(bytes);
    }

    public void Dispose()
    {
        Disconnect();
        _linker?.Dispose();
    }
}