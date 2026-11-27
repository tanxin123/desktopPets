using System;
using System.IO;
using System.Text.Json;

namespace Spider;

// 用户配置，存放在 %AppData%\DesktopPet\settings.json
public sealed class AppSettings
{
    public string Pet { get; set; } = "spider";            // spider / roach / beetle / ant / centipede
    public string Size { get; set; } = "medium";           // small / medium / large
    public string Speed { get; set; } = "normal";          // slow / normal / fast
    public int Monitor { get; set; } = 0;                  // 0 = 所有显示器；n = 第 n 块显示器（按从左到右排序）
    public string ExitHotkey { get; set; } = "Ctrl+Shift+Q";   // 退出快捷键，例如 Ctrl+Alt+Q、Ctrl+Shift+F12
    public bool FeedingEnabled { get; set; } = true;
    public bool AutoStart { get; set; } = false;           // 开机自启（计划任务）
    public bool FollowMouse { get; set; } = false;         // 跟随鼠标
    public bool AutoHideFullscreen { get; set; } = true;   // 前台有全屏应用时自动隐藏并暂停
    public bool SoundEnabled { get; set; } = false;        // 音效（吃东西、冲刺），默认关闭
    public bool AutoCheckUpdate { get; set; } = true;      // 启动后自动检查 GitHub 上的新版本
    public string IgnoredVersion { get; set; } = "";       // 用户选择“忽略此版本”的版本号，自动检查时不再提示

    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopPet");
    static readonly string FilePath = Path.Combine(Dir, "settings.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (s != null)
                {
                    // 非法值一律回退默认
                    if (s.Pet is not ("spider" or "roach" or "beetle" or "ant" or "centipede" or "ladybug" or "custom")) s.Pet = "spider";
                    if (s.Size is not ("small" or "medium" or "large")) s.Size = "medium";
                    if (s.Speed is not ("slow" or "normal" or "fast")) s.Speed = "normal";
                    if (s.Monitor < 0) s.Monitor = 0;
                    if (string.IsNullOrWhiteSpace(s.ExitHotkey)) s.ExitHotkey = "Ctrl+Shift+Q";
                    return s;
                }
            }
        }
        catch { /* 文件损坏就用默认值，下次保存时覆盖 */ }

        var d = new AppSettings();
        d.Save();   // 首次运行生成默认配置文件
        return d;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opt));
        }
        catch { /* 写不了配置不影响运行 */ }
    }
}
