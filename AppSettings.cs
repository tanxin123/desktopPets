using System;
using System.IO;
using System.Text.Json;

namespace Spider;

// 用户配置：上次选的宠物、是否启用喂食。存放在 %AppData%\DesktopPet\settings.json
public sealed class AppSettings
{
    public string Pet { get; set; } = "spider";      // "spider" 或 "roach"
    public bool FeedingEnabled { get; set; } = true;
    public bool AutoHideFullscreen { get; set; } = true;   // 前台有全屏应用时自动隐藏并暂停

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
                    if (s.Pet is not ("spider" or "roach" or "beetle" or "ant" or "centipede")) s.Pet = "spider";   // 非法值回退默认
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
