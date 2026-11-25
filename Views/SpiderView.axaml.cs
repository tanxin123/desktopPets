using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Spider.Views;

public partial class SpiderView : UserControl
{
    /// <summary>身体根节点（原点 = 头胸部与腹部连接处）。</summary>
    public Canvas BodyCanvas => BodyRoot;

    /// <summary>8 条腿 × 4 段，顺序：L1~L4、R1~R4。</summary>
    public Line[][] Legs { get; }

    public SpiderView()
    {
        InitializeComponent();
        var list = new Line[8][];
        int n = 0;
        foreach (var side in "LR")
            for (int i = 1; i <= 4; i++)
                list[n++] = "abcd".Select(c => this.FindControl<Line>($"{side}{i}{c}")!).ToArray();
        Legs = list;
    }
}