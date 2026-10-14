using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using KW.CommunicationLib.Configuration;
using KW.Sensor.DataModel;

using ForcePlateDevTool.Models;
using ForcePlateDevTool.Services;

namespace ForcePlateDevTool
{
    public class FrameRow
    {
        public int FrameSeq { get; set; }
        public int Index { get; set; }
        public string Pedal { get; set; } = "";
        public string Time { get; set; } = "";
        public string Address { get; set; } = "";
        public string Fx { get; set; } = "";
        public string Fy { get; set; } = "";
        public string Fz { get; set; } = "";
        public string RawFloats { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        private readonly DiagnosticEngine _engine = new();
        private readonly ObservableCollection<FrameRow> _frames = new();
        private readonly StringBuilder _rawBuilder = new();
        private readonly object _rawLock = new();
        private readonly DispatcherTimer _rawFlushTimer;

        private readonly List<FrameRow> _pendingFrames = new();
        private readonly object _framesLock = new();

        private readonly BoundedQueue<ForceSample> _pendingForceSamples =
            new(capacity: 5000);

        private long _byteCount;
        private long _frameCount;
        private int _frameIndex;
        private bool _rawDirty;
        private long _lastReceiveTicks;
        private long _lastDecodedTicks;
        private bool _isClosing;

        public MainWindow()
        {
            InitializeComponent();
            DgFrames.ItemsSource = _frames;

            _engine.RawBytesReceived += OnRawBytes;
            _engine.FrameDecoded += OnFrameDecoded;
            _engine.LinkStateChanged += OnLinkState;
            _engine.LogMessage += OnLogMessage;

            _rawFlushTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _rawFlushTimer.Tick += (_, _) =>
            {
                FlushRawBuffer();
                FlushPendingFrames();
                FlushPendingForceSamples();
                UpdateStatisticsDisplay();
            };
            _rawFlushTimer.Start();

            Closing += async (_, e) =>
            {
                if (_isClosing)
                    return;

                e.Cancel = true;
                _isClosing = true;

                _rawFlushTimer.Stop();

                // Hide immediately so the window vanishes from the screen while
                // cleanup runs in the background. Without this, the window stays
                // visible for the full duration of disposal + visual-tree teardown.
                Hide();

                try
                {
                    // Bound the cleanup so a hung receive loop can never block
                    // window close indefinitely. On timeout the process exit and
                    // OS socket reclamation still guarantee resource release.
                    Task disposeTask = _engine.DisposeAsync().AsTask();
                    Task timeout = Task.Delay(2000);
                    await Task.WhenAny(disposeTask, timeout);
                }
                catch
                {
                    // Best-effort cleanup; the window will close regardless.
                }

                // Empty the heavy controls before tearing down the visual tree.
                // TxtLog is unbounded during a session, so leaving it full makes
                // WPF's TextContainer disposal noticeably slow.
                _frames.Clear();
                TxtRawStream.Clear();
                TxtLog.Clear();

                Close();
            };

            SetStage(TxtNetworkStageStatus, "待检测", Brushes.Gray);
            SetStage(TxtUdpStageStatus, "未就绪", Brushes.Gray);
            SetStage(TxtCommandStageStatus, "待验证", Brushes.Gray);
            SetStage(TxtReceiveStageStatus, "等待数据", Brushes.Gray);
            SetStage(TxtParseStageStatus, "等待帧", Brushes.Gray);
            SetStage(TxtDecodeStageStatus, "等待解码", Brushes.Gray);

            SetConnectionSettingsEnabled(true);
        }

        private static void SetStage(TextBlock target, string message, Brush color)
        {
            target.Text = message;
            target.Foreground = color;
            target.ToolTip = message;
        }

        private void SetConnectionSettingsEnabled(bool enabled)
        {
            TxtRemoteIP.IsEnabled = enabled;
            TxtRemotePort.IsEnabled = enabled;
            TxtLocalIP.IsEnabled = enabled;
            TxtLocalPort.IsEnabled = enabled;
            CmbMode.IsEnabled = enabled;

            BtnConnect.IsEnabled = enabled;
            BtnDisconnect.IsEnabled = !enabled;
        }

        private DiagnosticConfig BuildConfig()
        {
            CollectionMode mode = CmbMode.SelectedIndex switch
            {
                0 => CollectionMode.Initiative,
                1 => CollectionMode.Passive,
                2 => CollectionMode.OnlyTrigger,
                _ => CollectionMode.Passive
            };

            return new DiagnosticConfig
            {
                RemoteIP = TxtRemoteIP.Text.Trim(),
                RemotePort = int.Parse(TxtRemotePort.Text.Trim()),
                LocalIP = TxtLocalIP.Text.Trim(),
                LocalPort = int.Parse(TxtLocalPort.Text.Trim()),
                StationAddress = 0xAA,
                CollectionMode = mode
            };
        }

        private void Log(string msg)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(() => Log(msg));
                return;
            }

            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
            TxtLog.ScrollToEnd();
        }

