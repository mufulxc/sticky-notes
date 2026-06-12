using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using StickyNotes.Models;

namespace StickyNotes.Services;

public class SupabaseService : ISupabaseService, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public SupabaseService()
    {
        // 从 supabase.json 读取密钥（不提交到 Git）
        var configPath = Path.Combine(AppContext.BaseDirectory, "supabase.json");
        SupabaseConfig config;
        if (File.Exists(configPath))
        {
            var configJson = File.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<SupabaseConfig>(configJson) ?? new();
        }
        else
        {
            config = new(); // 没有配置文件时静默降级，云端功能不可用
        }

        _baseUrl = config.Url ?? "";
        _http = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
        _http.DefaultRequestHeaders.Add("apikey", config.ServiceKey ?? "");
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ServiceKey ?? ""}");
        _http.DefaultRequestHeaders.Add("User-Agent", "StickyNotes/1.0");
    }

    public async Task UploadAsync(NoteItem note, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_baseUrl)) return;
        try
        {
            var body = new
            {
                note_local_id = note.Id,
                folder = note.Folder,
                panel_index = note.PanelIndex,
                content,
                title = note.Title,
                updated_at = DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(body, JsonOpts);
            var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/sticky_notes")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Prefer", "resolution=merge-duplicates");
            await _http.SendAsync(req, ct);
        }
        catch { /* 网络不通 / 密钥无效时静默忽略 */ }
    }

    public async Task<List<(NoteItem note, string content)>> DownloadFolderAsync(string folder, CancellationToken ct = default)
    {
        var result = new List<(NoteItem, string)>();
        if (string.IsNullOrEmpty(_baseUrl)) return result;
        try
        {
            var url = $"/rest/v1/sticky_notes?folder=eq.{Uri.EscapeDataString(folder)}&order=panel_index.asc";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return result;

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<List<SupabaseNote>>(json, JsonOpts) ?? [];
            foreach (var item in items)
            {
                var note = new NoteItem
                {
                    Id = item.note_local_id ?? "",
                    Folder = item.folder ?? folder,
                    PanelIndex = item.panel_index ?? 0,
                    Title = item.title ?? "",
                    UpdatedAt = item.updated_at ?? DateTime.MinValue,
                    FileName = $"{item.note_local_id ?? ""}.html"
                };
                result.Add((note, item.content ?? ""));
            }
        }
        catch { }
        return result;
    }

    public async Task<List<NoteItem>> ListAllAsync(CancellationToken ct = default)
    {
        var result = new List<NoteItem>();
        if (string.IsNullOrEmpty(_baseUrl)) return result;
        try
        {
            var url = "/rest/v1/sticky_notes?select=note_local_id,folder,panel_index,title,updated_at,id";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return result;

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<List<SupabaseNote>>(json, JsonOpts) ?? [];
            foreach (var item in items)
            {
                result.Add(new NoteItem
                {
                    Id = item.note_local_id ?? "",
                    Folder = item.folder ?? "",
                    PanelIndex = item.panel_index ?? 0,
                    Title = item.title ?? "",
                    UpdatedAt = item.updated_at ?? DateTime.MinValue,
                    FileName = $"{item.note_local_id ?? ""}.html"
                });
            }
        }
        catch { }
        return result;
    }

    public async Task DeleteAsync(string noteLocalId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_baseUrl)) return;
        try
        {
            var url = $"/rest/v1/sticky_notes?note_local_id=eq.{Uri.EscapeDataString(noteLocalId)}";
            await _http.DeleteAsync(url, ct);
        }
        catch { }
    }

    public void Dispose() => _http.Dispose();

    private class SupabaseConfig
    {
        public string? Url { get; set; }
        public string? AnonKey { get; set; }
        public string? ServiceKey { get; set; }
    }

    private class SupabaseNote
    {
        public string? note_local_id { get; set; }
        public string? folder { get; set; }
        public int? panel_index { get; set; }
        public string? content { get; set; }
        public string? title { get; set; }
        public DateTime? updated_at { get; set; }
    }
}
