using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace Spider;

public partial class MainWindow : Window
{
    const double StepTime = 0.07, TurnRate = 3.6;
    double BaseSpeed = 420;   // 受速度档位影响，见 SetSpeed
    const double DashSpeed = 1500, DashTurn = 16, DashStepTime = 0.04;
    const double ChaseSpeed = 900, ChaseTurn = 10;
    const double EatTime = 1.3, FollowNear = 180, FollowFar = 280;   // 吃东西时长（秒）；跟随鼠标时停下 / 出发的距离
    const double CrawlSpeed = 180, LookTurn = 1.8;      // 沿边爬行速度、原地转向速度
    const double IdleMin = 2.5, IdleMax = 6.0;          // 两次空闲小动作之间的间隔（秒）

    // 每种宠物各自的体型参数；视图与样式都在各自的 View 里，这里只放运动学参数
    sealed class Pet
    {
        public Control View = null!;
        public Canvas Body = null!;
        public Line[][] Legs = null!;                 // 顺序：左侧各腿，再右侧各腿
        public RotateTransform Rot = new();
        public ScaleTransform BodySc = new();         // 身体缩放（宠物自身比例 × 大小档位）
        public RotateTransform[]? SegRot;             // 分节宠物（蜈蚣）每个体节的旋转与缩放
        public ScaleTransform[]? SegSc;
        public double StepScale = 1;                  // 迈步阈值系数：体型小的宠物更勤快地迈步
        public double LenA, LenB, LenC, LenD;         // 腿四段长度
        public double Reach;                          // 脚离髋的最大伸展，超过强制迈步
        public double Scale;                          // 身体缩放（腿不缩放）
        public double Lead;                           // 迈步前伸系数
        public double HipX;
        public double[]? LegScale;                    // 每对腿的缩放系数（前→后），null 表示都是 1
        public double[] HipY = null!;
        public Control AntL = null!, AntR = null!;    // 触角（蟑螂）/ 须肢（蜘蛛），用来做抖动
        public RotateTransform AntRotL = new(), AntRotR = new();
        public Vector[] Out = null!, Rest = null!;
        public Canvas[]? Segs;                        // 分节宠物（蜈蚣）的体节，null 表示刚性身体
        public string Food = "fly";                   // 食物种类：fly / crumb / leaf / sugar / worm
        public double FoodScale = 1;                  // 食物显示缩放
        public double SegGap;                         // 相邻体节中心距    // 每侧每条腿的朝向与落点（右侧镜像）
    }

    sealed class Leg
    {
        public Line[] Seg = null!;
        public Vector Hip, Out, Rest;   // 本地坐标（头朝 -Y）
        public int Side;                // -1 左 / +1 右
        public int Group;               // 交替步态分组 0/1
        public bool Held;               // 空闲抬腿时，脚的位置由 UpdateIdle 接管
        public int SegIdx = -1;         // 所属体节下标（-1 = 刚性身体）
        public double K = 1;            // 这条腿的缩放系数
        public Vector Foot, From, To;   // 世界坐标
        public bool Stepping;
        public double T;
    }

    sealed class Fly   // 类名沿用 Fly，实际按宠物不同是各种食物
    {
        public Canvas C = null!; public Vector P;               // 食物图形与位置
        public ScaleTransform Sc = null!; public double Base;   // 吃的时候逐渐缩小
    }

    readonly Pet _spider, _roach, _beetle, _ant, _centipede, _ladybug, _custom;
    readonly Dictionary<string, Pet> _pets = new();
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
    double _ax, _ay, _aw = 1920, _ah = 1080;   // 宠物的活动范围（窗口坐标），默认整个虚拟桌面
    double _size = 1;                          // 大小档位系数
    int _monitor;                              // 0 = 所有显示器；n = 第 n 块
    int[] _hotVks = { 0x11, 0x10, 0x51 };      // 退出快捷键的虚拟键码，默认 Ctrl+Shift+Q
    DispatcherTimer? _timer;
    int _intervalMs = 16;
    bool _opened;
    bool _walking, _dash, _chase, _wasDown;
    bool _crawl, _look;
    double _lookTarget, _waveT, _twitchT, _phase, _antPhase, _idleWait = 3, _antAmp = 3;
    int _wp;
    bool _sleeping, _night, _capturing;                         // 睡觉中 / 夜间 / 窗口正在接收点击
    double _sleepT, _hunger, _timeCheck, _dodgeCd, _curPrevT = -1;   // 睡眠剩余时间；饥饿 0~1；时段检查计时；躲鼠标冷却；上次光标采样时间
    Vector _curPrev;
    IntPtr _hwnd;
    TextBlock? _zzz;
    sealed class Print { public Avalonia.Controls.Shapes.Ellipse E = null!; public double Life; }
    readonly List<Print> _prints = new();
    sealed class Web { public Canvas C = null!; public double Life; }
    readonly List<Web> _webs = new();
    static readonly IBrush PrintBrush = new SolidColorBrush(Color.Parse("#55000000"));
    Fly? _eat;                       // 正在吃的食物
    double _eatT, _nod;              // 吃东西剩余时间；头部点动角度（弧度，只影响显示）
    bool _follow, _following;        // 跟随鼠标是否生效 / 是否正在靠近光标
    double _ox, _oy, _sc = 1;        // 本窗口原点（屏幕像素）与缩放，用来换算其他窗口的矩形
    Vector[] _chain = new Vector[0], _segPos = new Vector[0];   // 分节宠物：沿头部轨迹跟随的链、各体节中心
    double[] _segHead = new double[0];                          // 各体节朝向

