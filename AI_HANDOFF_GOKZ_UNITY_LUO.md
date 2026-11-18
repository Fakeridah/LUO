# LUO 项目：GOKZ / KZTimer Unity 移植交接文档

> 文档用途：把本项目的背景、目标、现状、已完成内容、已知问题、源码依据和协作规则交给其他 AI 或开发者继续完成。
>
> 当前状态：进行中。本文档描述的是已经检查到的事实，不代表运动系统已经达到 GOKZ 源码一致性。
>
> 建立日期：2026-11-13
>
> 当前工作区状态以第 17 节为准；早期会话记录与阶段计划是历史快照，若与当前代码冲突，不得照抄为现状。

---

## 0. 给下一位 AI 的直接指令

你正在接手一个 Unity 6.6 项目。请先完整阅读本文档，再阅读下面列出的项目文件和官方源码。

本项目的核心目标不是“做一个类似 KZ 的移动控制器”，而是：

> 在 Unity 中尽可能逐 tick、逐状态、逐调用阶段地复现 GOKZ KZTimer 在 CS:GO Source 引擎中的运动行为，同时保留 LUO 原有角色、事件、武器、触发器、反向重力、失重和旧控制器作为兼容参考。

必须遵守以下规则：

1. 不得把 SimpleKZ、bhop、surf 或自行设计的移动逻辑冒充 GOKZ KZTimer。
2. 不得为了让测试数字好看而添加无源码依据的最终速度硬截断。
3. 只有在本地 GOKZ 源码、本地 CS:GO 泄漏源码或 GOKZ 实际依赖的 MovementAPI 行为依据支持时，才可以修改运动规则。
4. 任何无法直接从公开源码确认的 CS:GO 引擎行为，都必须标记为“待参考运行数据验证”，不能写成“已经完全一致”。
5. 原有 `CharacterController` 路径和旧 `SurfController` 代码不能删除，应保留作为回退和参考。
6. 修改前先说明：源码依据、当前差异、修改范围、预期影响和验证方法。
7. Unity 场景、Prefab、事件和其他项目功能不能被无关重构破坏。
8. 优先使用 Unity CLI / Unity MCP / 项目内测试，不要控制用户桌面，不要擅自重启 Unity 或电脑。
9. 每次修改后都要确认 Unity 编译状态、场景是否变脏、测试结果和实际启用的组件路径。
10. 最终目标是可验证的源码行为一致性，而不是“看起来差不多”。

---

## 1. 项目基本信息

| 项目 | 当前信息 |
|---|---|
| Unity 项目 | `E:\Ridah\Unity\Projects\LUO` |
| Unity 版本 | Unity `6000.6.8f1`，即 Unity 6.6 系列 |
| 当前场景 | `Assets/Scenes/FirstRoom/FirstRoom.unity` |
| 当前活动场景状态 | 已加载，当前检查时未保存修改 |
| 当前玩家 | `/Player/RidahFPS` |
| 玩家脚本 | `LZM`，文件为 `Assets/Ridah/RidahFPS/Movement/SurfCharacter.cs` |
| 当前运动模式 | `UseGoKzMovement = true`，`GokzRuntime.mode = KzTimer` |
| 运动 tick | 128 Hz，`1 / 128` 秒 |
| 原有控制器 | `CharacterController`，仍保留但 GOKZ 路径运行时会禁用 |
| 新碰撞体 | 玩家子物体 `GokzSourceHull`，运行时用于盒体扫掠 |
| 地面层 | Layer `Wall`，编号 8 |
| 玩家根对象/碰撞体层 | Player 根对象为 Layer `Gameplay`（6）、Tag `Player`；场景中的 `GokzSourceHull` 子对象为 Layer `Player`（9） |
| 当前墙/地面碰撞 | Player 与 Wall 未被 Layer Collision Matrix 忽略 |

### 项目背景

LUO 原本已经有一个基于 `CharacterController` 和旧 KZ/Surf 逻辑的角色控制器。项目中还存在大量其他系统，包括：

- 武器和射击系统；
- PlayerAction、Spawn、重置、传送和事件逻辑；
- 触发器、推进器、弹跳器、失重和反向重力；
- 梯子和水体；
- HUD、速度显示、跳跃统计和计时器；
- 相机、角色模型和视角控制；
- URP 后处理和其他场景功能。

目标是在不破坏这些系统的前提下，把角色的核心移动模式替换成 GOKZ KZTimer 逻辑。

---

## 2. 权威参考来源

### 2.1 GOKZ KZTimer 模式

主要参考：

