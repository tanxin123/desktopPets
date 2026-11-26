using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace Spider.Views;

public partial class CentipedeView : UserControl
{
    /// <summary>体节数 = 腿的对数。</summary>
    public const int Pairs = 18;

    /// <summary>头的根节点（原点 = 颈部）。</summary>
    public Canvas BodyCanvas => BodyRoot;

    /// <summary>左右触角（零尺寸 Canvas，旋转它即绕根部摆动）。</summary>
    public Control AntennaL => AntL;
    public Control AntennaR => AntR;

    /// <summary>18 对腿 × 4 段，顺序：左侧 18 条（头到尾），再右侧 18 条。</summary>
    public Line[][] Legs { get; }

    /// <summary>体节（头到尾）。每个都是零尺寸 Canvas，原点 = 体节中心，朝向由代码设置 RotateTransform。</summary>
    public Canvas[] Segments { get; }

    public CentipedeView()
    {
        InitializeComponent();

        var legs = new Line[Pairs * 2][];
        for (int i = 0; i < legs.Length; i++)
        {
            legs[i] = new Line[4];
            for (int s = 0; s < 4; s++)
            {
                var ln = new Line();
                ln.Classes.Add("cp-leg");
                ln.Classes.Add("cp-" + "abcd"[s]);   // 样式只在本视图里，和其他宠物不共用
                LegLayer.Children.Add(ln);
                legs[i][s] = ln;
            }
        }
        Legs = legs;

        var fill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#22100A"), 0),
                new GradientStop(Color.Parse("#4A1E12"), 0.5),
                new GradientStop(Color.Parse("#22100A"), 1),
            }
        };
        var edge = new SolidColorBrush(Color.Parse("#0E0502"));
        var cerci = new SolidColorBrush(Color.Parse("#2A160A"));

        var segs = new Canvas[Pairs];
        for (int k = Pairs - 1; k >= 0; k--)   // 先加尾部，头侧的体节压在上面
        {
            var c = new Canvas { Width = 0, Height = 0, RenderTransform = new RotateTransform() };
            double w = 30 - 10.0 * k / (Pairs - 1);   // 越靠尾越窄
            var e = new Ellipse { Width = w, Height = 20, Fill = fill, Stroke = edge, StrokeThickness = 1 };
            Canvas.SetLeft(e, -w / 2);
            Canvas.SetTop(e, -10);
            c.Children.Add(e);
            if (k == Pairs - 1)   // 尾须
                c.Children.Add(new Path
                {
                    Stroke = cerci,
                    StrokeThickness = 2,
                    StrokeLineCap = PenLineCap.Round,
                    Data = Geometry.Parse("M -3,8 L -6,22 M 3,8 L 6,22")
                });
            SegLayer.Children.Add(c);
            segs[k] = c;
        }
        Segments = segs;
    }
}
