using System.ComponentModel;

namespace KW.Sensor.DataModel;

[Description("三维起跑器")]
public class SensorData_76B_StartingBlock : SensorData_56Bit
{
    public SensorData_76B_StartingBlock() : base() { }

    public SensorData_76B_StartingBlock(byte address, params float[] values) : base(address, values) { }

    public SensorData_76B_StartingBlock(float[] values) : base(values) { }

    public SensorData_76B_StartingBlock(float tableWidth, float tableHeight, float sensorWidth, float sensorHeight, float az0)
        : base(tableWidth, tableHeight, sensorWidth, sensorHeight, az0)
    {
        this._Az0 = az0;
    }

    #region override

    protected override float GetFx()
    {
        return (Data[0] + Data[3] + Data[6]) * -1;
    }

    protected override float GetFy()
    {
        return (Data[1] + Data[4] + Data[7]) * -1;
    }

    protected override float GetFz()
    {
        return (Data[2] + Data[5] + Data[8]) * -1;
    }

    public override string ToString()
    {
        return $"\"{SenserAddress}\",\"{Time.TotalSeconds.ToString("0.000")}\",\"{Fx.ToString("F5")}\",\"{Fy.ToString("F5")}\",\"{Fz.ToString("F5")}\"";
    }

    #endregion
}
