using StickyNotes.Models;

namespace StickyNotes.Services;

public interface IStorageService
{
    List<NoteItem> LoadNotes();
    void SaveNotes(List<NoteItem> notes);
    string LoadContent(NoteItem note);
    void SaveContent(NoteItem note, string markdown);
    string GetDataFolder();
}
