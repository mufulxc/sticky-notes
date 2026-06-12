using System.IO;
using System.Text.Json;
using StickyNotes.Models;

namespace StickyNotes.Services;

public class StorageService : IStorageService
{
    private readonly string _dataFolder;
    private readonly string _metaFile;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public StorageService()
    {
        _dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StickyNotes");
        _metaFile = Path.Combine(_dataFolder, "notes.json");
        Directory.CreateDirectory(_dataFolder);
    }

    public string GetDataFolder() => _dataFolder;

    public List<NoteItem> LoadNotes()
    {
        if (!File.Exists(_metaFile)) return CreateDefaultNotes();

        try
        {
            var json = File.ReadAllText(_metaFile);
            return JsonSerializer.Deserialize<List<NoteItem>>(json) ?? CreateDefaultNotes();
        }
        catch
        {
            return CreateDefaultNotes();
        }
    }

    public void SaveNotes(List<NoteItem> notes)
    {
        var json = JsonSerializer.Serialize(notes, JsonOpts);
        File.WriteAllText(_metaFile, json);
    }

    public string LoadContent(NoteItem note)
    {
        var path = GetFilePath(note);
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    public void SaveContent(NoteItem note, string markdown)
    {
        note.FileName = $"{note.Id}.html";
        note.UpdatedAt = DateTime.Now;
        File.WriteAllText(GetFilePath(note), markdown);
    }

    private string GetFilePath(NoteItem note)
        => Path.Combine(_dataFolder, string.IsNullOrEmpty(note.FileName) ? $"{note.Id}.md" : note.FileName);

    private List<NoteItem> CreateDefaultNotes()
    {
        var notes = new List<NoteItem>
        {
            new()
            {
                Title = "欢迎使用便利贴",
                Folder = "文件夹1",
                FileName = "welcome.md"
            }
        };
        SaveNotes(notes);
        SaveContent(notes[0], SampleContent);
        return notes;
    }

    private const string SampleContent = """
<p><strong>欢迎使用 随手贴 ✨</strong></p><p>这是一个<strong>富文本</strong>桌面便利贴应用。</p><p>你可以：</p><ul><li>使用顶部<strong>工具栏</strong>设置文字样式</li><li>改变<strong><span style="color: rgb(124, 92, 252);">字体颜色</span></strong>和<strong><span style="font-size: 24px;">字号</span></strong></li><li>插入分隔线</li><li>链接会自动识别 https://example.com</li></ul><hr/><p>开始记录你的想法吧！</p>
""";
}
