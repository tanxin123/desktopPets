using Avalonia;
using System;

namespace Spider;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 单实例：拿不到全局互斥体说明已有一只蜘蛛在运行，直接退出
        using var mutex = new System.Threading.Mutex(true, "Global\\Spider.SingleInstance", out bool createdNew);
        if (!createdNew) return;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
