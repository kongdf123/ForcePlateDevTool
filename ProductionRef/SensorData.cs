using System;
using System.Drawing;

namespace KW.Sensor.DataModel;

/// <summary>
/// 标准六维传感器数据   注:出数单位都是N
/// 传感器出数为kg，但是外部处理逻辑已经使用N作为单位，所以在取力值时候统一做了 * G操作
/// 在兼容其他品牌力台时需要注意出数问题，可能需要在Decoder中做处理
/// </summary>
public class SensorData
{
    #region 属性

    /// <summary>
    /// 传感器序号
    /// </summary>
    public byte SenserAddress = 0xAA;

    /// <summary>
    /// 传感器数据
    /// </summary>
    public float[] Data  = new float[14];

    public TimeSpan Time;

    protected float _TorqueFactorX = 1;
    protected float _TorqueFactorY  = 1;
    protected float _Az0 = -10;

    public static float TableSizeX = 0;

    public static float TableSizeY = 0;

    public float _TableSizeX = 0;

    public float _TableSizeY = 0;

    /// <summary>
    /// 传感器X轴间距
    /// </summary>
    protected float _SensorSizeX = 0;

    /// <summary>
    /// 传感器Y轴间距
    /// </summary>
    protected float _SensorSizeY = 0;

    //输出乘数
    public static float[] OutputCoefficient = new float[] { 1, 1, 1, 1, 1, 1 };

    protected bool _NeedOutputCoefficient = true;

    #endregion

    public bool Linked { get; set; }

    #region 构造函数

    public SensorData() { }

    public SensorData(byte address, params float[] values)
    {
        SenserAddress = address;
        Data = new float[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            Data[i] = values[i];
        }
    }

    public SensorData(float[] values)
    {
        Data = new float[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
            Data[i] = values[i];
        }
    }

    public SensorData(float tableWidth, float tableHeight, float sensorWidth, float sensorHeight)
    {
        _TableSizeX = tableWidth;
        _TableSizeY = tableHeight;
        _SensorSizeX = sensorWidth;
        _SensorSizeY = sensorHeight;
    }

    #endregion 

    public float Fx => GetFx() * (_NeedOutputCoefficient ? OutputCoefficient[0] : 1f);

    public float Fy => GetFy() * (_NeedOutputCoefficient ? OutputCoefficient[1] : 1f);

    public float Fz => GetFz() * (_NeedOutputCoefficient ? OutputCoefficient[2] : 1f);

    public float Mx => GetMx() * (_NeedOutputCoefficient ? OutputCoefficient[3] : 1f);

    public float My => GetMy() * (_NeedOutputCoefficient ? OutputCoefficient[4] : 1f);

    public float Mz => GetMz() * (_NeedOutputCoefficient ? OutputCoefficient[5] : 1f);

    public float COPX => GetCOPX();

    public float COPY => GetCOPY();

    #region override

    public override string ToString()
    {
        return $"\"{SenserAddress}\",\"{Time.TotalSeconds.ToString("0.000")}\",\"{Fx.ToString("F5")}\",\"{Fy.ToString("F5")}\",\"{Fz.ToString("F5")}\",\"{Mx.ToString("F5")}\",\"{My.ToString("F5")}\",\"{Mz.ToString("F5")}\",\"{COPX.ToString("F0")}\",\"{COPY.ToString("F0")}\"";
    }

    protected virtual float GetFx()
    {
        return Data[0];
    }

    protected virtual float GetFy()
    {
        return Data[1];
    }

    protected virtual float GetFz()
    {
        return Data[2];
    }

    protected virtual float GetMx()
    {
        return Data[3];
    }

    protected virtual float GetMy()
    {
        return Data[4];
    }

    protected virtual float GetMz()
    {
        return Data[5];
    }

    protected virtual float GetCOPX()
    {
        return 0;
    }

    protected virtual float GetCOPY()
    {
        return 0;
    }

    protected virtual PointF Decode4FToCOP(float[] value)
    {
        if (Math.Abs(Fz) > 10)
            return new PointF
            {
                X = ((value[0] + value[3] - value[1] - value[2]) * _SensorSizeX / 2) / (value[0] + value[1] + value[2] + value[3]),
                Y = -((value[0] + value[1] - value[2] - value[3]) * _SensorSizeY / 2) / (value[0] + value[1] + value[2] + value[3])
            };
        //else if (Fz != 0)
        //    return new PointF
        //    {
        //        X = (value[0] + value[3] - value[1] - value[2]) / SensorSizeX,
        //        Y = -(value[0] + value[1] - value[2] - value[3]) / SensorSizeY
        //    };
        else
            return new PointF(0, 0);
    }

    protected virtual PointF Decode4FToCOP_V2()
    {
        if (Math.Abs(Fz) > 10)
        {
            return new PointF
            {
                X = My / Fz * -1000,
                Y = Mx / Fz * 1000
            };
        }
        else
            return new PointF(0, 0);
    }

    //public static void SetSensorSize(float x, float y)
    //{
    //    SensorSizeX = x;
    //    SensorSizeY = y;
    //}

    #endregion
}