    public MainWindow()
    {
        InitializeComponent();

        _spider = new Pet
        {
            View = SpiderPet, Body = SpiderPet.BodyCanvas, Legs = SpiderPet.Legs,
            AntL = SpiderPet.AntennaL, AntR = SpiderPet.AntennaR,
            LenA = 14, LenB = 70, LenC = 84, LenD = 30, Reach = 210, Scale = 0.68, Lead = 1.0,
            HipX = 14, FoodScale = 0.5,
            HipY = new double[] { -30, -20, -10, 0 },
            Out = new Vector[] { new(0.7, -0.7), new(1, -0.2), new(1, 0.2), new(0.7, 0.7) },
            Rest = new Vector[] { new(125, -135), new(165, -60), new(165, 40), new(135, 120) },
        };
        _roach = new Pet
        {
            View = RoachPet, Body = RoachPet.BodyCanvas, Legs = RoachPet.Legs,
            AntL = RoachPet.AntennaL, AntR = RoachPet.AntennaR,
            LenA = 6, LenB = 25, LenC = 31, LenD = 10, Reach = 74, Scale = 0.7, Lead = 0.4,
            HipX = 10, Food = "crumb",
            LegScale = new double[] { 0.6, 1, 1 },   // 前、中、后三对腿；前腿缩到 60%
            HipY = new double[] { -22, 4, 30 },
            Out = new Vector[] { new(0.7, -0.7), new(1, 0), new(0.7, 0.7) },
            Rest = new Vector[] { new(46, -49), new(57, 4), new(45, 53) },
        };
        _beetle = new Pet
        {
            View = BeetlePet, Body = BeetlePet.BodyCanvas, Legs = BeetlePet.Legs,
            AntL = BeetlePet.AntennaL, AntR = BeetlePet.AntennaR,
            LenA = 6, LenB = 22, LenC = 26, LenD = 10, Reach = 62, Scale = 1, Lead = 0.5, StepScale = 0.6,
            HipX = 16, Food = "leaf",
            HipY = new double[] { -18, 8, 34 },
            Out = new Vector[] { new(0.7, -0.7), new(1, 0), new(0.7, 0.7) },
            Rest = new Vector[] { new(44, -38), new(56, 8), new(44, 58) },
        };
        _ant = new Pet
        {
            View = AntPet, Body = AntPet.BodyCanvas, Legs = AntPet.Legs,
            AntL = AntPet.AntennaL, AntR = AntPet.AntennaR,
            LenA = 3, LenB = 17, LenC = 20, LenD = 7, Reach = 48, Scale = 0.7, Lead = 0.5, StepScale = 0.45,   // 整体按 70% 缩小
            HipX = 4, Food = "sugar",
            HipY = new double[] { -11, -6, 1 },
            Out = new Vector[] { new(0.7, -0.7), new(1, 0), new(0.7, 0.7) },
            Rest = new Vector[] { new(24, -28), new(31, -4), new(24, 24) },
        };
        _ladybug = new Pet
        {
            View = LadybugPet, Body = LadybugPet.BodyCanvas, Legs = LadybugPet.Legs,
            AntL = LadybugPet.AntennaL, AntR = LadybugPet.AntennaR,
            LenA = 4, LenB = 15, LenC = 18, LenD = 7, Reach = 52, Scale = 1, Lead = 0.5, StepScale = 0.5,
            HipX = 13, Food = "leaf", FoodScale = 0.8,
            HipY = new double[] { -14, 6, 28 },
            Out = new Vector[] { new(0.7, -0.7), new(1, 0), new(0.7, 0.7) },
            Rest = new Vector[] { new(38, -30), new(48, 6), new(38, 42) },
        };
        _custom = new Pet
        {
            View = CustomPet, Body = CustomPet.BodyCanvas, Legs = CustomPet.Legs,
            AntL = CustomPet.AntennaL, AntR = CustomPet.AntennaR,
            LenA = 5, LenB = 20, LenC = 24, LenD = 8, Reach = 60, Scale = 1, Lead = 0.5, StepScale = 0.55,
            HipX = 14, Food = "crumb",
            HipY = new double[] { -16, 6, 28 },
            Out = new Vector[] { new(0.7, -0.7), new(1, 0), new(0.7, 0.7) },
            Rest = new Vector[] { new(42, -34), new(52, 6), new(42, 44) },
        };
        // 蜈蚣：每个体节一对腿，腿根在体节中心，Hip/Rest 都是相对所在体节的坐标；最后一对腿更长并向后拖
        const int cn = Views.CentipedeView.Pairs;
        var cHipY = new double[cn];
        var cOut = new Vector[cn];
        var cRest = new Vector[cn];
        var cScale = new double[cn];
        for (int i = 0; i < cn; i++)
        {
            cHipY[i] = 0;
            cOut[i] = new Vector(1, (i - 8.5) * 0.03);
            cRest[i] = new Vector(40, 0);
            cScale[i] = 1;
        }
        cScale[cn - 1] = 1.5;
        cOut[cn - 1] = new Vector(0.7, 0.7);
        cRest[cn - 1] = new Vector(40, 24);
        _centipede = new Pet
        {
            View = CentipedePet, Body = CentipedePet.BodyCanvas, Legs = CentipedePet.Legs,
            AntL = CentipedePet.AntennaL, AntR = CentipedePet.AntennaR,
            LenA = 4, LenB = 16, LenC = 18, LenD = 8, Reach = 48, Scale = 1, Lead = 0.3, StepScale = 0.5,
            HipX = 12, LegScale = cScale, HipY = cHipY, Out = cOut, Rest = cRest,
            Segs = CentipedePet.Segments, SegGap = 13, Food = "worm",
        };
        _pets["spider"] = _spider;
        _pets["roach"] = _roach;
        _pets["beetle"] = _beetle;
        _pets["ant"] = _ant;
        _pets["centipede"] = _centipede;
        _pets["ladybug"] = _ladybug;
        _pets["custom"] = _custom;

        foreach (var p in _pets.Values)
            p.Body.RenderTransform = new TransformGroup { Children = { p.BodySc, p.Rot } };

        // 蜈蚣的每个体节原来只有旋转，这里换成“缩放 + 旋转”，这样大小档位对体节也生效
        {
            var segs = _centipede.Segs!;
            _centipede.SegRot = new RotateTransform[segs.Length];
            _centipede.SegSc = new ScaleTransform[segs.Length];
            for (int i = 0; i < segs.Length; i++)
            {
                var r = (RotateTransform)segs[i].RenderTransform!;
                var sc = new ScaleTransform();
                _centipede.SegRot[i] = r;
                _centipede.SegSc[i] = sc;
                segs[i].RenderTransform = new TransformGroup { Children = { sc, r } };
            }
        }
        ApplySize();

        foreach (var pet in _pets.Values)
        {
            pet.AntL.RenderTransform = pet.AntRotL;
            pet.AntR.RenderTransform = pet.AntRotR;
        }

        _zzz = new TextBlock { Text = "Z z z", FontSize = 18, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.Parse("#CC7FB2FF")), IsVisible = false, IsHitTestVisible = false };
        Stage.Children.Add(_zzz);
        SetPet("spider");

