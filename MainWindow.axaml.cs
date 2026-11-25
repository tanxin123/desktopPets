using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace Spider;

public partial class MainWindow : Window
{
    const double StepTime = 0.07, BaseSpeed = 420, TurnRate = 3.6;
    const double DashSpeed = 1500, DashTurn = 16, DashStepTime = 0.04;
    const double ChaseSpeed = 900, ChaseTurn = 10;

    // 每种宠物各自的体型参数；视图与样式都在各自的 View 里，这里只放运动学参数
    sealed class Pet
    {
        public Control View = null!;
        public Canvas Body = null!;
        public Line[][] Legs = null!;                 // 顺序：左侧各腿，再右侧各腿
        public RotateTransform Rot = new();
        public double LenA, LenB, LenC, LenD;         // 腿四段长度
        public double Reach;                          // 脚离髋的最大伸展，超过强制迈步
        public double Scale;                          // 身体缩放（腿不缩放）
        public double Lead;                           // 迈步前伸系数
        public double HipX;
        public double[]? LegScale;                    // 每对腿的缩放系数（前→后），null 表示都是 1
        public double[] HipY = null!;
        public Vector[] Out = null!, Rest = null!;    // 每侧每条腿的朝向与落点（右侧镜像）
    }

    sealed class Leg
    {
        public Line[] Seg = null!;
        public Vector Hip, Out, Rest;   // 本地坐标（头朝 -Y）
        public int Side;                // -1 左 / +1 右
        public int Group;               // 交替步态分组 0/1
        public double K = 1;            // 这条腿的缩放系数
        public Vector Foot, From, To;   // 世界坐标
        public bool Stepping;
        public double T;
    }

    sealed class Fly { public Canvas C = null!; public Vector P; }

    readonly Pet _spider, _roach;
    Pet _pet = null!;
    readonly List<Leg> _legs = new();
    readonly List<Fly> _flies = new();
    readonly List<Vector> _route = new();
    readonly Random _rnd = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();

    Vector _pos, _dashTarget;
    double _heading;   // 弧度，0 = 朝上，顺时针为正（与 RotateTransform 一致）
    double _last, _pause;
    double _w = 1920, _h = 1080;
    bool _walking, _dash, _chase, _wasDown;
    int _wp;

