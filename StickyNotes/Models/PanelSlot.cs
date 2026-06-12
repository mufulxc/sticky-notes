using CommunityToolkit.Mvvm.ComponentModel;

namespace StickyNotes.Models;

/// <summary>
/// 单个便签面板的完整状态（左/中/右复用同一套逻辑）
/// </summary>
public partial class PanelSlot : ObservableObject
{
    public int Index { get; }

    [ObservableProperty]
    private NoteItem? _note;

    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private bool _isVisible = true;

    public PanelSlot(int index, bool visible)
    {
        Index = index;
        _isVisible = visible;
    }
}
