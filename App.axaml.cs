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
            desktop.MainWindow = win;
            RefreshMenu();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
