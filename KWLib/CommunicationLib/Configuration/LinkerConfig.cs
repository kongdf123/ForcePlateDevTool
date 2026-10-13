using System.Windows.Controls;

using KW.CommunicationLib.Linker;

namespace KW.CommunicationLib.Configuration;

public interface ILinkerConfigBase
{
    string GetName();

    bool GetNeedConnect();

    byte GetAddress();

    void SetAddress(byte address);

    void SetNeedConnect(bool needConnect);
}

public interface ILinkerConfig : ILinkerConfigBase
{
    Type GetLinkerClass();
    
    UserControl GetSetupView();

    ILinkerConfig Clone();

    void Copy(ILinkerConfig config);

    bool Check(out string msg);
}

public class LinkerConfig : ILinkerConfigBase
{
    public string CurrentClass;

    private bool NeedConnect = true;

    /// <summary>
    /// 个别通讯方式确实需要用到地址 所以这里保留Address的属性，同时用于记录真实通讯地址位数据
    /// 严格来说真实通讯地址位应该放在decoder中更加合适
    /// Can FD
    /// </summary>
    public byte Address = 0xAA;

    public virtual byte GetAddress()
    {
        return this.Address;
    }

    public void SetAddress(byte address)
    {
        this.Address = address;
    }

    public virtual string GetName()
    {
        var attrs = Attribute.GetCustomAttributes(this.GetType());

        Attribute attr = attrs.FirstOrDefault(a => a is LinkerAttribute);

        if (attr != null)
        {
            return  (attr as LinkerAttribute).Name;
        }

        return "";
    }

    public bool GetNeedConnect()
    {
        return this.NeedConnect;
    }

    public void SetNeedConnect(bool needConnect)
    {
        this.NeedConnect = needConnect;
    }
}
