using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using TextDock.Services;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using Screen = System.Windows.Forms.Screen;

namespace TextDock.Views;

public partial class MainWindow : Window
{
    private delegate void WinEventProc(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventProc lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint MONITOR_DEFAULTTONEAREST = 0x0002;
    private const int MDT_EFFECTIVE_DPI = 0;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private readonly App _app;
    private readonly PasteService _pasteService = new();
    private readonly WinEventProc _winEventProc;
    private IntPtr _winEventHook;
    private MemoRepository _repo;
    private FileSystemWatcher? _watcher;
    private List<string> _allMemos = new();
    private List<string> _allLines = new();
    private TargetWindow? _target;
    private readonly System.Windows.Threading.DispatcherTimer _targetCheckTimer;
    private string? _rightClickedMemo;
    private bool _hoverEnabled = true;
    private bool _initializing = true;

    public MainWindow(App app)
    {
        InitializeComponent();
        _app = app;
        _repo = new MemoRepository(app.Settings.MemoFolder);

        Width = app.Settings.WindowWidth;
        Height = app.Settings.WindowHeight;
        CloseAfterPasteCheck.IsChecked = app.Settings.CloseAfterPaste;
        ApplyPaneRatio(app.Settings.PaneRatio);

        UpdateFolderButton();
        ReloadMemoList(selectFirst: true);
        _initializing = false;

        _winEventProc = OnForegroundChanged;
        _winEventHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

        StartWatcher(_app.Settings.MemoFolder);

        _targetCheckTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _targetCheckTimer.Tick += (_, _) =>
        {
            if (_target != null && !IsWindow(_target.Hwnd))
            {
                _target = null;
                UpdateStatus();
            }
        };
    }

    // ---- 表示制御 ----

    public void ShowFromHotkey()
    {
        if (IsVisible)
        {
            HideWindow();
            return;
        }
        _target = TargetWindow.Capture();
        _hoverEnabled = true;
        ShowAtCursor();
    }

    public void ShowFromTray()
    {
        _target = null;
        _hoverEnabled = true;
        ShowAtCursor();
    }

    public void ToggleFromTray()
    {
        if (IsVisible)
            HideWindow();
        else
            ShowFromTray();
    }

    public void OpenSettings(string? initialAppName = null, string? initialTab = null)
    {
        var window = new SettingsWindow(_app, initialAppName, initialTab);
        if (IsVisible)
            window.Owner = this;
        if (window.ShowDialog() == true)
        {
            _repo = new MemoRepository(_app.Settings.MemoFolder);
            UpdateFolderButton();
            ReloadMemoList(selectFirst: true);
            UpdateStatus();
        }
    }

    private void ChangeMode_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings(_target?.ProcessName, initialTab: "Paste");
    }

    private void ShowAtCursor()
    {
        // マウスカーソル付近へ表示し、画面外は自動補正する（仕様書23章）。
        // PerMonitorV2 環境ではモニターごとにDPIが異なるため、物理ピクセルで
        // 直接配置する。Width/Height(DIP) を移動先モニターのDPIで物理サイズへ換算する。
        var pos = System.Windows.Forms.Cursor.Position;
        var area = Screen.FromPoint(pos).WorkingArea;

        var monitor = MonitorFromPoint(new POINT { X = pos.X, Y = pos.Y }, MONITOR_DEFAULTTONEAREST);
        double scaleX = 1.0, scaleY = 1.0;
        if (GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY) == 0)
        {
            scaleX = dpiX / 96.0;
            scaleY = dpiY / 96.0;
        }

        var physW = (int)Math.Round(Width * scaleX);
        var physH = (int)Math.Round(Height * scaleY);

        var left = pos.X + 8;
        var top = pos.Y + 8;
        if (left + physW > area.Right) left = area.Right - physW;
        if (top + physH > area.Bottom) top = area.Bottom - physH;
        left = Math.Max(area.Left, left);
        top = Math.Max(area.Top, top);

