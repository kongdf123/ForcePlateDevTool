using ForcePlateDevTool.Services;
using ForcePlateDevTool.Models;

namespace ForcePlateDevTool.Tests;

public class ForceSampleRouterTests
{
    [Fact]
    public void Route_LeftPedalAddress_ReturnsLeft()
    {
        Pedal result = ForceSampleRouter.Route(0xAA);

        Assert.Equal(Pedal.Left, result);
    }

    [Fact]
    public void Route_RightPedalAddress_ReturnsRight()
    {
        Pedal result = ForceSampleRouter.Route(0xAB);

        Assert.Equal(Pedal.Right, result);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x01)]
    [InlineData(0xAC)]
    [InlineData(0xFF)]
    public void Route_UnknownAddress_ReturnsUnknown(byte address)
    {
        Pedal result = ForceSampleRouter.Route(address);

        Assert.Equal(Pedal.Unknown, result);
    }
}