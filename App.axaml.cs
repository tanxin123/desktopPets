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

    void OnPickSpider(object? sender, System.EventArgs e) => PickPet("spider");
    void OnPickRoach(object? sender, System.EventArgs e) => PickPet("roach");
    void OnPickBeetle(object? sender, System.EventArgs e) => PickPet("beetle");
    void OnPickAnt(object? sender, System.EventArgs e) => PickPet("ant");
    void OnPickCentipede(object? sender, System.EventArgs e) => PickPet("centipede");

    void PickPet(string pet)
    {
        MainWin()?.SetPet(pet);
        _settings.Pet = pet;
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

    void OnToggleFollow(object? sender, System.EventArgs e)
    {
        _settings.FollowMouse = !_settings.FollowMouse;
        MainWin()?.SetFollow(_settings.FollowMouse);
        _settings.Save();
        RefreshMenu();
    }

    void OnToggleAutoStart(object? sender, System.EventArgs e)
    {
        bool want = !_settings.AutoStart;
        if (AutoStart.Set(want)) _settings.AutoStart = want;   // 注册计划任务失败就保持原状态，菜单勾选会弹回去
        _settings.Save();
        RefreshMenu();
    }

    void OnToggleAutoHide(object? sender, System.EventArgs e)
    {
        _settings.AutoHideFullscreen = !_settings.AutoHideFullscreen;
        MainWin()?.SetAutoHide(_settings.AutoHideFullscreen);
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
                case "蜘蛛": item.IsChecked = _settings.Pet == "spider"; break;
                case "甲虫": item.IsChecked = _settings.Pet == "beetle"; break;
                case "蚂蚁": item.IsChecked = _settings.Pet == "ant"; break;
                case "蜈蚣": item.IsChecked = _settings.Pet == "centipede"; break;
                case "蟑螂": item.IsChecked = _settings.Pet == "roach"; break;
                case "启用喂食": item.IsChecked = _settings.FeedingEnabled; break;
                case "全屏时自动隐藏": item.IsChecked = _settings.AutoHideFullscreen; break;
                case "跟随鼠标": item.IsChecked = _settings.FollowMouse; break;
                case "开机自启": item.IsChecked = _settings.AutoStart; break;
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
            win.SetPet(_settings.Pet);        // 恢复上次选择的宠物
            win.SetFeeding(_settings.FeedingEnabled);
            win.SetAutoHide(_settings.AutoHideFullscreen);   // 恢复全屏自动隐藏开关    // 恢复喂食开关
            win.SetFollow(_settings.FollowMouse);        // 恢复跟随鼠标开关
            _settings.AutoStart = AutoStart.Exists();    // 开机自启以系统里计划任务的实际状态为准
            desktop.MainWindow = win;
            RefreshMenu();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
