using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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

        private long _byteCount;
        private long _frameCount;
        private long _errorCount;
        private int _frameIndex;
        private bool _rawDirty;

        public MainWindow()
        {
            InitializeComponent();
            DgFrames.ItemsSource = _frames;

            _engine.RawBytesReceived += OnRawBytes;
            _engine.FrameDecoded += OnFrameDecoded;
            _engine.LinkStateChanged += OnLinkState;
            _engine.LogMessage += OnLogMessage;

            _rawFlushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _rawFlushTimer.Tick += (_, _) => FlushRawBuffer();
            _rawFlushTimer.Start();

            Closed += (_, _) => { _engine.Dispose(); };
        }

        private DiagnosticConfig BuildConfig()
        {
            byte stationAddr = 0xAA;
            if (byte.TryParse(TxtStationAddr.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte sa))
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
            Dispatcher.Invoke(() =>
            {
                TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
                TxtLog.ScrollToEnd();
            });
        }

        private void OnLogMessage(object? sender, string e) => Log(e);

        private void OnLinkState(object? sender, LinkStateEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatus.Text = e.Message;
                TxtStatus.Foreground = e.IsConnected
                    ? System.Windows.Media.Brushes.ForestGreen
                    : System.Windows.Media.Brushes.Gray;
            });
            Log(e.Message);
        }

        private void OnRawBytes(object? sender, RawBytesEventArgs e)
        {
            lock (_rawLock)
            {
                _byteCount += e.Bytes.Length;
                foreach (var b in e.Bytes)
                {
                    _rawBuilder.Append(b.ToString("X2"));
                    _rawBuilder.Append(' ');
                }
                if (_rawBuilder.Length > 200_000)
                {
                    _rawBuilder.Remove(0, _rawBuilder.Length - 100_000);
                }
                _rawDirty = true;
            }

            Dispatcher.Invoke(() =>
            {
                TxtByteCount.Text = _byteCount.ToString();
            });
        }

        private void FlushRawBuffer()
        {
            if (!_rawDirty) return;
            lock (_rawLock)
            {
                if (!_rawDirty) return;
                TxtRawStream.Text = _rawBuilder.ToString();
                TxtRawHeader.Text = $"已接收 {_byteCount} 字节";
                _rawDirty = false;
            }
        }

        private void OnFrameDecoded(object? sender, FrameDecodedEventArgs e)
        {
            Interlocked.Increment(ref _frameCount);

            Dispatcher.Invoke(() =>
            {
                TxtFrameCount.Text = _frameCount.ToString();
                TxtFrameHeader.Text = $"已解码 {_frameCount} 帧 (帧长 {e.FrameBytes.Length}B)";

                foreach (var obj in e.Decoded)
                {
                    if (obj is not SensorData sd) continue;

                    var raw = new StringBuilder();
                    for (int i = 0; i < sd.Data.Length; i++)
                    {
                        raw.Append(sd.Data[i].ToString("F2"));
                        if (i < sd.Data.Length - 1) raw.Append(", ");
                    }

                    var row = new FrameRow
                    {
                        Index = Interlocked.Increment(ref _frameIndex),
                        Time = e.Timestamp.ToString("HH:mm:ss.fff"),
                        Address = $"0x{sd.SenserAddress:X2}",
                        Fx = sd.Fx.ToString("F3"),
                        Fy = sd.Fy.ToString("F3"),
                        Fz = sd.Fz.ToString("F3"),
                        RawFloats = raw.ToString()
                    };

                    _frames.Add(row);
                    if (_frames.Count > 500)
                    {
                        _frames.RemoveAt(0);
                    }
                }
            });
        }

        private async void BtnPing_Click(object sender, RoutedEventArgs e)
        {
            BtnPing.IsEnabled = false;
            var result = await _engine.PingAsync(TxtRemoteIP.Text.Trim());
            Log(result);
            BtnPing.IsEnabled = true;
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            BtnConnect.IsEnabled = false;
            _engine.Configure(BuildConfig());
            bool ok = await _engine.ConnectAsync();
            if (ok)
            {
                _engine.StartReceiveLoop();
            }
            BtnConnect.IsEnabled = true;
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            _engine.Disconnect();
        }

        private void BtnStartReceive_Click(object sender, RoutedEventArgs e)
        {
            _engine.StartReceiveLoop();
            Log("已启动原始数据接收（不发送采集指令）");
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _engine.Configure(BuildConfig());
            await _engine.SendStartCommandAsync();
            Log("已发送开始采集指令");
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            await _engine.SendStopCommandAsync();
            Log("已发送停止采集指令");
        }

        private async void BtnSendRaw_Click(object sender, RoutedEventArgs e)
        {
            var hex = TxtRawCmd.Text.Trim();
            if (string.IsNullOrEmpty(hex))
            {
                Log("请输入要发送的 HEX 指令");
                return;
            }

            try
            {
                var bytes = ParseHex(hex);
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
            {
                return;
            }

            if (CmbPresetCmd.SelectedItem is ComboBoxItem item && item.Tag is string hex)
            {
                TxtRawCmd.Text = hex;
            }
        }

        private static byte[] ParseHex(string hex)
        {
            var parts = hex.Split(new[] { ' ', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var bytes = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                bytes[i] = Convert.ToByte(parts[i], 16);
            }
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
            Dispatcher.Invoke(() => TxtByteCount.Text = "0");
        }

        private void BtnClearFrames_Click(object sender, RoutedEventArgs e)
        {
            _frames.Clear();
            _frameIndex = 0;
            TxtFrameCount.Text = "0";
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TxtLog.Clear();
        }
    }
}