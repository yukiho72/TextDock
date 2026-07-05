using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TextDock.Services;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ComboBox = System.Windows.Controls.ComboBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace TextDock.Views;

public partial class SettingsWindow : Window
{
    private readonly App _app;
    private readonly Dictionary<string, string> _appPasteSettings;
    private string _colorBackground;
    private string _colorText;
    private string _colorSelection;

    public SettingsWindow(App app, string? initialAppName = null, string? initialTab = null)
    {
        InitializeComponent();
        _app = app;
        var s = app.Settings;
        _appPasteSettings = new Dictionary<string, string>(s.AppPasteSettings);
        _colorBackground = s.ColorBackground;
        _colorText = s.ColorText;
        _colorSelection = s.ColorSelection;

        foreach (ComboBoxItem item in LanguageCombo.Items)
            if ((string)item.Tag == s.Language) { LanguageCombo.SelectedItem = item; break; }

        MemoFolderBox.Text = s.MemoFolder;
        HotkeyBox.Text = s.Hotkey;
        MaxLinesBox.Text = s.MaxLines.ToString();
        MemoSearchPartialCheck.IsChecked = s.MemoSearchPartial;
        LineSearchPartialCheck.IsChecked = s.LineSearchPartial;

        SelectComboItem(ThemeCombo, s.Theme);
        FontCombo.ItemsSource = Fonts.SystemFontFamilies
            .Select(f => f.Source).OrderBy(n => n).ToList();
        FontCombo.SelectedItem = s.FontName;
        FontSizeBox.Text = s.FontSize.ToString();
        UpdateColorButtons();

        SelectComboItem(MethodCombo, s.DefaultPasteMethod);
        ProtectCheck.IsChecked = s.ClipboardProtection;
        PasteNewlineCheck.IsChecked = s.AppendNewlineOnPaste;
        PsNewlineCheck.IsChecked = s.AppendNewlineOnPowerShell;
        ClipDelayBox.Text = s.ClipboardDelay.ToString();
        WmDelayBox.Text = s.WmcharDelay.ToString();
        SiDelayBox.Text = s.SendinputDelay.ToString();
        AppMethodCombo.SelectedIndex = 0;
        RefreshAppSettingsList();

        if (!string.IsNullOrEmpty(initialAppName))
        {
            MainTabControl.SelectedItem = PasteTab;
            AppNameBox.Text = initialAppName;
        }
        else if (initialTab == "Paste")
        {
            MainTabControl.SelectedItem = PasteTab;
        }
    }

    private static void SelectComboItem(ComboBox combo, string value)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if ((string)item.Content == value)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static string ComboValue(ComboBox combo) =>
        (string)((ComboBoxItem)combo.SelectedItem).Content;

    // ---- General ----

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Loc.S("Settings_FolderDialog_Desc"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = MemoFolderBox.Text,
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            MemoFolderBox.Text = dialog.SelectedPath;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return;

        var parts = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        HotkeyBox.Text = string.Join("+", parts);
    }

