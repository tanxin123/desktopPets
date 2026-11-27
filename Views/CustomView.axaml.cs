using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media.Imaging;

namespace Spider.Views;

public partial class CustomView : UserControl
{
    public static readonly string ImagePath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopPet", "custom.png");

    public Canvas BodyCanvas => BodyRoot;
    public Control AntennaL => AntL;
    public Control AntennaR => AntR;
    public Line[][] Legs { get; }

    public CustomView()
    {
        InitializeComponent();
        var list = new Line[6][];
        int n = 0;
        foreach (var side in "LR")
            for (int i = 1; i <= 3; i++)
                list[n++] = "abcd".Select(c => this.FindControl<Line>($"{side}{i}{c}")!).ToArray();
        Legs = list;
    }

    // 每次切到自定义宠物时重新读图，改了 custom.png 不用重启；读不到就留空，只剩腿
    public bool ReloadImage()
    {
        try
        {
            if (!File.Exists(ImagePath)) { Pic.Source = null; return false; }
            using var fs = File.OpenRead(ImagePath);
            Pic.Source = new Bitmap(fs);
            return true;
        }
        catch { Pic.Source = null; return false; }
    }
}
