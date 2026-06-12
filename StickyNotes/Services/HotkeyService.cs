using System.Runtime.InteropServices;

namespace StickyNotes.Services;

public class HotkeyService : IHotkeyService
{
    public event Action? HotkeyPressed;

    private const int HOTKEY_ID = 9001;
    private const uint MOD_ALT = 0x0001;
    private const uint VK_2 = 0x32;
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public bool Register(IntPtr hwnd)
    {
        return RegisterHotKey(hwnd, HOTKEY_ID, MOD_ALT, VK_2);
    }

    public void Unregister(IntPtr hwnd)
    {
        UnregisterHotKey(hwnd, HOTKEY_ID);
    }

    /// <summary>
    /// 由 MainWindow 的 WndProc 调用，处理热键消息
    /// </summary>
    public bool HandleMessage(int msg)
    {
        if (msg == WM_HOTKEY)
        {
            HotkeyPressed?.Invoke();
            return true;
        }
        return false;
    }
}
