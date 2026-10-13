
using System.Reflection;

using KW.Communicationlib.Decoder;

namespace KW.CommunicationLib.Decoder;

public abstract class DecoderBase
{
    public static string FactorFile = "";
    public static float Az0_Offset = 0;

    public Action<string> MessageOutput = null;

    #region 变量

    /// <summary>
    /// 存放实际解析到的地址为
    /// </summary>
    protected byte _Adress = 0xAA;

    //内置分度数
    protected double[] _InternalCoefficient = new double[] { 1, 1 };

    ////输出乘数
    protected int _DataLength = 28;
    protected string _Capacity;
    protected DecoderConfig _Config = new DecoderConfig();
    public static Dictionary<byte, SensorFactor[]> VoltageFactor = new Dictionary<byte, SensorFactor[]>();  //用于电压计算力值的外部系数

    protected float _TorqueFactorX = 1;
    protected float _TorqueFactorY  = 1;
    protected float _Az0 = 0;
    protected float _TableSizeX = 0;
    protected float _TableSizeY = 0;
    protected bool _CalibrationModel = false;
    protected float _RealAZ0
    {
        get => _Az0 + Az0_Offset;
    }

    #endregion

    #region 属性

    public int DataLength => _DataLength;

    /// <summary>
    /// 力值系数
    /// </summary>
    public double ForceCoefficient => _InternalCoefficient[0];

    /// <summary>
    /// 扭矩系数
    /// </summary>
    public double TorqueCoefficient => _InternalCoefficient[1];

    /// <summary>
    /// 解析过程中是否加入地址位
    /// </summary>
    public bool IsSupportAddress
    {
        get
        {
            System.Attribute[] attrs = System.Attribute.GetCustomAttributes(this.GetType());
            System.Attribute attr = attrs.FirstOrDefault(a => a is DecoderAttribute);

            if (attr != null)
            {
                return (attr as DecoderAttribute).SupportAddress;
            }

            return true;
        }
    }

    /// <summary>
    /// 虚拟通讯地址 用于标记区分输出数据
    /// </summary>
    public byte FakeAddress
    {
        get => _Config.FakeAddress;
        set => _Config.FakeAddress = value; 
    }

    public byte[] StartBit { get; protected set; } = new byte[] { 0x48 };
    public byte[] EndBit { get; protected set; } = new byte[] { 0x0D, 0x0A };

    public KW_CMD StartCMD => GetStartCMD();

    public KW_CMD StopCMD => GetStopCMD();

    #endregion

    #region 构造函数       

    public DecoderBase() { }

    public DecoderBase(DecoderConfig config)
    {
        _Config = config;

        switch (config.CollectionMode)
        {
            case CollectionMode.Passive:
                _Config.StartCMD = KW_CMD.CMD48;
                break;
            case CollectionMode.Initiative:
                _Config.StartCMD = KW_CMD.CMD49;
                break;
            // case CollectionMode.SyncIn:
            //     _Config.StartCMD = KW_CMD.CMD68;
            //     break;
            // case CollectionMode.OnlySyncIn:
            //     _Config.StartCMD = KW_CMD.CMD78;
            //     break;
        }

        _Config.StopCMD = KW_CMD.CMD43;
        _Capacity = _Config.Capacity;
        StartBit = GetBytes(StartCMD);
        EndBit = GetBytes(KW_CMD.CMD0D0A);
        _TorqueFactorX = config.TorqueFactorX;
        _TorqueFactorY = config.TorqueFactorY;
        _Az0 = config.Az0;

        //计算内置系数
        SetInternalCoefficient();
    }

    #endregion       

    #region 公共接口

    /// <summary>
    /// 解码
    /// </summary>
    /// <param name="array"></param>
    /// <param name="data"></param>
    /// <returns></returns>
    public virtual bool Decode(byte[] array, out SensorData[] data)
    {
        data = new SensorData[0];

        //长度检查
        if (array.Length != _DataLength)
        {
#if DEBUG                
            Log.LogInfo("XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX " + array.Length.ToString());
            //KW.Tools.Log.LogInfo("XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX "+ String.Join(" ", array.Select(a => a.ToString("X2")))) ;                
#endif
            return false;
        }

        try
        {
            ParseByteArray(array);
            data = GetDigitalData();
            return true;
        }
        catch (Exception ex)
        {
            Log.LogError($"NaN Error [{string.Join(" ", array)}]");
            Log.LogError(ex);
            return false;
        }
    }