    // ---- Appearance ----

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomColorGroup != null)
            CustomColorGroup.IsEnabled = ComboValue(ThemeCombo) == "Custom";
    }

    private void UpdateColorButtons()
    {
        BgColorButton.Content = _colorBackground;
        TextColorButton.Content = _colorText;
        SelColorButton.Content = _colorSelection;
        CustomColorGroup.IsEnabled = ComboValue(ThemeCombo) == "Custom";
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var current = (string)button.Content;

        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(current);
            dialog.Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B);
        }
        catch (FormatException) { }

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        if (button == BgColorButton) _colorBackground = hex;
        else if (button == TextColorButton) _colorText = hex;
        else _colorSelection = hex;
        button.Content = hex;
    }

    // ---- Paste ----

    private void RefreshAppSettingsList()
    {
        AppSettingsList.ItemsSource = _appPasteSettings
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => $"{kv.Key} → {kv.Value}")
            .ToList();
    }

    private void AddAppSetting_Click(object sender, RoutedEventArgs e)
    {
        var name = AppNameBox.Text.Trim();
        if (name.Length == 0)
            return;
        _appPasteSettings[name] = ComboValue(AppMethodCombo);
        AppNameBox.Text = "";
        RefreshAppSettingsList();
    }

    private void RemoveAppSetting_Click(object sender, RoutedEventArgs e)
    {
        if (AppSettingsList.SelectedItem is not string entry)
            return;
        var name = entry.Split(" → ")[0];
        _appPasteSettings.Remove(name);
        RefreshAppSettingsList();
    }

    // ---- OK ----

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(MaxLinesBox.Text, out var maxLines) || maxLines < 1)
        {
            MessageBox.Show(this, Loc.S("Settings_Err_MaxLines"),
                "TextDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(FontSizeBox.Text, out var fontSize) || fontSize < 6 || fontSize > 72)
        {
            MessageBox.Show(this, Loc.S("Settings_Err_FontSize"),
                "TextDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!TryParseRange(ClipDelayBox.Text, 50, 500, out var clipDelay) ||
            !TryParseRange(WmDelayBox.Text, 5, 200, out var wmDelay) ||
            !TryParseRange(SiDelayBox.Text, 5, 200, out var siDelay))
        {
            MessageBox.Show(this, Loc.S("Settings_Err_Timing"),
                "TextDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!HotkeyManager.TryParse(HotkeyBox.Text, out _, out _))
        {
            MessageBox.Show(this, Loc.S("Settings_Err_Hotkey"),
                "TextDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var s = _app.Settings;
        var oldHotkey = s.Hotkey;
        var oldFolder = s.MemoFolder;

        s.MemoFolder = MemoFolderBox.Text;
        if (!string.Equals(s.MemoFolder, oldFolder, StringComparison.OrdinalIgnoreCase))
        {
            s.RecentFolders.RemoveAll(f => string.Equals(f, s.MemoFolder, StringComparison.OrdinalIgnoreCase));
            s.RecentFolders.Insert(0, s.MemoFolder);
        }
        s.Hotkey = HotkeyBox.Text;
        s.MaxLines = maxLines;
        s.MemoSearchPartial = MemoSearchPartialCheck.IsChecked == true;
        s.LineSearchPartial = LineSearchPartialCheck.IsChecked == true;
        s.Theme = ComboValue(ThemeCombo);
        s.FontName = FontCombo.SelectedItem as string ?? s.FontName;
        s.FontSize = fontSize;
        s.ColorBackground = _colorBackground;
        s.ColorText = _colorText;
        s.ColorSelection = _colorSelection;
        s.DefaultPasteMethod = ComboValue(MethodCombo);
        s.ClipboardProtection = ProtectCheck.IsChecked == true;
        s.AppendNewlineOnPaste = PasteNewlineCheck.IsChecked == true;
        s.AppendNewlineOnPowerShell = PsNewlineCheck.IsChecked == true;
        s.ClipboardDelay = clipDelay;
        s.WmcharDelay = wmDelay;
        s.SendinputDelay = siDelay;
        s.AppPasteSettings = new Dictionary<string, string>(_appPasteSettings);

        s.Language = (string)((ComboBoxItem)LanguageCombo.SelectedItem).Tag;

        _app.SettingsService.Save(s);
        LanguageService.Apply(s.Language);
        ThemeService.Apply(s);

        // ホットキー再登録。失敗時は通知して旧キーの再登録を試みる（仕様書6章）
        if (s.Hotkey != oldHotkey && !_app.TryRegisterHotkey(s.Hotkey))
        {
            MessageBox.Show(this,
                Loc.S("Settings_Err_HotkeyConflict", s.Hotkey),
                "TextDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            s.Hotkey = oldHotkey;
            _app.SettingsService.Save(s);
            _app.TryRegisterHotkey(oldHotkey);
        }

        DialogResult = true;
    }

    private static bool TryParseRange(string text, int min, int max, out int value) =>
        int.TryParse(text, out value) && value >= min && value <= max;
}
