using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StickyNotes.Models;
using StickyNotes.Services;

namespace StickyNotes.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IStorageService _storage;

    // ─── 文件夹 ──────────────────────────────

    [ObservableProperty]
    private ObservableCollection<FolderInfo> _folders = [];

    [ObservableProperty]
    private FolderInfo? _selectedFolder;

    // ─── 面板（索引数组，复用同一套逻辑）────

    public PanelSlot[] Panels { get; } =
    [
        new(0, true),
        new(1, true),
        new(2, true),
    ];

    public PanelSlot PanelL => Panels[0];
    public PanelSlot PanelC => Panels[1];
    public PanelSlot PanelR => Panels[2];

    // ─── 面板标签（含所属文件夹名）─────────

    public string PanelLabelL => $"{(SelectedFolder?.Name ?? "")} · ①";
    public string PanelLabelC => $"{(SelectedFolder?.Name ?? "")} · ②";
    public string PanelLabelR => $"{(SelectedFolder?.Name ?? "")} · ③";

    partial void OnSelectedFolderChanged(FolderInfo? value)
    {
        OnPropertyChanged(nameof(PanelLabelL));
        OnPropertyChanged(nameof(PanelLabelC));
        OnPropertyChanged(nameof(PanelLabelR));
    }

    // ─── 当前激活面板索引 ──────────────────

    [ObservableProperty]
    private int _activeIndex;

    // ─── 事件 ──────────────────────────────

    public event Action? VisibilityChanged;
    public event Action? FolderChanged;

    public MainViewModel(IStorageService storage)
    {
        _storage = storage;
        InitDefaultFolders();
        LoadFromDisk();
    }

    // ─── 文件夹命令 ─────────────────────────

    [RelayCommand]
    private void SwitchFolder(FolderInfo folder)
    {
        SaveCurrentNotes();
        SelectedFolder = folder;
        // 加载该文件夹的可见性设置
        for (int i = 0; i < 3; i++)
            Panels[i].IsVisible = folder.PanelVisible[i];
        LoadFromDisk();
        VisibilityChanged?.Invoke();
        FolderChanged?.Invoke();
    }

    public void AddFolder()
    {
        SaveCurrentNotes();
        var n = Folders.Count + 1;
        var name = $"文件夹{n}";
        while (Folders.Any(f => f.Name == name)) name = $"文件夹{++n}";

        var folder = new FolderInfo(name);
        Folders.Add(folder);
        SelectedFolder = folder;

        Notes = new ObservableCollection<NoteItem>();
        EnsureNotes();
        RefreshPanels();
        PersistFolders();
        FolderChanged?.Invoke();
    }

    public bool DeleteFolder(FolderInfo folder)
    {
        if (Folders.Count <= 1) return false;
        var all = _storage.LoadNotes();
        all.RemoveAll(n => n.Folder == folder.Name);
        _storage.SaveNotes(all);
        Folders.Remove(folder);
        PersistFolders();

        if (SelectedFolder == folder)
        {
            SelectedFolder = Folders.First();
            LoadFromDisk();
        }
        return true;
    }

    public bool RenameFolder(FolderInfo folder, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        if (folder.Name == newName) return true;
        if (Folders.Any(f => f.Name == newName)) return false;

        var oldName = folder.Name;
        folder.Name = newName;

        var all = _storage.LoadNotes();
        foreach (var n in all.Where(n => n.Folder == oldName))
            n.Folder = newName;
        _storage.SaveNotes(all);
        foreach (var n in Notes) n.Folder = newName;
        SaveToDisk();
        PersistFolders();
        return true;
    }

    // ─── 面板激活 ──────────────────────────

    public void Activate(int index)
    {
        ActiveIndex = index;
    }

    // ─── 面板可见性切换 ────────────────────

    public void TogglePanel(int index)
    {
        Panels[index].IsVisible = !Panels[index].IsVisible;
        if (SelectedFolder is not null)
            SelectedFolder.PanelVisible[index] = Panels[index].IsVisible;
        PersistFolders();
        VisibilityChanged?.Invoke();
    }

    // ─── 数据加载 ──────────────────────────

    [ObservableProperty]
    private ObservableCollection<NoteItem> _notes = [];

    /// <summary>
    /// 确保当前文件夹恰好有 3 条便签（PanelIndex 0/1/2 各一条）。
    /// 自动修复旧数据（重复 PanelIndex / 丢失 PanelIndex）。
    /// </summary>
    public void EnsureNotes()
    {
        var changed = false;

        // ── 第一步：修复旧数据（所有 PanelIndex 都为 0 或重复的情况）──
        var byPanel = new Dictionary<int, NoteItem>();
        var orphans = new List<NoteItem>();

        foreach (var note in Notes.ToList())
        {
            if (!byPanel.ContainsKey(note.PanelIndex))
                byPanel[note.PanelIndex] = note;
            else
                orphans.Add(note); // 重复 PanelIndex → 需要重新分配
        }

        // 给孤儿便签分配空缺的 PanelIndex
        foreach (var orphan in orphans)
        {
            var freeSlot = Enumerable.Range(0, 3).First(i => !byPanel.ContainsKey(i));
            orphan.PanelIndex = freeSlot;
            byPanel[freeSlot] = orphan;
            changed = true;
        }

        // ── 第二步：补齐缺失的 PanelIndex ──
        for (int i = 0; i < 3; i++)
        {
            if (!byPanel.ContainsKey(i))
            {
                var note = new NoteItem
                {
                    Folder = SelectedFolder?.Name ?? "文件夹1",
                    PanelIndex = i
                };
                Notes.Add(note);
                _storage.SaveContent(note, "");
                changed = true;
            }
        }

        if (changed)
            SaveToDisk();
    }

    public void SaveCurrentNotes()
    {
        for (int i = 0; i < 3; i++)
        {
            var slot = Panels[i];
            // 防御：如果用户输入了内容但没有 Note，自动创建
            if (slot.Note is null && !string.IsNullOrWhiteSpace(slot.Text))
            {
                slot.Note = new NoteItem
                {
                    Folder = SelectedFolder?.Name ?? "文件夹1",
                    PanelIndex = i
                };
                // 找到正确的位置插入 Notes
                if (!Notes.Any(n => n.PanelIndex == i))
                    Notes.Add(slot.Note);
                else
                    slot.Note.PanelIndex = i; // 绑定到已有便签
            }

            if (slot.Note is not null)
            {
                slot.Note.UpdatedAt = DateTime.Now;
                _storage.SaveContent(slot.Note, slot.Text);
            }
        }
        EnsureNotes();
        SaveToDisk();
    }

    private void InitDefaultFolders()
    {
        var saved = _storage.LoadFolders();
        if (saved.Count > 0)
        {
            Folders = new ObservableCollection<FolderInfo>(saved);
        }
        else
        {
            Folders = new ObservableCollection<FolderInfo>
            {
                new("文件夹1"), new("文件夹2"), new("文件夹3")
            };
        }
        SelectedFolder = Folders[0];
    }

    private void PersistFolders() => _storage.SaveFolders(Folders);

    private void LoadFromDisk()
    {
        var folderName = SelectedFolder?.Name ?? "文件夹1";
        var all = _storage.LoadNotes();
        var folderNotes = all
            .Where(n => n.Folder == folderName)
            .OrderBy(n => n.PanelIndex)
            .ThenBy(n => n.UpdatedAt)
            .ToList();

        Notes = new ObservableCollection<NoteItem>(folderNotes);
        EnsureNotes();    // 修复旧数据 + 补齐到 3 条
        RefreshPanels();  // 按 PanelIndex 匹配面板
    }

    private void RefreshPanels()
    {
        for (int i = 0; i < 3; i++)
        {
            var note = Notes.FirstOrDefault(n => n.PanelIndex == i);
            Panels[i].Note = note;
            Panels[i].Text = note is not null ? _storage.LoadContent(note) : "";
        }
    }

    private void SaveToDisk()
    {
        var folderName = SelectedFolder?.Name ?? "文件夹1";
        var all = _storage.LoadNotes();
        all.RemoveAll(n => n.Folder == folderName);
        all.AddRange(Notes);
        _storage.SaveNotes(all);
    }
}
