using System.Reflection;
using KW.Commons.Tools.Utilities;
using KW.CommunicationLib.Configuration;

namespace KW.CommunicationLib.Linker
{
    public abstract class LinkerBase : IDisposable
    {
        protected LinkerConfig _Config;

        #region 构造函数

        public LinkerBase(LinkerConfig config)
        {
            _Config = JSON.Copy( config);
        }

        #endregion

        #region 静态方法

        public static LinkerBase GetLinker(Type type, LinkerConfig config)
        {
            if (type != null && typeof(LinkerBase).IsAssignableFrom(type))
            {
                var constructor = type.GetConstructor(new Type[] { typeof(LinkerConfig) }); // 检查构造函数

                if (constructor != null)
                    return (LinkerBase)Activator.CreateInstance(type, config);
            }

            return null;
        }

        public static ILinkerConfig GetLinkerConfig(Type type)
        {
            if (type != null && typeof(ILinkerConfig).IsAssignableFrom(type))
            {
                var constructor = type.GetConstructor(new Type[0]); // 检查构造函数

                if (constructor != null)
                    return (ILinkerConfig)Activator.CreateInstance(type);
            }

            return null;
        }

        public static LinkerBase GetLinker(ILinkerConfig config)
        {
            try
            {
                Type type = config.GetLinkerClass();

                if (type != null)
                {
                    return (LinkerBase)Activator.CreateInstance(type, config);
                }
            }
            catch (Exception ex)
            {
                Log.LogError(JSON.SerializeObject(config));
                Log.LogError(ex);
            }

            return null;
        }

        public static List<KeyValuePair<string, string>> GetAllLinkerTypes()
        {
            return GetTypesWithAttribute<LinkerBase>(attr => Language.GetMultiLanguage(attr.Name));
        }

        public static List<KeyValuePair<string, string>> GetAllLinkerConfigTypes()
        {
            return GetTypesWithAttribute<LinkerConfig>(attr => Language.GetMultiLanguage(attr.Name));
        }

        private static List<KeyValuePair<string, string>> GetTypesWithAttribute<TBaseType>(Func<LinkerAttribute, string> nameSelector) where TBaseType : class
        {
            var result = new List<KeyValuePair<string, string>>();

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.BaseType == typeof(TBaseType)) // 注意：这里用的是精确匹配，可以改为 IsAssignableFrom 更灵活
                {
                    var attr = Attribute.GetCustomAttributes(type)
                                        .OfType<LinkerAttribute>()
                                        .FirstOrDefault();
                    if (attr != null)
                    {
                        // 这里可以根据 TBaseType 的不同，选择不同的 nameSelector
                        result.Add(new KeyValuePair<string, string>(type.FullName, nameSelector(attr)));
                    }
                }
            }
            return result;
        }

        #endregion

        #region 公共接口

        public abstract bool IsOpen();

        public abstract bool Open();

        public abstract bool Close();

        public abstract int Receive(out byte[] array, int timeOut = 100, Func<List<byte[]>, byte[]> callback = null);

        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="array">接收到的数据</param>
        /// <param name="timeOut">超时时间，单位为毫秒(ms)</param>
        /// <returns>实际接收到的字节数</returns>
        public virtual int ReceiveUntilTimeout(out byte[] array, int timeOut = 100)
        {
            return Receive(out array,timeOut);
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="array">接收到的数据</param>
        /// <param name="timeOut">超时时间，单位为毫秒(ms)<</param>
        /// <param name="callback">可选的数据处理回调</param>
        /// <returns>实际接收到的字节数</returns>
        public virtual int ReceiveUntilTimeout(out byte[] array, int timeOut = 100, Func<List<byte[]>, byte[]> callback = null)
        {
            return Receive(out array, timeOut, callback);
        }

        public abstract bool Send(byte[] array);

        /// <summary>
        /// 发送并匹配返回
        /// </summary>
        /// <param name="array"></param>
        /// <param name="receive"></param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public virtual bool SendAndReceive(byte[] array, out byte[] receive, int timeOut = 1000)
        {
            receive = new byte[0];

            if (Send(array) && Receive(out receive, timeOut) > 0)
            {
                return true;
            }

            return false;
        }

        public virtual bool SendAndReceiveUntilTimeout(byte[] array, out byte[] receive, int timeOut = 1000)
        {
            receive = new byte[0];

            if (Send(array) && ReceiveUntilTimeout(out receive, timeOut) > 0)
            {
                return true;
            }

            return false;
        }

        public virtual LinkerConfig GetConfig()
        {
            return _Config;
        }

        public abstract void Dispose();

        #endregion
    }

    internal class LinkerAttribute : Attribute
    {
        private string _DecoderName;
        private string _Description;

        public LinkerAttribute(string name, string description)
        {
            this._DecoderName = name;
            this._Description = description;
        }

        public string Name => _DecoderName;
        public string Description => _Description;
    }
}