    public MainWindow()
    {
        InitializeComponent();

        _spider = new Pet
        {
            View = SpiderPet, Body = SpiderPet.BodyCanvas, Legs = SpiderPet.Legs,
            LenA = 14, LenB = 70, LenC = 84, LenD = 30, Reach = 210, Scale = 0.68, Lead = 1.0,
            HipX = 14,
            HipY = new double[] { -30, -20, -10, 0 },
            Out = new Vector[] { new(0.7, -0.7), new(1, -0.2), new(1, 0.2), new(0.7, 0.7) },
            Rest = new Vector[] { new(125, -135), new(165, -60), new(165, 40), new(135, 120) },
        };
        _roach = new Pet
        {
            View = RoachPet, Body = RoachPet.BodyCanvas, Legs = RoachPet.Legs,
            LenA = 6, LenB = 25, LenC = 31, LenD = 10, Reach = 74, Scale = 0.7, Lead = 0.4,
            HipX = 10,
            LegScale = new double[] { 0.6, 1, 1 },   // 前、中、后三对腿；前腿缩到 60%
            HipY = new double[] { -22, 4, 30 },
            Out = new Vector[] { new(0.7, -0.7), new(1, 0), new(0.7, 0.7) },
            Rest = new Vector[] { new(46, -49), new(57, 4), new(45, 53) },
        };
        foreach (var p in new[] { _spider, _roach })
            p.Body.RenderTransform = new TransformGroup { Children = { new ScaleTransform(p.Scale, p.Scale), p.Rot } };

        SetPet(false);

        Opened += (_, _) =>
        {
            FitToAllScreens();
            // 视图本身没有内容尺寸（身体和腿都用绝对坐标画），显式撑满窗口，避免 0×0 导致不渲染
            foreach (var v in new Control[] { SpiderPet, RoachPet }) { v.Width = _w; v.Height = _h; }
            MakeClickThrough();
            _pos = new Vector(_w / 2, _h / 2);
            foreach (var l in _legs) l.Foot = ToWorld(l.Rest);
            StartRoute();
            _last = _clock.Elapsed.TotalSeconds;
            var t = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(8) };
            t.Tick += (_, _) => Tick();
            t.Start();
        };
    }

    // ───────── 切换宠物（托盘菜单调用）─────────
    public void SetPet(bool roach)
    {
        _pet = roach ? _roach : _spider;
        _spider.View.IsVisible = !roach;
        _roach.View.IsVisible = roach;
        BuildLegs();
    }

    void BuildLegs()
    {
        _legs.Clear();
        int n = _pet.HipY.Length;
        for (int s = 0; s < 2; s++)
        {
            int side = s == 0 ? -1 : 1;
            for (int i = 0; i < n; i++)
            {
                double k = _pet.LegScale?[i] ?? 1;
                var hip = new Vector(side * _pet.HipX, _pet.HipY[i]);
                var rest = new Vector(side * _pet.Rest[i].X, _pet.Rest[i].Y);
                _legs.Add(new Leg
                {
                    Seg = _pet.Legs[s * n + i],
                    Side = side,
                    K = k,
                    Hip = hip,
                    Out = Unit(new Vector(side * _pet.Out[i].X, _pet.Out[i].Y)),
                    Rest = hip + (rest - hip) * k,   // 落点也按系数向腿根收拢
                    Group = (i + s) % 2,
                });
            }
        }
        foreach (var l in _legs) l.Foot = ToWorld(l.Rest);
    }

    // ───────── Win32：点击穿透 + 全局轮询 ─────────
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint p);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int index, IntPtr value);

    [StructLayout(LayoutKind.Sequential)]
    struct NativePoint { public int X, Y; }

    // 窗口铺满所有显示器组成的虚拟桌面
    void FitToAllScreens()
    {
        var all = Screens.All;
        if (all.Count == 0) { _w = Bounds.Width; _h = Bounds.Height; return; }
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach (var s in all)
        {
            x0 = Math.Min(x0, s.Bounds.X); y0 = Math.Min(y0, s.Bounds.Y);
            x1 = Math.Max(x1, s.Bounds.Right); y1 = Math.Max(y1, s.Bounds.Bottom);
        }
        double scale = RenderScaling;
        Position = new PixelPoint(x0, y0);
        _w = (x1 - x0) / scale;
        _h = (y1 - y0) / scale;
        Width = _w;
        Height = _h;
    }

    void MakeClickThrough()
    {
        var h = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (h == IntPtr.Zero) return;
        const int GWL_EXSTYLE = -20;
        const long WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000;
        long ex = GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(ex | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW));
    }

    // 每帧轮询：点宠物 -> 冲刺；点别处 -> 放一只苍蝇；Ctrl+Shift+Q -> 退出
    void PollInput()
    {
        bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0;
        if (down && !_wasDown && !_dash && GetCursorPos(out var c))
        {
            var pt = this.PointToClient(new PixelPoint(c.X, c.Y));
            if (Len(new Vector(pt.X, pt.Y) - _pos) < 170) StartDash();
            else AddFly(new Vector(pt.X, pt.Y));
        }
        _wasDown = down;

        bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
        bool q = (GetAsyncKeyState(0x51) & 0x8000) != 0;
        if (ctrl && shift && q) Close();
    }

    // ───────── 苍蝇 ─────────
    void AddFly(Vector p)
    {
        if (_flies.Count > 0) return;   // 屏幕上同时只允许一只苍蝇
        var c = new Canvas { Width = 0, Height = 0 };
        void E(double x, double y, double w, double h, double rot = 0)
        {
            var e = new Avalonia.Controls.Shapes.Ellipse { Width = w, Height = h, Fill = Brushes.Black };
            if (rot != 0) e.RenderTransform = new RotateTransform(rot);
            Canvas.SetLeft(e, x); Canvas.SetTop(e, y);
            c.Children.Add(e);
        }
        E(-14, -9, 7, 17, -40);   // 左翅
        E(7, -9, 7, 17, 40);      // 右翅
        E(-4.5, -5, 9, 15);       // 身体
        E(-3.5, -11, 7, 7);       // 头
        c.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            Data = Geometry.Parse("M -4,-2 L -11,-6 M 4,-2 L 11,-6 M -4,2 L -11,4 M 4,2 L 11,4 M -4,5 L -10,11 M 4,5 L 10,11")
        });
        c.RenderTransform = new TransformGroup { Children = { new ScaleTransform(0.5, 0.5), new RotateTransform(_rnd.NextDouble() * 360) } };
        Canvas.SetLeft(c, p.X); Canvas.SetTop(c, p.Y);
        Stage.Children.Add(c);
        _flies.Add(new Fly { C = c, P = p });
    }

    Fly? NearestFly()
    {
        Fly? best = null; double bd = double.MaxValue;
        foreach (var f in _flies) { double d = Len(f.P - _pos); if (d < bd) { bd = d; best = f; } }
        return best;
    }

    // ───────── 路线 ─────────
    // 点击宠物：随机朝一个方向快速冲出一小段距离到 B 点，到达后 StartRoute 以正常速度走向 C 点
    void StartDash()
    {
        double w = Math.Max(_w, 800), h = Math.Max(_h, 600);
        double ang = _rnd.NextDouble() * Math.PI * 2, dist = 200 + _rnd.NextDouble() * 150;
        _dashTarget = new Vector(
            Math.Clamp(_pos.X + Math.Sin(ang) * dist, 80, w - 80),
            Math.Clamp(_pos.Y - Math.Cos(ang) * dist, 80, h - 80));
        _dash = true; _walking = true; _pause = 0;
    }

    void StartRoute()
    {
        _route.Clear();
        double w = Math.Max(_w, 800), h = Math.Max(_h, 600);
        double m = Math.Min(160, Math.Min(w, h) / 4);
        int n = _rnd.Next(2, 5);
        for (int i = 0; i < n; i++)
            _route.Add(new Vector(m + _rnd.NextDouble() * (w - 2 * m), m + _rnd.NextDouble() * (h - 2 * m)));
        _wp = 0; _pause = 0; _walking = true;
    }

    // ───────── 每帧更新 ─────────
    void Tick()
    {
        double now = _clock.Elapsed.TotalSeconds, dt = Math.Min(now - _last, 0.05);
        _last = now;
        double speed = 0;

        _chase = !_dash && _flies.Count > 0;
        if (_chase) { _walking = true; _pause = 0; }

        if (_walking)
        {
            if (_pause > 0) _pause -= dt;
            else
            {
                var fly = _chase ? NearestFly() : null;
                var d = (fly != null ? fly.P : _dash ? _dashTarget : _route[_wp]) - _pos;
                if (Len(d) < (_dash ? 25 : 30))
                {
                    if (fly != null)
                    {
                        Stage.Children.Remove(fly.C); _flies.Remove(fly);   // 吃掉
                        if (_flies.Count == 0) { StartRoute(); _pause = 0.6; }
                    }
                    else if (_dash) { _dash = false; StartRoute(); }
                    else if (++_wp >= _route.Count) _walking = false;
                    else _pause = 0.2 + _rnd.NextDouble() * 0.8;   // 走走停停
                }
                else
                {
                    double diff = Wrap(Math.Atan2(d.X, -d.Y) - _heading);
                    double turn = (_dash ? DashTurn : fly != null ? ChaseTurn : TurnRate) * dt;
                    _heading += Math.Clamp(diff, -turn, turn);
                    speed = (_dash ? DashSpeed : fly != null ? ChaseSpeed : BaseSpeed)
                            * Math.Clamp(1 - Math.Abs(diff) / 1.3, (_dash || fly != null) ? 0.5 : 0.15, 1.0);
                    _pos += new Vector(Math.Sin(_heading), -Math.Cos(_heading)) * (speed * dt);
                }
            }
        }

        PollInput();
        UpdateLegs(dt, speed);
        Canvas.SetLeft(_pet.Body, _pos.X);
        Canvas.SetTop(_pet.Body, _pos.Y);
        _pet.Rot.Angle = _heading * 180 / Math.PI;
    }

    void UpdateLegs(double dt, double speed)
    {
        bool moving = speed > 1;
        double thresh = moving ? 18 : 6;
        double stepT = (_dash || _chase) ? DashStepTime : StepTime;
        var fwd = new Vector(Math.Sin(_heading), -Math.Cos(_heading));
        var busy = new bool[2];
        foreach (var l in _legs) if (l.Stepping) busy[l.Group] = true;
        foreach (var l in _legs)
        {
            var rest = ToWorld(l.Rest);
            if (l.Stepping)
            {
                l.T += dt / stepT;
                if (l.T >= 1) { l.T = 1; l.Stepping = false; }
                double e = l.T * l.T * (3 - 2 * l.T);
                l.Foot = l.From + (l.To - l.From) * e;
            }
            else if ((!busy[1 - l.Group] && Len(l.Foot - rest) > thresh) || Len(l.Foot - ToWorld(l.Hip)) > _pet.Reach * l.K)
            {
                l.Stepping = true; l.T = 0; l.From = l.Foot;
                l.To = rest + fwd * (moving ? (speed * stepT + 24) * _pet.Lead * l.K : 0);
                busy[l.Group] = true;
            }
        }
        foreach (var l in _legs) Draw(l);
    }

    void Draw(Leg l)
    {
        double k = l.K;
        var h = ToWorld(l.Hip);
        var p = h + Rot(l.Out) * (_pet.LenA * k);
        var q = l.Foot - Unit(l.Foot - p) * (_pet.LenD * k);
        var knee = Knee(p, q, _pet.LenB * k, _pet.LenC * k);
        Set(l.Seg[0], h, p); Set(l.Seg[1], p, knee); Set(l.Seg[2], knee, q); Set(l.Seg[3], q, l.Foot);
    }

    // 两节骨 IK：膝盖取离身体中心更远的那个解，保证朝外拱
    Vector Knee(Vector p, Vector q, double lb, double lc)
    {
        var d = q - p;
        double dist = Math.Clamp(Len(d), Math.Abs(lb - lc) + 1, lb + lc - 0.5);
        var u = Unit(d);
        double a = (lb * lb - lc * lc + dist * dist) / (2 * dist);
        double hgt = Math.Sqrt(Math.Max(0, lb * lb - a * a));
        var perp = new Vector(-u.Y, u.X);
        var mid = p + u * a;
        var k1 = mid + perp * hgt;
        var k2 = mid - perp * hgt;
        return Len(k1 - _pos) >= Len(k2 - _pos) ? k1 : k2;
    }

    static void Set(Line ln, Vector a, Vector b)
    {
        ln.StartPoint = new Point(a.X, a.Y);
        ln.EndPoint = new Point(b.X, b.Y);
    }

    Vector Rot(Vector v)
    {
        double c = Math.Cos(_heading), s = Math.Sin(_heading);
        return new Vector(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    Vector ToWorld(Vector v) => _pos + Rot(v);
    static double Len(Vector v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);
    static Vector Unit(Vector v) { double n = Len(v); return n < 1e-9 ? new Vector(0, -1) : new Vector(v.X / n, v.Y / n); }
    static double Wrap(double a) { while (a > Math.PI) a -= 2 * Math.PI; while (a < -Math.PI) a += 2 * Math.PI; return a; }
}