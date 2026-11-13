using System.Windows;

namespace FilePermissionReporter;

public partial class App : Application
{
    public static bool IsEnglish { get; private set; }

    public static void SetLanguage(bool english)
    {
        IsEnglish = english;
        var dictionaries = Current.Resources.MergedDictionaries;
        if (dictionaries.Count > 0)
            dictionaries.RemoveAt(0);
        dictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri(english ? "Resources/Strings.en-US.xaml" : "Resources/Strings.zh-CN.xaml", UriKind.Relative)
        });
    }

    public static string Text(string key) => Current.TryFindResource(key) as string ?? key;
}
