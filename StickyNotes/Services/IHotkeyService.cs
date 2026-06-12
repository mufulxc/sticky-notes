namespace StickyNotes.Services;

public interface IHotkeyService
{
    event Action? HotkeyPressed;
    bool Register(IntPtr hwnd);
    void Unregister(IntPtr hwnd);
    bool HandleMessage(int msg);
}
