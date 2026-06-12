using CommunityToolkit.Mvvm.ComponentModel;

namespace StickyNotes.Models;

/// <summary>
/// 文件夹信息：名称 + 3 面板可见性
/// </summary>
public partial class FolderInfo : ObservableObject
{
    [ObservableProperty]
    private string _name;

    /// <summary>3 个面板各自是否可见 [左, 中, 右]</summary>
    public bool[] PanelVisible { get; set; }

    public FolderInfo() : this("新建文件夹") { }

    public FolderInfo(string name, bool[]? visible = null)
    {
        _name = name;
        PanelVisible = visible ?? [true, true, true];
    }

    public override string ToString() => Name;
}
