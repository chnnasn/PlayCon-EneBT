# 通用地面导航与战斗站位

Unity 2022.3.48f1；使用项目已有的内置 AI 模块，无新增 UPM 依赖。

## 直接体验

打开 `Assets/Scenes/Navigation Playground.unity`，进入 Play Mode。画面从左到右是五条独立测试道：

1. 青色角色自动往返并绕过静态墙。
2. 青色小型角色通过 1.4 米窄门；橙色大型角色因体型不符停在门前。
3. 两个角色反向穿行，互相避让。
4. 黄色角色经过每 5 秒开关的动态门；关闭时等待，打开后重新寻路。
5. 六个绿色角色占据目标周围不同的战斗位置。

`Assets/Scenes/Free Look character.unity` 中的敌人也已接入导航。`Enemy` 的 Use Behavior Tree 勾选决定 BT/FSM，两套 AI 共用导航和攻击执行器。可复用的敌人预制体为 `Assets/Navigation/Navigation Enemy.prefab`。

## 分工

| 文件 | 职责 |
| --- | --- |
| `Assets/Scripts/Navigation/ActorNavigation.cs` | 目的地、寻路刷新、局部避让、到达/失败状态、停止/取消 |
| `Assets/Scripts/Navigation/CharacterMotor.cs` | 配置角色碰撞体，通过 CharacterController 执行水平位移、转向 |
| `Assets/Scripts/Navigation/NavigationProfile.cs` | 共享体型与导航参数，不存储单个怪物的路径或站位 |
| `Assets/Scripts/Navigation/NavigationWorld.cs` | 注册、卸载场景中各体型的烘焙数据 |
| `Assets/Scripts/Navigation/DynamicNavigationObstacle.cs` | 动态盒形障碍、静止后切割导航网格、门的开关 |
| `Assets/Scripts/Navigation/CombatPositionRing.cs` | 目标周围位置的可达性、空间间距、预订和释放 |
| `Assets/Editor/GameplayNavigationSetup.cs` | 场景装配、按体型烘焙、创建五阶段演示场景 |

FSM/BT 决定去哪里、什么时候攻击；导航提供绕路和避让速度；CharacterController 处理实际碰撞。NavMeshAgent 不直接更新 Transform，动画也不额外推动敌人。敌人重力保留在原有 ControllerBase 中。

移动终点通过 `NavMeshAgent.nextPosition` 的原生连通性和边界约束裁定，再由 CharacterController 执行并反馈实际位置，避免简单射线截断在墙角产生零位移死锁。相关 API 约束见 [Unity 2022.3 官方文档](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AI.NavMeshAgent-nextPosition.html)。

## 接入另一种怪物

1. 根对象挂 `CharacterController`、`ActorNavigation`；依赖的 `CharacterMotor`、`NavMeshAgent` 自动添加。物体脚底作为根位置，模型放在子对象上。推荐根缩放为 1。
2. 给 ActorNavigation 指定 Small Ground 或 Large Ground 配置；速度由怪物决策传入。小型半径 0.5 米、高 2.75 米；大型半径 0.9 米、高 3.6 米。
3. 每个需要行走的 Update 调用 `MoveTo(目标点, 米每秒, 停止距离)`。本帧未提交移动命令会暂停。若本帧已经提交过移动、随后才收到受击等中断，立即调用 `Pause()` 或 `Cancel()` 撤销该命令；已有 Enemy 已在受击、破防、后撤、死亡和回巢入口处理取消。离开战斗或回收对象时调用 `Cancel()`；重新启用不会自动追赶旧目标。
4. 根据 `Status` 处理 Arrived、PartialPath、Unreachable、Stuck、OffNavMesh。已有 Enemy 会在无法通行时转入回巢流程；普通导航使用者可自行选择等待、换目标或失败。
5. 需要多人包围时，目标挂 `CombatPositionRing`，请求 `TryReserve` 后走到返回点；离开战斗时 Release。无位置时留在外圈等待。预订定期检查可达性，拥有者禁用或预订超时会释放。

`Assets/Navigation/Small Ground.asset` 和 `Large Ground.asset` 使用不同 agentTypeId，各自有独立 NavMesh 数据。仅放大模型或 NavMeshAgent.radius 不能替代重新烘焙。调整半径、高度、台阶或坡度后重新烘焙；新增通行类别时需为其建立独立 Agent Type，并放进 NavigationWorld.profiles 后调用烘焙方法。配置菜单会使用项目预置的两类配置。

## 场景与障碍

保存场景后，在编辑模式执行 `Tools > Gameplay > Navigation > Configure And Bake Current Scene`。该命令给现有 Enemy/Player 补齐导航和站位组件，并生成当前场景的两套数据，保存到 `Assets/Navigation/<场景名>/`。静态几何来自 Physics Colliders，默认使用 Default 和 Ground 层；角色控制器及动态障碍不参与静态烘焙。

门和移动箱体挂 `DynamicNavigationObstacle`，使用 BoxCollider；尺寸来自本地碰撞盒及 Transform 缩放。移动时依赖局部避让和碰撞，静止约 0.15 秒后切割网格。开门调用 `SetBlocking(false)`，关门调用 `SetBlocking(true)`，同步切换碰撞和导航障碍；视觉开关由具体门逻辑处理。改变盒形碰撞尺寸后调用 Configure。

禁止给同一个敌人同时启用 NavMeshObstacle 和负责避让的 NavMeshAgent。不要在另一脚本里同时移动敌人的 Transform 或施加额外根位移。动态障碍切割和异步寻路需要若干帧，不保证开门当帧就获得新路线。

## 验证与范围

导航完整回归 **10/10 通过，0 失败、0 跳过**，UnitySkills 作业 `45da9cb5`；原始记录见 [NavigationTestResults.json](NavigationTestResults.json)。真实敌人联动分别覆盖 FSM/BT 的左右绕墙、抵达攻击、同帧受击/破防/死亡取消移动和释放站位。

最终代码的原有玩法兼容性回归 **23/23 通过，0 失败、0 跳过**，作业 `511d9416`；原始记录见 [NavigationCompatibilityResults.json](NavigationCompatibilityResults.json)。覆盖玩家动画退出、敌我伤害、死亡复活、多怪独立数据与状态优先级。

`Window > General > Test Runner > EditMode` 中运行 `Navigation.Tests`。十个用例临时搭建场地并进入 Play Mode，覆盖静态绕墙、大小体型、相向避让、开关门、移动箱体、六人站位、FSM/BT 绕墙攻击、禁用重启和脱离网格后的不瞬移，并直接加载已保存的演示场景验证五条测试道。旧战斗/玩家回归仍位于 `Gameplay.Tests`。

帧率用例以 30/60/120 Hz 的模拟步长执行同一条绕墙路线，断言各次到达时间差小于 0.35 秒。它检查移动时间单位和拐角约束，不等于实际 GPU 帧率或大规模敌人的性能测试。

这是地面怪物的可复用基础。飞行、游泳、攀爬、跳跃链路、移动平台，以及狭窄道路的大规模拥堵调度尚未实现。局部避让不保证任意密度下无死锁，持续卡住会反馈 Stuck，由上层决定下一步。战斗站位分配空间，不限制同一时刻允许几只怪物攻击；若需要轮流出手，应另外增加攻击名额机制。
