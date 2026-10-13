namespace KW.CommunicationLib.Decoder;

[Decoder("三维起跑器 KWFS02", "", IndirectConnect:true)]
public class Decoder_76B_StartingBlock : DecoderBase
{
    protected byte[][] _DataArray = Array.Empty<byte[]>();

    public Decoder_76B_StartingBlock(DecoderConfig config) : base(config)
    {
        StartBit = new byte[] { 0x48 };
        _DataLength = 76;
        _DataArray = new byte[][] { new byte[4], new byte[4], new byte[4], new byte[4], new byte[4], new byte[4],
        new byte[4], new byte[4], new byte[4], new byte[4], new byte[4], new byte[4], new byte[4],new byte[4],new byte[4],new byte[4],new byte[4],new byte[4]};
    }

    protected override void ParseByteArray(byte[] array)
    {
        for (int i = 0; i < 72; i++)
        {
            _DataArray[i / 4][i % 4] = array[i + 2];
        }
    }

    protected override SensorData[] GetDigitalData()
    {
        SensorData_76B_StartingBlock tempDataA = new SensorData_76B_StartingBlock(_Config.TableWidth, _Config.TableHeight, _Config.SensorWidth, _Config.SensorHeight, this._RealAZ0);
        SensorData_76B_StartingBlock tempDataB = new SensorData_76B_StartingBlock(_Config.TableWidth, _Config.TableHeight, _Config.SensorWidth, _Config.SensorHeight, this._RealAZ0);

        tempDataA.SenserAddress = 0xAA;
        tempDataA.Data[0] = BitConverter.ToSingle(_DataArray[0], 0) * GlobalValue.G;
        tempDataA.Data[1] = BitConverter.ToSingle(_DataArray[1], 0) * GlobalValue.G;
        tempDataA.Data[2] = BitConverter.ToSingle(_DataArray[2], 0) * GlobalValue.G;
        tempDataA.Data[3] = BitConverter.ToSingle(_DataArray[3], 0) * GlobalValue.G;
        tempDataA.Data[4] = BitConverter.ToSingle(_DataArray[4], 0) * GlobalValue.G;
        tempDataA.Data[5] = BitConverter.ToSingle(_DataArray[5], 0) * GlobalValue.G;
        tempDataA.Data[6] = BitConverter.ToSingle(_DataArray[6], 0) * GlobalValue.G;
        tempDataA.Data[7] = BitConverter.ToSingle(_DataArray[7], 0) * GlobalValue.G;
        tempDataA.Data[8] = BitConverter.ToSingle(_DataArray[8], 0) * GlobalValue.G;

        tempDataB.SenserAddress = 0xAB;
        tempDataB.Data[0] = BitConverter.ToSingle(_DataArray[9], 0) * GlobalValue.G;
        tempDataB.Data[1] = BitConverter.ToSingle(_DataArray[10], 0) * GlobalValue.G;
        tempDataB.Data[2] = BitConverter.ToSingle(_DataArray[11], 0) * GlobalValue.G;
        tempDataB.Data[3] = BitConverter.ToSingle(_DataArray[12], 0) * GlobalValue.G;
        tempDataB.Data[4] = BitConverter.ToSingle(_DataArray[13], 0) * GlobalValue.G;
        tempDataB.Data[5] = BitConverter.ToSingle(_DataArray[14], 0) * GlobalValue.G;
        tempDataB.Data[6] = BitConverter.ToSingle(_DataArray[15], 0) * GlobalValue.G;
        tempDataB.Data[7] = BitConverter.ToSingle(_DataArray[16], 0) * GlobalValue.G;
        tempDataB.Data[8] = BitConverter.ToSingle(_DataArray[17], 0) * GlobalValue.G;

        return new SensorData_76B_StartingBlock[] { tempDataA, tempDataB };
    }
}
