using ForcePlateDevTool.Services;

namespace ForcePlateDevTool.Tests;

public class EndpointValidatorTests
{
    [Theory]
    [InlineData("not-an-ip", "5152", "192.168.1.100", "8886", "设备 IP 地址格式无效")]
    [InlineData("192.168.1.25", "5152", "not-an-ip", "8886", "本地 IP 地址格式无效")]
    [InlineData("192.168.1.25", "0", "192.168.1.100", "8886", "设备端口必须为 1–65535")]
    [InlineData("192.168.1.25", "70000", "192.168.1.100", "8886", "设备端口必须为 1–65535")]
    [InlineData("192.168.1.25", "abc", "192.168.1.100", "8886", "设备端口必须为 1–65535")]
    [InlineData("192.168.1.25", "5152", "192.168.1.100", "0", "本地端口必须为 1–65535")]
    [InlineData("192.168.1.25", "5152", "192.168.1.100", "99999", "本地端口必须为 1–65535")]
    [InlineData("192.168.1.25", "5152", "192.168.1.100", "xyz", "本地端口必须为 1–65535")]
    public void TryParse_InvalidInput_ReturnsFalseWithMessage(
        string remoteIp, string remotePort, string localIp, string localPort, string expectedError)
    {
        bool ok = EndpointValidator.TryParse(remoteIp, remotePort, localIp, localPort, out string error);

        Assert.False(ok);
        Assert.Contains(expectedError, error);
    }

    [Theory]
    [InlineData("192.168.1.25", "5152", "192.168.1.100", "8886")]
    [InlineData("127.0.0.1", "1", "127.0.0.1", "65535")]
    [InlineData("0.0.0.0", "1024", "0.0.0.0", "1024")]
    public void TryParse_ValidInput_ReturnsTrue(
        string remoteIp, string remotePort, string localIp, string localPort)
    {
        bool ok = EndpointValidator.TryParse(remoteIp, remotePort, localIp, localPort, out string error);

        Assert.True(ok);
        Assert.Equal(string.Empty, error);
    }
}