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
        public int Index { get; set; }
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

        private long _byteCount;
        private long _frameCount;
        private long _errorCount;
        private int _frameIndex;
        private bool _rawDirty;
        private long _lastReceiveTicks;

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
            };
            _rawFlushTimer.Start();

            Closed += (_, _) =>
            {
                _rawFlushTimer.Stop();
                _engine.Dispose();
            };

            SetStage(TxtNetworkStageStatus, "待检测", Brushes.Gray);
            SetStage(TxtTcpStageStatus, "未连接", Brushes.Gray);
            SetStage(TxtCommandStageStatus, "待验证", Brushes.Gray);
            SetStage(TxtReceiveStageStatus, "等待数据", Brushes.Gray);
            SetStage(TxtParseStageStatus, "等待帧", Brushes.Gray);
            SetStage(TxtDecodeStageStatus, "等待解码", Brushes.Gray);
        }

        private static void SetStage(TextBlock target, string message, Brush color)
        {
            target.Text = message;
            target.Foreground = color;
            target.ToolTip = message;
        }

        private DiagnosticConfig BuildConfig()
        {
            byte stationAddr = 0xAA;
            if (byte.TryParse(
                    TxtStationAddr.Text.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out byte sa))
            {
                stationAddr = sa;
            }

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
                RemotePort = int.TryParse(TxtRemotePort.Text.Trim(), out int rp) ? rp : 5152,
                LocalIP = TxtLocalIP.Text.Trim(),
                LocalPort = int.TryParse(TxtLocalPort.Text.Trim(), out int lp) ? lp : 8886,
                StationAddress = stationAddr,
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

            // DiagnosticEngine currently reports malformed data as log messages rather
            // than exposing a dedicated invalid-frame event. Count these as anomaly
            // events, not as an exact count of invalid frames.
            if (e.Contains("无效数据", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("解码失败", StringComparison.OrdinalIgnoreCase) ||
                e.Contains("解码异常", StringComparison.OrdinalIgnoreCase))
            {
                long count = Interlocked.Increment(ref _errorCount);
                Dispatcher.InvokeAsync(() => TxtErrorCount.Text = count.ToString());

                Dispatcher.InvokeAsync(() =>
                    SetStage(TxtParseStageStatus, "发现异常数据", Brushes.OrangeRed));
            }
        }

        private void OnLinkState(object? sender, LinkStateEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                TxtStatus.Text = e.Message;
                TxtStatus.Foreground = e.IsConnected ? Brushes.ForestGreen : Brushes.Gray;

                SetStage(
                    TxtTcpStageStatus,
                    e.IsConnected ? "已连接" : "未连接",
                    e.IsConnected ? Brushes.ForestGreen : Brushes.Gray);

                if (!e.IsConnected)
                {
                    SetStage(TxtReceiveStageStatus, "等待连接", Brushes.Gray);
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

            if (lastTicks > 0)
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

        private void OnFrameDecoded(object? sender, FrameDecodedEventArgs e)
        {
            Interlocked.Increment(ref _frameCount);

            var rows = new List<FrameRow>();

            foreach (var obj in e.Decoded)
            {
                if (obj is not SensorData sd)
                    continue;

                var raw = new StringBuilder();
                for (int i = 0; i < sd.Data.Length; i++)
                {
                    raw.Append(sd.Data[i].ToString("F2", CultureInfo.InvariantCulture));
                    if (i < sd.Data.Length - 1)
                        raw.Append(", ");
                }

                rows.Add(new FrameRow
                {
                    Index = Interlocked.Increment(ref _frameIndex),
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
                    ok ? "可达" : "未确认可达",
                    ok ? Brushes.ForestGreen : Brushes.OrangeRed);
            }
            finally
            {
                BtnPing.IsEnabled = true;
            }
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            BtnConnect.IsEnabled = false;
            SetStage(TxtTcpStageStatus, "连接中", Brushes.DarkOrange);

            try
            {
                _engine.Configure(BuildConfig());
                bool ok = await _engine.ConnectAsync();

                if (ok)
                {
                    SetStage(TxtCommandStageStatus, "待验证", Brushes.Gray);
                    SetStage(TxtReceiveStageStatus, "等待数据", Brushes.DarkOrange);
                    _engine.StartReceiveLoop();
                }
            }
            catch (Exception ex)
            {
                Log($"连接操作失败: {ex.Message}");
                SetStage(TxtTcpStageStatus, "连接异常", Brushes.OrangeRed);
            }
            finally
            {
                BtnConnect.IsEnabled = true;
            }
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            _engine.Disconnect();
        }

        private void BtnStartReceive_Click(object sender, RoutedEventArgs e)
        {
            _engine.StartReceiveLoop();
            SetStage(TxtReceiveStageStatus, "等待数据", Brushes.DarkOrange);
            Log("已请求启动原始数据接收（不发送采集指令）");
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _engine.Configure(BuildConfig());
            SetStage(TxtCommandStageStatus, "已发送开始指令", Brushes.DarkOrange);
            await _engine.SendStartCommandAsync();
            Log("已请求发送开始采集指令");
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            await _engine.SendStopCommandAsync();
            Log("已请求发送停止采集指令");
        }

        private async void BtnSendRaw_Click(object sender, RoutedEventArgs e)
        {
            string hex = TxtRawCmd.Text.Trim();
            if (string.IsNullOrEmpty(hex))
            {
                Log("请输入要发送的 HEX 指令");
                return;
            }

            try
            {
                byte[] bytes = ParseHex(hex);
                await _engine.SendCommandAsync(bytes);
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