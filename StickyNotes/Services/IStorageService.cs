using StickyNotes.Models;

namespace StickyNotes.Services;

public interface IStorageService
{
    List<NoteItem> LoadNotes();
    void SaveNotes(List<NoteItem> notes);
    string LoadContent(NoteItem note);
    void SaveContent(NoteItem note, string markdown);
    string GetDataFolder();

    /// <summary>加载文件夹元数据（名称 + 面板可见性）</summary>
    List<FolderInfo> LoadFolders();
    /// <summary>保存文件夹元数据</summary>
    void SaveFolders(IEnumerable<FolderInfo> folders);
}