    public DecoderConfig GetConfig()
    {
        return _Config;
    }

    public bool CustomCommand
    {
        get
        {
            Assembly assembly = Assembly.GetAssembly(typeof(DecoderBase));
            Type type = this.GetType();

            if (type != null)
            {
                Attribute[] attrs = Attribute.GetCustomAttributes(type);
                Attribute attr = attrs.FirstOrDefault(a => a is DecoderAttribute);

                if (attr != null)
                {
                    return (attr as DecoderAttribute).CustomCommand;
                }
            }

            return false;
        }
    }

    public bool IndirectConnect
    {
        get
        {
            Assembly assembly = Assembly.GetAssembly(typeof(DecoderBase));
            Type type = this.GetType();

            if (type != null)
            {
                Attribute[] attrs = Attribute.GetCustomAttributes(type);
                Attribute attr = attrs.FirstOrDefault(a => a is DecoderAttribute);

                if (attr != null)
                {
                    return (attr as DecoderAttribute).IndirectConnect;
                }
            }

            return false;
        }
    }

    
    public void OpenCalibrationModel()
    {
        _CalibrationModel = true;
    }

    public void CloseCalibrationModel()
    {
        _CalibrationModel = false;
    }

    protected virtual KW_CMD GetStartCMD()
    {
        return _Config.StartCMD;
    }

    protected virtual KW_CMD GetStopCMD()
    {
        return _Config.StopCMD;
    }

    public virtual byte[] GetClutchCommand()
    {
        return new byte[0];
    }

    public virtual byte[] GetCommand(int cmd, byte linkerAddress = 0xAA)
    {
        switch (cmd)
        {
            case (int)KW_CMD.CMD43:
                return new byte[] { 0x43, linkerAddress, 0x0D, 0x0A };
            case (int)KW_CMD.CMD46:
                return new byte[] { 0x46, 0xAA, 0x0D, 0x0A };
            case (int)KW_CMD.CMD47:
                return new byte[] { 0x47, 0xAA, 0x0D, 0x0A };
            case (int)KW_CMD.CMD48:
                return new byte[] { 0x48, linkerAddress, 0x0D, 0x0A };
            case (int)KW_CMD.CMD49:
                return new byte[] { 0x49, linkerAddress, 0x0D, 0x0A };
            case (int)KW_CMD.CMD51On:
            case (int)KW_CMD.CMD51:
                return new byte[] { 0x51, 0x00, 0x0D, 0x0A };
            case (int)KW_CMD.CMD51Off:
                return new byte[] { 0x51, linkerAddress, 0x0D, 0x0A };
            case (int)KW_CMD.CMD58:
                return new byte[] { 0x58, linkerAddress, 0x0D, 0x0A };
            case (int)KW_CMD.CMD68:
                return new byte[] { 0x68, linkerAddress, 0x0D, 0x0A };
            case (int)KW_CMD.CMD78:
                return new byte[] { 0x78, linkerAddress, 0x0D, 0x0A };
            case (int)KW_CMD.CMD41:
                return new byte[] { 0x41, linkerAddress, 0x0D, 0x0A };
            case (int)KW_CMD.CMD61:
                return new byte[] { 0x61, linkerAddress, 0x0D, 0x0A };
            case (int)KW_CMD.CMDEE:
                return new byte[] { 0xEE, 0xAA, 0x0D, 0x0A };
            case (int)KW_CMD.Speed_115200:
                return new byte[] { 0x50, 0x01, 0x0D, 0x0A };
            case (int)KW_CMD.Speed_230400:
                return new byte[] { 0x50, 0x02, 0x0D, 0x0A };
            case (int)KW_CMD.CMD_Shake:
                return new byte[] { 0xDD, 0xAA, 0x0D, 0x0A };
            case (int)KW_CMD.CMDJAKA:
                return new byte[] { 0xAA, 0x55 };
            case (int)KW_CMD.CMD_DE00:
                return new byte[] { 0xDE, 0x00,0x0D,0x0A };
            case (int)KW_CMD.CMD_DE01:
                return new byte[] { 0xDE, 0x01, 0x0D, 0x0A };
            case (int)KW_CMD.CMD_DE02:
                return new byte[] { 0xDE, 0x02, 0x0D, 0x0A };
            case (int)KW_CMD.CMD_DE03:
                return new byte[] { 0xDE, 0x03, 0x0D, 0x0A };
            case (int)KW_CMD.CMD_DE04:
                return new byte[] { 0xDE, 0x04, 0x0D, 0x0A };
            default:
                return new byte[0];
        }
    }

