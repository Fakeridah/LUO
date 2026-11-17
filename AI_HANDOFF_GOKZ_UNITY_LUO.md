# LUO 项目：GOKZ / KZTimer Unity 移植交接文档

> 文档用途：把本项目的背景、目标、现状、已完成内容、已知问题、源码依据和协作规则交给其他 AI 或开发者继续完成。
>
> 当前状态：进行中。本文档描述的是已经检查到的事实，不代表运动系统已经达到 GOKZ 源码一致性。
>
> 建立日期：2026-11-13

---

## 0. 给下一位 AI 的直接指令

你正在接手一个 Unity 6.6 项目。请先完整阅读本文档，再阅读下面列出的项目文件和官方源码。

本项目的核心目标不是“做一个类似 KZ 的移动控制器”，而是：

> 在 Unity 中尽可能逐 tick、逐状态、逐调用阶段地复现 GOKZ KZTimer 在 CS:GO Source 引擎中的运动行为，同时保留 LUO 原有角色、事件、武器、触发器、反向重力、失重和旧控制器作为兼容参考。

必须遵守以下规则：

1. 不得把 SimpleKZ、bhop、surf 或自行设计的移动逻辑冒充 GOKZ KZTimer。
2. 不得为了让测试数字好看而添加无源码依据的最终速度硬截断。
3. 只有在官方源码、Source 引擎代码或明确的 GOKZ/MovementAPI 行为依据支持时，才可以修改运动规则。
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
| 玩家层 | Layer `Player`，编号 9 |
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

主要参考：