- [GOKZ 官方仓库](https://github.com/KZGlobalTeam/gokz)
- [gokz-mode-kztimer.sp](https://github.com/KZGlobalTeam/gokz/blob/master/addons/sourcemod/scripting/gokz-mode-kztimer.sp)
- [GOKZ 插件清单](https://github.com/KZGlobalTeam/gokz/blob/master/PLUGINS.md)
- [GOKZ README](https://github.com/KZGlobalTeam/gokz/blob/master/README.md)

重要事实：GOKZ 不是完整的 Source 引擎源码。它是依赖 CS:GO 原生 `CCSGameMovement`、SourceMod、DHooks 和 MovementAPI 的插件。仅仅复制 `gokz-mode-kztimer.sp` 不能得到完整的 CS:GO 移动实现。

### 2.2 MovementAPI

本项目当前随附的 MovementAPI 文件只有 `ThirdParty/gokz-master/addons/sourcemod/scripting/include/movementapi.inc`。它是接口声明，不含 MovementAPI 的运行时代码。插件里能看到 GOKZ 如何订阅 MovementAPI 事件、读取 `player.HitPerf` 并在条件成立时修改跳跃速度；但不能从这个 include 推断 `HitPerf` 的内部判定时序。

旧记录中把 Perf 解释成由 `OldWalkMoved` 判定的说法，随附源码并不能支持，应视为未证实。任何关于 Perf 窗口或 MovementAPI landing/takeoff 更新先后的结论，都必须有 MovementAPI 实现源码或真实参考轨迹支撑。当前 Unity 的 `_landedThisTick` 是项目内近似，不能写成 MovementAPI 的等价实现。

### 2.3 CS:GO 引擎移动代码

唯一的 CS:GO 引擎参考是用户放入项目的 `ThirdParty/cstrike15_src-master`，对应仓库 [`perilouswithadollarsign/cstrike15_src`](https://github.com/perilouswithadollarsign/cstrike15_src)。这是社区发布的 CS:GO 泄漏源码，不是 Valve 官方 SDK，也不能据此声称已获得 Valve 官方完整源码。

运动规则必须优先逐项对照本地 CS:GO 覆盖实现：

- `ThirdParty/cstrike15_src-master/game/shared/cstrike15/cs_gamemovement.cpp`：`CCSGameMovement` 的 `ProcessMovement`、`PlayerMove`、`CheckJumpButton`、`Duck`、`Accelerate`、`OnJump`、`OnLand`、梯子与体力逻辑。
- `ThirdParty/cstrike15_src-master/game/shared/gamemovement.cpp`：仅当本地 `CCSGameMovement` 继承或实际调用基类实现时，才用它解释对应的被调用阶段，例如 `FullWalkMove`、`Friction`、`WalkMove`、`AirMove`、`AirAccelerate`、`TryPlayerMove`、`CategorizePosition`、`StartGravity`/`FinishGravity`。
- 同仓库中的 `game/shared/cstrike15/cs_player_shared.cpp`、`cs_player_shared.h` 与 `cs_gamemovement.cpp`：核对 CS:GO 玩家状态、duck 变量、视角偏移及其与移动代码的关系。

不得用 Valve Source SDK 2013、Source SDK Base、通用 Source 示例或其他 Source 游戏的实现替代上述本地 CS:GO 源码。即使引用同仓库的 `gamemovement.cpp`，也必须先确认 CS:GO 的覆盖函数是否调用它，以及调用发生的准确阶段。

---

## 3. GOKZ KZTimer 的核心规则

官方 KZTimer 模式中目前关注的关键值：

| 参数 | 值 | 说明 |
|---|---:|---|
| Tickrate | 128 | GOKZ KZTimer 要求 128 tick |
| `SPEED_NORMAL` | 250 HU/s | 普通地面最大期望速度 |
| `PRE_VELMOD_MAX` | 1.104 | `250 × 1.104 = 276 HU/s`，是预加速速度修正上限 |
| `PERF_SPEED_CAP` | 380 HU/s | 只用于 HitPerf 跳跃前的水平速度修正 |
| `sv_accelerate` | 6.5 | 地面加速 |
| `sv_airaccelerate` | 100 | 空中加速 |
| `sv_air_max_wishspeed` | 30 | 空气加速的 add-speed 上限 |
| `sv_friction` | 5 | 地面摩擦 |
| `sv_stopspeed` | 80 | 低速摩擦控制值 |
| `sv_gravity` | 800 | 重力 |
| `sv_jump_impulse` | 301.993377 | 跳跃冲量 |
| `sv_maxvelocity` | 2000 | Source 对每个速度分量的上限 |
| `sv_walkable_normal` | 0.7 | 可行走表面法线阈值 |
| 普通碰撞盒 | `[-16,-16,0]` 到 `[16,16,72]` | 32×32×72 HU |
| 下蹲碰撞盒 | `[-16,-16,0]` 到 `[16,16,54]` | 32×32×54 HU |

### 重要语义

#### 276 不是简单的最终速度硬限制

GOKZ 通过修改 `GetPlayerMaxSpeed` 影响移动过程中的最大期望速度：

```text
player max wish speed = 250 × realPreVelMod
```

它不是在每个 tick 结束后无条件执行：

```text
horizontalVelocity = Min(horizontalVelocity, 276)
```

因此 Unity 代码不能用任意最终截断来掩盖错误的地面状态、空中状态、输入状态或碰撞状态。应先使调用顺序和状态机一致。

#### 380 只属于 HitPerf 跳跃

官方 GOKZ `TweakJump` 只在 `player.HitPerf` 为真时，把起跳前水平速度限制到 380 HU/s。它不是普通空气速度的全局上限，也不是每个 tick 的最终速度限制。

#### Perf 的 MovementAPI 判定目前无法从随附源码确认

本地 `gokz-mode-kztimer.sp` 在 `Movement_OnJumpPre` 调用 `TweakJump`，并由 `player.HitPerf` 决定是否应用 380 HU/s 起跳速度上限；`Movement_OnJumpPost` 再保存 `HitPerf` 与 `TakeoffSpeed`。本项目没有 MovementAPI 实现，因此 `_landedThisTick` 只能标注为 Unity 近似，不能声称等同于 GOKZ 的 HitPerf 状态机。

---

## 4. 当前 Unity 代码结构

### 4.1 运动入口和角色

| 文件 | 作用 | 当前状态 |
|---|---|---|
| [SurfCharacter.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/SurfCharacter.cs) | `LZM`，输入、事件、视角、旧控制器和新控制器的总入口 | 仍然承载大量项目逻辑，需要谨慎拆分 |
| [SurfController.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/SurfController.cs) | 保留旧运动路径，同时调用 `SourceMovementCore` | KZTimer 路径从 `SurfCharacter.FixedUpdate` 进入；旧逻辑仍保留 |
| [GokzKzTimerMovement.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzKzTimerMovement.cs) | 128 Hz Source/GOKZ 运动适配核心 | 已包含移动、盒体扫掠/多平面裁剪、蹲、涉水、梯子、坡面和触发器分支；Unity 碰撞和缺少 MovementAPI runtime 仍使其不能宣称逐 tick 完全一致 |
| [MoveData.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/MoveData.cs) | 输入、速度、碰撞、触发器和项目状态容器 | 当前/旧按键已使用 CS:GO `in_buttons.h` 位；前/侧/上 move 值已分开保存，但没有完整的 `CUserCmd` 元数据 |
| [GokzModeSettings.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzModeSettings.cs) | Vanilla/SimpleKZ/KzTimer 参数 | KzTimer 数值大部分正确 |
| [GokzMovementRules.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzMovementRules.cs) | 摩擦、加速、空气加速和速度换算 | 公式可静态对照；本轮没有执行测试或运行时验证 |

### 4.2 周边功能

| 文件 | 作用 | 当前状态 |
|---|---|---|
| [GokzHud.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzHud.cs) | Unity 版速度、计时、按键、Perf HUD | 信息简化，Perf 状态采样时序存在问题 |
| [GokzJumpStats.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzJumpStats.cs) | 本地跳跃统计 | 目前主要支持 LongJump/Bhop，不能视为完整 GOKZ jumpstats |
| [GokzRuntime.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzRuntime.cs) | 模式、计时器、课程、Checkpoint、传送 | 项目内 Unity 替代实现 |
| [GokzMovementDiagnostics.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzMovementDiagnostics.cs) | 本地运动诊断 | 不是服务器反作弊的完整替代 |
| [GokzPortManifest.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzPortManifest.cs) | 迁移清单 | 只能作为清单，不能作为“已经完成”的证明 |
| `Movement/Tests/Editor` | EditMode 单元测试和碰撞测试 | 当前测试偏向验证项目自己的假设 |

---

## 5. 已经完成或确认的内容

以下内容已在当前项目中确认存在，但“存在”不代表已经达到源码一致性：

1. Unity 已升级并打开到 `6000.6.8f1`。
2. 当前 `FirstRoom` 场景可以加载，编辑器当前处于非运行、非编译状态。
3. GOKZ 运动路径已经接入 `LZM`。
4. `CharacterController` 保留为旧路径参考；GOKZ 路径使用 `GokzSourceHull`。
5. `GokzSourceHull` 的目标尺寸对应 32×32×72 HU；下蹲目标尺寸对应 32×32×54 HU。
6. `Wall` 层的地面/墙体可以被新扫掠碰撞查询检测到。
7. `Wall` 层现有碰撞体均有 `GokzSurfaceProperties`，普通地面摩擦为 1。
8. 128 Hz 固定更新路径已经接入。
9. 空间滚轮上滑可以设置跳跃请求。
10. 失重、反向重力、梯子、水体、触发器和旧控制器没有被删除，仍需作为项目适配层继续维护。
11. 项目中存在公式与碰撞 EditMode 测试文件；本轮没有运行测试或编译，历史测试记录不能代替当前验证。

---

## 6. 当前已知问题

### 6.1 当前核心差异与验证边界

#### MovementAPI Perf 状态

KZTimer 的 `TweakJump` 依据 MovementAPI 的 `HitPerf` 执行 380 HU/s 起跳速度处理。Unity 代码以本地落地 tick 状态估算 Perf；由于仓库没有 MovementAPI runtime，实现是否一致尚无依据。不得把 `_landedThisTick` 或任何推测写成 MovementAPI 源码逻辑。

#### Source UserCmd 输入模型

当前输入在 Unity `Update()` 读取、在 128 Hz `FixedUpdate()` 消费，并将滚轮/按键跳跃请求锁存到单个 tick。Source 则围绕每个 usercmd 维护：

- `m_nButtons`；
- `m_nOldButtons`；
- `m_flForwardMove`；
- `m_flSideMove`；
- `m_flUpMove`；
- cmdnum；
- tickcount；
- 视角变化；
- 按键边沿。

活动运动链已经把移动相关按键映射到本地 CS:GO `in_buttons.h` 位，并维护 `Buttons`/`OldButtons`：GOKZ `OnPlayerRunCmd` 钩子在 `CheckParameters` 前读取当前输入，`FinishMove` 对应边界更新 Source 旧按键；跳跃防连跳和 raw duck 边沿据此处理。前进/侧移命令也以 450 HU/s 的 Source 客户端命令速度生成。

这仍不是完整 `CUserCmd`：Unity `Update()` 采样输入再由 128 Hz `FixedUpdate()` 消费，没有 Source 的 cmdnum、tickcount、随机种子、mouse delta、完整角度/按钮集，也不能保证渲染帧与 movement command 的采样完全相同。因此 MovementAPI Perf 和多输入更新合并仍不能宣称与原引擎一致。

#### CS:GO Duck 状态

活动实现已经映射连续 `m_flDuckAmount`、`m_flDuckSpeed` 恢复、空中立即切换 hull、eye spline、CanUnduck、9 HU 空中 origin 偏移、蹲速裁剪及 GOKZ 的落地 CanUnduck/按键 hook。仍需谨慎核对状态边缘：Unity 的 hull overlap/扫掠不能复刻 Source `TracePlayerBBox`，本轮没有运行时跳蹲轨迹可比对。不要把这部分误写成“尚未实现”，也不要声称已经验证完全相同。

#### Source TracePlayerBBox 碰撞差异

活动实现已包含 8 bump 的 GOKZ momsurffix 路径、最多 5 个裁剪面、StepMove、StayOnGround、地面分类、斜坡重试、端点穿透探测和 Unity BoxCollider。GOKZ `SlopeFix` 的专用扫掠现按 `MASK_PLAYERSOLID_BRUSHONLY` 语义过滤 `Player`/`Enemy` 标签；其他 movement trace 仍是 Unity 物理查询，不能自动等同于 Source：

- PhysX `startsolid/allsolid` 与 Source trace 的定义不同；
- Unity query hull 的 skin 与 Source hull trace fraction 需要换算；
- Unity layer/tag 过滤不能完整表达 Source contents mask 与 `playerclip`；
- touch list 缺失；
- edge/distbug/edgebug 状态缺失；
- 步进、斜坡三角面及移动 Rigidbody 的接触行为不完全一致。

继续修改时先维护活动适配器中的调用顺序与 trace 映射，不要再并行造第二套运动控制器。

#### 276 与 380 的运行结果

源码中 276 来自 `250 × PRE_VELMOD_MAX(1.104)` 的最大期望速度路径；380 只在 `HitPerf` 时由 KZTimer `TweakJump` 处理起跳速度。活动 Unity 代码按 Source `WalkMove` 的 wish-speed 和地速裁剪路径实现相关运算，但 Perf 状态仍是近似。没有逐 tick 参考轨迹前，不能仅凭 HUD 或常数声称实测一致：

- 平地行走超过预期 276；
- 非 Perf 的起跳速度状态被错误识别；
- Perf 绿色提示不出现或出现在错误跳跃上；
- 落地后的摩擦 tick 数不对；
- W+A/D 预加速和纯前进状态不同于 GOKZ。

不得通过无源码依据的最终 `Clamp` 掩盖这些问题。

### 6.2 仍需继续核验

1. Unity `Physics.BoxCast` 对复杂 Mesh、贴角、穿透恢复和 moving Rigidbody 的结果需要实测与 Source trace 对照。
2. MovementAPI runtime 未随项目提供，`HitPerf`、完整 landing/takeoff 与 bug 事件不能由现有本地文件完全核实。
3. HUD 与 jumpstats 的事件/采样节奏仍需确认是否按 movement tick 更新。
4. 水体、梯子、坡面、移动平台及 LUO 的失重/反向重力扩展需分别验证；后三项不是 KZTimer 的原生规则。

### 6.3 外围功能和完整 GOKZ 模块

以下功能目前是 Unity 替代实现或缺失，不应宣称为官方 GOKZ 完整移植：

- SourceMod server/plugin 生命周期；
- Steam/GlobalAPI；
- MySQL/SQLite 本地数据库语义；
- spectator/team API；
- Source weapon/sound hook 细节；
- replays 与真实 Source demo 兼容；
- 服务器反作弊信任边界。

如果项目只要求单机 Unity 运动体验，可以把这些功能标为“Unity 适配功能”，但不能说“源码全部迁移”。

---

## 7. 测试覆盖状态

本轮只做静态代码与本地源码对照，没有运行 Unity、编译或测试。项目存在 EditMode 测试文件，但本交接文档中旧的“测试通过”记录属于历史状态，不代表当前工作区结果。

已确认的测试问题：

1. 测试主要验证直线前进和基础公式，没有验证 W+A/D 预加速全过程。
2. 没有验证真实 cmdnum/tickcount 和输入边沿。
3. 没有验证 MovementAPI 的 `HitPerf` 判定；随附文件只有接口声明，缺少判定实现。
4. 没有验证 edgebug、distbug、jumpbug、duckbug。
5. 没有验证真实 Source TracePlayerBBox 的 startsolid/allsolid 行为。
6. 测试把“下蹲落地后仍保留较高水平速度”写成了预期，这只是当前项目的假设，不是源码证明。
7. 测试没有与真实 GOKZ 的逐 tick 轨迹做 golden comparison。

必须新增两类测试：

### 7.1 纯规则测试

用于验证独立公式：

- Ground acceleration；
- AirAccelerate；
- Friction；
- Gravity half-step；
- Jump impulse；
- PreVelMod；
- Perf cap 只在 HitPerf 时生效；
- Component-wise max velocity。

### 7.2 逐 tick 行为测试

每个 tick 至少记录：

```text
tick
cmdnum
buttons
oldButtons
origin
velocity
grounded
groundNormal
walkMoved
duckAmount
isDucked
isDucking
landingTick
takeoffTick
hitPerf
takeoffSpeed
collisionCount
collision planes
```

然后用相同输入序列与真实 GOKZ/Source 参考数据逐 tick 比较，而不是只比较最后速度。

---

## 8. 推荐的正式实现顺序

### Phase 0：冻结现状

- 保留旧 `CharacterController` 和旧 Surf 路径。
- 明确 `SourceMovementCore` 是当前实验性适配器，不再称作完整移植。
- 为当前版本创建可回滚副本或版本控制提交。
- 记录当前场景、玩家初始位置、组件和 Layer 设置。

### Phase 1：建立 Source 用户命令层

新增独立的 `SourceUserCmd` 和 `SourceMovementState`，包含：

- Buttons/OldButtons；
- ForwardMove/SideMove/UpMove；
- view angles；
- cmdnum/tickcount；
- 当前/上一 tick Grounded；
- WalkMoved；
- ladder、duck、jump、landing、takeoff 状态。

Unity Input System 只负责生成命令，不能直接决定 Source 状态。

### Phase 2：重建 Source 调用顺序

目标调用顺序应明确写在代码和测试中：

```text
PlayerMove
  -> Duck
  -> CheckWater / WaterJump
  -> StartGravity
  -> CheckJumpButton
  -> Friction
  -> WalkMove 或 AirMove
  -> CategorizePosition
  -> CheckVelocity
  -> FinishGravity
  -> Clear vertical velocity if grounded
  -> CheckFalling / MovementAPI state update
```

每一步都要有 Source 依据和输入输出说明。

### Phase 3：实现独立 Source 碰撞适配器

建议拆出：

- `ISourceTraceWorld`；
- `SourceTraceResult`；
- `TracePlayerBBox`；
- `TryPlayerMove`；
- `StepMove`；
- `StayOnGround`；
- `CategorizePosition`；
- `ClipVelocity`。

Unity BoxCast 只是底层查询实现，不应直接承担 Source 状态语义。

### Phase 4：完整实现 Duck/Unduck 与 MovementAPI 状态

必须覆盖：

- Duck transition；
- Unduck transition；
- duck amount；
- ceiling block；
- landing-tick CanUnduck；
- displacement stuck；
- crouch jump bind；
- duckbug/jumpbug；
- edgebug/distbug；
- ladder takeoff。

### Phase 5：接入 GOKZ KZTimer 专属修改

只在 Source movement 基础正确后接入：

- PreVelMod；
- `GetPlayerMaxSpeed` 修改；
- AirAccelerate wishspeed 修正；
- Perf 380 cap；
- ReduceDuckSlowdown；
- FixWaterBoost；
- FixDisplacementStuck；
- SlopeFix。

### Phase 6：接入 LUO 项目功能

通过适配层接入：

- 失重；
- 反向重力；
- 水体；
- 梯子；
- 传送；
- Checkpoint；
- Spawn；
- 事件；
- 武器；
- 相机和视角；
- HUD。

反向重力和失重不是官方 GOKZ 功能，必须明确作为 Unity 项目扩展，不能混入“官方 KZTimer 基准逻辑”。

### Phase 7：逐 tick 对照验证

至少验证：

1. 静止状态。
2. 纯 W 地面加速。
3. W+A、W+D 预加速。
4. 普通跳跃。
5. 非 Perf 落地后再跳。
6. Perf 跳跃。
7. 高速非 Perf 起跳。
8. 落地后立即蹲下。
9. 空中开始蹲下。
10. 蹲着落地。
11. 顶部空间不足时起身。
12. 斜坡落地。
13. 台阶。
14. 墙角碰撞。
15. 边缘、edgebug、distbug。
16. 梯子进入、爬行、跳离。
17. 水体进入、游泳、跳出。
18. 反向重力和失重。
19. 传送/重置后速度和状态清理。

---

## 9. 验收标准

只有满足以下条件，才能说“GOKZ KZTimer Unity 运动核心完成”：

### 源码和设计

- 每个核心规则都能指向 GOKZ、MovementAPI 或 Source 引用。
- 没有未经标注的自定义速度限制、摩擦、蹲速或 Perf 规则。
- 官方规则与 LUO 扩展规则有清晰边界。
- 旧实现仍可切换和参考。

### 速度和状态

- 普通地面移动、W+A/D 预加速、落地摩擦都符合参考轨迹。
- 276 只通过官方 PreVelMod/最大期望速度逻辑产生，不是最终硬截断。
- 380 只在准确 HitPerf 的跳跃前触发。
- 非 Perf 不会因为错误的状态判断触发 Perf 380 规则。
- HUD 的绿色 Perf 状态与实际 `HitPerf` 同一个 tick。

### 下蹲和碰撞

- 站立盒体为 32×32×72 HU。
- 下蹲盒体为 32×32×54 HU。
- 视角高度、过渡时间和碰撞体变化可通过逐 tick 数据验证。
- 顶部阻挡、蹲跳、duckbug、jumpbug 和落地 tick 状态正确。
- 盒体碰撞与 Unity 静态场景、动态物体和项目触发器兼容。
- 台阶、坡面、墙角和边缘行为有单独测试。

### 项目集成

- Spawn、传送、Checkpoint、触发器、武器、相机、HUD 和计时器不被破坏。
- 失重和反向重力可用，并且不会改变正常 GOKZ 基准测试结果。
- Unity 编译无错误、无新增未解释警告。
- 场景不会因为测试或工具调用被意外保存修改。

---

## 10. 给其他 AI 的工作协议

每次开始工作前：

1. 阅读本文档。
2. 检查 Unity 当前版本、场景、Play Mode、编译状态。
3. 阅读实际代码，不根据类名或注释推断功能已经完成。
4. 阅读对应官方源码位置。
5. 写出“当前代码实际做什么”和“官方源码要求什么”的差异。

每次修改时：

1. 只修改当前任务所需文件。
2. 不删除旧路径。
3. 不添加无源码依据的硬性速度修正。
4. 不把 Unity 物理默认行为当作 Source 行为。
5. 为新规则添加纯公式测试和至少一个状态/碰撞测试。
6. 修改后运行相关测试，检查 Unity 是否重新编译。
7. 报告实际修改的绝对路径、源码依据、测试结果和仍未解决的问题。

每次汇报必须回答：

```text
1. 这次改了哪些文件？
2. 每个修改对应哪一段官方源码？
3. 修改前的实际错误是什么？
4. 是否改变了旧控制器或项目事件？
5. 是否加入了任何非官方逻辑？如果有，为什么？
6. 用什么测试验证？
7. 哪些行为仍然没有真实 GOKZ 数据证明？
```

禁止使用以下表述，除非已有逐 tick 对照证据：

- “已经完全一样”；
- “源码全部迁移完成”；
- “速度肯定正确”；
- “Unity 碰撞等于 Source 碰撞”；
- “测试通过所以 GOKZ 一致”。

---

## 11. 可直接复制给其他 AI 的任务提示词

```text
你正在接手 Unity 项目 E:\Ridah\Unity\Projects\LUO。

请先完整阅读项目根目录的 AI_HANDOFF_GOKZ_UNITY_LUO.md，不要先修改代码。

项目目标是在 Unity 6.6 中逐 tick 复现 KZGlobalTeam/gokz 的 KZTimer 运动模式，同时保留 LUO 原有角色、事件、武器、Spawn、传送、失重和反向重力功能。参考必须包括：

1. GOKZ 官方 gokz-mode-kztimer.sp；
2. 项目随附的 MovementAPI 文件（目前只有 `movementapi.inc` 声明；不得猜测缺失实现的行为）；
3. 本地 `ThirdParty/cstrike15_src-master` 中的 `cs_gamemovement.cpp` 及其实际调用的同仓库基类实现；
4. 当前项目实际代码，而不是只看注释。

重要限制：

- 目标是 GOKZ KZTimer，不是 SimpleKZ、bhop、surf 或自定义移动；
- 不得用无源码依据的最终速度 Clamp 掩盖问题；
- 276 是官方 PreVelMod/最大期望速度逻辑，380 只属于准确 HitPerf 跳跃；
- 必须实现 Source usercmd、old buttons、tick/cmd 状态、WalkMoved、landing/takeoff、duck 和碰撞状态；
- CharacterController 和旧 Surf 路径必须保留作参考；
- Unity BoxCast 只是底层查询，不能直接声称等同 Source TracePlayerBBox；
- 失重和反向重力是 LUO 的扩展，必须与官方 GOKZ 基准逻辑分层；
- 不要控制用户桌面，不要擅自重启 Unity 或电脑，优先使用 Unity CLI/MCP。

开始时请只做审计，并输出：

1. 当前实际启用的运动调用链；
2. GOKZ 源码与当前代码的逐项差异；
3. P0/P1/P2 问题清单；
4. 需要修改的文件和原因；
5. 可验证的实施顺序；
6. 不要把“部分公式正确”说成“完整迁移”。

未得到用户继续指令前，不要直接大范围修改。
```

---

## 12. 当前结论

当前项目的活动运动路径已经包含较多 CS:GO/GOKZ 调用阶段与状态映射，但它仍是 Unity 适配，不能称作完整逐行移植或逐 tick 等同。当前继续工作的重点是：

1. 补齐 MovementAPI 实现参考或以真实 GOKZ/CS:GO tick 轨迹确定 Perf 状态；
2. 维护 Unity usercmd 输入与 Source 按键边沿的映射；
3. 继续按 CS:GO Duck 与 GOKZ detour 核验蹲起边缘情况；
4. 逐项核对 BoxCast 与 Source trace 在角落、贴地、台阶、斜坡及穿透时的分歧；
5. 用相同 128 tick 输入序列验证地速、380 Perf 起跳、摩擦、落地、蹲和碰撞；
6. 将 LUO 失重、反向重力及事件功能作为独立扩展验证。

---

## 13. 2026-11-13 会话更新日志

### 13.1 已提交到 SVN 的 GOKZ 源码

已将用户指定的 GOKZ 仓库完整快照放入项目：

```text
E:\Ridah\Unity\Projects\LUO\ThirdParty\gokz-master
```

当前快照包含 323 个文件，其中包括 195 个 SourcePawn/include 文件。已确认存在：

- `ThirdParty/gokz-master/addons/sourcemod/scripting/gokz-mode-kztimer.sp`
- `ThirdParty/gokz-master/addons/sourcemod/scripting/include/movementapi.inc`
- `ThirdParty/gokz-master/addons/sourcemod/gamedata/gokz-core.games.txt`
- `ThirdParty/gokz-master/addons/sourcemod/scripting/momsurffix/`

SVN 已提交，`ThirdParty` 的提交版本为 `r4`，提交说明为：

```text
Add GOKZ source reference
```

注意：`ThirdParty/GOKZ-master.zip` 是下载时留下的未版本控制压缩包，不是 SVN 中的源码；继续工作时只使用 `ThirdParty/gokz-master`。当前 SVN 状态中该压缩包显示为 `?`。

### 13.2 本次源码核对得到的事实

GOKZ 仓库不是完整的 CS:GO 引擎源码。它依赖引擎内部的 `CCSGameMovement`，并通过 SourceMod、DHooks、MovementAPI 进行 Hook 和规则修改。

已在本地源码中核对：

- `gokz-mode-kztimer.sp` 的 `OnPlayerRunCmd` 调用 `RemoveCrouchJumpBind`、`ReduceDuckSlowdown`、`FixWaterBoost` 和 `FixDisplacementStuck`；
- `DHooks_OnCanUnduck_Pre` 读取 `m_flDuckAmount`、`m_bDucked` 并处理刚落地完全下蹲时的 `CanUnduck`；
- `RemoveCrouchJumpBind` 只移除特定的“地面首次跳跃同时新按下蹲”的输入组合；
- `movementapi.inc` 暴露 Duck、Jump、Landing、WalkMove 等事件，但不包含引擎内部的 Duck 状态机；
- 完整的 `CCSGameMovement::Duck`、碰撞盒切换、视角高度和 DuckJump 实现不在 GOKZ 仓库的 SourcePawn 文件中，而在 CS:GO 引擎内部。

因此，不能把“复制 GOKZ 仓库”说成“已经获得完整蹲实现”。Unity 仍需要实现 `CCSGameMovement::Duck` 的等价状态机，再叠加 GOKZ 的 Hook 规则。

### 13.3 本次 Unity 工作副本修改

以下清单和“空中保持视角高度”的描述是 2026-11-13 的历史快照，不是当前工作区状态。后续对照本地 `cs_gamemovement.cpp::FinishDuck` 确认：空中完成蹲下时会把 origin 上移 9 HU，并把 view offset 设为 46 HU；旧记录中的“眼睛高度保持空中蹲之前的状态”不符合指定源码，已由后续运动核心修订取代。此处仅保留历史，不能作为当前实现或行为说明。

以下文件当时处于 SVN 工作副本 `M` 状态；该历史清单不代表当前 Git/SVN 状态：

- `Assets/Ridah/RidahFPS/Movement/GokzKzTimerMovement.cs`
- `Assets/Ridah/RidahFPS/Movement/GokzKzTimerMovementCompatibility.cs`
- `Assets/Ridah/RidahFPS/Movement/SurfController.cs`
- `Assets/Ridah/RidahFPS/Movement/SurfCharacter.cs`
- `Assets/Ridah/RidahFPS/Movement/Tests/Editor/SourceMovementCoreCollisionTests.cs`

本次修改内容：

1. 当时在 `SourceMovementCore` 中增加独立的 `EyeDuckAmount`；
2. 当时采用了空中不变更眼睛高度的适配；该做法随后经 CS:GO 源码对照确认不正确，并已修正；
3. 当时仅地面 tick 推进视角蹲起表现；该结论也不再代表当前代码；
4. `SurfCharacter.UpdateGoKzViewHeight` 改为读取 `GokzEyeDuckAmount`；
5. 增加 `AirborneDuckChangesPhysicalHullWithoutChangingEyeAmount` 测试。

这是一段已被后续修改取代的 Unity 适配逻辑，不能据此判断当前蹲实现。当前已按本地 CS:GO `Duck`、`FinishDuck`、`FinishUnDuck`、`CanUnduck` 路径核对状态与调用顺序；Unity hull query 仍不等同于 Source trace，MovementAPI runtime 也未随仓库提供。

### 13.4 编译和测试结果

以下是 2026-11-13 的历史记录，不能作为当前代码的编译/测试证据：

- Unity Editor：`6000.6.8f1`；
- Editor 连接状态：ready，Pipeline 端口 `7800`；
- 未重启 Unity 或电脑；
- 使用 Unity CLI 强制脚本重新编译，`compilationFailed=false`；
- EditMode 测试：23 个全部通过；
- 新增测试名称：`AirborneDuckChangesPhysicalHullWithoutChangingEyeAmount`；
- 本次没有修改场景、Prefab 或项目事件。

### 13.5 下一台电脑继续工作前必须知道

1. 先从 SVN 更新项目，确认 `ThirdParty/gokz-master` 已存在；
2. 阅读本文件和 `ThirdParty/gokz-master/addons/sourcemod/scripting/gokz-mode-kztimer.sp`；
3. 不要把当前 `EyeDuckAmount` 适配层称为官方 GOKZ 蹲实现；
4. 不要直接提交当前五个 `M` 文件，先审计并补齐 `CCSGameMovement::Duck` 等价状态机；
5. 需要明确建模：`m_flDuckAmount`、`m_bDucked`、`m_bDucking`、Duck/Unduck 计时、空中蹲、CanUnduck、天花板阻挡、DuckJump、视角状态和碰撞盒状态；
6. 保留旧 `CharacterController`/`SurfController` 路径和 LUO 的失重、反向重力、事件、传送、触发器功能；
7. 修改前必须列出源码依据、当前差异、预期影响和测试方法；
8. 未有真实逐 tick 对照数据前，不得声称与 GOKZ/CS:GO 完全一致。

## 14. 2026-11-17 角落蹲起/静步碰撞回归记录

### 14.1 用户要求恢复的状态

用户要求先更新本日志，再把运动/碰撞代码恢复到聊天截图对应的状态。截图中的状态为：进入角落时通常不会立刻卡死；部分方向可以脱出；但在角落蹲起后可能卡住、不能跳，且不同角度的可移动性不同。

### 14.2 当前报告的问题

- 在两个碰撞体夹角中蹲起时会抖动。
- 按 Shift 静步走时也会抖动。
- 蹲起卡点问题再次出现，当前报告为按任何移动键都无法脱出。
- 用户指出问题集中在角色碰撞体/碰撞解析，不应要求用户反复保持按键来保存现场。

### 14.3 版本定位与回退目标

- 当前 Git 提交 `6daff974`（2026-11-17 17:12）晚于截图中的 16:25 状态；该提交只修改 `Assets/Ridah/RidahFPS/Movement/GokzKzTimerMovement.cs`。
- 回退目标为其父提交 `e7ab6fed`（2026-11-17 15:11），即保留截图所述“部分方向还能脱出”的此前碰撞实现。
- `6daff974` 改动了 BoxCast 命中面重建、零距离命中筛选、`ResolveInitialPenetration` 调用及 `CanFitHullAt` 查询尺寸。这些改动涉及角落滑动与蹲起空间判定，可能导致回归；原因尚未由恢复后的游戏操作确认。
- 已只将 `Assets/Ridah/RidahFPS/Movement/GokzKzTimerMovement.cs` 恢复到 `e7ab6fed`，撤销 `6daff974` 对该文件的改动；其他文件及其现有改动均保留。未提交 SVN。

### 14.4 执行与验证记录

- 问题记录先写入并推送到 GitHub（提交 `0ef6f6d1`），之后执行代码回退。
- 不替用户操作角色，不重置场景，不运行测试；回退后核对目标文件与 `e7ab6fed` 一致，并读取 Unity 编译状态。
- 恢复后的游戏内表现尚未由用户验证；不能据此声称抖动或卡点已解决。

## 15. 2026-11-18 运动代码源码核对

### 15.1 本次对照范围与唯一引擎参考

- CS:GO 引擎源码只使用项目内 `ThirdParty/cstrike15_src-master`，即用户指定的 `perilouswithadollarsign/cstrike15_src`；没有使用 Source SDK 2013 替代。
- GOKZ 规则使用项目内 `ThirdParty/gokz-master`，重点核对 `gokz-mode-kztimer.sp` 与 `gokz-momsurffix.sp`。
- 对照了活动入口 `SurfCharacter.FixedUpdate -> SurfController.ProcessMovement -> GokzKzTimerMovement.Process/Move/Finish`，以及运动参数、碰撞、蹲、水、梯子、表面与移动体积适配代码。
- 旧 `CharacterController` 和 `SurfController` 运动实现仍作为旧路径保留；本次没有重写或删除。

### 15.2 本次按源码修正

1. `gokz-momsurffix.sp` 的完整位移扫掠在全程移动后做独立终点 hull trace。若第一 bump 的终点落入实体，进入 ramp 重试；若之后的 grounded bump 仍落入实体，则把速度归零并退出。Unity 适配此前把这两种情况都送入 ramp 重试，现已按该分支区分。
2. `gokz-momsurffix.sp::IsValidMovementTrace` 检查 startsolid/allsolid、零 fraction 的指定非 walkable plane、法线范围，以及 `tr.endpos` 的静态 hull trace；`TryPlayerMove` 另有完整位移终点检查。Unity 适配分别保留 ramp trace 终点探测和完整位移终点处理。
3. GOKZ 的 `TryPlayerMove` 替换实现没有基础 `CGameMovement::TryPlayerMove` 的 `MINIMUM_MOVE_FRACTION` 截断；活动实现已移除这条额外截断。
4. CS:GO `CreateStuckTable` 的大偏移组合按 Z、X、Y 循环生成。Unity 适配之前按 X、Y、Z 生成，现已调整为源码顺序，以保持每次卡住重试的偏移序列一致。
5. CS:GO `CGameMovement::CheckWater` 在每次 `PlayerMove` 采样水位；LUO 原先只在 render `Update` 更新水位。GOKZ 活动路径现于每个 128 Hz 固定 tick 重算触发器状态和水位，并移除仅比较触发器数量的缓存判断，避免数量不变但对象更换时沿用旧状态。
6. CSGO `PhysicsTouchTriggers(&prevOrigin)` 检测本次位移路径经过的触发器。普通 Push 体积现除了终点重叠，也检查 tick 起点重叠和起点至终点的盒体扫掠；推力仍通过 `FL_BASEVELOCITY` 对应的跨 tick 路径施加。

### 15.3 仍不能声称完全一致的边界

- Unity `Physics.BoxCast` / `ComputePenetration` 与 Source `TracePlayerBBox` 的碰撞过滤、startsolid、接触边界和位移精度不是同一个实现；当前代码仍有碰撞皮肤适配。需要真实场景逐 tick 对照，不能仅凭代码宣称完全一致。
- 项目只有 `movementapi.inc` 声明，没有 MovementAPI 原生实现源码；因此 `Turning`、`HitPerf` 及完整 landing/takeoff 事件时序不能由本地源码完全验证。
- 失重、反向重力与 Unity Rigidbody/Trigger 交互是 LUO/Unity 适配扩展，不是 KZTimer 的原生 CS:GO 运动规则。
- 本次没有启动 Unity/游戏，没有运行测试或编译；尚未得到引擎编译结果或玩家实测结果。
- `Assets/Ridah/RidahWeapon/Weapon/Portal/Scripts/Rotater.cs` 及其 `.meta` 是既有未跟踪文件，与运动审计无关，本次未修改。

## 16. 2026-11-18 活动运动链逐项核对与修正

### 16.1 核对依据

- CS:GO 引擎依据始终是项目本地 `ThirdParty/cstrike15_src-master`，主要是 `game/shared/cstrike15/cs_gamemovement.cpp` 和它调用的 `game/shared/gamemovement.cpp`。
- GOKZ 依据是项目本地 `ThirdParty/gokz-master`，主要是 `gokz-mode-kztimer.sp`、`gokz-momsurffix.sp` 和随附 `movementapi.inc`。
- 核对入口 `SurfCharacter.FixedUpdate/UpdateMovement -> SurfController.ProcessGoKzMovement -> SourceMovementCore.Process/Move/Finish`，以及 `MoveData`、规则公式、输入、碰撞、蹲、水、梯子、地面和移动体积代码。

### 16.2 已修正的源码时序差异

1. `SurfCharacter.UpdateMovement` 原先在 `ProcessMovement` 之后才启用 Source 盒体，导致首个 tick 的落地分类和蹲状态更新在碰撞适配未就绪时执行。现在先启用/禁用对应碰撞路径，再进入运动处理；核心也只在碰撞体就绪后锁定初始地面状态。
2. Source 优化移动的初始分类条件还包括当前 movetype 不是 `MOVETYPE_WALK`。Unity 适配现在会对上一 tick 的梯子状态重新分类；正常行走时仍按 `m_bGameCodeMovedPlayer` 与 `sv_optimizedmovement` 默认路径处理。
3. Source 优化分支清地条件是 `mv->m_vecVelocity.z > 250 HU/s`，不是相对地面速度。Unity 适配改为直接比较沿当前 up 方向的速度；`CategorizePosition` 的独立 140 HU/s 地面相对速度判断仍保留。
4. `CGameMovement::WalkMove` 在直线 trace 无阻挡时调用 `StayOnGround`，在阻挡并执行 `StepMove` 后也调用。活动适配之前只在无碰撞路径执行；现在两个路径都会执行，低于 1 HU/s 的早退仍不 snap。
5. Unity sweep 采用缩小 `CollisionSkin` 的查询盒。映射到 Source 全尺寸 hull trace 时，扫掠距离、fraction 与 trace end position 需要扣回这一 skin；当前实现据此计算安全距离，避免把缩小后的查询盒误当作 Source 原尺寸 hull。
6. CS:GO 在地面摩擦之后、WalkMove/AirMove 加速之前执行一次 `CheckVelocity`，并在普通 FullWalkMove 的移动与地面分类之后、`FinishGravity` 之前再次检查。Unity 适配将检查放到对应阶段；水游、梯子及活动 water-jump 分支保持各自 Source 调用路径。
7. `FinishDuck`/`FinishUnDuck` 都会重新分类地面。活动代码之前在重新分类失败时保留旧 grounded 状态；现在会同步清除旧 ground hit、表面状态和 `MoveData` 地面标记。
8. 起身可容纳检测之前把 hull 整体缩小 2% 并额外抬高查询中心；现在改用 32×32×目标高度的 Source 盒体尺寸，只保留与其他 sweep 相同的 Unity collision skin。
9. `FullWalkMove` 根据进入该 tick 时的水位选择游泳或普通行走分支。Unity 适配现在保存本 tick 实际选择的分支，再决定是否执行 `CheckVelocity`/`FinishGravity`，不再用移动后的水位反推调用路径。
10. 输入状态现使用本地 `in_buttons.h` 的移动相关位，并把 `Buttons`/`OldButtons` 接入 GOKZ hook 与 `CheckJumpButton`；`OldButtons` 在 normal/CheckStuck 退出路径都按 `FinishMove` 边界更新。它仍不是完整 `CUserCmd`。
11. `gokz-mode-kztimer.sp::SlopeFix` 的 clip helper 现按其实际循环处理两个水平轴，并清零垂直分量；没有再套用完整三维 Source `ClipVelocity`。Unity XZ 水平面映射 GOKZ 的 Source XY。
12. CS:GO 梯子逻辑在跳跃保护窗口内不会按方向重新计算攀爬速度；活动适配现在在 0.2 秒保护期内保留梯子速度，到期才按源码以 270 HU/s 蹬离。
13. `CGameMovement::GetCheckInterval(STUCK)` 的普通检查间隔按多玩家 Source 默认 1 秒处理；已有偏移重试时逐 movement command 尝试，并保留 0.05 秒的尝试节流。Unity 没有 Source `entindex`，所以只能匹配间隔，无法匹配 `(cmdnum + entindex)` 的相位。
14. `SlopeFix` 现在用单独的 brush-only 近似过滤：保留世界/可移动几何，跳过项目 `Player`/`Enemy` 标签；Source `CONTENTS_*` 和 Unity layer/tag 仍不是一一对应。

### 16.3 仍需保留的限制

- GOKZ 的 380 处理确实只在 `HitPerf` 时触发；当前 Unity Perf 判定仍是 `_landedThisTick` 近似。项目未包含 MovementAPI runtime，不能依据 `movementapi.inc` 宣称两者相同。
- Unity BoxCast/Overlap/ComputePenetration 仍不能复现 Source trace 的实体过滤、面片选择、startsolid/allsolid 与原生地形精度；本轮只校正能够由源码直接确认的调用条件和几何换算。
- `SlopeFix` 的 Unity brush-only 映射目前只能按 LUO 的 `Player`/`Enemy` 标签过滤，未覆盖 Source `playerclip` contents 与所有自定义实体类别。
- 本轮未启动 Unity/游戏、未编译、未运行测试，也没有角色实测结果。修改后的 Unity 行为仍需在项目内验证。

## 17. 2026-11-18 全运动调用链继续审计

### 17.1 本轮唯一参考

- GOKZ 规则仍只以本地 `ThirdParty/gokz-master` 为准；CS:GO 引擎移动调用以本地 `ThirdParty/cstrike15_src-master/game/shared/cstrike15/cs_gamemovement.cpp` 及其实际调用的同仓库 `game/shared/gamemovement.cpp` 为准。
- 本地 GOKZ `gokz-mode-kztimer.sp::SlopeFix` 注释写明它的一个小型 `ClipVelocity` helper 历史上参考过 SDK 2013；这只是 GOKZ 插件自身的来源注释，不是 LUO 的 CS:GO 引擎移动依据。本轮移植的是 GOKZ 文件里的水平分量计算。
- `movementapi.inc` 仍然只有接口声明，没有 MovementAPI runtime；Perf 内部判定和精确事件时序不能从当前项目文件证实。

### 17.2 本轮代码修改

- `GokzKzTimerMovement.ApplySlopeFix` 之前用普通最近命中查询选坡面，可能把玩家/NPC Collider 作为 GOKZ 的 brush-only 斜坡。现在单独过滤 `Player`、`Enemy` 标签，并保留世界和 movable 几何；这是对 `MASK_PLAYERSOLID_BRUSHONLY` 与 GOKZ trace filter 的项目映射。
- 修订了本文件中已过时的输入位字段说明：当前运动相关 Source button 位和 `OldButtons` 已接入活动链；完整 `CUserCmd` 与 Unity 输入采样仍有差异。
- 将 2026-11-13 空中蹲眼高实现明确标为历史错误做法；当前代码依本地 CS:GO `FinishDuck` 同命令的 9 HU origin 偏移和 46 HU view offset 处理。
- 修正 `GokzSurfaceProperties` 对 Ice 预设的注释：本地泄漏源码有 `surfacegameprops_t`/`surfacedata_t` 字段定义，但项目未带 CS:GO 的 shipped surface-property data table；`0.05` 是 LUO Unity 映射值，不能当成 CS:GO 官方 Ice friction 数据。
- 复核 `GokzModeSettings` 的 KZTimer 参数表以及 `GokzMovementRules` 的地面加速、空气加速和摩擦公式：与本地 `gokz-mode-kztimer.sp`、`CCSGameMovement::Accelerate` 及其调用的 `CGameMovement` 公式相符；本轮未改这些公式。`PRE_VELMOD_MAX` 和 `PERF_SPEED_CAP` 仍分别只用于 KZTimer prestrafe max-speed hook 与 `HitPerf` 的跳前水平速度修正。
- `GokzPortManifest` 增补 `ProcessMovement/PlayerMove/CheckParameters` 与 `CheckStuck` 审计项，并明确 Perf 状态依赖仓库缺失的 MovementAPI runtime。

### 17.3 未验证边界

- Unity 物理查询依然不能实现 Source contents mask、brush/playerclip 分类和原生 trace 语义；本轮只修正了可由当前项目标签映射的 `SlopeFix` 玩家/NPC 排除。
- `GokzSurfacePreset.Ice` 的 `0.05` 仍是 LUO 可配置映射值；真实 CS:GO material surface 数据不在当前本地源码包中，不能据此宣称该预设的摩擦数值与游戏相同。
- 运动规则、参数和调用阶段做了静态源码核对；按用户要求没有启动 Unity/游戏，没有构建或运行测试，也没有声称运行时行为已验证。
- 本轮仍未得到 MovementAPI runtime 或真实 CS:GO/GOKZ 逐 tick 参考轨迹；不能宣称 Perf 状态和所有碰撞边界完全相同。

