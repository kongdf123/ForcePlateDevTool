using System.Net;

namespace ForcePlateDevTool.Services;

public static class EndpointValidator
{
    public static bool TryParse(
        string remoteIp,
        string remotePortText,
        string localIp,
        string localPortText,
        out string error)
    {
        error = string.Empty;

        if (!IPAddress.TryParse(remoteIp, out _))
        {
            error = "设备 IP 地址格式无效。";
            return false;
        }

        if (!IPAddress.TryParse(localIp, out _))
        {
            error = "本地 IP 地址格式无效。";
            return false;
        }

        if (!int.TryParse(remotePortText, out int remotePort) ||
            remotePort is < 1 or > 65535)
        {
            error = "设备端口必须为 1–65535。";
            return false;
        }

        if (!int.TryParse(localPortText, out int localPort) ||
            localPort is < 1 or > 65535)
        {
            error = "本地端口必须为 1–65535。";
            return false;
        }

        return true;
    }
}