        Show();
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, IntPtr.Zero, left, top, physW, physH, SWP_NOZORDER | SWP_NOACTIVATE);
        Activate();
        MemoSearchBox.Focus();
        UpdateStatus();
        _targetCheckTimer.Start();
    }

    private void HideWindow()
    {
        _targetCheckTimer.Stop();
        SaveLayout();
        Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // ×ボタンでは終了せずタスクトレイへ格納する（仕様書5章）
        e.Cancel = true;
        HideWindow();
    }

    private void SaveLayout()
    {
        _app.Settings.WindowWidth = (int)Width;
        _app.Settings.WindowHeight = (int)Height;
        var total = LeftCol.ActualWidth + RightCol.ActualWidth;
        if (total > 0)
            _app.Settings.PaneRatio = LeftCol.ActualWidth / total;
        _app.SettingsService.Save(_app.Settings);
    }

    private void ApplyPaneRatio(double ratio)
    {
        LeftCol.Width = new GridLength(ratio, GridUnitType.Star);
        RightCol.Width = new GridLength(1 - ratio, GridUnitType.Star);
    }

    // ---- メモ一覧（左ペイン） ----

    private void ReloadMemoList(bool selectFirst = false, string? selectName = null)
    {
        ClearError();
        try
        {
            _allMemos = _repo.ListMemos();
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException)
        {
            _allMemos = new List<string>();
            ShowError(Loc.S("Main_Err_FolderNotFound", _app.Settings.MemoFolder));
        }

        var previous = selectName ?? MemoList.SelectedItem as string;
        ApplyMemoFilter();

        if (previous != null && MemoList.Items.Contains(previous))
            MemoList.SelectedItem = previous;
        else if (selectFirst && MemoList.Items.Count > 0)
            MemoList.SelectedIndex = 0;
    }

    private void ApplyMemoFilter()
    {
        var query = MemoSearchBox.Text;
        var filtered = string.IsNullOrEmpty(query)
            ? _allMemos
            : _allMemos.Where(m => _app.Settings.MemoSearchPartial
                ? ContainsAll(m, query)
                : m.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();
        var previous = MemoList.SelectedItem as string;
        MemoList.ItemsSource = filtered;
        if (previous != null && filtered.Contains(previous))
            MemoList.SelectedItem = previous;
        else if (filtered.Count > 0)
            MemoList.SelectedIndex = 0;
    }

    private void MemoSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initializing)
            ApplyMemoFilter();
    }

    private void MemoSearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Enter)
        {
            MemoList.Focus();
            FocusListItem(MemoList, MemoList.SelectedIndex >= 0 ? MemoList.SelectedIndex : 0);
            e.Handled = true;
        }
    }

    private void MemoItem_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_hoverEnabled)
            return;
        ((ListBoxItem)sender).IsSelected = true;
    }

    private void MemoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadLines();
    }

    private void MemoList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _hoverEnabled = false;
    }

    private void MemoList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _hoverEnabled = false;
        if (FindListBoxItem(e.OriginalSource) is { Content: string name } item)
        {
            item.IsSelected = true;
            _rightClickedMemo = name;
        }
    }

    private void MemoList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindListBoxItem(e.OriginalSource) is { Content: string name })
            EditMemo(name);
    }

    private void MemoList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Enter で右ペインへフォーカス移動（仕様書10章）
            LineList.Focus();
            if (LineList.Items.Count > 0 && LineList.SelectedIndex < 0)
                LineList.SelectedIndex = 0;
            FocusListItem(LineList, LineList.SelectedIndex >= 0 ? LineList.SelectedIndex : 0);
            e.Handled = true;
        }
        else if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (MemoList.SelectedItem is string name)
                EditMemo(name);
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            if (MemoList.SelectedItem is string name)
                RenameMemo(name);
            e.Handled = true;
        }
    }

    // ---- 定型文一覧（右ペイン） ----

    private void LoadLines()
    {
        ClearError();
        _allLines = new List<string>();
        if (MemoList.SelectedItem is string name)
        {
            try
            {
                _allLines = _repo.LoadLines(name, _app.Settings.MaxLines);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException)
            {
                ShowError(Loc.S("Main_Err_FileLoadFailed", name));
            }
        }
        ApplyLineFilter();
    }

    private void ApplyLineFilter()
    {
        var query = LineSearchBox.Text;
        var filtered = string.IsNullOrEmpty(query)
            ? _allLines
            : _allLines.Where(l => _app.Settings.LineSearchPartial
                ? ContainsAll(l, query)
                : l.StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();
        LineList.ItemsSource = filtered.Select(l => new LineItem(l)).ToList();
        UpdateStatus();
    }

    private class LineItem
    {
        public string Text { get; }
        public LineItem(string text) => Text = text;
        public override string ToString() => Text;
    }

    private void LineSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initializing)
            ApplyLineFilter();
    }

    private void LineList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStatus();
    }

    private void LineList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PasteLines(SelectedLinesInDisplayOrder());
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopySelectedLines();
            e.Handled = true;
        }
        else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
        {
            PasteLines(SelectedLinesInDisplayOrder());
            e.Handled = true;
        }
    }

    private void LineList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindListBoxItem(e.OriginalSource) is { Content: LineItem li })
            PasteLines(new[] { li.Text });
    }

    private void CopyMenuItem_Click(object sender, RoutedEventArgs e) => CopySelectedLines();

    private void PasteMenuItem_Click(object sender, RoutedEventArgs e) =>
        PasteLines(SelectedLinesInDisplayOrder());

    private void SendToPowerShell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string exe })
            return;
        var lines = SelectedLinesInDisplayOrder();
        if (lines.Count == 0)
            return;
        var text = string.Join("; ", lines);
        Clipboard.SetDataObject(text, true);
        var sendKeys = _app.Settings.AppendNewlineOnPowerShell ? "'^v{ENTER}'" : "'^v'";
        var script =
            "Add-Type -AssemblyName System.Windows.Forms; " +
            "$null = Register-EngineEvent -SourceIdentifier PowerShell.OnIdle -MaxTriggerCount 1 -Action { " +
            "if (-not $global:__mlDone) { $global:__mlDone = $true; " +
            $"[System.Windows.Forms.SendKeys]::SendWait({sendKeys}) }} }}";
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"-NoExit -EncodedCommand {encoded}",
            UseShellExecute = true,
        });
    }

    private void ToggleFocusedLine()
    {
        if (LineList.IsKeyboardFocusWithin &&
            Keyboard.FocusedElement is ListBoxItem item && item.Content is LineItem)
        {
            item.IsSelected = !item.IsSelected;
        }
    }

    private List<string> SelectedLinesInDisplayOrder()
    {
        var selected = LineList.SelectedItems.Cast<LineItem>().ToHashSet();
        return ((IEnumerable<LineItem>)LineList.ItemsSource)
            .Where(selected.Contains)
            .Select(li => li.Text)
            .ToList();
    }

    private void CopySelectedLines()
    {
        var lines = SelectedLinesInDisplayOrder();
        if (lines.Count == 0)
            return;
        Clipboard.SetDataObject(string.Join("\n", lines), true);
    }

    // ---- 貼り付け ----

    private void PasteLines(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            return;
        if (_target == null)
        {
            ShowError(Loc.S("Main_Err_NoTarget"));
            return;
        }

        var closeAfter = CloseAfterPasteCheck.IsChecked == true;
        if (closeAfter)
            HideWindow();

        try
        {
            var text = string.Join("\n", lines);
            if (_app.Settings.AppendNewlineOnPaste)
                text += "\n";
            _pasteService.Paste(_target, text, _app.Settings);
        }
        catch (Exception ex)
        {
            ShowError(Loc.S("Main_Err_PasteFailed", ex.Message));
            if (closeAfter)
                Show();
        }
        // チェックOFF時はウィンドウを開いたまま、フォーカスは対象アプリに留まる（仕様書12章）
        _hoverEnabled = true;
    }

    // ---- フォルダ切り替え ----

    private void FolderButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu
        {
            FontFamily = (System.Windows.Media.FontFamily)FindResource("AppFontFamily"),
            FontSize = (double)FindResource("AppFontSize"),
        };
        var current = _app.Settings.MemoFolder;

        var allFolders = new List<string> { current };
        foreach (var f in _app.Settings.RecentFolders)
            if (!string.Equals(f, current, StringComparison.OrdinalIgnoreCase) && !allFolders.Contains(f))
                allFolders.Add(f);

        foreach (var folder in allFolders)
        {
            var isCurrent = string.Equals(folder, current, StringComparison.OrdinalIgnoreCase);
            var item = new MenuItem
            {
                Header = Path.GetFileName(folder),
                Tag = folder,
                IsChecked = isCurrent,
            };
            // 存在しないフォルダは無効化せずグレー表示に留める。
            // 無効化するとマウスイベントが届かず、右クリックでの履歴削除ができなくなるため。
            if (!Directory.Exists(folder))
                item.Foreground = System.Windows.Media.Brushes.Gray;
            item.Click += FolderMenuItem_Click;
            if (!isCurrent)
                item.PreviewMouseRightButtonUp += FolderMenuItem_RightClick;
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var browse = new MenuItem { Header = Loc.S("Main_Ctx_BrowseFolder") };
        browse.Click += BrowseFolder_Click;
        menu.Items.Add(browse);

        menu.PlacementTarget = FolderButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
    }

    private void FolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string path })
            return;
        if (Directory.Exists(path))
        {
            SwitchFolder(path);
            return;
        }
        // 現在のフォルダは履歴削除の対象にしない（切断ドライブの再接続待ちの可能性がある）
        if (string.Equals(path, _app.Settings.MemoFolder, StringComparison.OrdinalIgnoreCase))
            return;
        var result = System.Windows.MessageBox.Show(
            Loc.S("Main_Confirm_RemoveMissingFolder", Path.GetFileName(path)),
            "TextDock", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _app.Settings.RecentFolders.RemoveAll(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
            _app.SettingsService.Save(_app.Settings);
        }
    }

    private void FolderMenuItem_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not MenuItem { Tag: string path } item)
            return;
        var topMenu = item.Parent as System.Windows.Controls.ContextMenu;
        topMenu?.SetValue(System.Windows.Controls.ContextMenu.IsOpenProperty, false);
        var result = System.Windows.MessageBox.Show(
            Loc.S("Main_Confirm_RemoveHistory", Path.GetFileName(path)),
            "TextDock", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _app.Settings.RecentFolders.RemoveAll(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
            _app.SettingsService.Save(_app.Settings);
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = Loc.S("Main_FolderDialog_Desc"),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = _app.Settings.MemoFolder,
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            SwitchFolder(dialog.SelectedPath);
    }

    public void SwitchFolder(string path)
    {
        _app.Settings.MemoFolder = path;

        var recent = _app.Settings.RecentFolders;
        recent.RemoveAll(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, path);
        _app.SettingsService.Save(_app.Settings);

        _repo = new MemoRepository(path);
        StartWatcher(path);
        UpdateFolderButton();
        ReloadMemoList(selectFirst: true);
    }

    private void UpdateFolderButton()
    {
        FolderButton.Content = "\U0001F4C1 " + Path.GetFileName(_app.Settings.MemoFolder);
    }

    // ---- ボタン操作 ----

    private void New_Click(object sender, RoutedEventArgs e)
    {
        var editor = new EditorWindow(_app, _repo, name: null, isNew: true) { Owner = this };
        editor.ShowDialog();
        if (editor.Saved)
            ReloadMemoList(selectName: editor.SavedName);
    }

    private void ContextEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_rightClickedMemo is string name)
            EditMemo(name);
    }

    private void ContextDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_rightClickedMemo is string name)
            DeleteMemo(name);
    }

    private void ContextRename_Click(object sender, RoutedEventArgs e)
    {
        if (_rightClickedMemo is string name)
            RenameMemo(name);
    }

    private void ContextClipCreate_Click(object sender, RoutedEventArgs e)
    {
        CreateFromClipboard();
    }

    private void ContextClipAppend_Click(object sender, RoutedEventArgs e)
    {
        if (MemoList.SelectedItem is not string name)
        {
            CreateFromClipboard();
            return;
        }
        var text = GetClipboardText();
        if (text == null)
            return;
        try
        {
            _repo.AppendText(name, text);
            LoadLines();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(Loc.S("Main_Err_AppendFailed", ex.Message));
        }
    }

    private void CreateFromClipboard()
    {
        var text = GetClipboardText();
        if (text == null)
            return;
        var name = _repo.NextPasteName();
        try
        {
            _repo.SaveText(name, text);
            ReloadMemoList(selectName: name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(Loc.S("Main_Err_CreateFailed", ex.Message));
        }
    }

    private static string? GetClipboardText()
    {
        if (!Clipboard.ContainsText())
            return null;
        var text = Clipboard.GetText();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private void EditMemo(string name)
    {
        var editor = new EditorWindow(_app, _repo, name, isNew: false) { Owner = this };
        editor.ShowDialog();
        if (editor.Saved)
            LoadLines();
    }

    private void DeleteMemo(string name)
    {
        var result = MessageBox.Show(this, Loc.S("Main_Confirm_Delete", name), "TextDock",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _repo.Delete(name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(Loc.S("Main_Err_DeleteFailed", name));
            return;
        }
        ReloadMemoList(selectFirst: true);
    }

    private void RenameMemo(string name)
    {
        var dialog = new NameInputDialog(name) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ResultName == name)
            return;

        try
        {
            _repo.Rename(name, dialog.ResultName);
        }
        catch (MemoValidationException ex)
        {
            MessageBox.Show(this, ex.Message, "TextDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(Loc.S("Main_Err_RenameFailed", name));
            return;
        }
        ReloadMemoList(selectName: dialog.ResultName);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        // 外部エディタ等での直接変更を反映する手動更新（仕様書3章）
        ReloadMemoList();
        LoadLines();
    }

    private void CloseAfterPasteCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing)
            return;
        _app.Settings.CloseAfterPaste = CloseAfterPasteCheck.IsChecked == true;
        _app.SettingsService.Save(_app.Settings);
    }

    // ---- ステータスエリア ----

    private void UpdateStatus()
    {
        var targetName = _target?.ProcessName ?? Loc.S("Main_Status_NoTarget");
        var mode = PasteService.ResolveMethod(_target, _app.Settings);
        var count = LineList.SelectedItems.Count;
        StatusText.Text = $"Target : {targetName}    Mode : {mode}"
            + (count > 0 ? $"    {Loc.S("Main_Status_RowsSelected", count)}" : "");
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void ClearError()
    {
        ErrorText.Visibility = Visibility.Collapsed;
    }

    // ---- 共通 ----

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideWindow();
            e.Handled = true;
        }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            New_Click(this, e);
            e.Handled = true;
        }
        else if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            Refresh_Click(this, e);
            e.Handled = true;
        }
        else if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control)
        {
            FolderButton_Click(this, e);
            e.Handled = true;
        }
        else if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ChangeMode_Click(this, e);
            e.Handled = true;
        }
    }

    private static void FocusListItem(ListBox list, int index)
    {
        if (index >= 0 && index < list.Items.Count &&
            list.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item)
        {
            item.Focus();
        }
    }

    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!IsVisible)
            return;

        var captured = TargetWindow.Capture();
        if (captured != null)
        {
            _target = captured;
            ClearError();
        }
        else if (_target != null && !IsWindow(_target.Hwnd))
        {
            _target = null;
        }
        UpdateStatus();
    }

    private System.Windows.Threading.DispatcherTimer? _watcherDebounce;

    private void StartWatcher(string folder)
    {
        _watcher?.Dispose();
        _watcher = null;
        if (!Directory.Exists(folder))
            return;
        _watcher = new FileSystemWatcher(folder, "*.txt")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += OnFileChanged;
        _watcher.Changed += OnFileChanged;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _watcherDebounce?.Stop();
            _watcherDebounce = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300),
            };
            _watcherDebounce.Tick += (_, _) =>
            {
                _watcherDebounce.Stop();
                ReloadMemoList();
                LoadLines();
            };
            _watcherDebounce.Start();
        });
    }

    public void Cleanup()
    {
        _watcher?.Dispose();
        if (_winEventHook != IntPtr.Zero)
        {
            UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
    }

    private static bool ContainsAll(string text, string query)
    {
        var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return keywords.All(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static ListBoxItem? FindListBoxItem(object source)
    {
        var element = source as DependencyObject;
        while (element != null && element is not ListBoxItem)
            element = VisualTreeHelper.GetParent(element);
        return element as ListBoxItem;
    }
}
