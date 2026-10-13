using System.Net;
using System.Net.Sockets;
using System.ComponentModel;
using System.Windows.Controls;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;


using KW.Commons.Tools.Utilities;
using KW.CommunicationLib.Configuration;
using KW.Commons.Tools.Queues;

namespace KW.CommunicationLib.Linker;

/// <summary>预览升正式采 / Close 时常见的 Socket 关停判定（SocketLinker 与 Client 共用）。</summary>
internal static class SocketLinkerShutdown
{
    internal static bool IsBenign(SocketException ex) =>
        ex.SocketErrorCode == SocketError.OperationAborted
        || ex.SocketErrorCode == SocketError.ConnectionReset
        || ex.SocketErrorCode == SocketError.Shutdown
        || ex.SocketErrorCode == SocketError.Interrupted
        || ex.SocketErrorCode == SocketError.ConnectionAborted
        || ex.NativeErrorCode == 10004
        || (!string.IsNullOrWhiteSpace(ex.Message)
            && (ex.Message.Contains("WSACancelBlockingCall", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("封锁操作", StringComparison.Ordinal)));
}

[Linker("以太网通信", "")]
public class SocketLinker : LinkerBase
{
    #region 变量

    private SocketMode _SocketMode = SocketMode.Client;
    private string _RemoteIP = "192.168.1.25";
    private int _RemotePort = 5152;
    private string _LocalIP = "192.168.1.100";
    private int _LocalPort = 8886;
    private bool _BindLocal = false;
    protected Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private ConcurrentDictionary<string, Client> _ClientList = new ConcurrentDictionary<string, Client>();
    private bool _isRunning = false;

    //UDP模式
    private byte[] _Buffer = new byte[65535];
    private CountableQueue<byte> _TempBuffer = new CountableQueue<byte>(65535);

    //缓冲区池
    private static ConcurrentQueue<byte[]> _BufferPool = new ConcurrentQueue<byte[]>();
    private const int BUFFER_SIZE = 65535;
    private const int MAX_POOL_SIZE = 10;

    #endregion

    #region 构造函数

    public SocketLinker(SocketConfig config) : base(config)
    {
        _SocketMode = config.SocketMode;
        _RemoteIP = config.RemoteIP;
        _RemotePort = config.RemotePort;
        _BindLocal = config.BindLocal;
        _LocalIP = config.LocalIP;
        _LocalPort = config.LocalPort;
    }

    #endregion

    #region 属性

    /// <summary>
    /// Socket通讯模式
    /// </summary>
    public SocketMode SocketMode => _SocketMode;

    /// <summary>
    /// 服务端IP 当使用Server模式时用于记录传感器IP
    /// </summary>
    public string RemoteIP => _RemoteIP;

    /// <summary>
    /// 服务端口号 当使用Server模式时用于记录传感器IP
    /// </summary>
    public int RemotePort => _RemotePort;

    public bool BindLocal => _BindLocal;

    /// <summary>
    /// 本地IP
    /// </summary>
    public string LocalIP => _LocalIP;

    /// <summary>
    /// 本地端口号
    /// </summary>
    public int LocalPort => _LocalPort;

    #endregion

    #region 缓冲区池管理

    private byte[] GetBuffer()
    {
        if (_BufferPool.TryDequeue(out byte[] buffer))
        {
            return buffer;
        }
        return new byte[BUFFER_SIZE];
    }

    private void ReleaseBuffer(byte[] buffer)
    {
        if (buffer != null && _BufferPool.Count < MAX_POOL_SIZE)
        {
            _BufferPool.Enqueue(buffer);
        }
    }

    #endregion

    #region 对外方法

