# LUO · GOKZ Unity 开发日志

> 最近更新：2026-11-25
> 项目：`E:\Ridah\Unity\Projects\LUO`
> 用途：给后续开发者/AI快速交接当前事实和下一步。只记录会影响工作的决定与变化，不重复堆叠运动公式和逐 tick 审计过程。

## 项目与参考基线

- Unity：`6000.6.8f1`。本次通过 Unity CLI 连接到项目编辑器。
- GOKZ 源码：本地快照 `ThirdParty/gokz-master`。
- CS:GO 引擎源码：用户指定的本地目录 `ThirdParty/cstrike15_src-master`，对应 `perilouswithadollarsign/cstrike15_src` 泄漏源码；不是 Valve 官方 SDK。
- MovementAPI：项目只有 `movementapi.inc` 接口声明，没有运行时实现源码。
- 后续源码核对只使用以上本地文件；用户没有要求时不搜索网页。

## 已定的运动实现

- Player 的活动运动入口使用 GOKZ KZTimer 路径：`SurfCharacter.cs` → `SourceMovementCore`（位于 `GokzKzTimerMovement.cs`），固定 `128 Hz`；Unity `CharacterController.Move` 不驱动这条路径。
- 地面/空中移动、摩擦、重力、跳跃、蹲起、碰撞 hull、坡面，以及梯子、水和运动体积已接入当前运动实现。后续日志不再逐项重复这些已落地内容；只有出现具体问题或源码证据变化时才补记。
- KZTimer 关键语义已定：`250 HU/s` 普通地面期望速度；`PRE_VELMOD_MAX = 1.104` 对应最高 `276 HU/s` 的预加速速度修正；`PERF_SPEED_CAP = 380 HU/s` 只属于 HitPerf 起跳分支。不要添加源码没有的全局最终速度截断。
- Unity 物理查询与 Source 原生 trace 并非同一实现；本地缺少 MovementAPI runtime，因此不能仅凭接口文件证明 HitPerf 内部状态完全一致。不要把这一项扩写成运动功能清单，也不要无依据改动已定运动逻辑。

## 玩家组件与输入

`Assets/Ridah/Player.prefab` 当前包含 `LZM`、`GokzRuntime`、`GokzProfile`、`GokzPlayerFeatures` 和 `GokzHud`。

玩家功能入口集中在 `GokzPlayerFeatures.cs`，涵盖 jump stats、涂鸦、测距、本地记录、回放、聊天/提示、安静模式、传送视角修正、竞速、结束处理和运动诊断。运动核心仍在 `GokzKzTimerMovement.cs`。

输入约定：滚轮上滑跳跃；涂鸦只用 **P 键按住绘制**，不再增加其他涂鸦按键。

## 本次更新（2026-11-19）

- 涂鸦已改用 URP `DecalProjector` 投射，语义对应本地 GOKZ `TE_SetupWorldDecal` 和 CS:GO 客户端 `DecalShoot`；不再用每笔一个 quad mesh 的替代实现。
- 当前启用的 `High Universal Render Deferred` Renderer 已启用 `DecalRendererFeature`。涂鸦材质使用 GOKZ 贴图，并按颜色缓存材质；P 键、连续绘制节奏和现有颜色/尺寸选项保持不变。
- 删除了旧的 quad 专用 `GokzPaint.shader`。
- Unity 编译状态：通过（`failed=false`，`errors=[]`）；Resources 材质已加载。没有进入 Play Mode，故涂鸦外观尚未目测确认。

## 碰撞问题（2026-11-20）

- 用户反馈：普通 BoxCollider 和有体积的 MeshCollider 碰撞目前看起来正常；零厚度 Plane MeshCollider 仍会卡住。
- 已修正短距离 Trace 被跳过、斜向扫掠的皮肤距离补偿，并针对零厚度平面增加起始穿面恢复及切向/离开方向命中过滤。平面迎面穿越仍应阻挡。
- 起始穿面恢复经用户运行反馈未解决问题；之后的切向命中过滤已通过 Unity 编译（`failed=false`，`errors=[]`），尚未进行运行时复测。当前不能记为已解决。
- 下一步只复测 Plane MeshCollider；若仍卡住，需定位实际 Trace 的命中比例、法线和 `startSolid/allSolid` 状态，再改对应分支。

## 传送门递归画面黑条（2026-11-24）

- 当前问题范围仅限两扇传送门面对面、递归 RT 显示时：`recursionDepth = 1` 时，递归尽头的另一扇门显示灰色；角色穿越时，该部分正确地从灰色切换为远端场景，但正确画面上会额外闪出几条黑色长条。斜向/侧向穿越时更明显，正面穿越较轻。
- 之前已解决的“穿越前看见墙后场景”和“穿越后视角异常”不属于本次问题，不要混入诊断。
- 根因尚未确认。此前尝试固定递归层数、按相机到门洞最近点调整根视图资格，用户反馈均无效果；这两处试验改动已撤回，不能记为修复或已验证原因。
- 代码路径：`RecursivePortalCamera.cs` 生成递归 RT 和近裁剪补片；`PortalApertureRendererFeature.cs` 在不透明物体之后写门洞 Stencil 并合成 RT。Unity 6.6 本地 URP 源码显示 Deferred 材质类型占用 stencil bits 5、6，用户 bits 为 0–3；当前门户引用值 4、8 位于用户 bits 内，不能据此认定存在 stencil 位冲突。
- 下一步需在同一闪烁帧区分原始递归 RT 与最终门洞合成结果：原始 RT 已有黑条时查递归相机/出口斜裁剪；原始 RT 干净而最终画面有黑条时查 aperture stencil、近裁剪补片和合成阶段，再单独隔离补片。当前没有运行时帧捕获，不能把补片或视锥裁剪猜测写成根因。
- 本轮没有确认修复；不要保留或重新应用无效的递归深度/近门距离猜测改动。

