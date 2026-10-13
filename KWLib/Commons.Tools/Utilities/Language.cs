using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Xml.Serialization;

namespace KW.Commons.Tools.Utilities;

public class Language
{
    private const string _ChineseRessource = "pack://application:,,,/KW.WPF.Controls;component/Languages/Chinese.xaml";
    private const string _EnglishRessource = "pack://application:,,,/KW.WPF.Controls;component/Languages/English.xaml";

    private static string _LanguageFile = Path.Combine(Environment.CurrentDirectory, "Language.xml");
    private static CultureInfo _CultureInfo;
    private static bool _Enabled = false;
    private static ResourceDictionary _CurrentLanguage = null;
    static Language()
    {
        //SetCultureInfo(CultureInfo.CurrentCulture);
    }

    public static string GetMultiLanguage(string lang)
    {
        ////if (!_Enabled)
        ////{
        ////    return lang;
        ////}

        ////if (!Collection.ContainsKey(lang))
        ////{
        ////    XmlSerializer xmlSerializer = new XmlSerializer(typeof(Langs));

        ////    using (FileStream fileStream = new FileStream(_LanguageFile, FileMode.OpenOrCreate))
        ////    {
        ////        Langs langs = new Langs();

        ////        if (fileStream.Length > 0)
        ////        {
        ////            langs = (Langs)xmlSerializer.Deserialize(fileStream);
        ////        }

        ////        langs.Items.Add(new LangItems
        ////        {
        ////            CN = lang,
        ////            EN = ""
        ////        });

        ////        fileStream.Position = 0;
        ////        xmlSerializer.Serialize(fileStream, langs);
        ////    }

        ////    SetCultureInfo(_CultureInfo);
        ////}

        ////return Collection[lang];

        //if (_Enabled && _CurrentLanguage != null)
        //{
        //    try
        //    {
        //        return _CurrentLanguage[lang].ToString();
        //    }
        //    catch
        //    {
        //        //Log.LogError($"{lang} MultiLanguage UnDefined!");
        //    }
        //}

        //return lang;

        if (string.IsNullOrWhiteSpace(lang))
        {
            return "";
        }

        return TryFindResource(lang);
    }

    public static string TryFindResource(string lang)
    {
        if (_Enabled && _CurrentLanguage != null)
        {
            try
            {
                if (_CurrentLanguage[lang] == null)
                {
                    //Log.LogInfo(lang);
                    return lang;
                }

                return _CurrentLanguage[lang] as string;
            }
            catch (Exception ex)
            {
                Log.LogError(ex);
            }
        }

        return lang;
    }

    public static void SetCultureInfo(CultureInfo cultureInfo)
    {
        _Enabled = true;
        _CultureInfo = cultureInfo;
        //Collection.Clear();

        ResourceDictionary remove = null;
        ResourceDictionary add = null;

        XmlSerializer xmlSerializer = new XmlSerializer(typeof(Langs));

        //using (FileStream fileStream = new FileStream(_LanguageFile, FileMode.OpenOrCreate))
        //{
        //    if (fileStream.Length > 0)
        //    {
        //Langs langs = (Langs)xmlSerializer.Deserialize(fileStream);

        if (cultureInfo.Name.Equals("en", StringComparison.CurrentCultureIgnoreCase))
        {
            //foreach (LangItems item in langs.Items)
            //{
            //    Collection.Add(item.CN, item.EN);
            //}

            foreach (ResourceDictionary dictionary in Application.Current.Resources.MergedDictionaries)
            {
                if (dictionary.Source.OriginalString.Equals(_ChineseRessource))
                {
                    remove = dictionary;
                }
            }

            add = new ResourceDictionary
            {
                Source = new Uri(_EnglishRessource, UriKind.Absolute)
            };
        }
        else
        {
            //foreach (LangItems item in langs.Items)
            //{
            //    Collection.Add(item.CN, item.CN);
            //}

            add = new ResourceDictionary
            {
                Source = new Uri(_ChineseRessource, UriKind.Absolute)
            };
        }

        if (remove != null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(remove);
        }

        Application.Current.Resources.MergedDictionaries.Add(add);
        _CurrentLanguage = add;
        //    }
        //}
    }

    public static void SetLanguages(string resourcePath)
    {
        _Enabled = true;

        ResourceDictionary add = new ResourceDictionary
        {
            Source = new Uri(resourcePath, UriKind.Absolute)
        };

        Application.Current.Resources.MergedDictionaries.Add(add);
        _CurrentLanguage = add;
    }

    public static string GetLocalizedString(string key, string cultureName,string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        try
        {
            var dict = GetOrLoadResourceDictionary(cultureName, resourcePath);
            if (dict?.Contains(key) == true)
                return dict[key]?.ToString() ?? key;
        }
        catch
        {
            
        }

        return key;
    }

    private static readonly Dictionary<string, ResourceDictionary> _languageCache =
        new Dictionary<string, ResourceDictionary>();

    private static ResourceDictionary GetOrLoadResourceDictionary(string cultureName,string resourcePath)
    {
        string normalized = string.IsNullOrEmpty(cultureName) ||
                            cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "zh";

        if (_languageCache.TryGetValue(normalized, out var dict))
            return dict;
        dict = new ResourceDictionary { Source = new Uri(resourcePath, UriKind.Absolute) };
        _languageCache[normalized] = dict;
        return dict;
    }

    private static LanguageCollection Collection = new LanguageCollection();
}

public class LanguageCollection : Dictionary<string, string>
{
    public new string this[string key]
    {
        get
        {
            if (this.ContainsKey(key))
            { 
                string val = base[key];
                return string.IsNullOrWhiteSpace(val) ? key : val;
            }
            else
                return key;
        }
    }
}


// 注意: 生成的代码可能至少需要 .NET Framework 4.5 或 .NET Core/Standard 2.0。
/// <remarks/>
[Serializable] 
public partial class Langs
{
    public Langs()
    {
        itemField = new List<LangItems>();
    }

    private List<LangItems> itemField;
    
    public List<LangItems> Items
    {
        get
        {
            return this.itemField;
        }
        set
        {
            this.itemField = value;
        }
    }
}

/// <remarks/>
[Serializable]
public partial class LangItems
{
    /// <remarks/>
    public string CN { get; set; }

    /// <remarks/>
    public string EN { get; set; }
}

public class LangObj
{
    public string this[string key]
    {
        get
        {
            return Language.GetMultiLanguage(key);
        }
    }
}
