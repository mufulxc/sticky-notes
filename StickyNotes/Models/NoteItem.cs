using CommunityToolkit.Mvvm.ComponentModel;

namespace StickyNotes.Models;

/// <summary>
/// 单条便签的数据模型（支持 MVVM 双向绑定）
/// </summary>
public partial class NoteItem : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    private string _folder = "文件夹1";

    [ObservableProperty]
    private string _fileName = "";

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.Now;

    /// <summary>面板索引（0=左, 1=中, 2=右），建立 Note→面板 的稳定映射</summary>
    [ObservableProperty]
    private int _panelIndex;

    /// <summary>标题（不持久化，仅用于 UI 显示）</summary>
    public string Title { get; set; } = "";
}
