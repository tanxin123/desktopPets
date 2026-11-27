using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Spider.Views;

public partial class LadybugView : UserControl
{
    /// <summary>身体根节点（原点 = 前胸背板与鞘翅连接处）。</summary>
    public Canvas BodyCanvas => BodyRoot;

    /// <summary>左右触角（零尺寸 Canvas，旋转它即绕根部摆动）。</summary>
    public Control AntennaL => AntL;
    public Control AntennaR => AntR;

    /// <summary>6 条腿 × 4 段，顺序：L1~L3、R1~R3。</summary>
    public Line[][] Legs { get; }

    public LadybugView()
    {
        InitializeComponent();
        var list = new Line[6][];
        int n = 0;
        foreach (var side in "LR")
            for (int i = 1; i <= 3; i++)
                list[n++] = "abcd".Select(c => this.FindControl<Line>($"{side}{i}{c}")!).ToArray();
        Legs = list;
    }
}