        private void OnLogMessage(object? sender, string e)
        {
            Log(e);
        }

        private void OnLinkState(object? sender, LinkStateEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                TxtStatus.Text = e.Message;
                TxtStatus.Foreground = e.IsConnected ? Brushes.ForestGreen : Brushes.Gray;

                SetStage(
                    TxtUdpStageStatus,
                    e.IsConnected ? "UDP socket 已就绪" : "未就绪",
                    e.IsConnected ? Brushes.ForestGreen : Brushes.Gray);

                SetConnectionSettingsEnabled(!e.IsConnected);

                if (!e.IsConnected)
                {
                    Interlocked.Exchange(ref _lastReceiveTicks, 0);

                    SetStage(TxtCommandStageStatus, "待验证", Brushes.Gray);
                    SetStage(TxtReceiveStageStatus, "等待连接", Brushes.Gray);
                    SetStage(TxtParseStageStatus, "等待帧", Brushes.Gray);
                    SetStage(TxtDecodeStageStatus, "等待解码", Brushes.Gray);
                }
            });

            Log(e.Message);
        }

        private void OnRawBytes(object? sender, RawBytesEventArgs e)
        {
            lock (_rawLock)
            {
                _byteCount += e.Bytes.Length;

                foreach (byte b in e.Bytes)
                {
                    _rawBuilder.Append(b.ToString("X2"));
                    _rawBuilder.Append(' ');
                }

                // Keep the diagnostic view bounded to avoid unbounded UI memory growth.
                if (_rawBuilder.Length > 200_000)
                {
                    _rawBuilder.Remove(0, _rawBuilder.Length - 100_000);
                }

                _rawDirty = true;
            }

            Interlocked.Exchange(ref _lastReceiveTicks, e.Timestamp.Ticks);
        }

        private void FlushRawBuffer()
        {
            long totalBytes;
            long lastTicks = Interlocked.Read(ref _lastReceiveTicks);

            lock (_rawLock)
            {
                totalBytes = _byteCount;

                if (_rawDirty)
                {
                    string snapshot = _rawBuilder.ToString();
                    _rawDirty = false;

                    TxtRawStream.Text = snapshot;
                    TxtRawHeader.Text = $"累计接收 {totalBytes:N0} 字节（显示最近缓存内容）";
                    TxtRawStream.ScrollToEnd();
                }
            }

            TxtByteCount.Text = totalBytes.ToString();

            if (lastTicks > 0 && _engine.IsConnected)
            {
                SetStage(
                    TxtReceiveStageStatus,
                    $"正在接收 · {new DateTime(lastTicks):HH:mm:ss}",
                    Brushes.ForestGreen);
            }
        }

        private void FlushPendingFrames()
        {
            List<FrameRow> batch;
            lock (_framesLock)
            {
                if (_pendingFrames.Count == 0)
                    return;
                batch = _pendingFrames.ToList();
                _pendingFrames.Clear();
            }

            foreach (var row in batch)
            {
                _frames.Add(row);
            }

            while (_frames.Count > 500)
                _frames.RemoveAt(0);

            long totalFrames = Interlocked.Read(ref _frameCount);
            TxtFrameCount.Text = totalFrames.ToString();
            TxtFrameHeader.Text = $"已解码 {totalFrames:N0} 帧";

            SetStage(TxtParseStageStatus, "有效帧已识别", Brushes.ForestGreen);
            SetStage(TxtDecodeStageStatus, "已解码", Brushes.ForestGreen);
        }

        private void FlushPendingForceSamples()
        {
            int processed = 0;
            const int MaxSamplesPerTick = 1000;

            while (processed < MaxSamplesPerTick &&
                   _pendingForceSamples.TryDequeue(out var sample))
            {
                processed++;

                Pedal pedal = ForceSampleRouter.Route(sample.SensorAddress);

                if (pedal == Pedal.Left)
                {
                    LeftForceChart.AddSample(sample);

                    TxtLeftFx.Text = sample.Fx.ToString("F3", CultureInfo.InvariantCulture);
                    TxtLeftFy.Text = sample.Fy.ToString("F3", CultureInfo.InvariantCulture);
                    TxtLeftFz.Text = sample.Fz.ToString("F3", CultureInfo.InvariantCulture);
                }
                else if (pedal == Pedal.Right)
                {
                    RightForceChart.AddSample(sample);

                    TxtRightFx.Text = sample.Fx.ToString("F3", CultureInfo.InvariantCulture);
                    TxtRightFy.Text = sample.Fy.ToString("F3", CultureInfo.InvariantCulture);
                    TxtRightFz.Text = sample.Fz.ToString("F3", CultureInfo.InvariantCulture);
                }

                Interlocked.Exchange(
                    ref _lastDecodedTicks,
                    sample.Timestamp.Ticks);
            }
        }

        private void UpdateStatisticsDisplay()
        {
            var stats = _engine.Statistics;

            TxtByteCount.Text = stats.ReceivedBytes.ToString();
            TxtFrameCount.Text = stats.DecodedFrames.ToString();

            long anomalies = stats.InvalidDataEvents +
                             stats.DecodeFailures +
                             stats.DecodeExceptions;
            TxtErrorCount.Text = anomalies.ToString();

            if (stats.InvalidDataEvents > 0 || stats.DecodeFailures > 0)
            {
                SetStage(TxtParseStageStatus, "发现异常数据", Brushes.OrangeRed);
            }

            if (stats.DecodeExceptions > 0)
            {
                SetStage(TxtDecodeStageStatus, "解码异常", Brushes.OrangeRed);
            }

            bool hasDecodedData = stats.LastDecodedTime.HasValue;
            bool isStale = !hasDecodedData ||
                DateTime.Now - stats.LastDecodedTime!.Value >
                TimeSpan.FromSeconds(2);

            if (_engine.IsConnected)
            {
                if (!hasDecodedData)
                {
                    SetStage(TxtReceiveStageStatus, "已连接·等待数据", Brushes.DarkOrange);
                }
                else if (isStale)
                {
                    SetStage(
                        TxtReceiveStageStatus,
                        $"数据已停滞 · {stats.LastDecodedTime:HH:mm:ss}",
                        Brushes.DarkOrange);
                }
                else
                {
                    SetStage(
                        TxtReceiveStageStatus,
                        $"正在接收 · {stats.LastDecodedTime:HH:mm:ss}",
                        Brushes.ForestGreen);
                }
            }

            long dropped = _pendingForceSamples.DroppedCount;
            if (dropped > 0)
            {
                TxtFrameHeader.Text =
                    $"已解码 {stats.DecodedFrames:N0} 帧 · 丢弃显示样本 {dropped:N0}";
            }
        }

        private static string MapPedal(byte address)
        {
            return ForceSampleRouter.Route(address) switch
            {
                Pedal.Left => "左踏板",
                Pedal.Right => "右踏板",
                _ => $"未知(0x{address:X2})"
            };
        }

        private void OnFrameDecoded(object? sender, FrameDecodedEventArgs e)
        {
            try
            {
                if (e.Decoded == null)
                    return;

                long frameSeq = Interlocked.Increment(ref _frameCount);

                var rows = new List<FrameRow>();

                foreach (var obj in e.Decoded)
                {
                    if (obj is not SensorData sd)
                        continue;

                    _pendingForceSamples.Enqueue(new ForceSample(
                        e.Timestamp,
                        sd.SenserAddress,
                        sd.Fx,
                        sd.Fy,
                        sd.Fz));

                    float[] data = sd.Data ?? Array.Empty<float>();
                    int usedCount = Math.Min(data.Length, 9);

                    var raw = new StringBuilder();
                    for (int i = 0; i < usedCount; i++)
                    {
                        raw.Append(data[i].ToString("F2", CultureInfo.InvariantCulture));
                        if (i < usedCount - 1)
                            raw.Append(", ");
                    }

                    rows.Add(new FrameRow
                    {
                        FrameSeq = (int)frameSeq,
                        Index = Interlocked.Increment(ref _frameIndex),
                        Pedal = MapPedal(sd.SenserAddress),
                        Time = e.Timestamp.ToString("HH:mm:ss.fff"),
                        Address = $"0x{sd.SenserAddress:X2}",
                        Fx = sd.Fx.ToString("F3", CultureInfo.InvariantCulture),
                        Fy = sd.Fy.ToString("F3", CultureInfo.InvariantCulture),
                        Fz = sd.Fz.ToString("F3", CultureInfo.InvariantCulture),
                        RawFloats = raw.ToString()
                    });
                }

                if (rows.Count > 0)
                {
                    lock (_framesLock)
                    {
                        _pendingFrames.AddRange(rows);
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() => Log($"OnFrameDecoded 异常: {ex}"));
            }
        }

        private async void BtnPing_Click(object sender, RoutedEventArgs e)
        {
            BtnPing.IsEnabled = false;
            SetStage(TxtNetworkStageStatus, "检测中", Brushes.DarkOrange);

            try
            {
                string ip = TxtRemoteIP.Text.Trim();
                string result = await _engine.PingAsync(ip);
                Log(result);

                bool ok = result.Contains("成功", StringComparison.Ordinal);
                SetStage(
                    TxtNetworkStageStatus,
                    ok ? "可达" : "不可达",
                    ok ? Brushes.ForestGreen : Brushes.OrangeRed);
            }
            finally
            {
                BtnPing.IsEnabled = true;
            }
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            SetConnectionSettingsEnabled(false);

            try
            {
                if (!EndpointValidator.TryParse(
                        TxtRemoteIP.Text.Trim(),
                        TxtRemotePort.Text.Trim(),
                        TxtLocalIP.Text.Trim(),
                        TxtLocalPort.Text.Trim(),
                        out string error))
                {
                    Log($"配置错误: {error}");
                    MessageBox.Show(error, "连接配置错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _engine.Configure(BuildConfig());

                // Ping is diagnostic evidence only. A failed ICMP request must not
                // prevent UDP testing.
                string ip = TxtRemoteIP.Text.Trim();
                string pingResult = await _engine.PingAsync(ip, 1500);
                bool reachable = pingResult.Contains("成功", StringComparison.Ordinal);

                Log(pingResult);

                SetStage(
                    TxtNetworkStageStatus,
                    reachable ? "Ping 可达" : "Ping 未响应",
                    reachable ? Brushes.ForestGreen : Brushes.DarkOrange);

                // Continue regardless of the Ping result.
                SetStage(TxtUdpStageStatus, "初始化 UDP", Brushes.DarkOrange);
                bool ok = await _engine.ConnectAsync();

                if (!ok)
                {
                    SetStage(TxtUdpStageStatus, "初始化失败", Brushes.OrangeRed);
                    return;
                }

                SetStage(TxtCommandStageStatus, "待验证", Brushes.Gray);
                SetStage(TxtReceiveStageStatus, "等待数据", Brushes.DarkOrange);

                bool receiveStarted = _engine.StartReceiveLoop();
                if (!receiveStarted)
                {
                    SetStage(TxtReceiveStageStatus, "接收循环未启动", Brushes.OrangeRed);
                    return;
                }

                var result = await _engine.SendStartCommandAsync();

                SetStage(
                    TxtCommandStageStatus,
                    result.Success
                        ? "本地发送成功，等待设备数据"
                        : "发送失败",
                    result.Success
                        ? Brushes.DarkOrange
                        : Brushes.OrangeRed);
            }
            catch (Exception ex)
            {
                Log($"连接操作失败: {ex.Message}");
                SetStage(TxtUdpStageStatus, "异常", Brushes.OrangeRed);
            }
            finally
            {
                if (!_engine.IsConnected)
                {
                    SetConnectionSettingsEnabled(true);
                }
            }
        }

        // 断开连接按钮点击事件处理
        private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            // 先禁用按钮，防止用户在断开过程中重复点击
            BtnDisconnect.IsEnabled = false;

            try
            {
                // 异步断开与设备的连接，等待操作完成
                await _engine.DisconnectAsync();
            }
            finally
            {
                // 无论断开成功还是失败，最终都恢复按钮可用状态
                BtnDisconnect.IsEnabled = true;
            }
        }

        private void BtnStartReceive_Click(object sender, RoutedEventArgs e)
        {
            bool started = _engine.StartReceiveLoop();
            if (started)
            {
                SetStage(TxtReceiveStageStatus, "等待数据", Brushes.DarkOrange);
                Log("已请求启动原始数据接收（不发送采集指令）");
            }
            else
            {
                SetStage(TxtReceiveStageStatus, "接收循环未启动", Brushes.OrangeRed);
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!_engine.IsConnected)
            {
                Log("尚未连接设备，请先连接。");
                SetStage(
                    TxtCommandStageStatus,
                    "未连接",
                    Brushes.OrangeRed);
                return;
            }

            BtnStart.IsEnabled = false;

            try
            {
                // Do not rebuild configuration here.
                // The active socket and decoder must use the same configuration
                // captured when ConnectAsync() initialized the connection.
                var result = await _engine.SendStartCommandAsync();

                SetStage(
                    TxtCommandStageStatus,
                    result.Success ? "开始指令已提交" : "发送失败",
                    result.Success ? Brushes.DarkOrange : Brushes.OrangeRed);

                Log(result.Message);
            }
            catch (Exception ex)
            {
                Log($"启动采集失败: {ex.Message}");
                SetStage(
                    TxtCommandStageStatus,
                    "启动失败",
                    Brushes.OrangeRed);
            }
            finally
            {
                BtnStart.IsEnabled = true;
            }
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (!_engine.IsConnected)
            {
                Log("尚未连接设备，请先连接。");
                SetStage(
                    TxtCommandStageStatus,
                    "未连接",
                    Brushes.OrangeRed);
                return;
            }

            var result = await _engine.SendStopCommandAsync();

            SetStage(
                TxtCommandStageStatus,
                result.Success ? "停止指令已发送" : "发送失败",
                result.Success ? Brushes.DarkOrange : Brushes.OrangeRed);

            Log(result.Message);
        }

        private async void BtnSendRaw_Click(object sender, RoutedEventArgs e)
        {
            string hex = TxtRawCmd.Text.Trim();
            if (string.IsNullOrEmpty(hex))
            {
                Log("请输入要发送的 HEX 指令");
                return;
            }

            if (!_engine.IsConnected)
            {
                Log("尚未连接设备，请先连接。");
                return;
            }

            try
            {
                byte[] bytes = ParseHex(hex);
                var result = await _engine.SendCommandAsync(bytes);
                Log(result.Message);
            }
            catch (Exception ex)
            {
                Log($"HEX 解析失败: {ex.Message}");
            }
        }

        private void CmbPresetCmd_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TxtRawCmd == null)
                return;

            if (CmbPresetCmd.SelectedItem is ComboBoxItem item &&
                item.Tag is string hex)
            {
                TxtRawCmd.Text = hex;
            }
        }

        private static byte[] ParseHex(string hex)
        {
            string[] parts = hex.Split(
                new[] { ' ', ',', '-' },
                StringSplitOptions.RemoveEmptyEntries);

            var bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                bytes[i] = Convert.ToByte(parts[i], 16);

            return bytes;
        }

        private void BtnClearRaw_Click(object sender, RoutedEventArgs e)
        {
            lock (_rawLock)
            {
                _rawBuilder.Clear();
                _rawDirty = true;
                _byteCount = 0;
            }

            Dispatcher.InvokeAsync(() =>
            {
                TxtByteCount.Text = "0";
                TxtRawHeader.Text = "原始字节显示已清空";
            });
        }

        private void BtnClearFrames_Click(object sender, RoutedEventArgs e)
        {
            lock (_framesLock)
            {
                _pendingFrames.Clear();
            }

            _frames.Clear();
            Interlocked.Exchange(ref _frameIndex, 0);
            Interlocked.Exchange(ref _frameCount, 0);
            TxtFrameCount.Text = "0";
            TxtFrameHeader.Text = "帧列表已清空";
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TxtLog.Clear();
        }
    }
}