    public virtual byte[] GetBytes(KW_CMD cmd)
    {
        switch (cmd)
        {
            case KW_CMD.CMD49:
                return new byte[] { 0x49 };
            case KW_CMD.CMD58:
                return new byte[] { 0x58 };
            case KW_CMD.CMD41:
            case KW_CMD.CMD61:
                return new byte[] { 0x61 };
            case KW_CMD.CMD0D0A:
                return new byte[] { 0x0D, 0x0A };
            case KW_CMD.CMDJAKA:
                return new byte[] { 0xAA, 0x55 };
            default:
                return new byte[] { 0x48 };
        }
    }

    public virtual void GetMapping(ref List<byte> tempList, byte decodeAddress, Func<byte[], bool> callBack, Action<byte[]> onMapFalid = null)
    {
        byte[] dataBlock = new byte[0];
        int headIndex = 0;
        bool mappingByte = false;

        try
        {
            while (tempList.Count >=  DataLength)
            {
                if (IsSupportAddress)
                {
                    mappingByte = false;

                    headIndex = tempList.FindIndex(headIndex, element =>
                    {
                        if (mappingByte && element == decodeAddress)
                        {
                            return true;
                        }

                        mappingByte = element == StartBit[0];
                        return false;
                    });
                }
                else
                {
                    headIndex = tempList.FindIndex(headIndex, element => element == StartBit[0]);
                }

                if (headIndex == -1)
                {
                    break;
                }

                if (IsSupportAddress)
                {
                    headIndex--;
                }

                if (headIndex != 0)
                {
                    onMapFalid?.Invoke(tempList.Take(headIndex).ToArray());
                    tempList = tempList.Skip(headIndex).ToList();
                    headIndex = 0;
                }

                if (tempList.Count >= _DataLength)
                {
                    dataBlock = tempList.Take(_DataLength).ToArray();

                    if (dataBlock[DataLength - 2] == EndBit[0] && dataBlock[DataLength - 1] == EndBit[1] && callBack(dataBlock))
                    {
                        tempList = tempList.Skip(_DataLength).ToList();
                        continue;
                    }

                    headIndex++;
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogError(ex);
            onMapFalid?.Invoke(tempList.ToArray());
            tempList.Clear();
        }
    }

    /// <summary>
    /// 调整输出
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public virtual SensorData AdjustData(SensorData data, Dictionary<byte, SensorData> zeroOffsetObj)
    {
        if (zeroOffsetObj.ContainsKey(data.SenserAddress))
        {
            for (int i = 0; i < data.Data.Length; i++)
            {
                //if (i < _ZeroOffsetObj[data.SenserAddress].Data.Length)
                //软件清零补偿
                data.Data[i] -= zeroOffsetObj[data.SenserAddress].Data[i];
            }
        }
        return data;
    }

    public virtual byte[] AdjustByteBlock(List<byte[]> blocks)
    {
        List<byte> result = new List<byte>();

        blocks.ForEach(block =>
        {
            result.AddRange(block);
        });

        return result.ToArray();
    }

    #endregion

    #region 静态方法

    /// <summary>
    /// 通过类名获取解码对象
    /// </summary>
    /// <param name="className"></param>
    /// <returns></returns>
    public static DecoderBase GetDecoder(string className, float capacity)
    {
        Type type = Type.GetType(className, false);

        if(type is null)
        {
            Assembly assembly = Assembly.Load(className.Substring(0, className.LastIndexOf(".")));
            type = assembly.GetType(className);
        }

        if (type != null)
        {
            return (DecoderBase)Activator.CreateInstance(type, capacity);
        }

        return null;
    }

    public static DecoderBase GetDecoder(DecoderConfig config)
    {
        Type type = config.GetDecoderClass();

        if (type != null)
        {
            return (DecoderBase)Activator.CreateInstance(type, config);
        }

        return null;
    }

    /// <summary>
    /// 获取解码规则清单  类名,解码规则名
    /// </summary>
    /// <returns></returns>
    public static List<KeyValuePair<string, string>> GetAllDecoderTypes()
    {
        List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
        try
        {

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.BaseType == typeof(DecoderBase)
                    || type.BaseType == typeof(Decoder_ModbusBase)
                    || type.BaseType == typeof(Decoder_CANBase)
                    || type.BaseType == typeof(Decoder_CANFDBase))
                {
                    Attribute[] attrs = Attribute.GetCustomAttributes(type);
                    Attribute attr = attrs.FirstOrDefault(a => a is DecoderAttribute);

                    if (attr != null)
                    {
                        result.Add(new KeyValuePair<string, string>(type.FullName, Language.GetMultiLanguage((attr as DecoderAttribute).Name)));
                    }
                }
            }
        }
        catch(ReflectionTypeLoadException ex)
        {
            foreach(Exception e in ex.LoaderExceptions)
            {
                Log.LogError(e);
            }
        }

        return result;
    }

