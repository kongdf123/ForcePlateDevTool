using System.ComponentModel;

namespace KW.Sensor.DataModel;

public enum KW_CMD
{
    //CMD06 = 0x06,
    [Description("43命令")]
    CMD43,
    [Description("48命令")]
    CMD48,
    [Description("49命令")]
    CMD49,
    CMD61,
    CMD41,
    //CMD45 = 0x45,        
    CMD46,//调速
    CMD47,
    CMDJAKA,
    CMDEE,
    CMD51,
    CMD51On,
    CMD51Off,
    CMD58,
    [Description("68命令")]
    CMD68,
    [Description("78命令")]
    CMD78,
    CMD0D0A = 0x0D0A,
    Speed_115200,
    Speed_230400,
    //Speed_460800,
    CMD_Shake,    //握手协议
    CMD_Modbus_Stop,
    [Description("1000Hz")]
    CMD_DE00,
    [Description("1600Hz")]
    CMD_DE01,
    [Description("2000Hz")]
    CMD_DE02,
    [Description("2400Hz")]
    CMD_DE03,
    [Description("2800Hz")]
    CMD_DE04,
    //DataLeft = 0,
    //DataRight = 1,

    //CMDEESaveZero = 13,
    //CMD47SetZero = 14,

    //CMD50Speed460800 = 17,
    //CMD51PowerOnStart = 18,
    //CMD51PowerOnStop = 19,
    //CMD58WaitStart = 20,
    //CMDCRC100HZ = 21,
    //CMDCRC500HZ = 22,
    //CMDCRCStop = 23,
    //DataStartCutCRC = 24,
    //UserCMD01 = 27,
    //UserCMD02 = 28,
    //UserCMD03 = 29,
    //UserCMD04 = 30,
    //UserCMD05 = 31,
    [Description("Modbus命令")]
    CMD_Modbus = 0b1000000000000000,
    
}

public enum KW_Unit
{
    [Description("kg")]
    kg,
    [Description("N")]
    N
}

public enum Dimension
{
    Fx, Fy, Fz, Mx, My, Mz
}
