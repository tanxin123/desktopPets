using System;
using System.Diagnostics;

namespace Spider;

// 开机自启：注册一个“最高权限”的登录计划任务，这样登录时自动启动，并且不会弹 UAC。
// 注册 / 删除任务需要管理员权限，本程序的清单本来就要求管理员。
public static class AutoStart
{
    const string TaskName = "DesktopPet";

    public static bool Set(bool on)
    {
        try
        {
            if (on)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return false;
                // /TR 里的路径要再套一层引号，路径带空格也能用
                return Run($"/Create /TN \"{TaskName}\" /TR \"\\\"{exe}\\\"\" /SC ONLOGON /RL HIGHEST /F") == 0;
            }
            Run($"/Delete /TN \"{TaskName}\" /F");
            return !Exists();   // 任务本来就不存在也算成功
        }
        catch { return false; }
    }

    public static bool Exists()
    {
        try { return Run($"/Query /TN \"{TaskName}\"") == 0; }
        catch { return false; }
    }

    static int Run(string args)
    {
        var psi = new ProcessStartInfo("schtasks.exe", args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        p.StandardOutput.ReadToEnd();   // 读空输出，避免管道写满卡住
        p.StandardError.ReadToEnd();
        if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } return -1; }
        return p.ExitCode;
    }
}
