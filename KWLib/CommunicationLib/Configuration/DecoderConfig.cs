
using System.Reflection;
using System.ComponentModel;

using KW.Sensor.DataModel;
using KW.Commons.Tools.Utilities;
using KW.CommunicationLib.Decoder;

namespace KW.CommunicationLib.Configuration;

public class DecoderConfig
{
    #region 属性
    /// <summary>
    /// 量程
    /// </summary>
    public string Capacity = "KW.CommunicationLib.Capacity.KWR75B";
    /// <summary>
    /// 解码方式
    /// </summary>
    public string DecoderClass = "KW.CommunicationLib.Decoder.Decoder_28Bit";
    /// <summary>
    /// 采集模式  默认位被动接收
    /// </summary>
    public CollectionMode CollectionMode = CollectionMode.Passive;
    /// <summary>
    /// 开始指令
    /// </summary>
    public KW_CMD StartCMD = KW_CMD.CMD48;
    /// <summary>
    /// 结束指令
    /// </summary>
    public KW_CMD StopCMD = KW_CMD.CMD43;

    public byte StationAddress = 0xAA;

    /// <summary>
    /// 伪地址  用于解析后替换  
    /// </summary>
    public byte FakeAddress = 0xAA;

    /// <summary>
    /// 
    /// </summary>
    public KW_CMD TailByte = KW_CMD.CMD0D0A;
    public float TorqueFactorX = 1;
    public float TorqueFactorY = 1;
    public float Az0 = 0;

    /// <summary>
    /// 传感器间距
    /// </summary>
    public float SensorWidth, SensorHeight, TableWidth, TableHeight;

    #endregion

    #region Method       

    public Type GetDecoderClass()
    {
        Type type = Type.GetType(DecoderClass);

        if (type is null)
        {
            Assembly assembly = Assembly.Load(DecoderClass.Substring(0, DecoderClass.LastIndexOf(".")));
            type = assembly.GetType(DecoderClass,false);
        }

        return type;
    }

    public virtual DecoderConfig Clone()
    {
        return new DecoderConfig
        {
            FakeAddress = this.FakeAddress,
            StationAddress = this.StationAddress,
            Capacity = this.Capacity,
            DecoderClass = this.DecoderClass,
            CollectionMode = this.CollectionMode,
            StartCMD = this.StartCMD,
            StopCMD = this.StopCMD,
            TailByte = this.TailByte,
            SensorWidth = this.SensorWidth,
            SensorHeight =this.SensorHeight,
            TableWidth = this.TableWidth,
            TableHeight = this.TableHeight,
        };
    }

    #endregion

    public static List<KeyValuePair<CollectionMode,string>> GetAllCollectionModes()
    {
        return Eunm.GetEnumDescriptionItems<CollectionMode>();
    }
}

public enum CollectionMode
{
    [Description("主动获取(49)")]
    Initiative,
    [Description("被动接收(48)")]
    Passive,
    // [Description("触发+同步输入(68)")]
    // SyncIn,
    // [Description("仅同步输入(78)")]
    // OnlySyncIn,
    [Description("仅外部触发(58)")]
    OnlyTrigger
}