# 020-railway-interlock-sim 铁路信号联锁运行模拟器

轨道交通控制专业教具 - 基于 Unity (C#) 引擎开发的俯视角铁路运行模拟器

---

## 项目概述

本项目是一套完整的铁路信号联锁仿真教学系统，包含：

- **严密的信号联锁（Interlocking）状态机系统** - 纯 C# 实现的布尔逻辑计算引擎
- **3股道复杂站场** - 包含 4 副道岔、12 个轨道区段、9 架信号机的演示站场
- **列车运行仿真** - 支持自动驾驶、物理制动、红灯触发紧急制动
- **信号显示控制** - 红/黄/绿三色信号机，按铁路行业规则动态计算
- **调试控制台** - 完整的 GUI 控制面板与键盘快捷键

---

## 快速开始

### 方法一：Unity 菜单一键初始化（推荐）

1. 用 Unity Hub 打开本项目（推荐 Unity 2021.3 LTS 或更高版本）
2. 点击顶部菜单 **「铁路联锁仿真」→「初始化项目...」**
3. 确认对话框后，系统会自动创建场景、文件夹并构建演示站场
4. 点击 Unity 工具栏 **Play ▶** 按钮运行

### 方法二：手动创建空场景

1. 创建一个新的空场景
2. 在场景中添加一个空 GameObject，命名任意
3. 为该 GameObject 添加 **`SceneBootstrapper`** 组件
4. 勾选 `Auto Setup On Start`
5. 点击 Play，系统将自动构建所有内容

---

## 项目架构

```
Assets/Scripts/
├── Core/                          # 核心架构定义
│   ├── Enums.cs                   # 信号/道岔/轨道状态等枚举
│   ├── Interfaces.cs              # 联锁系统各组件接口
│   └── DataStructures.cs          # 轨道/道岔/信号/进路数据结构
│
├── Interlocking/                  # 联锁逻辑核心
│   ├── BooleanLogicEvaluator.cs   # 布尔逻辑计算器（条件组求值）
│   └── InterlockController.cs     # 联锁控制器（进路建立/取消/信号计算）
│
├── Components/                    # Unity 组件
│   ├── TrackCircuit.cs            # 轨道区段（碰撞体检测占用状态）
│   ├── SwitchPoint.cs             # 道岔（定位/反位转换 + 动作动画）
│   ├── Signal.cs                  # 信号机（红/黄/绿灯 + 发光材质）
│   └── Train.cs                   # 列车（刚体运动 + 信号检测 + 自动制动）
│
├── Management/
│   └── GameManager.cs             # 全局管理器 + 事件协调
│
├── Scenarios/                     # 场景配置
│   ├── ScenarioBuilder.cs         # 程序化场景构建工具
│   └── DemoScenarioSetup.cs       # 3股道演示站场完整配置
│
├── UI/
│   └── DebugControlPanel.cs       # 运行时调试控制台 GUI
│
├── CameraSystem/
│   └── TopDownCameraController.cs # 俯视角摄像机控制（缩放/平移/旋转）
│
├── Setup/
│   ├── SceneBootstrapper.cs       # 场景引导启动器
│   └── AutoSceneLoader.cs         # 空场景自动检测与加载
│
├── Editor/
│   ├── RailwaySceneSetup.cs       # Unity 编辑器菜单工具
│   └── RailwayInterlock.Editor.asmdef
│
└── RailwayInterlock.asmdef        # 主程序集定义
```

---

## 核心系统详解

### 1. 信号联锁状态机 (Interlocking)

#### 布尔逻辑计算引擎
[BooleanLogicEvaluator.cs](file:///d:/SOLO-13/020-railway-interlock-sim/Assets/Scripts/Interlocking/BooleanLogicEvaluator.cs)

支持的条件类型：
- **轨道占用条件** `TrackOccupiedCondition` - 区段占用/空闲判断
- **道岔位置条件** `SwitchPositionCondition` - 道岔定位/反位判断  
- **信号机显示条件** `SignalAspectCondition` - 前方信号红/黄/绿判断

条件组合规则：
```
条件组内所有条件 → AND 关系
多个条件组之间 → OR 关系
```

#### 联锁控制器
[InterlockController.cs](file:///d:/SOLO-13/020-railway-interlock-sim/Assets/Scripts/Interlocking/InterlockController.cs)

核心功能：
- `CanSetRoute()` - 检查进路是否可建立（冲突/占用检测）
- `SetRoute()` - 建立进路（转换道岔→检查一致性→锁闭）
- `CancelRoute()` - 取消进路
- `EvaluateAllSignals()` - 重新计算所有信号机显示
- `CalculateSignalAspect()` - 按闭塞分区+进路状态计算显示

### 2. 轨道区段 (TrackCircuit)

[TrackCircuit.cs](file:///d:/SOLO-13/020-railway-interlock-sim/Assets/Scripts/Components/TrackCircuit.cs)

- 使用 **BoxCollider (Trigger)** 检测列车进入/离开
- 支持多列车占用计数（引用计数方式）
- 颜色变化：空闲=深灰，占用=红色（含自发光）
- 事件回调：`OnStateChanged` 触发联锁重新计算

### 3. 道岔 (SwitchPoint)

[SwitchPoint.cs](file:///d:/SOLO-13/020-railway-interlock-sim/Assets/Scripts/Components/SwitchPoint.cs)

- 两种位置：**定位(Normal)** - 直线 / **反位(Reverse)** - 侧线
- 转换动画：可调 `switchTime`（默认1.5秒）
- 占用锁闭：区段被占用时禁止转换
- 位置一致性检查：`IsInConsistentPosition()`（供联锁检查用）

### 4. 信号机 (Signal)

[Signal.cs](file:///d:/SOLO-13/020-railway-interlock-sim/Assets/Scripts/Components/Signal.cs)

三种显示：
- 🔴 **红灯** - 禁止越过（进路未建立/前方占用）
- 🟡 **黄灯** - 减速运行（下一闭塞分区占用/闪烁显示）
- 🟢 **绿灯** - 正常速度（前方至少两个闭塞分区空闲）

技术实现：
- 使用 `_EmissionColor` 实现发光效果
- 黄灯使用正弦波实现闪烁动画
- 配置 `stopZone`（碰撞体）供列车检测

### 5. 列车 (Train)

[Train.cs](file:///d:/SOLO-13/020-railway-interlock-sim/Assets/Scripts/Components/Train.cs)

自动控制逻辑：
```
每帧检测前方信号 →
  若红灯：
    计算所需制动距离 = v² / (2·减速度)
    距离 ≤ 制动距离+安全距离 → 施加常用制动
    距离 ≤ 安全距离 且 速度>0.5m/s → 触发紧急制动
  若绿灯/黄灯：
    缓解制动 → 加速至限速
```

物理参数（可调）：
| 参数 | 默认值 | 说明 |
|------|--------|------|
| MaxSpeedKmh | 60~80 | 最大运行速度 |
| Acceleration | 1.5 m/s² | 启动加速度 |
| BrakeDeceleration | 4 m/s² | 常用制动减速度 |
| EmergencyBrake | 8 m/s² | 紧急制动减速度 |

---

## 演示站场配置

[DemoScenarioSetup.cs](file:///d:/SOLO-13/020-railway-interlock-sim/Assets/Scripts/Scenarios/DemoScenarioSetup.cs) 构建了如下站场：

```
                    X1  X2  X3        ← 出站信号机(下行方向)
         ┌───────────────────────────┐
T1道:   │ T1-1 │ T1-2 │ T1-3 │ T1-4 │  ← 1道（4个闭塞分区）
         └────×SW3────×SW1─────┘
T2道:   │ T2-1 │ T2-2 │ T2-3 │ T2-4 │  ← 2道
         └────×SW4────×SW2─────┘
T3道:   │ T3-1 │ T3-2 │ T3-3 │ T3-4 │  ← 3道
         ┌───────────────────────────┐
                    S1  S2  S3        ← 进站信号机(上行方向)

SW1/SW2 - 南端渡线道岔     SW3/SW4 - 北端渡线道岔
SD1~SD4  - 4条渡线轨道
```

**预置进路（共7条）**：

| 编号 | 进路ID | 名称 | 涉及道岔 |
|------|--------|------|---------|
| 1 | ROUTE_UP_1 | 上行1道通过 | SW1定、SW3定 |
| 2 | ROUTE_UP_1_TO_2 | 上行1道转2道 | SW1定、SW3反 |
| 3 | ROUTE_UP_2 | 上行2道通过 | SW2定、SW4定 |
| 4 | ROUTE_UP_2_TO_3 | 上行2道转3道 | SW2定、SW4反 |
| 5 | ROUTE_UP_3 | 上行3道通过 | - |
| 6 | ROUTE_DOWN_2 | 下行2道通过 | SW2定、SW4定 |
| 7 | ROUTE_DOWN_3 | 下行3道通过 | - |

**预置列车（共2列）**：
- **T001 红箭1号** - 位于站场北端（T1道北侧），上行方向，80km/h
- **T002 白驹2号** - 位于站场南端（T2道南侧），下行方向，60km/h

---

## 操作指南

### 运行时控制台（左上角）

| 分区 | 功能 |
|------|------|
| **仿真控制** | 暂停/继续、重置、重建场景、速度倍率(0.1x~5x) |
| **进路管理** | 每条进路：显示状态（未设置/设置中/已设置/占用/取消中）+ 建立/取消按钮 |
| **道岔控制** | 每副道岔：位置状态 + 切换按钮（占用时禁用） |
| **信号机状态** | 9架信号机实时显示（红/黄/绿） |
| **列车状态** | 状态/速度/前方信号/当前轨道 + 手动切换/紧急制动/鸣笛 |
| **轨道区段** | 12个轨道区段的占用状态 |

### 键盘快捷键

| 按键 | 功能 |
|------|------|
| `Space` | 暂停 / 继续仿真 |
| `R` | 重置仿真 |
| `1`~`9` | 建立第 N 条进路 |
| `Shift` + `1`~`9` | 取消第 N 条进路 |
| `Q` / `W` / `E` | 切换道岔 1 / 2 / 3 |
| `A` / `S` / `D` | 切换道岔 4 / 5 / 6 |
| `F` | 鸣笛 |
| `鼠标滚轮` | 缩放视图 |
| `鼠标中键拖拽` | 平移视图 |
| `鼠标右键拖拽` | 旋转视角 |
| `WASD` | 键盘平移（Shift加速） |
| `Q/E` | 升高/降低摄像机 |
| `方向键` | 旋转/俯仰视角 |

---

## 典型教学演示流程

### 演示1：正常通过进路
1. 点击 Play 启动仿真
2. 在控制台「进路管理」中，点击 `ROUTE_UP_1` 的【建立】按钮
3. 观察：
   - SW1、SW3 道岔自动转到定位
   - S1、SZ1、X1 信号机依次点亮绿灯
   - T001列车自动加速运行，依次通过T1-1→T1-2→T1-3→T1-4
   - 列车占用时轨道区段变红，离开后恢复灰色

### 演示2：红灯制动
1. 启动仿真，暂不建立任何进路（所有信号均为红灯）
2. 列车T001接近S1信号机时：
   - 先施加常用制动（减速）
   - 到达停车距离时触发紧急制动（鸣笛+烟雾效果）
   - 列车精确停在红灯信号机前

### 演示3：进路冲突检查
1. 先建立 `ROUTE_UP_1`（1道上行通过）
2. 尝试建立 `ROUTE_UP_1_TO_2`，观察按钮禁用（冲突）
3. 或尝试建立 `ROUTE_DOWN_2`（方向相反，部分区段重叠）

### 演示4：道岔转换
1. 手动点击道岔控制按钮切换SW1
2. 观察道岔的动画移动过程（约2秒）
3. 让列车进入T1-3区段后再次尝试切换道岔，观察：
   - 按钮禁用（占用锁闭）
   - 日志提示「道岔被占用，无法转换」

### 演示5：黄灯显示
1. 建立 `ROUTE_UP_1`，让T001进入T1-2区段
2. 观察SZ1（中转信号）变为红灯，S1变为黄灯
3. 解释：黄灯表示下一个闭塞分区已占用，需减速运行

---

## 信号显示规则表（参考）

| 前方闭塞分区 | 次前方闭塞分区 | 出站信号 | 本信号显示 |
|-------------|--------------|---------|----------|
| 占用 | - | - | 🔴 红灯 |
| 空闲 | 占用 | - | 🟡 黄灯 |
| 空闲 | 空闲 | 红灯 | 🟡 黄灯 |
| 空闲 | 空闲 | 黄灯 | 🟡 黄灯 |
| 空闲 | 空闲 | 绿灯 | 🟢 绿灯 |

---

## 自定义扩展

### 添加新的轨道区段

1. 在场景中添加Cube，添加 `TrackCircuit` 组件
2. 设置 `trackId`（唯一标识）、`displayName`
3. 添加 `BoxCollider` 并设置为 Trigger，调整大小覆盖轨道
4. 在 `DemoScenarioSetup.BuildTrackLayout()` 中注册

### 定义复杂信号逻辑（布尔条件）

```csharp
// 示例：信号X1绿灯条件
var greenLogic = new SignalLogicCondition
{
    TargetAspect = SignalAspect.Green,
    ConditionGroups = new List<ConditionGroup>
    {
        new ConditionGroup
        {
            TrackConditions = new List<TrackOccupiedCondition>
            {
                new() { TrackId = "T1-1", ShouldBeOccupied = false },
                new() { TrackId = "T1-2", ShouldBeOccupied = false },
                new() { TrackId = "T1-3", ShouldBeOccupied = false },
            },
            SwitchConditions = new List<SwitchPositionCondition>
            {
                new() { SwitchId = "SW1", RequiredPosition = SwitchPosition.Normal },
            },
            SignalConditions = new List<SignalAspectCondition>
            {
                new() { SignalId = "X2", RequiredAspect = SignalAspect.Green },
            }
        }
    }
};
```

### 添加新列车

1. 创建GameObject，添加 `Train`、`Rigidbody`、`BoxCollider`
2. 设置 `maxSpeedKmh`、`travelDirection`
3. 在车头前方创建 `SignalDetectionPoint` 空物体
4. 在 `GameManager.trains` 列表中注册

---

## 技术要点说明

### 联锁检查触发时机
- 轨道区段占用/空闲变化 → `TrackCircuit.OnStateChanged` 事件
- 道岔转换完成 → `SwitchPoint.OnPositionChanged` 事件
- 定时器（默认每50ms） → `GameManager.Update()`

### 事件流
```
列车进入区段 → TriggerEnter → TrackCircuit.SetOccupied()
    → OnStateChanged 事件
    → InterlockController.EvaluateAllSignals()
    → BooleanLogicEvaluator 重新计算
    → 所有信号机 SetAspect() 更新显示
    → 列车检测到信号变化 → ApplyBrake() / ReleaseBrake()
```

### 刚体运动 vs 直接变换
列车使用 `Rigidbody` 物理系统模拟：
- 优点：真实的惯性/制动感，支持碰撞
- 速度设置通过 `Rigidbody.velocity = moveDir * speedMs`
- 冻结旋转防止侧翻

---

## 兼容性

- **推荐 Unity 版本**：2021.3 LTS 及以上
- **渲染管线**：内置渲染管线（使用 Standard Shader）
- **平台**：PC/Mac Standalone（开发模式），编辑器运行即可
- **输入系统**：使用旧 Input Manager（兼容 Input System Package）

---

## 许可

轨道交通控制专业教学教具用途。
