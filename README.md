# 桌面宠物 (DesktopPet)

一个基于 Avalonia 的 Windows 桌面宠物。透明、置顶、点击穿透的全屏窗口里，有一只会自己走动的蜘蛛、蟑螂、甲虫、蚂蚁或蜈蚣。

## 功能

- **五种宠物**：蜘蛛（8 条腿）、蟑螂、甲虫、蚂蚁（各 6 条腿）和蜈蚣（18 对腿，转弯时身体会弯曲）。托盘图标右键切换，选择会记住。
- **步态**：每条腿由两节骨的 IK 驱动，按交替步态迈步，膝盖始终朝外。
- **随机移动**：走几个随机路径点，走走停停，走完后停在原地。
- **点宠物**：宠物会快速转向并冲刺到附近的 B 点，再以正常速度走向随机的 C 点。
- **喂食与不同的食物**：按住 `Ctrl` 再点其他位置，在点击处放一份食物，食物种类由当前宠物决定。屏幕上同时只有一份，托盘菜单里可以关闭喂食。

  | 宠物 | 食物 |
  | --- | --- |
  | 蜘蛛 | 苍蝇（黑色剪影） |
  | 蟑螂 | 面包屑 |
  | 甲虫 | 树叶 |
  | 蚂蚁 | 糖粒 |
  | 蜈蚣 | 小虫 |

- **吃东西动画**：宠物跑到食物旁会先停下，前腿抬起，头一点一点，食物逐渐变小，约 1.3 秒后吃完消失。吃的时候点宠物会被打断去冲刺，食物保留，之后继续去吃。
- **跟随鼠标**：托盘菜单开启后，宠物离光标超过一定距离就走过去，靠近后停下并保持一段距离。有食物时先去吃食物，点宠物冲刺也优先。
- **空闲小动作**：宠物停下时会随机抖触角、原地转头、抬前腿、沿屏幕边缘爬一段，或者沿别的窗口的边缘爬一段（上下左右任选一条边）。
- **全屏自动隐藏**：前台是游戏或全屏视频时，宠物自动隐藏并暂停，退出全屏后恢复（可在托盘菜单关闭）。
- **开机自启**：托盘菜单勾选后，会注册一个最高权限的登录计划任务，开机登录时自动启动，不会弹 UAC。
- **记住设置**：宠物选择和各个开关保存在 `%AppData%\DesktopPet\settings.json`，下次启动自动读取。
- **多显示器**：窗口覆盖所有显示器组成的虚拟桌面。
- **单实例**：同一时间只能运行一个，桌面上不会出现两只宠物。
- **点击穿透**：窗口不拦截鼠标，不影响你操作其他软件。点击由全局轮询判断。

## 操作

| 操作 | 效果 |
| --- | --- |
| 点击宠物身体附近 | 冲刺后继续随机走动 |
| 按住 `Ctrl` 点击其他位置 | 放一份食物，宠物去吃 |
| 不按 `Ctrl` 点击其他位置 | 无反应（点击仍会落到下面的程序） |
| 托盘图标右键 → 蜘蛛 / 蟑螂 / 甲虫 / 蚂蚁 / 蜈蚣 | 切换宠物（场上的食物会被清掉） |
| 托盘图标右键 → 启用喂食 / 全屏时自动隐藏 / 跟随鼠标 / 开机自启 | 开关对应功能 |
| 托盘图标右键 → 退出，或 `Ctrl+Shift+Q` | 退出程序 |

## 配置文件

`%AppData%\DesktopPet\settings.json`，首次运行自动生成，文件损坏或值不合法时回退到默认值。

| 字段 | 含义 | 默认 |
| --- | --- | --- |
| `Pet` | `spider` / `roach` / `beetle` / `ant` / `centipede` | `spider` |
| `FeedingEnabled` | 是否允许喂食 | `true` |
| `AutoHideFullscreen` | 前台全屏应用时自动隐藏 | `true` |
| `FollowMouse` | 跟随鼠标 | `false` |
| `AutoStart` | 开机自启（启动时以系统里计划任务的实际状态为准） | `false` |

## 运行要求

- Windows 10 及以上
- [.NET 8 运行时](https://dotnet.microsoft.com/download/dotnet/8.0)（不带运行时的发布版需要）
- **需要管理员权限**：程序在清单里请求管理员，手动启动时会弹出 UAC。否则点不到在管理员权限下运行的软件上方的宠物。开机自启走计划任务，不会弹。

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
├── AppSettings.cs          配置文件读写
├── AutoStart.cs            开机自启（schtasks 计划任务）
├── MainWindow.axaml(.cs)   透明窗口、运动逻辑、输入轮询、食物与吃东西、跟随鼠标、攀爬窗口边缘
├── Views/
│   ├── SpiderView.axaml    蜘蛛的外形与样式
│   ├── RoachView.axaml     蟑螂的外形与样式
│   ├── BeetleView.axaml    甲虫的外形与样式
│   ├── AntView.axaml       蚂蚁的外形与样式
│   └── CentipedeView.axaml 蜈蚣（体节与腿由代码生成）
├── Assets/spider.png       托盘图标
└── app.manifest            管理员权限清单
```

每种宠物的外形和样式分别放在各自的 View 里，互不共用。体型参数（腿长、落点、缩放、食物种类）在 `MainWindow.axaml.cs` 的 `Pet` 对象里。

## 调整

以下常量都在 `MainWindow.axaml.cs` 顶部：

- 移动速度：`BaseSpeed`、`TurnRate`；冲刺：`DashSpeed`、`DashTurn`；追食物：`ChaseSpeed`、`ChaseTurn`。
- 吃东西时长：`EatTime`；跟随鼠标的出发 / 停下距离：`FollowFar`、`FollowNear`。
- 沿边爬行速度：`CrawlSpeed`；空闲小动作间隔：`IdleMin`、`IdleMax`。
- 每种宠物的腿长和落点：构造函数里对应 `Pet` 的 `LenA`~`LenD`、`Reach`、`Rest`，以及每对腿的缩放系数 `LegScale`。
- 食物：`Pet.Food`（`fly` / `crumb` / `leaf` / `sugar` / `worm`）和 `Pet.FoodScale`。想加新食物，在 `AddFly` 的 `switch` 里加一项并写一个对应的 `MakeXxx()`。

## 已知限制

- 窗口点击穿透，所以点击会同时落到下面的程序上；按住 `Ctrl` 点击放食物时，下面的程序也会收到这次 Ctrl+点击（例如文件管理器里的多选）。
- 开机自启用 `schtasks` 创建的任务，默认在笔记本使用电池时可能不会启动；程序挪了位置后，需要关掉再重新勾选一次“开机自启”。
- 沿窗口边缘爬只在开始时读取一次窗口位置，爬行中窗口被移动或被别的窗口遮挡，宠物不会跟着变化。宠物窗口是置顶的，所以总是画在最上层。
- 多显示器且缩放比例不一致时，活动范围和点击坐标可能有偏差。
- 外形是用 XAML 图形手绘的，并不追求完全写实。

## 许可证

随便改 反正是ai生成的