    public override bool Open()
    {
        _TempBuffer.Clear();
        _isRunning = true;

        try
        {
            switch (_SocketMode)
            {
                case SocketMode.Client:
                    _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    _Socket.ReceiveTimeout = 3000;
                    _Socket.SendTimeout = 3000;

                    var connectDone = new ManualResetEvent(false);
                    Exception? connectError = null;

                    _Socket.BeginConnect(new IPEndPoint(IPAddress.Parse(_RemoteIP), _RemotePort), new AsyncCallback((result) =>
                    {
                        var remoteEndPoint = new IPEndPoint(IPAddress.Parse(_RemoteIP), _RemotePort);
                        using var connectTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
                        try
                        {
                            Socket socket = (Socket)result.AsyncState!;
                            socket.EndConnect(result);
                        }
                        catch (Exception ex)
                        {
                            connectError = ex;
                        }
                        finally
                        {
                            connectDone.Set();
                        }
                    }), _Socket);

                    // TCP 连接超时 3 秒（WiFi 环境下需要更充裕的时间）
                    if (!connectDone.WaitOne(3000, false))
                    {
                        try { _Socket.Close(); } catch { }
                        throw new TimeoutException($"连接 {_RemoteIP}:{_RemotePort} 超时（3秒）");
                    }

                    if (connectError != null || !_Socket.Connected)
                    {
                        try { _Socket.Close(); } catch { }
                        throw connectError ?? new SocketException((int)SocketError.ConnectionRefused);
                    }
                    break;
                case SocketMode.Server:
                    _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    _Socket.Bind(new IPEndPoint(IPAddress.Parse(_LocalIP), _LocalPort));
                    _Socket.Listen(24);//设置客户端最大连接数 24

                    // 使用线程池处理客户端连接
                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        while (_isRunning)//循环等待新客户端的连接
                        {
                            try
                            {
                                Socket client = _Socket.Accept();//客户端接入
                                string clientKey = (client.RemoteEndPoint as IPEndPoint).Address.ToString();
                                Log.LogInfo(clientKey + "已连接");

                                Client clientObj = new Client(client);
                                clientObj.Disconnected += (sender, e) =>
                                {
                                    // 客户端断开，从列表中移除
                                    if (_ClientList.TryRemove(clientKey, out Client removedClient))
                                    {
                                        Log.LogInfo(clientKey + "已断开连接");
                                        removedClient.Close();
                                    }
                                };

                                _ClientList.TryAdd(clientKey, clientObj);
                            }
                            catch (ObjectDisposedException)
                            {
                                // Socket已释放，属于正常关闭流程，退出循环
                                break;
                            }
                            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted ||
                                                             ex.SocketErrorCode == SocketError.Interrupted ||
                                                             !_isRunning)
                            {
                                // Socket操作被中止或服务已停止，属于正常关闭流程，退出循环
                                break;
                            }
                            catch (Exception ex)
                            {
                                if (_isRunning)
                                {
                                    Log.LogError(ex);
                                }
                                // 如果Socket已关闭，退出循环
                                if (!_isRunning)
                                    break;
                            }
                        }
                    });
                    break;
                case SocketMode.UDP:
                    _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                    if (_BindLocal)
                    {
                        _Socket.Bind(new IPEndPoint(IPAddress.Parse(_LocalIP), _LocalPort));
                    }

