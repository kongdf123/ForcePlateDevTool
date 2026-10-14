using ForcePlateDevTool.Models;
using ForcePlateDevTool.Services;

namespace ForcePlateDevTool.Tests;

public class ForceSampleBufferTests
{
    private static ForceSample Sample(
        DateTime timestamp,
        float fx = 1.0f,
        float fy = 2.0f,
        float fz = 3.0f)
    {
        return new ForceSample(timestamp, 0xAA, fx, fy, fz);
    }

    [Fact]
    public void Add_NeverExceedsMaxSamples()
    {
        var buffer = new ForceSampleBuffer();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 1500; i++)
        {
            buffer.Add(Sample(baseTime.AddMilliseconds(i)));
        }

        Assert.True(buffer.SampleCount <= 1200);
    }

    [Fact]
    public void Add_WithNonFiniteValues_DoesNotThrow()
    {
        var buffer = new ForceSampleBuffer();
        var time = DateTime.UtcNow;

        var exception = Record.Exception(() =>
        {
            buffer.Add(Sample(time, fx: float.NaN, fy: float.PositiveInfinity, fz: float.NegativeInfinity));
            buffer.Add(Sample(time.AddMilliseconds(10)));
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Add_NonFiniteValues_AreStillTrackedForCount()
    {
        var buffer = new ForceSampleBuffer();
        var time = DateTime.UtcNow;

        buffer.Add(Sample(time, fx: float.NaN));
        buffer.Add(Sample(time.AddMilliseconds(10)));

        Assert.Equal(2, buffer.SampleCount);
    }

    [Fact]
    public void Clear_RemovesAllSamples()
    {
        var buffer = new ForceSampleBuffer();
        var time = DateTime.UtcNow;

        buffer.Add(Sample(time));
        buffer.Add(Sample(time.AddMilliseconds(10)));
        Assert.Equal(2, buffer.SampleCount);

        buffer.Clear();

        Assert.Equal(0, buffer.SampleCount);
    }

    [Fact]
    public void Add_RemovesSamplesOutsideVisibleWindow()
    {
        var buffer = new ForceSampleBuffer();
        var oldest = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        buffer.Add(Sample(oldest));
        buffer.Add(Sample(oldest.AddSeconds(11)));

        Assert.Equal(1, buffer.SampleCount);
    }

    [Fact]
    public void Add_EmptyBuffer_DoesNotFabricateZeroReadings()
    {
        var buffer = new ForceSampleBuffer();

        Assert.Equal(0, buffer.SampleCount);
        Assert.Empty(buffer.Samples);
    }
}