using KW.CommunicationLib.Configuration;
using KW.CommunicationLib.Decoder;
using KW.Commons.Tools.Globals;

namespace ForcePlateDevTool.Tests;

public class DecoderFrameTests
{
    private const float G = 9.8f;

    private static Decoder_76B_StartingBlock CreateDecoder()
    {
        var config = new DecoderConfig
        {
            StationAddress = 0xAA,
            FakeAddress = 0xAA,
            CollectionMode = CollectionMode.Passive
        };
        return new Decoder_76B_StartingBlock(config);
    }

    private static byte[] BuildValidFrame(byte address, float value)
    {
        var frame = new byte[76];
        frame[0] = 0x48;
        frame[1] = address;

        byte[] floatBytes = BitConverter.GetBytes(value);
        for (int i = 0; i < 18; i++)
        {
            Array.Copy(floatBytes, 0, frame, 2 + i * 4, 4);
        }

        frame[74] = 0x0D;
        frame[75] = 0x0A;
        return frame;
    }

    [Fact]
    public void Decode_ValidFrame_ReturnsTwoSensorDataWithGScaledValues()
    {
        var decoder = CreateDecoder();
        byte[] frame = BuildValidFrame(0xAA, 1.0f);

        bool ok = decoder.Decode(frame, out var data);

        Assert.True(ok);
        Assert.NotNull(data);
        Assert.Equal(2, data.Length);

        // Left pedal (0xAA) and right pedal (0xAB) each carry 9 floats.
        Assert.Equal(0xAA, data[0].SenserAddress);
        Assert.Equal(0xAB, data[1].SenserAddress);

        foreach (var sensor in data)
        {
            // SensorData.Data is fixed-size 14; the 76B decoder fills the first 9 slots.
            Assert.Equal(14, sensor.Data.Length);
            for (int i = 0; i < 9; i++)
            {
                Assert.Equal(G, sensor.Data[i], precision: 1);
            }
            for (int i = 9; i < 14; i++)
            {
                Assert.Equal(0f, sensor.Data[i]);
            }
        }
    }

    [Fact]
    public void Decode_WrongLength_ReturnsFalse()
    {
        var decoder = CreateDecoder();
        byte[] tooShort = new byte[75];

        bool ok = decoder.Decode(tooShort, out var data);

        Assert.False(ok);
        Assert.Empty(data);
    }

    [Fact]
    public void Decode_AllZeroFrame_ReturnsTrueWithZeroValues()
    {
        var decoder = CreateDecoder();
        byte[] frame = BuildValidFrame(0xAA, 0.0f);

        bool ok = decoder.Decode(frame, out var data);

        Assert.True(ok);
        Assert.Equal(2, data.Length);
        Assert.All(data, sd => Assert.All(sd.Data, v => Assert.Equal(0f, v)));
    }

    [Fact]
    public void GetMapping_LeadingGarbageThenValidFrame_DiscardsGarbageAndYieldsFrame()
    {
        var decoder = CreateDecoder();
        byte[] frame = BuildValidFrame(0xAA, 2.5f);

        var stream = new List<byte>();
        stream.AddRange(new byte[] { 0x00, 0x01, 0x02, 0x48, 0x48 }); // garbage + a stray 0x48 not followed by 0xAA
        stream.AddRange(frame);

        byte[]? captured = null;
        decoder.GetMapping(ref stream, 0xAA, f =>
        {
            captured = f;
            return true;
        });

        Assert.NotNull(captured);
        Assert.Equal(76, captured!.Length);
        Assert.Equal(frame, captured);
    }

    [Fact]
    public void GetMapping_PartialFrameAtEnd_RetainsRemainder()
    {
        var decoder = CreateDecoder();
        byte[] frame = BuildValidFrame(0xAA, 1.0f);

        // Provide the full frame plus a partial next frame header.
        var stream = new List<byte>();
        stream.AddRange(frame);
        stream.AddRange(new byte[] { 0x48, 0xAA, 0x01 });

        int callbackCount = 0;
        decoder.GetMapping(ref stream, 0xAA, _ =>
        {
            callbackCount++;
            return true;
        });

        Assert.Equal(1, callbackCount);
        // The partial frame (0x48 0xAA 0x01) should remain in the buffer.
        Assert.Equal(3, stream.Count);
    }

    [Fact]
    public void GetMapping_BadTail_RejectsFrameAndAdvances()
    {
        var decoder = CreateDecoder();
        byte[] frame = BuildValidFrame(0xAA, 1.0f);
        frame[75] = 0x00; // corrupt the tail 0x0A -> 0x00

        var stream = new List<byte>(frame);

        int callbackCount = 0;
        decoder.GetMapping(ref stream, 0xAA, _ =>
        {
            callbackCount++;
            return true;
        });

        // The frame should not be delivered because the tail check fails.
        Assert.Equal(0, callbackCount);
    }
}