                    _Socket.Connect(new IPEndPoint(IPAddress.Parse(_RemoteIP), _RemotePort));
                    _Socket.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, ReceiveCallBack, _Socket);
                    break;
            }

            return _Socket.Connected;
        }
        catch (Exception ex)
        {
            _isRunning = false;
            // 清理失败的 socket
            try { _Socket?.Close(); } catch { }
            // 不在这里记录 Error 日志，由上层 Communicator 的重试逻辑记录
            Log.LogInfo($"SocketLinker.Open 失败: {ex.Message}");
        }

        return false;
    }

    public override bool IsOpen()
    {
        try
        {
            if (!_isRunning || _Socket == null)
                return false;

            // Server 模式：_Socket 是监听套接字，检查是否已绑定即可
            if (_SocketMode == SocketMode.Server)
            {
                return _Socket.IsBound;
            }

            // Client/UDP 模式：检查 Socket 是否已连接
            // Connected 只反映上次 I/O 状态，用 Poll+Available 检测半开连接
            if (_Socket.Connected)
            {
                // 对 TCP 客户端使用 Poll 检测对端已关闭但本地未感知的连接
                if (_Socket.ProtocolType == ProtocolType.Tcp)
                {
                    if (_Socket.Poll(0, SelectMode.SelectRead) && _Socket.Available == 0)
                    {
                        return false;
                    }
                }
                return true;
            }

            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }       

    public override bool Close()
    {
        try
        {
            _isRunning = false; // 设置标志为false，通知线程池任务退出

            switch (_SocketMode)
            {
                case SocketMode.Client:
                case SocketMode.UDP:
                    if (_Socket.Connected)
                    {
                        _Socket.Close();
                        _Socket.Dispose();
                    }
                    break;
                case SocketMode.Server:
                    if (_Socket.Connected)
                    {
                        _Socket.Close();
                        _Socket.Dispose();
                    }

                    foreach (var kvp in _ClientList)
                    {
                        kvp.Value.Close();
                    }
                    _ClientList.Clear();
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
        }
        finally
        {
            _Socket.Dispose();
        }

        return false;
    }

    public override bool Send(byte[] array)
    {
        try
        {
            if (array.Length == 0)
            {
                return true;
            }

            switch (_SocketMode)
            {
                case SocketMode.Client:
                case SocketMode.UDP:
                    if (_Socket.Connected)
                    {
                        _Socket.Send(array);
                    }
                    break;
                case SocketMode.Server:
                    foreach (Client item in _ClientList.Values)
                    {
                        item.Send(array);
                    }
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
        }

        return false;
    }

    public override int Receive(out byte[] array, int timeOut = 100, Func<List<byte[]>, byte[]> callback = null)
    {
        int size = 0;
        array = new byte[0];
        byte[] buffer = GetBuffer();
        DateTime deadLine = DateTime.Now.AddMilliseconds(timeOut);

        try
        {
            switch (_SocketMode)
            {
                case SocketMode.Client:
                    while (DateTime.Now < deadLine)
                    {
                        if (_Socket.Connected && _Socket.Available > 0)
                        {
                            size = _Socket.Receive(buffer);
                            array = buffer.Take(size).ToArray();
                            return size;
                        }
                    }
                    break;
                case SocketMode.Server:
                    List<byte> result = new List<byte>();
                    foreach (Client item in _ClientList.Values)
                    {
                        while (item.Receive(out array) > 0)
                        {
                            result.AddRange(array);
                        }
                    }
                    array = result.ToArray();
                    return array.Length;
                case SocketMode.UDP:
                    if (_Socket.Connected)
                    {
                        while (DateTime.Now < deadLine && array.Length == 0)
                        {
                            array = _TempBuffer.ReadAll();
                        }

                        return array.Length;
                    }
                    break;
            }
        }
        catch (ObjectDisposedException ex)
        {
            Log.LogInfo("Socket已释放: " + ex.Message);
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
        }
        finally
        {
            ReleaseBuffer(buffer);
        }

        return 0;
    }

    public bool SetUDPOption(int linkTypeint,int port, ref byte[] cmds, int Timeout = 1000)
    {
        byte[] array = new byte[0];
        Socket udpSocket = null;

        try
        {
            udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpSocket.Bind(new IPEndPoint(IPAddress.Parse(_LocalIP), 5153));
            udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            udpSocket.SendTo(cmds, new IPEndPoint(IPAddress.Broadcast, linkTypeint == 0 ? 5152 : port));

            DateTime deadLine = DateTime.Now.AddMilliseconds(Timeout);
            byte[] buffer = GetBuffer();

            try
            {
                while(deadLine > DateTime.Now)
                {
                    if(udpSocket.Available != 0)
                    {
                        int size = udpSocket.Receive(buffer);
                        byte[] temp = new byte[size];
                        Array.Copy(buffer, 0, temp, 0, size);
                        array = array.Concat(temp).ToArray();
                    }
                }
            }
            finally
            {
                ReleaseBuffer(buffer);
            }

            if(array.SequenceEqual(cmds))
            {
                udpSocket.SendTo(new byte[] { 0xAA, 0x55, 0x0D, 0x0A }, new IPEndPoint(IPAddress.Broadcast, linkTypeint == 0 ? 5152 : port));
                return true;
            }
            else
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
        }
        finally
        {
            cmds = array;
            udpSocket?.Dispose();
        }

        return false;
    }

    public bool SetMAC(byte[] mac, int Timeout = 1000)
    {
        byte[] array = new byte[0];
        Socket udpSocket = null;

        try
        {
            byte[] cmds = new byte[] { 0x4D, 0xAA };
            cmds = cmds.Concat(mac).ToArray();
            cmds = cmds.Concat(new byte[] { 0x0D,0x0A }).ToArray();

            udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpSocket.Bind(new IPEndPoint(IPAddress.Parse(_LocalIP), 5153));
            udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            udpSocket.SendTo(cmds, new IPEndPoint(IPAddress.Parse(_RemoteIP), _RemotePort));

            DateTime deadLine = DateTime.Now.AddMilliseconds(Timeout);
            byte[] buffer = GetBuffer();

            try
            {
                while (deadLine > DateTime.Now)
                {
                    if (udpSocket.Available != 0)
                    {
                        int size = udpSocket.Receive(buffer);
                        byte[] temp = new byte[size];
                        Array.Copy(buffer, 0, temp, 0, size);
                        array = array.Concat(temp).ToArray();
                    }
                }
            }
            finally
            {
                ReleaseBuffer(buffer);
            }

             return array.SequenceEqual(cmds);               
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
        }
        finally
        {
            udpSocket?.Dispose();
        }

        return false;
    }

    public static List<SocketMode> GetAllSocketMode()
    {
        return new List<SocketMode> {
            SocketMode.Client,
            SocketMode.Server,
            SocketMode.UDP
        };
    }

    #endregion

    private void ReceiveCallBack(IAsyncResult result)
    {
        try
        {
            if (!_isRunning)
                return;

            int size = _Socket.EndReceive(result);

            if (size > 0)
            {
                _TempBuffer.Write(_Buffer, 0, size);
            }
            else
            {
                // size == 0 表示对端已优雅关闭连接（TCP FIN），停止接收循环
                return;
            }

            if (_isRunning)
            {
                try
                {
                    _Socket.BeginReceive(_Buffer, 0, _Buffer.Length, SocketFlags.None, ReceiveCallBack, _Socket);
                }
                catch (ObjectDisposedException)
                {
                    // Socket 已释放，属于正常关闭流程
                }
                catch (SocketException ex) when (SocketLinkerShutdown.IsBenign(ex))
                {
                    // 关链时 WSACancelBlockingCall / Interrupted，停止接收循环
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Socket已释放，属于正常关闭流程，不记录错误
        }
        catch (SocketException ex) when (SocketLinkerShutdown.IsBenign(ex))
        {
            // Socket连接被重置或中止：对端断开、网线拔出、关链取消阻塞读等
        }
        catch (Exception ex)
        {
            if (_isRunning)
            {
                Log.LogError(ex);
            }
        }
    }

    public override void Dispose()
    {
        _isRunning = false; // 设置标志为false，通知线程池任务退出

        switch (_SocketMode)
        {
            case SocketMode.Client:
            case SocketMode.UDP:
                _Socket.Dispose();
                break;
            case SocketMode.Server:

                _Socket.Dispose();

                foreach (var kvp in _ClientList)
                {
                    kvp.Value.Close();
                }
                _ClientList.Clear();
                break;
        }
    }
}

public class Client
{
    private Thread _Thread;
    private byte[] _Data = new byte[4096];
    private ConcurrentQueue<byte[]> _MessageQueue = new ConcurrentQueue<byte[]>();
    private Socket _Socket;
    private bool _isRunning = false;

    public event EventHandler Disconnected;

    public Client(Socket socket)
    {
        _Socket = socket;
        _isRunning = true;
        _Thread = new Thread(ReceiveMessage);//开启线程执行异步接收消息
        _Thread.IsBackground = true;
        _Thread.Start();
    }

    public bool Send(byte[] array)
    {
        try
        {
            if (_Socket.Connected)
            {
                _Socket.Send(array);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
            OnDisconnected();
        }

        return false;
    }

    public int Receive(out byte[] array, int timeOut = 1000)
    {
        if (_MessageQueue.TryDequeue(out array))
        {
            return array.Length;
        }
        else
        {
            return 0;
        }
    }

    public bool Close()
    {
        _isRunning = false;
        
        try
        {
            if (_Socket.Connected)
            {
                _Socket.Close();
                _Socket.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
        }

        return true;
    }

    public bool Open()
    {
        return _isRunning && _Thread.IsAlive;
    }

    private void ReceiveMessage()//接收消息
    {
        try
        {
            byte[] buffer = new byte[4096];
            _Socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), buffer);
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
            OnDisconnected();
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            if (!_isRunning)
                return;

            byte[] buffer = (byte[])ar.AsyncState;
            int size = _Socket.EndReceive(ar);

            if (size > 0)
            {
                byte[] data = buffer.Take(size).ToArray();
                _MessageQueue.Enqueue(data);
            }
            else
            {
                // 连接优雅关闭
                OnDisconnected();
                return;
            }

            // 继续异步接收
            if (_isRunning)
            {
                try
                {
                    _Socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), buffer);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (SocketException ex) when (SocketLinkerShutdown.IsBenign(ex))
                {
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Socket已释放，属于正常关闭流程
            if (_isRunning)
                OnDisconnected();
        }
        catch (SocketException ex) when (SocketLinkerShutdown.IsBenign(ex))
        {
            // Socket连接断开或操作被中止，属于正常流程
            if (_isRunning)
                OnDisconnected();
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
            if (_isRunning)
                OnDisconnected();
        }
    }

    private void OnDisconnected()
    {
        _isRunning = false;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }
}

[Linker("以太网通信", "")]
public class SocketConfig : LinkerConfig, ILinkerConfig
{
    public SocketMode SocketMode = SocketMode.Client;
    public string RemoteIP = "192.168.1.25";
    public int RemotePort = 5152;
    public string LocalIP = "192.168.1.100";
    public int LocalPort = 8886;
    public bool BindLocal = false;

    public ILinkerConfig Clone()
    {
        SocketConfig config = new SocketConfig
        {
            SocketMode = this.SocketMode,
            RemoteIP = this.RemoteIP,
            RemotePort = this.RemotePort,
            LocalIP = this.LocalIP,
            LocalPort = this.LocalPort,
            BindLocal = this.BindLocal,
            Address = this.Address,
        };
        return config;
    }

    public Type GetLinkerClass()
    {
        return typeof(SocketLinker);
    }

    public UserControl GetSetupView()
    {
        return new SocketView(this);
    }       

    public override string ToString()
    {
        return $"地址位:{GetAddress().ToString("X2")};传感器IP:{RemoteIP};传感器端口:{RemotePort};绑定本地端口:{BindLocal};本地IP:{LocalIP};本地端口:{LocalPort};";
    }

    public bool Check(out string msg)
    {
        msg = "";

        if (!Regex.IsMatch(RemoteIP, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"))
        {
            msg = "测力台IP输入格式不正确!";
            return false;
        }

        if (BindLocal && !Regex.IsMatch(LocalIP, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"))
        {
            msg = "请选择要绑定的本地IP!";
            return false;
        }

        return true;
    }

    public void Copy(ILinkerConfig config)
    {
        var socket = (SocketConfig)config;
        this.SocketMode = socket.SocketMode;
        this.RemoteIP = socket.RemoteIP;
        this.RemotePort = socket.RemotePort;
        this.LocalIP = socket.LocalIP;
        this.LocalPort = socket.LocalPort;
        this.BindLocal = socket.BindLocal;
    }
}

/// <summary>
/// Socket 通讯模式
/// </summary>
public enum SocketMode
{
    [Description("TCP Client")]
    Client,
    //[Description("TCP Server")]
    Server,
    [Description("UDP")]
    UDP
}