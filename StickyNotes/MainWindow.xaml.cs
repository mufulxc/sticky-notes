using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using StickyNotes.Helpers;
using StickyNotes.Services;
using StickyNotes.ViewModels;
using Button = System.Windows.Controls.Button;

namespace StickyNotes;

public partial class MainWindow : Window
{
    private readonly IHotkeyService _hotkey;
    private readonly ITrayService _tray;
    private readonly MainViewModel _vm;
    private HwndSource? _hwndSource;
    private PanelTogglePopup? _togglePopup;
    private FolderDeletePopup? _deletePopup;
    private CoreWebView2Environment? _wv2Env;
    private readonly TaskCompletionSource _leftReady = new();
    private readonly TaskCompletionSource _centerReady = new();
    private readonly TaskCompletionSource _rightReady = new();
    private WebView2[] _wvs = [];
    private TaskCompletionSource[] _readys = [];

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int WM_HOTKEY = 0x0312;

    public MainWindow(IHotkeyService hotkey, ITrayService tray, MainViewModel vm)
    {
        _hotkey = hotkey;
        _tray = tray;
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _hwndSource?.AddHook(WndProc);

        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkey.Register(hwnd);

        _tray.Create();
        _tray.ShowRequested += () => ShowWindow();
        _tray.ExitRequested += () => System.Windows.Application.Current.Shutdown();

        _hotkey.HotkeyPressed += () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (IsVisible && IsActive) HideWindow();
                else ShowWindow();
            });
        };

        _vm.VisibilityChanged += UpdateColumnLayout;

        // 订阅内容变更：仅在切换文件夹 / 初始加载时推送到 WebView2
        // 不监听 PropertyChanged，避免用户每输入一个字符就触发回路推送
        _wvs = [EditorLeft, EditorCenter, EditorRight];
        _readys = [_leftReady, _centerReady, _rightReady];

        // 文件夹切换后强制推送所有面板内容到 WebView2
        _vm.FolderChanged += () =>
        {
            for (int i = 0; i < 3; i++)
                _ = SetWv2Content(_wvs[i], _vm.Panels[i].Text, _readys[i]);
        };

        // 全部初始化（无论可见与否，否则后续显示时是空白）
        await InitWebView2s();

        // 推送构造阶段加载的内容到 WebView2（构造时 CoreWebView2 尚未就绪）
        for (int i = 0; i < 3; i++)
            _ = SetWv2Content(_wvs[i], _vm.Panels[i].Text, _readys[i]);

        UpdateColumnLayout();
    }

    // ─── WebView2 初始化（并行 + 事件驱动） ──

    private async Task InitWebView2s()
    {
        try
        {
            _wv2Env = await CoreWebView2Environment.CreateAsync();

            var names = new[] { "left", "center", "right" };
            var wvs = new[] { EditorLeft, EditorCenter, EditorRight };
            var readys = new[] { _leftReady, _centerReady, _rightReady };
            var tasks = new List<Task>();
            for (int i = 0; i < 3; i++)
                tasks.Add(InitOneWv2(wvs[i], names[i], readys[i]));

            await Task.WhenAll(tasks);
        }
        catch
        {
            // WebView2 未安装时静默回退
        }
    }

    private async Task InitOneWv2(WebView2 wv, string side, TaskCompletionSource ready)
    {
        var navTcs = new TaskCompletionSource();
        wv.NavigationCompleted += (s, e) => navTcs.TrySetResult();

        await wv.EnsureCoreWebView2Async(_wv2Env);
        wv.CoreWebView2.Settings.IsScriptEnabled = true;
        wv.NavigateToString(QuillTemplate.GetHtml());

        // 等页面加载完成再标记就绪
        await navTcs.Task;
        await Task.Delay(200); // Quill JS 初始化缓冲
        ready.TrySetResult();

        wv.CoreWebView2.WebMessageReceived += (s, e) =>
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);
                var type = json.RootElement.GetProperty("type").GetString();
                if (type == "contentChanged")
                {
                    var html = json.RootElement.GetProperty("html").GetString() ?? "";
                    Dispatcher.Invoke(() =>
                    {
                        var idx = side switch { "left" => 0, "center" => 1, _ => 2 };
                        _vm.Panels[idx].Text = html;
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[WebView2] {ex.Message}"); }
        };
    }

    private static async Task SetWv2Content(WebView2 wv, string html, TaskCompletionSource ready)
    {
        if (wv.CoreWebView2 is null) return;
        await ready.Task; // 等 Quill 就绪
        var escaped = System.Text.Json.JsonSerializer.Serialize(string.IsNullOrEmpty(html) ? "" : html);
        await wv.CoreWebView2.ExecuteScriptAsync($"setContent({escaped})");
    }

    // ─── 窗口显示/隐藏 ─────────────────────

    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);

        var wvs = new[] { EditorLeft, EditorCenter, EditorRight };
        if ((uint)_vm.ActiveIndex < 3) wvs[_vm.ActiveIndex].Focus();
    }

    public void HideWindow()
    {
        _vm.SaveCurrentNotes();
        Hide();
    }

    // ─── 列宽自动调整 ─────────────────────

    private const double SPLITTER_W = 6;

    private void UpdateColumnLayout()
    {
        var l = _vm.PanelL.IsVisible;
        var c = _vm.PanelC.IsVisible;
        var r = _vm.PanelR.IsVisible;
        var n = (l ? 1 : 0) + (c ? 1 : 0) + (r ? 1 : 0);
        if (n == 0) { l = true; n = 1; }

        ColLeft.Width = new(0); ColSplit1.Width = new(0);
        ColCenter.Width = new(0); ColSplit2.Width = new(0);
        ColRight.Width = new(0);

        if (n == 1)
        {
            if (l) ColLeft.Width = Star();
            else if (c) ColCenter.Width = Star();
            else ColRight.Width = Star();
        }
        else if (n == 2)
        {
            if (l && c) { ColLeft.Width = Star(); ColSplit1.Width = Split(); ColCenter.Width = Star(); }
            else if (l && r) { ColLeft.Width = Star(); ColRight.Width = Star(); }
            else { ColCenter.Width = Star(); ColSplit2.Width = Split(); ColRight.Width = Star(); }
        }
        else
        {
            ColLeft.Width = Star(); ColSplit1.Width = Split();
            ColCenter.Width = Star(); ColSplit2.Width = Split();
            ColRight.Width = Star();
        }
    }

    private static GridLength Star() => new(1, GridUnitType.Star);
    private static GridLength Split() => new(SPLITTER_W);

    // ─── 标题栏 ────────────────────────────

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnHide_Click(object sender, RoutedEventArgs e) => HideWindow();
    private void BtnClose_Click(object sender, RoutedEventArgs e) => HideWindow();
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    // ─── 面板激活（Tag 存索引，复用同一套逻辑） ──

    private void Panel_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string s && int.TryParse(s, out var i))
        { _vm.Activate(i); }
    }
    private void Editor_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string s && int.TryParse(s, out var i))
            _vm.Activate(i);
    }

    // ─── 图标面板弹窗 ──────────────────────

    private void Tab_PanelToggle(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button btn) return;
        e.Handled = true;
        _togglePopup ??= new PanelTogglePopup(_vm);
        _togglePopup.PlacementTarget = btn;
        _togglePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        _togglePopup.HorizontalOffset = 0; _togglePopup.VerticalOffset = 2;
        _togglePopup.IsOpen = false; _togglePopup.IsOpen = true;
        Dispatcher.BeginInvoke(new Action(() => Mouse.AddPreviewMouseDownHandler(this, ClosePopupOnClick)));
    }

    private void ClosePopupOnClick(object sender, MouseButtonEventArgs e)
    {
        Mouse.RemovePreviewMouseDownHandler(this, ClosePopupOnClick);
        if (_togglePopup is not null) _togglePopup.IsOpen = false;
        if (_deletePopup is not null) _deletePopup.IsOpen = false;
    }

    // ─── + / - 文件夹 ─────────────────────

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e) => _vm.AddFolder();

    private void BtnDelFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        _deletePopup ??= new FolderDeletePopup(_vm);
        _deletePopup.PlacementTarget = btn;
        _deletePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        _deletePopup.HorizontalOffset = -80; _deletePopup.VerticalOffset = 4;
        _deletePopup.IsOpen = false; _deletePopup.IsOpen = true;
        Dispatcher.BeginInvoke(new Action(() => Mouse.AddPreviewMouseDownHandler(this, ClosePopupOnClick)));
    }

    // ─── Tab 双击重命名 ───────────────────

    private void Tab_Rename(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not Models.FolderInfo folder) return;
        var oldName = folder.Name;
        e.Handled = true;
        var dialog = new RenameDialog(oldName) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewName))
            _vm.RenameFolder(folder, dialog.NewName.Trim());
    }

    // ─── WndProc ───────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY) { _hotkey.HandleMessage(msg); handled = true; return IntPtr.Zero; }
        return IntPtr.Zero;
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _vm.SaveCurrentNotes();
        _hotkey.Unregister(new WindowInteropHelper(this).Handle);
        _tray.Remove();
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) { HideWindow(); e.Handled = true; }
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { _vm.SaveCurrentNotes(); e.Handled = true; }
    }
}
