using StickyNotes.Models;

namespace StickyNotes.Services;

public interface ISupabaseService
{
    /// <summary>上传一条笔记到云端（覆盖更新）</summary>
    Task UploadAsync(NoteItem note, string content, CancellationToken ct = default);

    /// <summary>拉取指定文件夹的所有笔记</summary>
    Task<List<(NoteItem note, string content)>> DownloadFolderAsync(string folder, CancellationToken ct = default);

    /// <summary>删除指定笔记</summary>
    Task DeleteAsync(string noteLocalId, CancellationToken ct = default);

    /// <summary>从云端加载所有笔记元数据</summary>
    Task<List<NoteItem>> ListAllAsync(CancellationToken ct = default);
}
