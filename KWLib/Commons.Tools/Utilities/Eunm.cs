using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace KW.Commons.Tools.Utilities;

public class Eunm
{
    public static List<KeyValuePair<T, string>> GetEnumDescriptionItems<T>()
    {
        List<KeyValuePair<T, string>> result = new List<KeyValuePair<T, string>>();

        Type type = typeof(T);

        if (type != null)
        {
            FieldInfo[] fields = type.GetFields();

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == type)
                {
                    Attribute attr = field.GetCustomAttribute(typeof(DescriptionAttribute));

                    if (attr != null)
                    {
                        foreach (PropertyInfo property in typeof(DescriptionAttribute).GetProperties())
                        {
                            if (property.Name == "Description")
                            {
                                result.Add(new KeyValuePair<T, string>((T)Enum.Parse(typeof(T), field.Name), Language.GetMultiLanguage(property.GetValue(attr).ToString())));
                            }
                        }
                    }
                }
            }
        }

        return result;
    }

    public static List<KeyValuePair<T, string>> GetEnumItems<T>()
    {
        List<KeyValuePair<T, string>> result = new List<KeyValuePair<T, string>>();

        Type type = typeof(T);

        if (type != null)
        {
            FieldInfo[] fields = type.GetFields();

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == type)
                {
                    result.Add(new KeyValuePair<T, string>((T)Enum.Parse(typeof(T), field.Name), field.Name));
                }
            }
        }

        return result;
    }

    public static string GetEnumDescription(object value)
    {
        Type type = value.GetType();

        if (type != null)
        {
            FieldInfo[] fields = type.GetFields();

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == type && field.Name == value.ToString())
                {
                    Attribute attr = field.GetCustomAttribute(typeof(DescriptionAttribute));

                    if (attr != null)
                    {
                        foreach (PropertyInfo property in typeof(DescriptionAttribute).GetProperties())
                        {
                            if (property.Name == "Description")
                            {
                                return Language.GetMultiLanguage((attr as DescriptionAttribute).Description);
                            }
                        }
                    }
                }
            }
        }

        return "";
    }

    public static T GetAttribute<T> (object value) where T : Attribute
    {
        Type type = value.GetType();

        if (type != null)
        {
            FieldInfo[] fields = type.GetFields();

            foreach (FieldInfo field in fields)
            {
                if (field.FieldType == type && field.Name == value.ToString())
                {
                    Attribute attr = field.GetCustomAttribute(typeof(T));

                    if (attr != null)
                    {
                        return (T)attr;
                    }
                }
            }
        }

        return null;
    }
}