- [MovementAPI 主文件](https://github.com/danzayau/MovementAPI/blob/master/addons/sourcemod/scripting/movementapi.sp)
- [MovementAPI hooks.sp](https://github.com/danzayau/MovementAPI/blob/master/addons/sourcemod/scripting/movementapi/hooks.sp)
- [MovementAPI forwards.sp](https://github.com/danzayau/MovementAPI/blob/master/addons/sourcemod/scripting/movementapi/forwards.sp)
- [MovementAPI include](https://github.com/danzayau/MovementAPI/blob/master/addons/sourcemod/scripting/include/movementapi.inc)

MovementAPI 不是可有可无的辅助文件。它负责记录和暴露：

- 当前 tick/cmdnum；
- landing/takeoff 状态；
- `WalkMoved`；
- HitPerf；
- duckbug、jumpbug、edgebug、distbug；
- ladder 状态；
- 碰撞次数和每次碰撞的起点、终点、法线；
- Duck、WalkMove、AirAccelerate、CategorizePosition、TryPlayerMove 等阶段的前后事件。

### 2.3 Source 引擎移动代码

- [Valve Source SDK 2013 gamemovement.cpp](https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/game/shared/gamemovement.cpp)

该代码可以作为公开的 Source 移动调用顺序和基础公式参考，但它不等于 CS:GO 的完整私有 `CCSGameMovement` 实现。需要把这一点明确记录下来。

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

#### Perf 的判定不是简单的“刚刚落地”

MovementAPI 的核心判断是：跳跃发生时，上一 tick 没有执行 `WalkMove`，并且不是梯子跳。当前 Unity 实现使用 `_landedThisTick` 代替，这不是同一个状态机。

---

## 4. 当前 Unity 代码结构

### 4.1 运动入口和角色

| 文件 | 作用 | 当前状态 |
|---|---|---|
| [SurfCharacter.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/SurfCharacter.cs) | `LZM`，输入、事件、视角、旧控制器和新控制器的总入口 | 仍然承载大量项目逻辑，需要谨慎拆分 |
| [SurfController.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/SurfController.cs) | 保留旧运动路径，同时调用 `SourceMovementCore` | 旧逻辑保留；新路径仍是适配器 |
| [GokzKzTimerMovement.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzKzTimerMovement.cs) | 当前所谓 GOKZ 核心移动控制器 | 公式部分有来源，状态机和碰撞不完整 |
| [MoveData.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/MoveData.cs) | 输入、速度、碰撞、触发器和项目状态容器 | 有 `Buttons/OldButtons` 字段，但当前没有真正按 Source usercmd 使用 |
| [GokzModeSettings.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzModeSettings.cs) | Vanilla/SimpleKZ/KzTimer 参数 | KzTimer 数值大部分正确 |
| [GokzMovementRules.cs](E:/Ridah/Unity/Projects/LUO/Assets/Ridah/RidahFPS/Movement/GokzMovementRules.cs) | 摩擦、加速、空气加速和 Perf 公式 | 基础数学测试通过，但不代表完整行为一致 |

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
11. 基础公式和碰撞测试当前可以通过，但这些测试不能证明与真实 CS:GO/GOKZ 逐 tick 一致。

---

## 6. 当前已知问题

### P0：必须优先解决，否则不能声称是 GOKZ

#### P0-1：Perf 状态机错误

当前代码：

```csharp
ApplyJump(_landedThisTick && !_wasOnLadder);
```

问题：官方使用 `OldWalkMoved`，当前使用“上一 tick 是否落地”。这会影响 Perf、380 上限、TakeoffSpeed 和 HUD。

必须做法：实现明确的 Source/MovementAPI 状态：

- 当前 tick 是否执行 `WalkMove`；
- 上一个 tick 是否执行 `WalkMove`；
- 当前 tick 的 landing/takeoff；
- 当前 tick 的 cmdnum/tickcount；
- 梯子离开状态；
- jumpbug/duckbug/edgebug 状态。

#### P0-2：没有真正的 Source UserCmd

当前输入主要在 Unity `Update()` 读取，在 `FixedUpdate()` 消费。Source 则围绕每个 usercmd 维护：

- `m_nButtons`；
- `m_nOldButtons`；
- `m_flForwardMove`；
- `m_flSideMove`；
- `m_flUpMove`；
- cmdnum；
- tickcount；
- 视角变化；
- 按键边沿。

当前 `MoveData.Buttons` 与 `OldButtons` 没有真正参与移动逻辑，无法保证跳跃、下蹲、预加速和 Perf 与 Source 同步。

#### P0-3：下蹲状态机是自定义近似

必须重新实现或明确模拟：

- Duck/Unduck 的状态和计时；
- `m_flDuckAmount`；
- `m_bDucked`；
- `m_bDucking`；
- 地面与空中的不同起身规则；
- Ceiling blocked；
- 刚落地完全下蹲时的 `CanUnduck` 规则；
- duck speed crop；
- displacement stuck 修复；
- Crouch jump bind 移除。

不能继续使用未经源码验证的 `MoveTowards`、`0.8f`、自定义恢复距离和自定义 spam penalty 作为最终实现。

#### P0-4：碰撞不是 Source TracePlayerBBox

当前 Unity `BoxCast` 能够实现“盒体碰撞”，但不能自动等同于 Source：

- bump 次数不同；
- `startsolid/allsolid` 处理不同；
- trace fraction 不同；
- trace mask 不同；
- Source brush-only 检测缺失；
- touch list 缺失；
- edge/distbug/edgebug 状态缺失；
- 步进和斜坡行为不完全一致。

需要独立的 `SourceTraceAdapter` 和可测试的 `TryPlayerMove/StepMove/CategorizePosition` 实现，而不是继续在 `SourceMovementCore` 中堆叠 Unity 查询特例。

#### P0-5：276 与 380 不能只看数值

当前的常量和基础公式虽然基本正确，但由于 Perf、Grounded、WalkMoved、输入边沿和碰撞状态不一致，以下现象仍然可能出现：

- 平地行走超过预期 276；
- 非 Perf 的起跳速度状态被错误识别；
- Perf 绿色提示不出现或出现在错误跳跃上；
- 落地后的摩擦 tick 数不对；
- W+A/D 预加速和纯前进状态不同于 GOKZ。

不得通过无源码依据的最终 `Clamp` 掩盖这些问题。

### P1：核心行为完成后解决

1. 落地斜坡的 `SlopeFix` 需要基于 Source 的 landing velocity 和 trace plane。
2. 需要补齐 edge/distbug/jumpbug/duckbug。
3. 需要补齐移动平台/base velocity 语义。
4. 需要补齐水体的 `CheckWater/WaterJump/WaterMove` 状态。
5. 需要处理动态 Unity Rigidbody 与静态碰撞体的差异。
6. 需要让 HUD 使用 tick/cmd 状态，而不是依赖普通渲染帧采样。
7. JumpStats 需要支持完整跳跃类型、落地速度、空中时间、strafe 和 bug 状态。

### P2：外围功能和完整 GOKZ 模块

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

## 7. 当前测试的问题

当前 EditMode 测试全部通过，但测试本身不足以证明源码一致性。

已确认的测试问题：

1. 测试主要验证直线前进和基础公式，没有验证 W+A/D 预加速全过程。
2. 没有验证真实 cmdnum/tickcount 和输入边沿。
3. 没有验证 MovementAPI 的 `OldWalkMoved` Perf 判定。
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
2. danzayau/MovementAPI；
3. Valve Source SDK gamemovement.cpp；
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

当前项目已经完成了“把部分 GOKZ/KZTimer 参数和基础公式接入 Unity”的阶段，但尚未完成“Source 调用顺序 + MovementAPI 状态机 + CS:GO 下蹲 + Source 碰撞语义”的完整移植。

接下来的工作重点不是继续猜参数，而是：

1. 建立准确的 Source usercmd/state 层；
2. 修正 MovementAPI 的 Perf/landing/takeoff 状态；
3. 重做 Duck/Unduck；
4. 重做 Source trace/collision 适配器；
5. 通过逐 tick 参考数据验证 276、380、摩擦、落地、下蹲和碰撞；
6. 最后再把 LUO 的失重、反向重力和其他事件接回去。

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

以下文件目前是 SVN 工作副本中的 `M` 状态，尚未提交：

- `Assets/Ridah/RidahFPS/Movement/GokzKzTimerMovement.cs`
- `Assets/Ridah/RidahFPS/Movement/GokzKzTimerMovementCompatibility.cs`
- `Assets/Ridah/RidahFPS/Movement/SurfController.cs`
- `Assets/Ridah/RidahFPS/Movement/SurfCharacter.cs`
- `Assets/Ridah/RidahFPS/Movement/Tests/Editor/SourceMovementCoreCollisionTests.cs`

本次修改内容：

1. 在 `SourceMovementCore` 中增加独立的 `EyeDuckAmount`，不再直接用物理 `_duckAmount` 驱动摄像机；
2. 空中按蹲时，`GokzSourceHull` 可以切换到 54HU 碰撞盒，但眼睛高度保持空中蹲之前的状态；
3. 地面 tick 才推进视角蹲起表现；
4. `SurfCharacter.UpdateGoKzViewHeight` 改为读取 `GokzEyeDuckAmount`；
5. 增加 `AirborneDuckChangesPhysicalHullWithoutChangingEyeAmount` 测试。

这是一层 Unity 适配逻辑，不能标记为“完整的 `CCSGameMovement::Duck` 源码移植”。它依据 GOKZ 源码中物理蹲状态与 Hook 边界，以及用户提供的 CS:GO 实际表现；仍需使用真实 CS:GO/GOKZ 逐 tick 数据验证。

### 13.4 编译和测试结果

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
- 按用户要求，只恢复上述一个运动代码文件到 `e7ab6fed`，保留其他文件及其现有改动；不自动提交 SVN。

### 14.4 执行与验证记录

- 先更新本日志，再执行代码回退。
- 不替用户操作角色，不重置场景，不运行测试；回退后只核对目标文件版本与 Unity 编译状态。
- 恢复后的游戏内表现尚未由用户验证；不能据此声称抖动或卡点已解决。

