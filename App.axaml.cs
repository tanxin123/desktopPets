using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Spider;

public partial class App : Application
{
    readonly AppSettings _settings = AppSettings.Load();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    MainWindow? MainWin() =>
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;

    void OnPickSpider(object? sender, System.EventArgs e) => PickPet(false);
    void OnPickRoach(object? sender, System.EventArgs e) => PickPet(true);

    void PickPet(bool roach)
    {
        MainWin()?.SetPet(roach);
        _settings.Pet = roach ? "roach" : "spider";
        _settings.Save();
        RefreshMenu();
    }

    void OnToggleFeeding(object? sender, System.EventArgs e)
    {
        // 以配置为准取反，不依赖菜单项自身的勾选状态
        _settings.FeedingEnabled = !_settings.FeedingEnabled;
        MainWin()?.SetFeeding(_settings.FeedingEnabled);
        _settings.Save();
        RefreshMenu();
    }

    // 按当前配置刷新托盘菜单的勾选状态
    void RefreshMenu()
    {
        var menu = TrayIcon.GetIcons(this)?.FirstOrDefault()?.Menu;
        if (menu == null) return;
        foreach (var item in menu.Items.OfType<NativeMenuItem>())
        {
            switch (item.Header)
            {
                case "蜘蛛": item.IsChecked = _settings.Pet != "roach"; break;
                case "蟑螂": item.IsChecked = _settings.Pet == "roach"; break;
                case "启用喂食": item.IsChecked = _settings.FeedingEnabled; break;
            }
        }
    }

    void OnExitClick(object? sender, System.EventArgs e)
    {
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var win = new MainWindow();
            win.SetPet(_settings.Pet == "roach");        // 恢复上次选择的宠物
            win.SetFeeding(_settings.FeedingEnabled);    // 恢复喂食开关
            desktop.MainWindow = win;
            RefreshMenu();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
