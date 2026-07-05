using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TextDock.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace TextDock.Views;

public partial class EditorWindow : Window
{
    private readonly App _app;
    private readonly MemoRepository _repo;
    private string? _name;
    private readonly bool _isNew;
    private bool _dirty;
    private bool _createdOnDisk;

    /// <summary>1回でも保存に成功したかどうか。呼び出し側の一覧更新判定に使う。</summary>
    public bool Saved { get; private set; }
    public string? SavedName => Saved ? _name : null;

    public EditorWindow(App app, MemoRepository repo, string? name, bool isNew)
    {
        InitializeComponent();
        _app = app;
        _repo = repo;
        _name = name;
        _isNew = isNew;
        Title = name != null ? Loc.S("Editor_Title_Edit", name) : Loc.S("Editor_Title_New");
        Width = app.Settings.EditorWidth;
        Height = app.Settings.EditorHeight;

        if (!isNew && name != null)
        {
            Body.Text = repo.LoadText(name);
            _dirty = false;
        }
    }

    private void Body_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _dirty = true;
    }

    private bool TrySave()
    {
        if (_isNew && _name == null)
        {
            var dialog = new NameInputDialog { Owner = this };
            if (dialog.ShowDialog() != true)
                return false;
            _name = dialog.ResultName;
            Title = Loc.S("Editor_Title_Edit", _name);
        }

        try
        {
            if (_isNew && !_createdOnDisk)
            {
                _repo.Create(_name!, Body.Text);
                _createdOnDisk = true;
            }
            else
            {
                _repo.SaveText(_name!, Body.Text);
            }
        }
        catch (MemoValidationException ex)
        {
            MessageBox.Show(this, ex.Message, "TextDock", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, Loc.S("Editor_Err_SaveFailed", _name ?? "", ex.Message),
                "TextDock", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        _dirty = false;
        Saved = true;
        return true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (TrySave())
            Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TrySave();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_dirty)
        {
            var result = MessageBox.Show(this, Loc.S("Editor_Confirm_Save"), "TextDock",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            switch (result)
            {
                case MessageBoxResult.Yes:
                    if (!TrySave())
                        e.Cancel = true;
                    break;
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }

        if (!e.Cancel)
        {
            _app.Settings.EditorWidth = (int)Width;
            _app.Settings.EditorHeight = (int)Height;
            _app.SettingsService.Save(_app.Settings);
        }
        base.OnClosing(e);
    }
}