    #endregion

    #region 方法

    /// <summary>
    /// 用于分割4浮点型16进制字节数据
    /// </summary>
    /// <param name="array"></param>
    protected abstract void ParseByteArray(byte[] array);

    /// <summary>
    /// 实际数据转化
    /// </summary>
    /// <returns></returns>
    protected abstract SensorData[] GetDigitalData();

    /// <summary>
    /// 通过量程和字长计算内置系数
    /// </summary>
    protected virtual void SetInternalCoefficient() { }

    #endregion

    protected virtual int BinaryStrToInt(string binaryStr)
    {
        if (string.IsNullOrEmpty(binaryStr))
            return 0;

        char symbol = binaryStr.First();
        string format = binaryStr.PadLeft(32, symbol);
        return Convert.ToInt32(format, 2);
    }


    protected virtual int BinaryToShort(byte[] binaryArray, int index)
    {
        byte[] array = new byte[2];
        short result = 0;

        switch (index)
        {
            case 0:
                result = (short)((short)binaryArray[0] << 4 + binaryArray[1] >> 4);
                break;
            case 1:
                result = (short)((short)(binaryArray[1] & 0b00001111) << 8 + binaryArray[2]);
                break;
            case 2:
                result = (short)((short)binaryArray[3] << 4 + binaryArray[4] >> 4);
                break;
            case 3:
                result = (short)((short)(binaryArray[4] & 0b00001111) << 8 + binaryArray[5]);
                break;
            case 4:
                result = (short)((short)(binaryArray[6] & 0b00001111) << 8 + binaryArray[7]);
                break;
            case 5:
                result = (short)((short)(binaryArray[7] & 0b00001111) << 8 + binaryArray[8]);
                break;
        }

        if ((result & 0b0000100000000000) > 0)
        {
            result = (short)(result | 0b1111000000000000);
        }

        return result;
    }
}

public class DecoderAttribute : Attribute
{
    private string _DecoderName;
    private string _Description;
    private bool _SupportCMD45;
    private bool _SupportAddress;
    private bool _CustomCommand;
    private bool _NeedCapacity;
    private bool _IndirectConnect;
    private bool _ShowStationAddress;

    public DecoderAttribute(string Name, string Description, bool SupportAddress = true, bool SupportCMD45 = true, bool CustomCommand = true, bool NeedCapacity = false,bool IndirectConnect = false,bool ShowStationAddress = false)
    {
        this._DecoderName = Name;
        this._Description = Description;
        this._SupportCMD45 = SupportCMD45;
        this._SupportAddress = SupportAddress;
        this._CustomCommand = CustomCommand;
        this._NeedCapacity = NeedCapacity;
        this._IndirectConnect = IndirectConnect;
        this._ShowStationAddress = ShowStationAddress;
    }

    public string Name => _DecoderName;
    public string Description => _Description;
    public bool SupportCMD45 => _SupportCMD45;
    public bool SupportAddress => _SupportAddress;
    public bool CustomCommand => _CustomCommand;
    public bool NeedCapacity => _NeedCapacity;
    public bool IndirectConnect => _IndirectConnect;
    public bool ShowStationAddress => _ShowStationAddress;
}

/// <summary>
/// 电压-力值(kg) 转换系数
/// </summary>
public class SensorFactor
{
    public SensorFactor() { }

    public SensorFactor(object value)
    {
        try
        {
            if (value is string && value.ToString().Contains(","))
            {
                A1 = Convert.ToSingle((value as string).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)[0]);
                A2 = Convert.ToSingle((value as string).Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)[1]);
            }
            else
            {
                A1 = A2 = Convert.ToSingle(value);
            }
        }
        catch { }
    }

    public float A1 = 1;
    public float A2 = 0;
}
