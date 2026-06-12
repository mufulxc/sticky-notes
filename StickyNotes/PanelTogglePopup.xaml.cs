using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using StickyNotes.ViewModels;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;

namespace StickyNotes;

public partial class PanelTogglePopup : Popup
{
    private readonly MainViewModel _vm;

    private static readonly SolidColorBrush BgActive   = new(Color.FromRgb(0x3D, 0x3E, 0x75));
    private static readonly SolidColorBrush BgInactive = new(Color.FromRgb(0x2A, 0x2B, 0x45));
    private static readonly SolidColorBrush BdActive   = new(Color.FromRgb(0x7C, 0x5C, 0xFC));
    private static readonly SolidColorBrush BdInactive = new(Color.FromRgb(0x4A, 0x4B, 0x70));
    private static readonly SolidColorBrush FgActive   = new(Color.FromRgb(0xE8, 0xE8, 0xF0));
    private static readonly SolidColorBrush FgInactive = new(Color.FromRgb(0x88, 0x88, 0xA0));

    public PanelTogglePopup(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        vm.VisibilityChanged += RefreshState;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        RefreshState();
    }

    private void RefreshState()
    {
        SetButtonState(BtnLeft,   _vm.PanelL.IsVisible);
        SetButtonState(BtnCenter, _vm.PanelC.IsVisible);
        SetButtonState(BtnRight,  _vm.PanelR.IsVisible);
    }

    private static void SetButtonState(Button btn, bool active)
    {
        if (btn.Template.FindName("bd", btn) is Border bd)
        {
            bd.Background = active ? BgActive : BgInactive;
            bd.BorderBrush = active ? BdActive : BdInactive;
            if (bd.Child is TextBlock tb)
                tb.Foreground = active ? FgActive : FgInactive;
        }
    }

    private void BtnLeft_Click(object sender, RoutedEventArgs e)   { _vm.TogglePanel(0); IsOpen = false; }
    private void BtnCenter_Click(object sender, RoutedEventArgs e) { _vm.TogglePanel(1); IsOpen = false; }
    private void BtnRight_Click(object sender, RoutedEventArgs e)  { _vm.TogglePanel(2); IsOpen = false; }

    private void Popup_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
        => IsOpen = false;
}
