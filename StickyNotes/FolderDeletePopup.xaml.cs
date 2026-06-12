using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using StickyNotes.ViewModels;
using Button = System.Windows.Controls.Button;

namespace StickyNotes;

public partial class FolderDeletePopup : Popup
{
    private readonly MainViewModel _vm;

    public FolderDeletePopup(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        FolderList.ItemsSource = _vm.Folders.ToList();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Models.FolderInfo folder)
        {
            if (_vm.DeleteFolder(folder))
                IsOpen = false;
        }
    }
}
