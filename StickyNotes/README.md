# 随手贴 (StickyNotes)

Windows 10/11 桌面富文本便签应用 — C# WPF + WebView2 + Quill.js

## 功能

| 功能 | 说明 |
|------|------|
| **富文本编辑器** | Quill.js（加粗、斜体、下划线、字体颜色、分隔线、链接） |
| **全局热键** | `Alt + 2` 显示/隐藏窗口 |
| **系统托盘** | 最小化到托盘，双击/右键菜单恢复或退出 |
| **多文件夹** | 支持新建、重命名、删除文件夹，数据按文件夹隔离 |
| **三栏面板** | 左/中/右三块独立便签，可自由开关每块面板 |
| **自动保存** | 输入 300ms 后自动保存，切文件夹/隐藏时强制保存 |
| **置顶窗口** | 始终在最前，无边框圆角（12px）设计 |

## 技术栈

```
.NET 10.0 (WPF)                     ← 窗口框架
Microsoft.Extensions.DependencyInjection  ← DI 容器
CommunityToolkit.Mvvm 8.4.0         ← MVVM 框架 + 源生成器
Microsoft.Web.WebView2 1.0.2903     ← 浏览器内核
Quill.js 1.3.7 (CDN)               ← 富文本编辑
P/Invoke (user32.dll)              ← 热键 / 窗口置顶
WinForms NotifyIcon                ← 系统托盘
```

## 项目结构

```
StickyNotes/
├── App.xaml / App.xaml.cs          # 应用程序入口 + DI 注册
├── MainWindow.xaml / .cs           # 主窗口（WindowChrome + 三栏布局）
├── StickyNotes.csproj              # .NET 10.0 项目文件
│
├── Models/
│   ├── NoteItem.cs                 # 便签数据模型（id / folder / fileName / updatedAt）
│   ├── PanelSlot.cs                # 单个面板状态封装（Note + Text + IsVisible + Index）
│   └── FolderInfo.cs               # 文件夹（Name + PanelVisible[3]）
│
├── ViewModels/
│   └── MainViewModel.cs            # 核心 VM（文件夹管理、面板切换、数据持久化）
│
├── Services/
│   ├── IStorageService.cs          # 存储接口
│   ├── StorageService.cs           # JSON 元数据 + .html 文件存储
│   ├── IHotkeyService.cs           # 热键接口
│   ├── HotkeyService.cs            # RegisterHotKey (Alt+2, ID=9001)
│   ├── ITrayService.cs             # 托盘接口
│   └── TrayService.cs              # NotifyIcon + 右键菜单
│
├── Converters/
│   ├── StringEqualityConverter.cs  # MultiBinding 字符串相等比较（激活标签高亮）
│   └── BoolToVisibility.cs         # bool ↔ Visibility 转换
│
├── Helpers/
│   └── QuillTemplate.cs            # Quill HTML 模板（工具栏 + 自定义 Blot + JS 桥接）
│
├── PanelTogglePopup.xaml / .cs     # 面板显隐切换弹窗（◀ ■ ▶）
├── FolderDeletePopup.xaml / .cs    # 文件夹删除弹窗
└── RenameDialog.xaml / .cs         # 文件夹重命名对话框
```

## 架构设计

### MVVM + DI

```
App.xaml.cs
  └─ ServiceCollection
       ├─ AddSingleton<IHotkeyService, HotkeyService>()
       ├─ AddSingleton<ITrayService, TrayService>()
       ├─ AddSingleton<IStorageService, StorageService>()
       ├─ AddSingleton<MainViewModel>()
       └─ AddSingleton<MainWindow>()
```

### 面板复用架构（v3）

三个面板共享同一套 `PanelSlot` 类，通过索引数组 `Panels[3]` 驱动：

```
MainViewModel.Panels[0] → PanelL  (左)    Tag="0"
MainViewModel.Panels[1] → PanelC  (中)    Tag="1"
MainViewModel.Panels[2] → PanelR  (右)    Tag="2"
```

每个 `PanelSlot` 封装：
- `Note` — 绑定的便签数据
- `Text` — HTML 内容（与 WebView2 双向同步）
- `IsVisible` — 可见性（绑定到 Border.Visibility）
- `Index` — 面板索引

PanelSlot.Text 变化 → PropertyChanged 事件 → MainWindow 推送到对应 WebView2
WebView2 内容变化 → JavaScript postMessage → MainWindow 更新 PanelSlot.Text

### 数据流

```
用户输入 → Quill text-change (debounce 300ms)
        → JS: window.chrome.webview.postMessage({type, html})
        → C#: WebMessageReceived → PanelSlot.Text = html
        → 切文件夹/隐藏 → SaveContent(note, html) → .html 文件
```

## 数据存储

位置：`%LocalAppData%\StickyNotes\`

```
StickyNotes/
├── notes.json       # 便签元数据（id, folder, fileName, updatedAt）
├── a1b2c3d4.html    # 每条的富文本 HTML 内容
├── e5f6g7h8.html
└── ...
```

## 操作说明

| 操作 | 方式 |
|------|------|
| 新建文件夹 | 点击标签栏 `+` 按钮 |
| 删除文件夹 | 点击标签栏 `−` 按钮，弹出列表选择删除 |
| 切换文件夹 | 点击文件夹标签 |
| 重命名文件夹 | **右键**文件夹标签 → 弹出重命名对话框 |
| 面板显隐 | **双击**文件夹标签 → 弹出 ◀ ■ ▶ 切换面板 |
| 激活面板 | 点击便签区域 / Ctrl+1,2,3 |
| 显示/隐藏 | `Alt + 2` 全局热键 |
| 退出 | 托盘右键 → 退出 |
| 手动保存 | `Ctrl + S` |
| 关闭弹窗 | `Esc` |

## 构建与运行

### 前置条件

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（Windows 10/11 通常已内置）

### 命令

```powershell
# 还原依赖
dotnet restore

# 构建
dotnet build -c Release

# 运行
dotnet run
```

### 发布

```powershell
dotnet publish -c Release -o .\publish
```

## 依赖

| 包 | 版本 | 用途 |
|----|------|------|
| Microsoft.Extensions.DependencyInjection | 8.0.1 | DI 容器 |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM + [ObservableProperty] / [RelayCommand] |
| Microsoft.Web.WebView2 | 1.0.2903.40 | WebView2 WPF 控件 |
| Quill.js | 1.3.7 (CDN) | 富文本编辑器 |
