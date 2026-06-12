namespace StickyNotes.Services;

public interface ITrayService
{
    event Action? ShowRequested;
    event Action? ExitRequested;
    void Create();
    void Remove();
}