        Opened += (_, _) =>
        {
            FitToAllScreens();
            ApplyArea();
            _opened = true;
            // 视图本身没有内容尺寸（身体和腿都用绝对坐标画），显式撑满窗口，避免 0×0 导致不渲染
            foreach (var pp in _pets.Values) { pp.View.Width = _w; pp.View.Height = _h; }
            MakeClickThrough();
            _pos = new Vector(_ax + _aw / 2, _ay + _ah / 2);
            ResetChain();
            foreach (var l in _legs) l.Foot = LW(l, l.Rest);
            StartRoute();
            _last = _clock.Elapsed.TotalSeconds;
            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(_intervalMs) };
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        };
    }

    // ───────── 切换宠物（托盘菜单调用）─────────
    public void SetPet(string name)
    {
        if (!_pets.TryGetValue(name, out var p)) { name = "spider"; p = _spider; }   // 未知名字回退蜘蛛
        if (_sleeping) WakeUp();
        if (name == "custom") CustomPet.ReloadImage();
        _pet = p;
        if (_flies.Count > 0 || _eat != null) ClearFood();   // 食物种类随宠物变化，切换时清掉场上的食物
        foreach (var kv in _pets) kv.Value.View.IsVisible = kv.Key == name;
        BuildLegs();
    }

    // ───────── 空闲小动作 ─────────
    // 清掉抬腿、转头等姿态，开始正式移动前调用
    void StopPose()
    {
        _look = false; _waveT = 0;
        foreach (var l in _legs) l.Held = false;
    }

    // 每帧调用：触角/须肢始终轻微摆动；宠物完全静止时，隔几秒随机做一个小动作
    void UpdateIdle(double dt)
    {
        bool still = !_walking && !_dash && !_chase && _eat == null;

        _hunger = Math.Min(1, _hunger + dt / 480);   // 约 8 分钟饿满
        if ((_timeCheck -= dt) <= 0) { _timeCheck = 30; int hr = DateTime.Now.Hour; _night = hr >= 23 || hr < 6; }
        if (_sleeping)
        {
            if (!still) WakeUp();
            else if ((_sleepT -= dt) <= 0) WakeUp();
            else
            {
                _antPhase += dt * 0.8;
                _pet.AntRotL.Angle = Math.Sin(_antPhase) * 1.2;
                _pet.AntRotR.Angle = Math.Sin(_antPhase + 1.7) * 1.2;
                double b = 1 + Math.Sin(_clock.Elapsed.TotalSeconds * 2.2) * 0.015;   // 呼吸起伏
                _pet.BodySc.ScaleX = _pet.BodySc.ScaleY = _pet.Scale * _size * b;
                UpdateZzz();
                return;
            }
        }

        // 触角（蟑螂）/ 须肢（蜘蛛）：平时慢摆，抖动时幅度和频率都变大
        if (_twitchT > 0) _twitchT -= dt;
        double targetAmp = _twitchT > 0 ? 14 : still ? 4 : 1.5;
        _antAmp += (targetAmp - _antAmp) * Math.Min(1, dt * 12);
        _antPhase += dt * (_twitchT > 0 ? 38 : 3.2);
        _pet.AntRotL.Angle = Math.Sin(_antPhase) * _antAmp;
        _pet.AntRotR.Angle = Math.Sin(_antPhase + 1.7) * _antAmp;

        // 原地转头：朝一个新方向慢慢转过去，脚会自然跟着碎步调整
        if (_look)
        {
            double diff = Wrap(_lookTarget - _heading);
            if (!still || Math.Abs(diff) < 0.03) _look = false;
            else _heading += Math.Clamp(diff, -LookTurn * dt, LookTurn * dt);
        }

        // 抬前腿：最前面一对腿抬起并晃动，结束后松开，由步态逻辑把脚放回原位
        if (_waveT > 0)
        {
            if (!still) StopPose();
            else
            {
                _waveT -= dt;
                _phase += dt * 16;
                if (_waveT <= 0) { foreach (var l in _legs) l.Held = false; }
                else
                {
                    RaiseLeg(0, dt);
                    RaiseLeg(_pet.HipY.Length, dt);
                }
            }
        }

        // 到点随机挑一个动作
        if (still && !_follow && !_look && _waveT <= 0 && _twitchT <= 0)
        {
            _idleWait -= dt;
            if (_idleWait <= 0)
            {
                _idleWait = (IdleMin + _rnd.NextDouble() * (IdleMax - IdleMin)) * (1 - 0.4 * _hunger);
                if (_rnd.NextDouble() < (_night ? 0.4 : 0.07) * (1 - _hunger)) { StartSleep(); return; }   // 夜里更容易睡，饿了不睡
                int pick = _rnd.Next(6);
                if (_hunger > 0.6 && _rnd.NextDouble() < 0.4) pick = 2;   // 饿了就爱抖触角
                switch (pick)
                {
                    case 0:
                        _look = true;
                        _lookTarget = _heading + (_rnd.NextDouble() < 0.5 ? -1 : 1) * (0.6 + _rnd.NextDouble() * 1.2);
                        break;
                    case 1: _waveT = 0.9 + _rnd.NextDouble() * 0.6; break;
                    case 2: _twitchT = 0.6 + _rnd.NextDouble() * 0.5; break;
                    case 3:
                    case 4: if (!StartWindowCrawl()) StartEdgeCrawl(); break;   // 沿别的窗口的边缘爬；没有合适窗口就退回屏幕边缘
                    default: StartEdgeCrawl(); break;
                }
            }
        }
        else if (!still) _idleWait = IdleMin;
    }

    void RaiseLeg(int idx, double dt)
    {
        if (idx < 0 || idx >= _legs.Count) return;
        var l = _legs[idx];
        var local = l.Hip + (l.Rest - l.Hip) * 0.5 + new Vector(Math.Sin(_phase) * 8 * l.Side, -22) * _size;
        var target = LW(l, local);
        l.Held = true;
        l.Foot += (target - l.Foot) * Math.Min(1, dt * 14);   // 平滑抬起，不瞬移
    }

    // 沿最近的屏幕边缘爬一小段：先走到边上，再以较慢的速度沿边爬
    void StartEdgeCrawl()
    {
        const double m = 70;
        double w = _aw, h = _ah;
        double px = _pos.X - _ax, py = _pos.Y - _ay;   // 在活动范围内的相对位置
        double dLeft = px, dRight = w - px, dTop = py, dBottom = h - py;
        double min = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));
        double dir = _rnd.NextDouble() < 0.5 ? -1 : 1, len = 300 + _rnd.NextDouble() * 500;
        _route.Clear();
        if (min == dTop || min == dBottom)
        {
            double y = min == dTop ? m : h - m;
            double x0 = Math.Clamp(px, m, w - m);
            _route.Add(new Vector(_ax + x0, _ay + y));
            _route.Add(new Vector(_ax + Math.Clamp(x0 + dir * len, m, w - m), _ay + y));
        }
        else
        {
            double x = min == dLeft ? m : w - m;
            double y0 = Math.Clamp(py, m, h - m);
            _route.Add(new Vector(_ax + x, _ay + y0));
            _route.Add(new Vector(_ax + x, _ay + Math.Clamp(y0 + dir * len, m, h - m)));
        }
        StopPose();
        _wp = 0; _pause = 0; _walking = true; _crawl = true;
    }

    // ───────── 全屏应用自动隐藏 ─────────
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out NativeRect r);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr mon, ref MonitorInfo mi);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder sb, int max);

    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int L, T, R, B; }

    [StructLayout(LayoutKind.Sequential)]
    struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }

    public bool AutoHideFullscreen { get; private set; } = true;
    bool _hidden;
    double _fsCheck;

    public void SetAutoHide(bool on)
    {
        AutoHideFullscreen = on;
        if (!on && _hidden) { _hidden = false; ShowPet(true); }   // 关闭功能时，如果正处于隐藏状态就立刻恢复
    }

    void ShowPet(bool show)
    {
        var h = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (h != IntPtr.Zero) ShowWindow(h, show ? 4 : 0);   // 4 = SW_SHOWNOACTIVATE（不抢焦点），0 = SW_HIDE
    }

    // 每 0.5 秒检查一次前台窗口；返回 true 表示当前处于隐藏状态，Tick 应整个跳过
    bool CheckHidden(double now)
    {
        if (now - _fsCheck >= 0.5)
        {
            _fsCheck = now;
            bool fs = AutoHideFullscreen && IsForegroundFullscreen();
            if (fs != _hidden) { _hidden = fs; ShowPet(!fs); }
        }
        return _hidden;
    }

    // 前台窗口无标题栏，且盖满它所在的整块显示器，才算全屏（游戏、视频、浏览器 F11）
    bool IsForegroundFullscreen()
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || !IsWindowVisible(fg) || IsIconic(fg)) return false;
        GetWindowThreadProcessId(fg, out uint pid);
        if (pid == (uint)Environment.ProcessId) return false;   // 宠物自己的窗口
        var sb = new System.Text.StringBuilder(64);
        GetClassName(fg, sb, sb.Capacity);
        if (sb.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;   // 桌面、任务栏
        const int GWL_STYLE = -16;
        const long WS_CAPTION = 0xC00000;
        if ((GetWindowLongPtr(fg, GWL_STYLE).ToInt64() & WS_CAPTION) == WS_CAPTION) return false;   // 有标题栏的最大化窗口不算
        if (!GetWindowRect(fg, out var r)) return false;
        var mi = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(fg, 2), ref mi)) return false;   // 2 = MONITOR_DEFAULTTONEAREST
        return r.L <= mi.Monitor.L && r.T <= mi.Monitor.T && r.R >= mi.Monitor.R && r.B >= mi.Monitor.B;
    }

    // ───────── 喂食开关（托盘菜单调用）─────────
    public bool FeedingEnabled { get; private set; } = true;

    public void SetFeeding(bool on)
    {
        FeedingEnabled = on;
        if (on || (_flies.Count == 0 && _eat == null)) return;
        // 关闭喂食：清掉场上的苍蝇。追击中 _walking 可能是被追击强行置位的，
        // 路线下标可能已越界，所以必须重新生成路线，避免 Tick 里访问 _route[_wp] 越界
        ClearFood();
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
                var hip = new Vector(side * _pet.HipX, _pet.HipY[i]) * _size;
                var rest = new Vector(side * _pet.Rest[i].X, _pet.Rest[i].Y) * _size;
                _legs.Add(new Leg
                {
                    Seg = _pet.Legs[s * n + i],
                    Side = side,
                    K = k * _size,   // 腿长、伸展距离随大小档位一起缩放
                    SegIdx = _pet.Segs != null ? i : -1,
                    Hip = hip,
                    Out = Unit(new Vector(side * _pet.Out[i].X, _pet.Out[i].Y)),
                    Rest = hip + (rest - hip) * k,   // 落点也按系数向腿根收拢
                    Group = (i + s) % 2,
                });
            }
        }
        ResetChain();
        foreach (var l in _legs) l.Foot = LW(l, l.Rest);
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
        _ox = x0; _oy = y0; _sc = scale;
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
        _hwnd = h;
        SetWindowLongPtr(h, GWL_EXSTYLE, new IntPtr(ex | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | 0x08000000L));   // 0x08000000 = WS_EX_NOACTIVATE，接收点击时也不抢焦点
    }

    // 每帧轮询：点宠物 -> 冲刺；点别处 -> 放一只苍蝇；Ctrl+Shift+Q -> 退出
    void PollInput(double dt)
    {
        bool down = (GetAsyncKeyState(0x01) & 0x8000) != 0;
        bool ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
        bool have = GetCursorPos(out var c), near = false;
        Vector cur = default;
        if (have)
        {
            var p0 = this.PointToClient(new PixelPoint(c.X, c.Y));
            cur = new Vector(p0.X, p0.Y);
            near = Len(cur - _pos) < Math.Max(90, 170 * _size);
            UpdateDodge(cur, dt);
        }
        // 光标在宠物附近（睡着时除外）或按住 Ctrl 时，窗口临时接收点击，点击就不会穿到下面的软件
        SetCapture(have && ((near && !_sleeping) || ctrl));
        if (down && !_wasDown && !_dash && have)
        {
            var pt = this.PointToClient(new PixelPoint(c.X, c.Y));
            if (Len(new Vector(pt.X, pt.Y) - _pos) < Math.Max(90, 170 * _size)) StartDash();
            else if ((GetAsyncKeyState(0x11) & 0x8000) != 0) AddFly(new Vector(pt.X, pt.Y));   // 只有按住 Ctrl 再点才出苍蝇
        }
        _wasDown = down;

        bool pressed = _hotVks.Length > 0;
        foreach (var vk in _hotVks)
            if ((GetAsyncKeyState(vk) & 0x8000) == 0) { pressed = false; break; }
        if (pressed) Close();
    }

    // ───────── 苍蝇 ─────────
    void AddFly(Vector p)
    {
        if (!FeedingEnabled) return;    // 喂食被禁用
        if (_flies.Count > 0) return;   // 屏幕上同时只允许一只苍蝇
        var c = _pet.Food switch
        {
            "crumb" => MakeCrumb(),
            "leaf" => MakeLeaf(),
            "sugar" => MakeSugar(),
            "worm" => MakeWorm(),
            _ => MakeFlyShape(),
        };
        var sc = new ScaleTransform(_pet.FoodScale, _pet.FoodScale);
        c.RenderTransform = new TransformGroup { Children = { sc, new RotateTransform(_rnd.NextDouble() * 360) } };
        Canvas.SetLeft(c, p.X); Canvas.SetTop(c, p.Y);
        Stage.Children.Add(c);
        _flies.Add(new Fly { C = c, P = p, Sc = sc, Base = _pet.FoodScale });
    }

    // 苍蝇：纯黑剪影
    static Canvas MakeFlyShape()
    {
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
        return c;
    }

    // 其他食物的外形：单位是像素，原点在中心，宠物各自的 FoodScale 再整体缩放
    static Avalonia.Controls.Shapes.Path Sh(string data, string? fill, string? stroke = null, double th = 1) => new()
    {
        Data = Geometry.Parse(data),
        Fill = fill == null ? null : new SolidColorBrush(Color.Parse(fill)),
        Stroke = stroke == null ? null : new SolidColorBrush(Color.Parse(stroke)),
        StrokeThickness = th,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
    };

    // 面包屑（蟑螂）
    static Canvas MakeCrumb()
    {
        var c = new Canvas { Width = 0, Height = 0 };
        c.Children.Add(Sh("M -7,-3 L -2,-8 L 6,-5 L 8,2 L 2,7 L -6,5 Z", "#D9B26F", "#8A6A30"));
        c.Children.Add(Sh("M 10,-9 L 14,-8 L 13,-4 L 9,-5 Z", "#D9B26F", "#8A6A30"));
        c.Children.Add(Sh("M -12,6 L -9,5 L -8,8 L -11,9 Z", "#D9B26F", "#8A6A30"));
        return c;
    }

    // 树叶（甲虫）
    static Canvas MakeLeaf()
    {
        var c = new Canvas { Width = 0, Height = 0 };
        c.Children.Add(Sh("M 0,-14 C 12,-6 12,8 0,16 C -12,8 -12,-6 0,-14 Z", "#4E8A3A", "#2A5A1E"));
        c.Children.Add(Sh("M 0,-11 L 0,15 M 0,-2 L 6,-7 M 0,4 L -6,-1 M 0,9 L 5,4", null, "#2A5A1E", 0.9));
        return c;
    }

    // 糖粒（蚂蚁）
    static Canvas MakeSugar()
    {
        var c = new Canvas { Width = 0, Height = 0 };
        c.Children.Add(Sh("M -5,-5 L 3,-5 L 3,3 L -5,3 Z", "#F4F4F0", "#A8A8A0"));
        c.Children.Add(Sh("M 5,1 L 10,1 L 10,6 L 5,6 Z", "#F4F4F0", "#A8A8A0"));
        c.Children.Add(Sh("M -9,4 L -5,4 L -5,8 L -9,8 Z", "#F4F4F0", "#A8A8A0"));
        return c;
    }

    // 小虫（蜈蚣）
    static Canvas MakeWorm()
    {
        var c = new Canvas { Width = 0, Height = 0 };
        c.Children.Add(Sh("M -13,0 C -9,-6 -5,6 0,0 S 9,-6 13,0", null, "#C4837A", 4));
        c.Children.Add(Sh("M 12,-1 L 14,0 L 12,1 Z", "#7A3A32", "#7A3A32", 1));
        return c;
    }

    // ───────── 食物：清除 / 吃东西 ─────────
    void ClearFood()
    {
        foreach (var f in _flies) Stage.Children.Remove(f.C);
        _flies.Clear();
        _eat = null; _nod = 0;
        StartRoute();   // 追击中 _walking 可能是被追击强行置位的，路线下标可能越界，所以重新生成路线
    }

    // 到达食物：先停下，吃完才让它消失
    void BeginEat(Fly f)
    {
        _eat = f; _eatT = EatTime; _phase = 0;
        SoundFx.Play("eat");
        StopPose();
    }

    // 被打断（比如点了宠物冲刺）：食物保留并恢复原大小，之后继续去吃
    void CancelEat()
    {
        if (_eat == null) return;
        _eat.Sc.ScaleX = _eat.Sc.ScaleY = _eat.Base;
        _eat = null; _nod = 0;
        foreach (var l in _legs) l.Held = false;
    }

    // 吃东西动画：前腿抬起抱住食物，头一点一点，食物一点点变小，吃完消失
    void UpdateEating(double dt)
    {
        var f = _eat!;
        _eatT -= dt;
        _phase += dt * 20;
        _nod = Math.Sin(_phase * 0.55) * 0.12;
        double k = Math.Max(0, 1 - _eatT / EatTime);   // 0 → 1
        f.Sc.ScaleX = f.Sc.ScaleY = f.Base * (1 - 0.9 * k);
        RaiseLeg(0, dt);
        RaiseLeg(_pet.HipY.Length, dt);
        if (_eatT > 0) return;
        _nod = 0;
        Stage.Children.Remove(f.C); _flies.Remove(f);
        _hunger = Math.Max(0, _hunger - 0.6);   // 吃饱一点
        _eat = null;
        foreach (var l in _legs) l.Held = false;
        if (_flies.Count == 0) { StartRoute(); _pause = 0.6; }
    }

    // ───────── 跟随鼠标（托盘菜单调用）─────────
    public bool FollowMouse { get; private set; }

    public void SetFollow(bool on)
    {
        if (FollowMouse == on) return;
        FollowMouse = on;
        _following = false; _crawl = false;
        if (on) StopPose();
        else if (_eat == null && _flies.Count == 0) StartRoute();   // 关闭后恢复随机走动
    }

    bool TryCursor(out Vector v)
    {
        v = default;
        if (!GetCursorPos(out var c)) return false;
        var pt = this.PointToClient(new PixelPoint(c.X, c.Y));
        v = new Vector(pt.X, pt.Y);
        return true;
    }

    // 离光标超过 FollowFar 就走过去，靠近到 FollowNear 以内停下（两个阈值留出滞回，避免在边界抖动）
    double UpdateFollow(double dt)
    {
        if (!TryCursor(out var cur)) return 0;
        var d = cur - _pos;
        double dist = Len(d);
        if (dist > FollowFar) _following = true;
        else if (dist < FollowNear) _following = false;
        _walking = _following;
        _crawl = false;
        if (!_following) return 0;
        double diff = Wrap(Math.Atan2(d.X, -d.Y) - _heading);
        double turn = TurnRate * dt;
        _heading += Math.Clamp(diff, -turn, turn);
        double speed = BaseSpeed * Pace * Math.Clamp(1 - Math.Abs(diff) / 1.3, 0.15, 1.0) * Math.Clamp(dist / 500, 0.5, 1.5);
        _pos += Fwd(_heading) * (speed * dt);
        return speed;
    }

    // ───────── 攀爬窗口边缘 ─────────
    delegate bool EnumProc(IntPtr h, IntPtr lp);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lp);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out NativeRect r, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);

    // 把一个顶层窗口的可见边框换算成本窗口坐标；桌面、任务栏、隐藏 / 最小化 / 太小的窗口返回 false
    bool TryWindowRect(IntPtr h, out Rect rect)
    {
        rect = default;
        if (!IsWindowVisible(h) || IsIconic(h)) return false;
        GetWindowThreadProcessId(h, out uint pid);
        if (pid == (uint)Environment.ProcessId) return false;   // 宠物自己的窗口
        if (DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return false;   // 14 = DWMWA_CLOAKED（UWP 后台窗口）
        long ex = GetWindowLongPtr(h, -20).ToInt64();
        if ((ex & 0x80) != 0 && (ex & 0x40000) == 0) return false;   // 工具窗口
        var sb = new System.Text.StringBuilder(64);
        GetClassName(h, sb, sb.Capacity);
        if (sb.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        NativeRect r;
        // 9 = DWMWA_EXTENDED_FRAME_BOUNDS：不含 Win10 窗口四周看不见的阴影边框；取不到再退回 GetWindowRect
        if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf<NativeRect>()) != 0 && !GetWindowRect(h, out r)) return false;
        double x0 = (r.L - _ox) / _sc, y0 = (r.T - _oy) / _sc, x1 = (r.R - _ox) / _sc, y1 = (r.B - _oy) / _sc;
        rect = new Rect(x0, y0, x1 - x0, y1 - y0);
        return rect.Width >= 220 && rect.Height >= 140;
    }

    // 随机挑一个可见窗口的一条边，先走到边上，再以较慢的速度沿边爬一段。没有合适的窗口返回 false
    bool StartWindowCrawl()
    {
        var wins = new List<Rect>();
        EnumWindows((hwnd, _) => { try { if (TryWindowRect(hwnd, out var wr)) wins.Add(wr); } catch { } return true; }, IntPtr.Zero);
        if (wins.Count == 0) return false;

        const double m = 30;
        double w = Math.Max(_w, 800), hh = Math.Max(_h, 600);
        for (int tries = 0; tries < 8; tries++)
        {
            var r = wins[_rnd.Next(wins.Count)];
            int edge = _rnd.Next(4);   // 0 上 1 右 2 下 3 左
            bool horiz = edge is 0 or 2;
            double fix = edge switch { 0 => r.Top, 1 => r.Right, 2 => r.Bottom, _ => r.Left };
            double lo = Math.Max((horiz ? r.Left : r.Top) + 20, (horiz ? _ax : _ay) + m);
            double hi = Math.Min((horiz ? r.Right : r.Bottom) - 20, (horiz ? _ax + _aw : _ay + _ah) - m);
            double fLo = (horiz ? _ay : _ax) + m, fHi = (horiz ? _ay + _ah : _ax + _aw) - m;
            if (fix < fLo || fix > fHi || hi - lo < 120) continue;   // 边在活动范围外或太短
            double len = Math.Min(hi - lo, 250 + _rnd.NextDouble() * 450);
            double s = lo + _rnd.NextDouble() * (hi - lo - len), e = s + len;
            if (_rnd.NextDouble() < 0.5) (s, e) = (e, s);
            Vector P(double t) => horiz ? new Vector(t, fix) : new Vector(fix, t);
            _route.Clear();
            _route.Add(P(s)); _route.Add(P(e));
            StopPose();
            _wp = 0; _pause = 0; _walking = true; _crawl = true;
            return true;
        }
        return false;
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
        double ang = _rnd.NextDouble() * Math.PI * 2, dist = 200 + _rnd.NextDouble() * 150;
        _dashTarget = new Vector(
            Math.Clamp(_pos.X + Math.Sin(ang) * dist, _ax + 80, _ax + _aw - 80),
            Math.Clamp(_pos.Y - Math.Cos(ang) * dist, _ay + 80, _ay + _ah - 80));
        SoundFx.Play("dash");
        CancelEat();
        _dash = true; _walking = true; _pause = 0;
        _crawl = false; StopPose();
    }

    void StartRoute()
    {
        _route.Clear();
        double m = Math.Min(160, Math.Min(_aw, _ah) / 4);
        int n = _rnd.Next(2, 5);
        for (int i = 0; i < n; i++)
            _route.Add(new Vector(_ax + m + _rnd.NextDouble() * (_aw - 2 * m), _ay + m + _rnd.NextDouble() * (_ah - 2 * m)));
        _wp = 0; _pause = 0; _walking = true;
        _crawl = false; StopPose();
    }

    // ───────── 每帧更新 ─────────
    void Tick()
    {
        double now = _clock.Elapsed.TotalSeconds, dt = Math.Min(now - _last, 0.05);
        _last = now;
        if (CheckHidden(now)) { SetInterval(250); return; }   // 有全屏应用时整个暂停：不移动、不响应点击，并降到很低的刷新率
        double speed = 0;

        _chase = !_dash && _eat == null && _flies.Count > 0;
        _follow = FollowMouse && !_dash && _eat == null && _flies.Count == 0;
        if (_sleeping && (_dash || _chase || _follow)) WakeUp();
        if (_chase) { _walking = true; _pause = 0; }

        if (_eat != null) UpdateEating(dt);
        else if (_follow) speed = UpdateFollow(dt);
        else if (_walking)
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
                        BeginEat(fly);   // 到达食物：先停下吃一会儿，吃完才消失
                    }
                    else if (_dash) { _dash = false; StartRoute(); }
                    else if (++_wp >= _route.Count) { if (_crawl && _pet == _spider) SpinWeb(_pos); _walking = false; _crawl = false; }
                    else _pause = 0.2 + _rnd.NextDouble() * 0.8;   // 走走停停
                }
                else
                {
                    double diff = Wrap(Math.Atan2(d.X, -d.Y) - _heading);
                    double turn = (_dash ? DashTurn : fly != null ? ChaseTurn : TurnRate) * dt;
                    _heading += Math.Clamp(diff, -turn, turn);
                    speed = (_dash ? DashSpeed : fly != null ? ChaseSpeed : _crawl && _wp >= 1 ? CrawlSpeed : BaseSpeed * Pace)
                            * Math.Clamp(1 - Math.Abs(diff) / 1.3, (_dash || fly != null) ? 0.5 : 0.15, 1.0);
                    _pos += new Vector(Math.Sin(_heading), -Math.Cos(_heading)) * (speed * dt);
                }
            }
        }

        PollInput(dt);
        UpdateIdle(dt);
        UpdatePrints(dt);
        UpdateWebs(dt);
        UpdateChain();
        UpdateLegs(dt, speed);
        Canvas.SetLeft(_pet.Body, _pos.X);
        Canvas.SetTop(_pet.Body, _pos.Y);
        _pet.Rot.Angle = (_heading + _nod) * 180 / Math.PI;   // 吃东西时头部点动

        bool active = speed > 1 || _eat != null || _dash || _chase || _waveT > 0 || _look || _twitchT > 0;
        SetInterval(active ? 16 : 33);
    }

    void UpdateLegs(double dt, double speed)
    {
        bool moving = speed > 1;
        double thresh = (moving ? 18 : 6) * _pet.StepScale * _size;
        double stepT = (_dash || _chase) ? DashStepTime : StepTime;
        var fwd = new Vector(Math.Sin(_heading), -Math.Cos(_heading));
        var busy = new bool[2];
        foreach (var l in _legs) if (l.Stepping) busy[l.Group] = true;
        foreach (var l in _legs)
        {
            if (l.Held) { l.Stepping = false; continue; }   // 抬腿中：脚的位置由 UpdateIdle 控制
            var rest = LW(l, l.Rest);
            var lf = l.SegIdx >= 0 ? Fwd(_segHead[l.SegIdx]) : fwd;   // 这条腿所在体节的前进方向
            if (l.Stepping)
            {
                l.T += dt / stepT;
                if (l.T >= 1) { l.T = 1; l.Stepping = false; if (moving) AddPrint(l.To); }   // 落脚时留一个脚印
                double e = l.T * l.T * (3 - 2 * l.T);
                l.Foot = l.From + (l.To - l.From) * e;
            }
            else if ((!busy[1 - l.Group] && Len(l.Foot - rest) > thresh) || Len(l.Foot - LW(l, l.Hip)) > _pet.Reach * l.K)
            {
                l.Stepping = true; l.T = 0; l.From = l.Foot;
                l.To = rest + lf * (moving ? (speed * stepT + 24) * _pet.Lead * l.K : 0);
                busy[l.Group] = true;
            }
        }
        foreach (var l in _legs) Draw(l);
    }

    void Draw(Leg l)
    {
        double k = l.K;
        var h = LW(l, l.Hip);
        double hd = l.SegIdx >= 0 ? _segHead[l.SegIdx] : _heading;
        var ctr = l.SegIdx >= 0 ? _segPos[l.SegIdx] : _pos;
        var p = h + RotBy(hd, l.Out) * (_pet.LenA * k);
        var q = l.Foot - Unit(l.Foot - p) * (_pet.LenD * k);
        var knee = Knee(p, q, _pet.LenB * k, _pet.LenC * k, ctr);
        Set(l.Seg[0], h, p); Set(l.Seg[1], p, knee); Set(l.Seg[2], knee, q); Set(l.Seg[3], q, l.Foot);
    }

    // 两节骨 IK：膝盖取离身体中心更远的那个解，保证朝外拱
    Vector Knee(Vector p, Vector q, double lb, double lc, Vector ctr)
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
        return Len(k1 - ctr) >= Len(k2 - ctr) ? k1 : k2;
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

    static Vector RotBy(double a, Vector v)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return new Vector(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    static Vector Fwd(double a) => new Vector(Math.Sin(a), -Math.Cos(a));

    // 腿的局部坐标 -> 世界坐标：分节宠物按所在体节的位置和朝向换算，刚性宠物按整体
    Vector LW(Leg l, Vector local) =>
        l.SegIdx >= 0 ? _segPos[l.SegIdx] + RotBy(_segHead[l.SegIdx], local) : ToWorld(local);

    // 分节宠物：把整条链拉直放到头的正后方（切换宠物、启动时调用）
    void ResetChain()
    {
        var segs = _pet.Segs;
        if (segs == null) return;
        int n = segs.Length;
        if (_segPos.Length != n) { _segPos = new Vector[n]; _segHead = new double[n]; }
        _chain = new Vector[n + 1];
        for (int i = 0; i <= n; i++) _chain[i] = _pos + Fwd(_heading) * (-_pet.SegGap * _size * i);
        UpdateChain();
    }

    // 每帧：链上每个点向前一个点靠拢并保持固定间距，身体因此沿头走过的轨迹弯曲；体节朝向 = 指向前一个点的方向
    void UpdateChain()
    {
        var segs = _pet.Segs;
        if (segs == null || _chain.Length != segs.Length + 1) return;
        double gap = _pet.SegGap * _size;
        _chain[0] = _pos;
        for (int i = 1; i < _chain.Length; i++)
            _chain[i] = _chain[i - 1] + Unit(_chain[i] - _chain[i - 1]) * gap;
        for (int k = 0; k < segs.Length; k++)
        {
            var p = _chain[k + 1];
            var d = _chain[k] - p;
            double ang = Math.Atan2(d.X, -d.Y);
            _segPos[k] = p;
            _segHead[k] = ang;
            Canvas.SetLeft(segs[k], p.X);
            Canvas.SetTop(segs[k], p.Y);
            _pet.SegRot![k].Angle = ang * 180 / Math.PI;
        }
    }
    // ───────── 睡觉 / 饥饿 / 躲鼠标 / 脚印 / 点击捕获 ─────────
    double Pace => (_night ? 0.75 : 1.0) * (1 - 0.3 * _hunger);   // 夜里慢一些，饿了也慢一些

    void StartSleep()
    {
        _sleeping = true;
        _sleepT = (_night ? 25 : 12) + _rnd.NextDouble() * 15;
        _look = false; _waveT = 0; _twitchT = 0;
        foreach (var l in _legs) l.Held = false;
    }

    void WakeUp()
    {
        _sleeping = false;
        _idleWait = IdleMin;
        ApplySize();   // 去掉呼吸造成的缩放偏差
        if (_zzz != null) _zzz.IsVisible = false;
    }

    void UpdateZzz()
    {
        if (_zzz == null) return;
        _zzz.IsVisible = true;
        _zzz.Opacity = 0.55 + 0.45 * Math.Sin(_clock.Elapsed.TotalSeconds * 2.2);
        Canvas.SetLeft(_zzz, _pos.X + 26 * _size);
        Canvas.SetTop(_zzz, _pos.Y - 56 * _size);
    }

    // 光标很快地冲到宠物身边时，宠物朝反方向快速躲开；躲完有 4 秒冷却
    void UpdateDodge(Vector cur, double dt)
    {
        double now = _clock.Elapsed.TotalSeconds, vel = 0;
        if (_curPrevT >= 0 && now > _curPrevT) vel = Len(cur - _curPrev) / (now - _curPrevT);
        _curPrev = cur; _curPrevT = now;
        if (_dodgeCd > 0) { _dodgeCd -= dt; return; }
        if (_dash || _follow || _eat != null || _flies.Count > 0) return;
        if ((GetAsyncKeyState(0x11) & 0x8000) != 0) return;   // 按着 Ctrl 是要喂食，不躲
        var away = _pos - cur;
        if (Len(away) < 130 * _size && vel > 900) StartFlee(away);
    }

    void StartFlee(Vector away)
    {
        var u = Unit(away);
        double dist = 260 + _rnd.NextDouble() * 120;
        _dashTarget = new Vector(
            Math.Clamp(_pos.X + u.X * dist, _ax + 80, _ax + _aw - 80),
            Math.Clamp(_pos.Y + u.Y * dist, _ay + 80, _ay + _ah - 80));
        _dodgeCd = 4;
        SoundFx.Play("dash");
        CancelEat();
        _dash = true; _walking = true; _pause = 0;
        _crawl = false; StopPose();
    }

    // 窗口是否接收点击：关掉 WS_EX_TRANSPARENT 就会接收，点击不再穿透
    void SetCapture(bool on)
    {
        if (_hwnd == IntPtr.Zero || on == _capturing) return;
        _capturing = on;
        long ex = GetWindowLongPtr(_hwnd, -20).ToInt64();
        ex = on ? ex & ~0x20L : ex | 0x20L;
        SetWindowLongPtr(_hwnd, -20, new IntPtr(ex));
    }

    // 脚印：落脚处放一个淡色小圆点，2.5 秒内淡出，最多同时 80 个
    void AddPrint(Vector p)
    {
        if (_sleeping) return;
        double d = Math.Max(2, 3.5 * _pet.Scale * _size);
        var e = new Avalonia.Controls.Shapes.Ellipse { Width = d, Height = d, Fill = PrintBrush, IsHitTestVisible = false };
        Canvas.SetLeft(e, p.X - d / 2); Canvas.SetTop(e, p.Y - d / 2);
        Stage.Children.Insert(0, e);   // 放在最底层，宠物盖在脚印上面
        _prints.Add(new Print { E = e, Life = 2.5 });
        if (_prints.Count > 80) { Stage.Children.Remove(_prints[0].E); _prints.RemoveAt(0); }
    }

    // 蜘蛛网：8 条辐射线加 3 圈环线，放在最底层，25 秒后淡出，最多同时 6 张
    void SpinWeb(Vector p)
    {
        if (_webs.Count >= 6) { Stage.Children.Remove(_webs[0].C); _webs.RemoveAt(0); }
        double R = 46 * _size, off = _rnd.NextDouble() * Math.PI;
        string F(double v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 8; i++)
        {
            double a = off + i * Math.PI / 4;
            sb.Append($"M 0,0 L {F(Math.Cos(a) * R)},{F(Math.Sin(a) * R)} ");
        }
        foreach (double k in new[] { 0.35, 0.65, 1.0 })
            for (int i = 0; i <= 8; i++)
            {
                double a = off + i * Math.PI / 4;
                sb.Append(i == 0 ? "M " : "L ").Append($"{F(Math.Cos(a) * R * k)},{F(Math.Sin(a) * R * k)} ");
            }
        var c = new Canvas { Width = 0, Height = 0, IsHitTestVisible = false };
        c.Children.Add(Sh(sb.ToString(), null, "#AADDDDDD", 0.8));
        Canvas.SetLeft(c, p.X); Canvas.SetTop(c, p.Y);
        Stage.Children.Insert(0, c);
        _webs.Add(new Web { C = c, Life = 25 });
    }

    void UpdateWebs(double dt)
    {
        for (int i = _webs.Count - 1; i >= 0; i--)
        {
            var w = _webs[i];
            w.Life -= dt;
            if (w.Life <= 0) { Stage.Children.Remove(w.C); _webs.RemoveAt(i); }
            else w.C.Opacity = Math.Min(1, w.Life / 3);
        }
    }

    void UpdatePrints(double dt)
    {
        for (int i = _prints.Count - 1; i >= 0; i--)
        {
            var p = _prints[i];
            p.Life -= dt;
            if (p.Life <= 0) { Stage.Children.Remove(p.E); _prints.RemoveAt(i); }
            else p.E.Opacity = Math.Min(1, p.Life / 1.2);
        }
    }

    static double Len(Vector v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);
    static Vector Unit(Vector v) { double n = Len(v); return n < 1e-9 ? new Vector(0, -1) : new Vector(v.X / n, v.Y / n); }
    static double Wrap(double a) { while (a > Math.PI) a -= 2 * Math.PI; while (a < -Math.PI) a += 2 * Math.PI; return a; }

    // ───────── 大小 / 速度 / 活动范围 / 退出快捷键 / 刷新率 ─────────
    public void SetSize(string s)
    {
        _size = s switch { "small" => 0.75, "large" => 1.4, _ => 1.0 };
        ApplySize();
        BuildLegs();   // 腿长、腿根、落点都按新大小重算，脚重新落地
    }

    void ApplySize()
    {
        foreach (var p in _pets.Values) p.BodySc.ScaleX = p.BodySc.ScaleY = p.Scale * _size;
        if (_centipede.SegSc != null)
            foreach (var sc in _centipede.SegSc) sc.ScaleX = sc.ScaleY = _size;
    }

    public void SetSpeed(string s)
    {
        BaseSpeed = 420 * (s switch { "slow" => 0.6, "fast" => 1.7, _ => 1.0 });
    }

    public void SetMonitor(int n)
    {
        _monitor = n;
        if (!_opened) return;   // 窗口还没打开，打开时会统一计算
        ApplyArea();
        if (_eat == null && _flies.Count == 0 && !FollowMouse) StartRoute();   // 重新规划路线，走进新的活动范围
    }

    // 按选择的显示器算出活动范围；序号超出显示器数量时退回所有显示器
    void ApplyArea()
    {
        _ax = 0; _ay = 0; _aw = _w; _ah = _h;
        if (_monitor <= 0) return;
        var all = Screens.All.OrderBy(s => s.Bounds.X).ToList();
        if (_monitor > all.Count) return;
        var b = all[_monitor - 1].Bounds;
        _ax = (b.X - _ox) / _sc; _ay = (b.Y - _oy) / _sc;
        _aw = b.Width / _sc; _ah = b.Height / _sc;
    }

    // 解析形如 Ctrl+Shift+Q / Ctrl+Alt+F12 的快捷键；必须至少一个修饰键加一个普通键，否则回退 Ctrl+Shift+Q
    public void SetHotkey(string? s)
    {
        var vks = new List<int>();
        bool ok = !string.IsNullOrWhiteSpace(s);
        if (ok)
        {
            foreach (var raw in s!.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                string t = raw.ToUpperInvariant();
                int vk = t switch { "CTRL" or "CONTROL" => 0x11, "SHIFT" => 0x10, "ALT" => 0x12, _ => 0 };
                if (vk == 0 && t.Length == 1 && t[0] is >= 'A' and <= 'Z') vk = t[0];
                else if (vk == 0 && t.Length is 2 or 3 && t[0] == 'F' && int.TryParse(t[1..], out int fn) && fn is >= 1 and <= 12) vk = 0x70 + fn - 1;
                if (vk == 0) { ok = false; break; }
                vks.Add(vk);
            }
        }
        bool hasMod = vks.Exists(v => v is 0x11 or 0x10 or 0x12);
        bool hasKey = vks.Exists(v => v is not (0x11 or 0x10 or 0x12));
        _hotVks = ok && hasMod && hasKey ? vks.ToArray() : new[] { 0x11, 0x10, 0x51 };
    }

    // 刷新率：活动时 16ms，静止 33ms，全屏隐藏 250ms；只在变化时才改定时器
    void SetInterval(int ms)
    {
        if (_timer == null || ms == _intervalMs) return;
        _intervalMs = ms;
        _timer.Interval = TimeSpan.FromMilliseconds(ms);
    }
}