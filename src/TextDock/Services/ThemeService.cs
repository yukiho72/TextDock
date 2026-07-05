using System.Windows;
using System.Windows.Media;
using TextDock.Models;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;

namespace TextDock.Services;

/// <summary>テーマ（仕様書25章）。Light/Dark は組込み配色、Custom は設定の3色を使用する。</summary>
public static class ThemeService
{
    public static void Apply(AppSettings settings)
    {
        var (bg, fg, sel) = settings.Theme switch
        {
            "Light" => ("#FFFFFF", "#1E1E1E", "#CCE8FF"),
            "Custom" => (settings.ColorBackground, settings.ColorText, settings.ColorSelection),
            _ => ("#1E1E1E", "#D4D4D4", "#264F78"),
        };

        var resources = Application.Current.Resources;
        resources["BgBrush"] = BrushOf(bg, Colors.Black);
        resources["FgBrush"] = BrushOf(fg, Colors.White);
        resources["SelBrush"] = BrushOf(sel, Colors.SteelBlue);
        resources["AppFontFamily"] = new FontFamily(settings.FontName);
        resources["AppFontSize"] = (double)settings.FontSize;
    }

    private static SolidColorBrush BrushOf(string hex, Color fallback)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch (FormatException)
        {
            return new SolidColorBrush(fallback);
        }
    }
}
