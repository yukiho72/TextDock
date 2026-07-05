using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using TextDock.Models;
using TextDock.Services;
using TextDock.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace TextDock;

public partial class App : Application
{
    private Mutex? _mutex;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;

    public SettingsService SettingsService { get; private set; } = null!;
    public AppSettings Settings { get; private set; } = null!;
    public HotkeyManager HotkeyManager { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 多重起動の防止（仕様書5章）
        _mutex = new Mutex(initiallyOwned: true, "TextDock_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        // ×ボタンで終了しないため、明示的な Shutdown のみで終了する
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TextDock");
        Directory.CreateDirectory(settingsDir);
        var isFirstRun = !File.Exists(Path.Combine(settingsDir, "settings.json"));

        SettingsService = new SettingsService(settingsDir);
        try
        {
            Settings = SettingsService.Load();
        }
        catch (SettingsCorruptedException)
        {
            var result = MessageBox.Show(
                "Settings file is corrupted. Reset to defaults?\n設定ファイルが破損しています。初期化しますか？",
                "TextDock", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                Shutdown();
                return;
            }
            Settings = SettingsService.ResetToDefault();
        }

        if (string.IsNullOrEmpty(Settings.Language))
        {
            Settings.Language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? "ja" : "en";
            SettingsService.Save(Settings);
        }
        LanguageService.Apply(Settings.Language);

        if (isFirstRun)
        {
            var memoFolder = SetupFirstRunFolder();
            if (memoFolder == null)
            {
                Shutdown();
                return;
            }
            SampleFiles.Create(memoFolder, Settings.Language);
            Settings.MemoFolder = memoFolder;
            Settings.RecentFolders.Insert(0, memoFolder);
            SettingsService.Save(Settings);
        }
        ThemeService.Apply(Settings);

        _mainWindow = new MainWindow(this);
        new WindowInteropHelper(_mainWindow).EnsureHandle();

        HotkeyManager.HotkeyPressed += () => _mainWindow.ShowFromHotkey();
        if (!TryRegisterHotkey(Settings.Hotkey))
        {
            MessageBox.Show(
                Loc.S("App_Err_HotkeyFailed", Settings.Hotkey),
                "TextDock", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        SetupNotifyIcon();

        if (isFirstRun)
            _mainWindow.ShowFromTray();
    }

    public bool TryRegisterHotkey(string hotkey)
    {
        if (_mainWindow == null || !HotkeyManager.TryParse(hotkey, out var mods, out var vk))
            return false;
        var hwnd = new WindowInteropHelper(_mainWindow).Handle;
        return HotkeyManager.Register(hwnd, mods, vk);
    }

    private void SetupNotifyIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Loc.S("App_Tray_Show"), null, (_, _) => _mainWindow?.ShowFromTray());
        menu.Items.Add(Loc.S("App_Tray_Settings"), null, (_, _) => _mainWindow?.OpenSettings());
        menu.Items.Add(Loc.S("App_Tray_OpenFolder"), null, (_, _) => OpenMemoFolder());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(Loc.S("App_Tray_Exit"), null, (_, _) => Shutdown());

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = CreateAppIcon(),
            Text = "TextDock",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                _mainWindow?.ToggleFromTray();
        };
    }

    private static Icon CreateAppIcon()
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            // 黄色の背景
            g.Clear(Color.Yellow);
            // 左の黒ページ
            g.FillRectangle(Brushes.Black, 2, 2, 5, 12);
            // 右の黒ページ
            g.FillRectangle(Brushes.Black, 9, 2, 5, 12);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static string? SetupFirstRunFolder()
    {
        var docsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TextDock");
        try
        {
            Directory.CreateDirectory(docsFolder);
            return docsFolder;
        }
        catch { }

        var localDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TextDock", "Data");
        try
        {
            Directory.CreateDirectory(localDataFolder);
            return localDataFolder;
        }
        catch { }

        MessageBox.Show(
            Loc.S("App_Err_FolderCreateFailed"),
            "TextDock", MessageBoxButton.OK, MessageBoxImage.Information);
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Loc.S("App_FolderDialog_Desc"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }

    private void OpenMemoFolder()
    {
        if (Directory.Exists(Settings.MemoFolder))
            Process.Start("explorer.exe", Settings.MemoFolder);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.Cleanup();
        _notifyIcon?.Dispose();
        HotkeyManager.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