## 重叠传送门显示（2026-11-25）

- 主相机的重叠 aperture 分别累加两扇门的 stencil 位；部分交叠区取最后成功放置的门的 `portalColour`，纯白和纯黑也作为有效颜色。递归终点的 quarter-gray 静态纹理值保持不变。
- 两个洞口四个世界角点在 0.001 Unity unit 内一致时按完全重合处理：最后放置的门覆盖先放置的门。递归视图同时隐藏两扇同位洞口表面，避免同位入口门把递归终点灰色贴图显示出来。
- 修复 `PortalApertureRendererFeature` 的 `CS0120`：将序列化回退色传入 Pass 实例，避免静态渲染回调访问外层字段。Unity 当前 `compiling=false`、活动 Console 错误数为 0；未进入 Play Mode，重叠画面仍待运行确认。
- 开口并集的边界面分类曾把任意非洞口法线轴的外露面都当作洞口侧壁，可能在相交洞口的前/后边界补出错误碰撞面（包括看起来像地面的面）。现在要求该面坐标确实等于对应洞口的开口边界，才生成侧壁；没有加回重叠放置限制。此生成修正尚未运行验证。

## 传送门出口前推与阻挡拒绝（2026-11-25）

- 角色和 PortalableObject 在映射到出口后，若碰撞体实际穿透障碍，沿出口门法线每 1 HU 搜索，最多 100 HU；第一个无穿透的位置才是可接受落点。正常清晰落点不移动，不改原传送速度与旋转。
- 角色现在先验证出口再提交传送状态（视角旋转、出口占用、速度和落地状态）。搜索范围内没有安全位置时，取消这次穿越；回退点沿入口门法线至少离开整个玩家 hull 的投影范围与碰撞皮肤，并验证入口侧碰撞，再移除朝入口门推进的速度分量。
- 动态 Rigidbody、启用了碰撞恢复的运动/变换驱动物体使用复合碰撞体进行出口查询；查询按物体碰撞层矩阵过滤，NonAlloc 重叠缓存填满时扩容重查。`ComputePenetration` 成对检查时，若传送物体 collider 不支持查询 A 形状则交换查询顺序；双方均不支持（如两个非凸 MeshCollider）时用候选 collider bounds 保守检查，不能把查询不支持当作空位。首个安全点成功后再触发传送事件。
- 动态刚体被前推到安全落点后，在下一物理步前沿当前速度做一次非分配 AABB 扫掠；若近处有阻挡，只裁掉朝阻挡面的速度分量，避免刚体冲量把相邻物体撞进墙/地面。仅出口恢复前推时执行，普通刚体运动保持原有物理行为。
- PortalableObject 的远端碰撞扫掠若同一帧符合多个相邻门，只采用运动路径上最先穿过的门；传送本身每次移动样本也只处理最先的门，不能混用后续门的另一条远端路径，避免错误碰撞法线把刚体带进地面。
- 同一个 host 上的重叠 portal carve 保持可放置。MeshCollider 的轴对齐 carve，以及平行/共面洞口的世界坐标 carve，都先裁出全部开口的源表面，再用洞口体积并集生成一次外边界，重叠区不再生成内部侧壁；源面恰好位于 through-hole 边界时也按洞内删除，避免留下假地面。非平行洞口仍用 portal-aligned clipping 回退。尚未在 Unity 运行时复测重叠地面场景。
- 物体出口搜索失败时拒绝传送；恢复上一帧入口侧姿态后，沿入口门法线把完整物体 bounds 移回入口平面外，并检查入口侧碰撞。动态 Rigidbody 恢复原速度并移除继续进入入口门的法向分量。避免把物体留在出口穿透位置，也不在失败时任意方向推开。
- 本次失败回退修正尚未通过 Unity Play Mode 运行验证；不要记为已完成运行时验证。

## 当前工作状态

- 交接时先用 `git status --short` 确认工作区状态；不要重置、覆盖或清理用户未授权处理的改动。
- 涂鸦投影外观仍未在 Play Mode 确认；P 键绘制约定不变。
- 运动逻辑无需在每次交接中重新展开。只有用户提出具体运动问题时，才针对对应调用阶段查本地 GOKZ/CS:GO 源码并修改。

## 协作约定

1. 不把 SimpleKZ、bhop、surf 或自创规则称为 GOKZ KZTimer。
2. 修改运动规则前先找到本地源码依据；不通过猜测、任意硬截断或无关重构“修好”现象。
3. 不改用户没有要求的功能，不清理工作区里的无关文件。
4. 使用 Unity CLI 检查/处理项目；不擅自控制桌面、重启 Unity/电脑、运行游戏或测试。用户明确要求后再做相应操作。
5. 日志只维护简洁的当前结论、实际变更、验证结果和仍需处理的具体事项；历史过程不再按每个小函数重复罗列。

