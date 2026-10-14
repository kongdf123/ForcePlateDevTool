using ForcePlateDevTool.Models;

namespace ForcePlateDevTool.Services;

public static class ForceSampleRouter
{
    public const byte LeftPedalAddress = 0xAA;
    public const byte RightPedalAddress = 0xAB;

    public static Pedal Route(byte sensorAddress)
    {
        return sensorAddress switch
        {
            LeftPedalAddress => Pedal.Left,
            RightPedalAddress => Pedal.Right,
            _ => Pedal.Unknown
        };
    }
}