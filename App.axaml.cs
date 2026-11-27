using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

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

    static string HeaderOf(object? sender) => (sender as NativeMenuItem)?.Header ?? "";

    // 保存配置并刷新菜单勾选
    void Commit()
    {
        _settings.Save();
        RefreshMenu();
    }

    // ───────── 选宠物 ─────────
    void OnPickSpider(object? sender, EventArgs e) => PickPet("spider");
    void OnPickRoach(object? sender, EventArgs e) => PickPet("roach");
    void OnPickBeetle(object? sender, EventArgs e) => PickPet("beetle");
    void OnPickAnt(object? sender, EventArgs e) => PickPet("ant");
    void OnPickCentipede(object? sender, EventArgs e) => PickPet("centipede");
    void OnPickLadybug(object? sender, EventArgs e) => PickPet("ladybug");
    void OnPickCustom(object? sender, EventArgs e) => PickPet("custom");

    void PickPet(string pet)
    {
        MainWin()?.SetPet(pet);
        _settings.Pet = pet;
        UpdateTrayIcon();
        Commit();
    }

    // 托盘图标跟着宠物走；图标文件缺失就保持原样
    void UpdateTrayIcon()
    {
        try
        {
            var tray = TrayIcon.GetIcons(this)?.FirstOrDefault();
            if (tray == null) return;
            using var s = AssetLoader.Open(new Uri($"avares://DesktopPet/Assets/{_settings.Pet}.png"));
            tray.Icon = new WindowIcon(s);
        }
        catch { }
    }

    // ───────── 大小 / 速度 / 活动范围 / 退出快捷键 ─────────
    void OnPickSize(object? sender, EventArgs e)
    {
        _settings.Size = HeaderOf(sender) switch { "小" => "small", "大" => "large", _ => "medium" };
        MainWin()?.SetSize(_settings.Size);
        Commit();
    }

    void OnPickSpeed(object? sender, EventArgs e)
    {
        _settings.Speed = HeaderOf(sender) switch { "慢速" => "slow", "快速" => "fast", _ => "normal" };
        MainWin()?.SetSpeed(_settings.Speed);
        Commit();
    }

    void OnPickMonitor(object? sender, EventArgs e)
    {
        string h = HeaderOf(sender);
        _settings.Monitor = h.StartsWith("显示器 ") && int.TryParse(h["显示器 ".Length..], out int n) ? n : 0;
        MainWin()?.SetMonitor(_settings.Monitor);
        Commit();
    }

    void OnPickHotkey(object? sender, EventArgs e)
    {
        string h = HeaderOf(sender);
        if (h.Length == 0) return;
        _settings.ExitHotkey = h;
        MainWin()?.SetHotkey(h);
        Commit();
    }

    // ───────── 开关 ─────────
    void OnToggleFeeding(object? sender, EventArgs e)
    {
        // 以配置为准取反，不依赖菜单项自身的勾选状态
        _settings.FeedingEnabled = !_settings.FeedingEnabled;
        MainWin()?.SetFeeding(_settings.FeedingEnabled);
        Commit();
    }

    void OnToggleFollow(object? sender, EventArgs e)
    {
        _settings.FollowMouse = !_settings.FollowMouse;
        MainWin()?.SetFollow(_settings.FollowMouse);
        Commit();
    }

    void OnToggleAutoStart(object? sender, EventArgs e)
    {
        bool want = !_settings.AutoStart;
        if (AutoStart.Set(want)) _settings.AutoStart = want;   // 注册计划任务失败就保持原状态，菜单勾选会弹回去
        Commit();
    }

    void OnToggleAutoHide(object? sender, EventArgs e)
    {
        _settings.AutoHideFullscreen = !_settings.AutoHideFullscreen;
        MainWin()?.SetAutoHide(_settings.AutoHideFullscreen);
        Commit();
    }

    // 按当前配置刷新托盘菜单的勾选状态（子菜单递归处理）
    void RefreshMenu()
    {
        var menu = TrayIcon.GetIcons(this)?.FirstOrDefault()?.Menu;
        if (menu != null) Apply(menu);
    }

    void Apply(NativeMenu menu)
    {
        foreach (var item in menu.Items.OfType<NativeMenuItem>())
        {
            switch (item.Header)
            {
                case "蜘蛛": item.IsChecked = _settings.Pet == "spider"; break;
                case "蟑螂": item.IsChecked = _settings.Pet == "roach"; break;
                case "甲虫": item.IsChecked = _settings.Pet == "beetle"; break;
                case "蚂蚁": item.IsChecked = _settings.Pet == "ant"; break;
                case "蜈蚣": item.IsChecked = _settings.Pet == "centipede"; break;
                case "瓢虫": item.IsChecked = _settings.Pet == "ladybug"; break;
                case "自定义": item.IsChecked = _settings.Pet == "custom"; break;
                case "小": item.IsChecked = _settings.Size == "small"; break;
                case "中": item.IsChecked = _settings.Size == "medium"; break;
                case "大": item.IsChecked = _settings.Size == "large"; break;
                case "慢速": item.IsChecked = _settings.Speed == "slow"; break;
                case "正常": item.IsChecked = _settings.Speed == "normal"; break;
                case "快速": item.IsChecked = _settings.Speed == "fast"; break;
                case "所有显示器": item.IsChecked = _settings.Monitor == 0; break;
                case "显示器 1": item.IsChecked = _settings.Monitor == 1; break;
                case "显示器 2": item.IsChecked = _settings.Monitor == 2; break;
                case "显示器 3": item.IsChecked = _settings.Monitor == 3; break;
                case "启用喂食": item.IsChecked = _settings.FeedingEnabled; break;
                case "全屏时自动隐藏": item.IsChecked = _settings.AutoHideFullscreen; break;
                case "跟随鼠标": item.IsChecked = _settings.FollowMouse; break;
                case "开机自启": item.IsChecked = _settings.AutoStart; break;
                case "自动检查更新": item.IsChecked = _settings.AutoCheckUpdate; break;
                case "音效": item.IsChecked = _settings.SoundEnabled; break;
                default:
                    // 退出快捷键的各个预设项，标题就是快捷键本身
                    if (item.Header is string h && h.Contains('+'))
                        item.IsChecked = string.Equals(h, _settings.ExitHotkey, StringComparison.OrdinalIgnoreCase);
                    break;
            }
            if (item.Menu != null) Apply(item.Menu);
        }
    }

    // ───────── 检查更新 ─────────
    void OnToggleAutoUpdate(object? sender, EventArgs e)
    {
        _settings.AutoCheckUpdate = !_settings.AutoCheckUpdate;
        Commit();
    }

    async void OnCheckUpdate(object? sender, EventArgs e) => await CheckUpdate(true);

    async Task DelayedCheck()
    {
        await Task.Delay(8000);   // 启动后稍等一会儿再查，不拖慢启动
        await CheckUpdate(false);
    }

    static void Notice(string title, string msg) => new NoticeWindow(title, msg, ("确定", null)).Show();

    // manual = true：用户手动点的，无论结果如何都给反馈；false：启动时自动查，只在有新版本且没被忽略时才提示
    async Task CheckUpdate(bool manual)
    {
        var r = await UpdateChecker.CheckAsync();   // await 之后回到 UI 线程，可以直接开窗口
        if (r == null)
        {
            if (manual) Notice("检查更新", "无法连接到 GitHub，请检查网络后再试。");
            return;
        }
        var (latest, tag, url) = r.Value;
        var cur = UpdateChecker.Current;
        if (latest <= cur)
        {
            if (manual) Notice("检查更新", $"已是最新版本（v{cur.Major}.{cur.Minor}.{cur.Build}）。");
            return;
        }
        if (!manual && _settings.IgnoredVersion == tag) return;
        new NoticeWindow("发现新版本", $"桌面宠物有新版本 {tag}，当前是 v{cur.Major}.{cur.Minor}.{cur.Build}。",
            ("前往下载", () => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })),
            ("忽略此版本", () => { _settings.IgnoredVersion = tag; _settings.Save(); }),
            ("稍后", null)).Show();
    }

    void OnToggleSound(object? sender, EventArgs e)
    {
        _settings.SoundEnabled = !_settings.SoundEnabled;
        SoundFx.Enabled = _settings.SoundEnabled;
        Commit();
    }

    // ───────── 设置窗口 ─────────
    SettingsWindow? _settingsWin;

    void OnOpenSettings(object? sender, EventArgs e)
    {
        if (_settingsWin != null) { _settingsWin.Activate(); return; }   // 已经打开就只置前，不重复开
        _settingsWin = new SettingsWindow(_settings, OnSettingChanged);
        _settingsWin.Closed += (_, _) => _settingsWin = null;
        _settingsWin.Show();
    }

    // 设置窗口已经改好了 _settings，这里只负责应用到宠物，再保存并刷新托盘菜单勾选
    void OnSettingChanged(string key)
    {
        var w = MainWin();
        switch (key)
        {
            case "pet": w?.SetPet(_settings.Pet); UpdateTrayIcon(); break;
            case "size": w?.SetSize(_settings.Size); break;
            case "speed": w?.SetSpeed(_settings.Speed); break;
            case "monitor": w?.SetMonitor(_settings.Monitor); break;
            case "hotkey": w?.SetHotkey(_settings.ExitHotkey); break;
            case "feeding": w?.SetFeeding(_settings.FeedingEnabled); break;
            case "hide": w?.SetAutoHide(_settings.AutoHideFullscreen); break;
            case "follow": w?.SetFollow(_settings.FollowMouse); break;
            case "sound": SoundFx.Enabled = _settings.SoundEnabled; break;
            case "autostart":
                if (!AutoStart.Set(_settings.AutoStart)) _settings.AutoStart = AutoStart.Exists();   // 注册失败就以系统里的实际状态为准
                break;
        }
        Commit();
    }

    void OnExitClick(object? sender, EventArgs e)
    {
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var win = new MainWindow();
            win.SetPet(_settings.Pet);                       // 恢复上次选择的宠物
            win.SetSize(_settings.Size);
            win.SetSpeed(_settings.Speed);
            win.SetMonitor(_settings.Monitor);
            win.SetHotkey(_settings.ExitHotkey);
            win.SetFeeding(_settings.FeedingEnabled);
            win.SetAutoHide(_settings.AutoHideFullscreen);
            win.SetFollow(_settings.FollowMouse);
            SoundFx.Enabled = _settings.SoundEnabled;
            _settings.AutoStart = AutoStart.Exists();        // 开机自启以系统里计划任务的实际状态为准
            desktop.MainWindow = win;
            UpdateTrayIcon();
            RefreshMenu();
            if (_settings.AutoCheckUpdate) _ = DelayedCheck();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
