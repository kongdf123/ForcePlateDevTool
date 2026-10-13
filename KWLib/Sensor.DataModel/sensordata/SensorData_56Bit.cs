using System.ComponentModel;

namespace KW.Sensor.DataModel;

[Description("56字节六维")]
public class SensorData_56Bit : SensorData
{       
    public SensorData_56Bit() : base() { }

    public SensorData_56Bit(byte address, params float[] values) : base(address, values) { }

    public SensorData_56Bit(float[] values) : base(values) { }

    public SensorData_56Bit(float tableWidth, float tableHeight, float sensorWidth, float sensorHeight, float az0)
        : base(tableWidth, tableHeight, sensorWidth, sensorHeight)
    {
        this._Az0 = az0;
    }

    #region override

    protected override float GetFx()
    {
        return (Data[0] + Data[3] + Data[6] + Data[9]) * -1;
    }

    protected override float GetFy()
    {
        return (Data[1] + Data[4] + Data[7] + Data[10]) * -1;
    }

    protected override float GetFz()
    {
        return (Data[2] + Data[5] + Data[8] + Data[11]) * -1;
    }

    protected float TMx => GetTMx();

    protected float TMy => GetTMy();

    protected float TMz => GetTMz();

    protected virtual float GetTMx()
    {
        return (Data[8] + Data[11] - Data[2] - Data[5]) * _SensorSizeY / 2 * -1;
    }

    protected virtual float GetTMy()
    {
        return (Data[5] + Data[8] - Data[2] - Data[11]) * _SensorSizeX / 2 * -1;
    }

    protected virtual float GetTMz()
    {
        return ((Data[0] + Data[3] - Data[6] - Data[9]) * _SensorSizeY / 2
            + (Data[1] + Data[10] - Data[4] - Data[7]) * _SensorSizeX / 2) * -1;
    }


    protected override float GetMx()
    {
        return (TMx + Fy * _Az0)/1000;
    }

    protected override float GetMy()
    {
        return (TMy - Fx * _Az0)/1000;
    }

    protected override float GetMz()
    {
        return (TMz - Fy * GetCOPX() + Fx * GetCOPY())/1000;
    }

    protected override float GetCOPX()
    {
        return Decode4FToCOP_V2().X;
    }

    protected override float GetCOPY()
    {
        return Decode4FToCOP_V2().Y;
    }

    public override string ToString()
    {
        return $"\"{SenserAddress}\",\"{Time.TotalSeconds.ToString("0.000")}\",\"{Fx.ToString("F5")}\",\"{Fy.ToString("F5")}\",\"{Fz.ToString("F5")}\",\"{Mx.ToString("F5")}\",\"{My.ToString("F5")}\",\"{Mz.ToString("F5")}\",\"{COPX.ToString("F5")}\",\"{COPY.ToString("F5")}\"";
    }

    #endregion
}
