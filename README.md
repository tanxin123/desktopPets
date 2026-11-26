# 桌面宠物 (DesktopPet)

一个基于 Avalonia 的 Windows 桌面宠物。透明、置顶、点击穿透的全屏窗口里，有一只会自己走动的蜘蛛或蟑螂。

## 功能

- **两种宠物**：蜘蛛（8 条腿）和蟑螂（6 条腿）。在托盘图标上右键即可切换。
- **步态**：每条腿由两节骨的 IK 驱动，按交替步态迈步，膝盖始终朝外。
- **随机移动**：走几个随机路径点，走走停停，走完后停在原地。
- **点宠物**：宠物会快速转向并冲刺到附近的 B 点，再以正常速度走向随机的 C 点。
- **点其他位置**：在点击处出现一只黑色小苍蝇，宠物跑过去吃掉。屏幕上同时只有一只苍蝇。
- **多显示器**：窗口覆盖所有显示器组成的虚拟桌面。
- **单实例**：同一时间只能运行一个，桌面上不会出现两只宠物。
- **点击穿透**：窗口不拦截鼠标，不影响你操作其他软件。点击由全局轮询判断。

## 操作

| 操作 | 效果 |
| --- | --- |
| 点击宠物身体附近 | 冲刺后继续随机走动 |
| 点击其他位置 | 放一只苍蝇，宠物去吃 |
| 托盘图标右键 → 蜘蛛 / 蟑螂 | 切换宠物 |
| 托盘图标右键 → 退出，或 `Ctrl+Shift+Q` | 退出程序 |

## 运行要求

- Windows 10 及以上
- [.NET 8 运行时](https://dotnet.microsoft.com/download/dotnet/8.0)（不带运行时的发布版需要）
- **需要管理员权限**：程序在清单里请求管理员，每次启动会弹出 UAC。否则点不到在管理员权限下运行的软件上方的宠物。

## 构建

```bash
git clone <本仓库地址>
cd Spider
dotnet build -c Debug
```

启动前先退出正在运行的实例，否则 `DesktopPet.exe` 被占用，编译会失败。

## 发布（单文件，不带运行时）

```bash
dotnet publish -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=none -p:DebugSymbols=false -p:SatelliteResourceLanguages=en -o publish
```

输出是 `publish\DesktopPet.exe`。不建议加 `PublishTrimmed`，Avalonia 的 XAML 加载依赖反射，裁剪后容易启动失败。

## 项目结构

```
Spider/
├── Program.cs              入口，单实例互斥体
├── App.axaml(.cs)          托盘图标与菜单
├── MainWindow.axaml(.cs)   透明窗口、运动逻辑、输入轮询、苍蝇
├── Views/
│   ├── SpiderView.axaml    蜘蛛的外形与样式
│   └── RoachView.axaml     蟑螂的外形与样式
├── Assets/spider.png       托盘图标
└── app.manifest            管理员权限清单
```

两种宠物的外形和样式分别放在各自的 View 里，互不共用。体型参数（腿长、落点、缩放）在 `MainWindow.axaml.cs` 的 `Pet` 对象里。

## 调整

- 移动速度：`BaseSpeed`、`TurnRate`；冲刺：`DashSpeed`、`DashTurn`；追苍蝇：`ChaseSpeed`、`ChaseTurn`。
- 蟑螂腿长：`_roach` 里的 `LenA`~`LenD`、`Reach`、`Rest`，以及每对腿的缩放系数 `LegScale`。

## 已知限制

- 窗口点击穿透，所以点击会同时落到下面的程序上，任何位置的左键点击都会出苍蝇。
- 多显示器且缩放比例不一致时，活动范围和点击坐标可能有偏差。
- 外形是用 XAML 图形手绘的，并不追求完全写实。

## 许可证

待定（请自行选择，例如 MIT）。