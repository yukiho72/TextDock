using System.Windows;
using Application = System.Windows.Application;

namespace TextDock.Services;

public static class LanguageService
{
    private static ResourceDictionary? _currentDict;

    public static void Apply(string language)
    {
        var uri = new Uri($"pack://application:,,,/Resources/Lang/Lang.{language}.xaml");
        var dict = new ResourceDictionary { Source = uri };
        var appRes = Application.Current.Resources;
        if (_currentDict != null)
            appRes.MergedDictionaries.Remove(_currentDict);
        appRes.MergedDictionaries.Add(dict);
        _currentDict = dict;
    }
}

public static class Loc
{
    public static string S(string key)
        => Application.Current.Resources[key] as string ?? $"[{key}]";

    public static string S(string key, params object[] args)
        => string.Format(S(key), args);
}
