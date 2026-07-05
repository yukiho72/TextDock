using System.Windows;

namespace TextDock.Views;

public partial class NameInputDialog : Window
{
    public string ResultName => NameBox.Text.Trim();

    public NameInputDialog(string initialName = "")
    {
        InitializeComponent();
        NameBox.Text = initialName;
        NameBox.SelectAll();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
