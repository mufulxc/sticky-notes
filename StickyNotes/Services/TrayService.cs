using System.Drawing;
using System.Runtime.InteropServices;

namespace StickyNotes.Services;

public class TrayService : ITrayService, IDisposable
{
    public event Action? ShowRequested;
    public event Action? ExitRequested;

    private NotifyIcon? _notifyIcon;

    public void Create()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "随手贴",
            Icon = SystemIcons.Application,
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("显示窗口", null, (s, e) => ShowRequested?.Invoke());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (s, e) =>
        {
            ExitRequested?.Invoke();
            Application.Exit();
        });

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ShowRequested?.Invoke();
    }

    public void Remove()
    {
        _notifyIcon?.Dispose();
        _notifyIcon = null;
    }

    public void Dispose() => Remove();
}
