using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Spider;

// 设置窗口：纯代码构建。每改一项就更新 AppSettings，并回调 onChanged(key)，由 App 负责应用到宠物、保存和刷新菜单
public sealed class SettingsWindow : Window
{
    public SettingsWindow(AppSettings s, Action<string> onChanged)
    {
        Title = "桌面宠物 设置";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };

        void Row(string label, Control c)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*") };
            g.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(c, 1);
            g.Children.Add(c);
            panel.Children.Add(g);
        }

        ComboBox Combo(string[] names, string[] keys, string cur, Action<string> set, string key)
        {
            var cb = new ComboBox { ItemsSource = names, HorizontalAlignment = HorizontalAlignment.Stretch };
            cb.SelectedIndex = Math.Max(0, Array.IndexOf(keys, cur));
            cb.SelectionChanged += (_, _) =>
            {
                if (cb.SelectedIndex < 0) return;
                set(keys[cb.SelectedIndex]);
                onChanged(key);
            };
            return cb;
        }

        void Check(string text, bool cur, Action<bool> set, string key)
        {
            var c = new CheckBox { Content = text, IsChecked = cur };
            c.IsCheckedChanged += (_, _) => { set(c.IsChecked == true); onChanged(key); };
            panel.Children.Add(c);
        }

        Row("宠物", Combo(new[] { "蜘蛛", "蟑螂", "甲虫", "蚂蚁", "蜈蚣", "瓢虫", "自定义" },
            new[] { "spider", "roach", "beetle", "ant", "centipede", "ladybug", "custom" }, s.Pet, v => s.Pet = v, "pet"));
        Row("大小", Combo(new[] { "小", "中", "大" }, new[] { "small", "medium", "large" }, s.Size, v => s.Size = v, "size"));
        Row("移动速度", Combo(new[] { "慢速", "正常", "快速" }, new[] { "slow", "normal", "fast" }, s.Speed, v => s.Speed = v, "speed"));
        Row("活动范围", Combo(new[] { "所有显示器", "显示器 1", "显示器 2", "显示器 3" },
            new[] { "0", "1", "2", "3" }, s.Monitor.ToString(), v => s.Monitor = int.Parse(v), "monitor"));

        var hk = new TextBox { Text = s.ExitHotkey, Watermark = "例如 Ctrl+Shift+Q" };
        hk.LostFocus += (_, _) =>
        {
            string t = (hk.Text ?? "").Trim();
            if (t.Length == 0 || t == s.ExitHotkey) return;
            s.ExitHotkey = t;
            onChanged("hotkey");
        };
        Row("退出快捷键", hk);

        Check("启用喂食（按住 Ctrl 点击放食物）", s.FeedingEnabled, v => s.FeedingEnabled = v, "feeding");
        Check("全屏时自动隐藏", s.AutoHideFullscreen, v => s.AutoHideFullscreen = v, "hide");
        Check("跟随鼠标", s.FollowMouse, v => s.FollowMouse = v, "follow");
        Check("开机自启", s.AutoStart, v => s.AutoStart = v, "autostart");
        Check("音效（吃东西、冲刺）", s.SoundEnabled, v => s.SoundEnabled = v, "sound");
        Check("启动时自动检查更新", s.AutoCheckUpdate, v => s.AutoCheckUpdate = v, "update");

        var close = new Button { Content = "关闭", HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        panel.Children.Add(close);
        Content = panel;
    }
}
