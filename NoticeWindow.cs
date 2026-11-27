using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Spider;

// 简单的提示窗口：一段文字加若干按钮，点任意按钮后执行回调并关闭。纯代码构建，不需要 axaml
public sealed class NoticeWindow : Window
{
    public NoticeWindow(string title, string message, params (string Text, Action? OnClick)[] buttons)
    {
        Title = title;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 18 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var (text, act) in buttons)
        {
            var b = new Button { Content = text };
            b.Click += (_, _) => { act?.Invoke(); Close(); };
            row.Children.Add(b);
        }
        panel.Children.Add(row);
        Content = panel;
    }
}
