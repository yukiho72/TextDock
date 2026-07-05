using System.IO;
using TextDock.Services;
using TextDock.Models;

namespace TextDock.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _dir;

    public SettingsServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "TextDockTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ファイルが無い場合はデフォルト値を返しファイルを作成する()
    {
        var service = new SettingsService(_dir);

        var settings = service.Load();

        Assert.Equal("", settings.MemoFolder);
        Assert.Equal("Ctrl+Space", settings.Hotkey);
        Assert.Equal("Dark", settings.Theme);
        Assert.Equal("Meiryo UI", settings.FontName);
        Assert.Equal(12, settings.FontSize);
        Assert.Equal("#1E1E1E", settings.ColorBackground);
        Assert.Equal("#D4D4D4", settings.ColorText);
        Assert.Equal("#264F78", settings.ColorSelection);
        Assert.Equal("Clipboard", settings.DefaultPasteMethod);
        Assert.True(settings.ClipboardProtection);
        Assert.True(settings.CloseAfterPaste);
        Assert.Equal(10000, settings.MaxLines);
        Assert.Equal(100, settings.ClipboardDelay);
        Assert.Equal(20, settings.WmcharDelay);
        Assert.Equal(20, settings.SendinputDelay);
        Assert.Empty(settings.AppPasteSettings);
        Assert.Equal(800, settings.WindowWidth);
        Assert.Equal(500, settings.WindowHeight);
        Assert.Equal(600, settings.EditorWidth);
        Assert.Equal(400, settings.EditorHeight);
        Assert.Equal(0.35, settings.PaneRatio);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
    }

    [Fact]
    public void 保存して読み込むと全プロパティが一致する()
    {
        var service = new SettingsService(_dir);
        var settings = new AppSettings
        {
            MemoFolder = @"D:\Memo",
            Hotkey = "Ctrl+Shift+M",
            Theme = "Custom",
            FontName = "Consolas",
            FontSize = 14,
            ColorBackground = "#000000",
            ColorText = "#FFFFFF",
            ColorSelection = "#333333",
            DefaultPasteMethod = "SendInput",
            ClipboardProtection = false,
            CloseAfterPaste = false,
            MaxLines = 200,
            ClipboardDelay = 150,
            WmcharDelay = 30,
            SendinputDelay = 40,
            AppPasteSettings = new Dictionary<string, string> { ["ttermpro.exe"] = "WM_CHAR" },
            WindowWidth = 1000,
            WindowHeight = 700,
            EditorWidth = 800,
            EditorHeight = 600,
            PaneRatio = 0.5,
        };

        service.Save(settings);
        var loaded = new SettingsService(_dir).Load();

        Assert.Equal(@"D:\Memo", loaded.MemoFolder);
        Assert.Equal("Ctrl+Shift+M", loaded.Hotkey);
        Assert.Equal("Custom", loaded.Theme);
        Assert.Equal("Consolas", loaded.FontName);
        Assert.Equal(14, loaded.FontSize);
        Assert.Equal("#000000", loaded.ColorBackground);
        Assert.Equal("#FFFFFF", loaded.ColorText);
        Assert.Equal("#333333", loaded.ColorSelection);
        Assert.Equal("SendInput", loaded.DefaultPasteMethod);
        Assert.False(loaded.ClipboardProtection);
        Assert.False(loaded.CloseAfterPaste);
        Assert.Equal(200, loaded.MaxLines);
        Assert.Equal(150, loaded.ClipboardDelay);
        Assert.Equal(30, loaded.WmcharDelay);
        Assert.Equal(40, loaded.SendinputDelay);
        Assert.Equal("WM_CHAR", loaded.AppPasteSettings["ttermpro.exe"]);
        Assert.Equal(1000, loaded.WindowWidth);
        Assert.Equal(700, loaded.WindowHeight);
        Assert.Equal(800, loaded.EditorWidth);
        Assert.Equal(600, loaded.EditorHeight);
        Assert.Equal(0.5, loaded.PaneRatio);
    }

    [Fact]
    public void JSONはcamelCaseキーで保存される()
    {
        var service = new SettingsService(_dir);
        service.Save(new AppSettings());

        var json = File.ReadAllText(Path.Combine(_dir, "settings.json"));

        Assert.Contains("\"memoFolder\"", json);
        Assert.Contains("\"hotkey\"", json);
        Assert.DoesNotContain("\"MemoFolder\"", json);
    }

    [Fact]
    public void 壊れたJSONの場合はSettingsCorruptedExceptionを投げる()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{not json");
        var service = new SettingsService(_dir);

        Assert.Throws<SettingsCorruptedException>(() => service.Load());
    }

    [Fact]
    public void 破損後にResetToDefaultでデフォルト値に再作成できる()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{not json");
        var service = new SettingsService(_dir);

        var settings = service.ResetToDefault();

        Assert.Equal("Ctrl+Space", settings.Hotkey);
        Assert.Equal("Ctrl+Space", new SettingsService(_dir).Load().Hotkey);
    }
}
