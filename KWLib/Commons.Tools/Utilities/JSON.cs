using Newtonsoft.Json;

namespace KW.Commons.Tools.Utilities;

public class JSON
{
    public static string SerializeObject(object obj)
    {
        return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
    }

    public static T DeserializeObject<T>(string str)
    {
        return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(str);
    }

    public static void PopulateObject(string value, object target)
    {
        Newtonsoft.Json.JsonConvert.PopulateObject(value, target);
    }

    public static T Copy<T>(T self) where T : class
    {
        if (self == null) 
            return null;

        var json = JsonConvert.SerializeObject(self, Formatting.None, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto // 如果对象图中有复杂继承关系，可能需要开启
        });

        return JsonConvert.DeserializeObject<T>(json, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto
        });
    }
}
