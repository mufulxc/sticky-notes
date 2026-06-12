using System.Windows;
using System.Windows.Input;

namespace StickyNotes;

public partial class RenameDialog : Window
{
    public string NewName { get; set; }

    public RenameDialog(string currentName)
    {
        NewName = currentName;
        DataContext = this;
        InitializeComponent();
        NameBox.SelectAll();
        NameBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NewName))
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void NameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (!string.IsNullOrWhiteSpace(NewName))
                DialogResult = true;
        }
        if (e.Key == Key.Escape)
            DialogResult = false;
    }
}
