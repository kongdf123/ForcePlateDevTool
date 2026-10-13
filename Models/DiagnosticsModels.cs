using KW.CommunicationLib.Configuration;

namespace ForcePlateDevTool.Models;

public class DiagnosticConfig
{
    public string RemoteIP { get; set; } = "192.168.1.25";
    public int RemotePort { get; set; } = 5152;
    public string LocalIP { get; set; } = "192.168.1.100";
    public int LocalPort { get; set; } = 8886;

    public byte StationAddress { get; set; } = 0xAA;
    public CollectionMode CollectionMode { get; set; } = CollectionMode.Passive;

    public float TableWidth { get; set; } = 0;
    public float TableHeight { get; set; } = 0;
    public float SensorWidth { get; set; } = 0;
    public float SensorHeight { get; set; } = 0;
    public float Az0 { get; set; } = 0;
}

public class RawBytesEventArgs : EventArgs
{
    public byte[] Bytes { get; }
    public DateTime Timestamp { get; }

    public RawBytesEventArgs(byte[] bytes, DateTime timestamp)
    {
        Bytes = bytes;
        Timestamp = timestamp;
    }
}

public class FrameDecodedEventArgs : EventArgs
{
    public byte[] FrameBytes { get; }
    public DateTime Timestamp { get; }
    public IReadOnlyList<object> Decoded { get; }

    public FrameDecodedEventArgs(byte[] frameBytes, DateTime timestamp, IReadOnlyList<object> decoded)
    {
        FrameBytes = frameBytes;
        Timestamp = timestamp;
        Decoded = decoded;
    }
}

public class LinkStateEventArgs : EventArgs
{
    public bool IsConnected { get; }
    public string Message { get; }

    public LinkStateEventArgs(bool isConnected, string message)
    {
        IsConnected = isConnected;
        Message = message;
    }
}