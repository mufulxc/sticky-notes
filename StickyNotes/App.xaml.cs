using Microsoft.Extensions.DependencyInjection;
using StickyNotes.Services;
using StickyNotes.ViewModels;
using System.Windows;

namespace StickyNotes;

public partial class App : System.Windows.Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        // 注册服务（单例）
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<ITrayService, TrayService>();

        // 注册 ViewModel（单例，整个应用只有一个实例）
        services.AddSingleton<MainViewModel>();

        // 注册窗口（单例）
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 从 DI 获取 MainWindow
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // 确保 WebView2 的用户数据文件夹设置在非提升权限下也能工作
        // （WebView2 在首次启动时会自动创建）
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider.Dispose();
        base.OnExit(e);
    }
}
