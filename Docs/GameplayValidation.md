# 玩家控制与敌人战斗回归

项目：Unity 2022.3.48f1；场景：`Assets/Scenes/Free Look character.unity`。

## 实测结果（2026-10-16）

最终完整运行 **23/23 通过，0 失败、0 跳过**，耗时 161 秒；结束后 Console 错误为 0，编辑器已退出 Play Mode，场景无未保存修改。原始结果见 [GameplayTestResults.json](GameplayTestResults.json)，UnitySkills 作业编号 `708f00bf`。

后续导航接入与同帧中断修复后，该组再次完整运行 **23/23 通过**，作业 `511d9416`，见 [NavigationCompatibilityResults.json](NavigationCompatibilityResults.json)。新增的 10 项导航测试及使用说明见 [NavigationGuide.md](NavigationGuide.md)。

| 测试组 | 通过数 | 主要断言 |
| --- | ---: | --- |
| GameplayCoreRegressionTests | 3 | FSM 停止/重启、BT 抢占与重置、计时器重启 |
| GameplayHealthRegressionTests | 6 | 定向伤害与破防、敌我死亡复活、订阅生命周期、多怪血条隔离 |
| GameplayRuntimeRegressionTests | 7 | 两种 AI 实际扣血、死亡取消命中、墙体阻挡、阻塞脱战、禁用重启、多攻击者、位移/计时器 |
| EnemyPriorityRegressionTests | 4 | 两种 AI 的后撤/破防/受击优先级、延迟回巢、死亡接管与复活 |
| PlayerAnimationRegressionTests | 3 | 闪避切技能、攻击切闪避、同一闪避重入时拒绝旧退出事件 |

位移测试在 30/60/120 Hz 下各输入累计 3 米的根位移，断言最终距离误差不超过 0.02 米；另断言 CharacterController 被墙体挡住。场景测试的时间等待使用逐帧比较 `Time.time`，兼容本项目 Test Framework 1.1.33 在 EditMode 测试中切换 Play Mode 的行为。

## 本轮实现

- 玩家恢复 `Movement` 混合树参数；动画 `deltaPosition` 作为本帧位移，仅经 CharacterController 处理一次，重力另行积分。
- FSM 停止会退出当前状态并移除输入/计时器；退出后的延迟回调不能改变新状态。死亡和禁用停止两个玩家 FSM，复活或重新启用按生命周期恢复。
- Animator 退出事件携带进入时的状态机版本和玩家会话；旧闪避/攻击退出不会结束新技能或新闪避。取消后的旧攻击命中事件被拒绝。
- FSM 与 BT 共用敌人攻击执行器：前摇、单次命中、后摇、独立冷却；命中时重新检查存活、距离、朝向、高差和视线。受击、破防、后撤、死亡、禁用会取消攻击。
- 敌我使用统一的定向伤害与生命值接口。敌人普通护甲、破防倍率保留；死亡只通知一次，死亡后拒绝伤害；禁用再启用不会自动复活。
- 每只敌人拥有独立运行时生命数据和血条；玩家拥有 HP 显示和死亡后的 `复活 / Revive` 按钮。
- 丢失视线、离巢过远或移动受阻会放弃追击；回巢也有阻塞/超时出口。出生、后撤、破防、受击完成前保留回巢意图，避免反应状态被低优先级移动打断。

## 使用与复跑

场景已配置玩家 `PlayerHealth` 和障碍层。若重新搭建场景，可在编辑模式执行 `Tools > Gameplay > Configure Current Scene`；该命令保存当前场景，添加缺失的玩家健康组件，并配置根运动和敌人障碍检测。

在 `Window > General > Test Runner` 的 **EditMode** 页运行 `Gameplay.Tests` 组。核心测试直接执行；场景测试会自动进入和退出 Play Mode，并在退出时丢弃临时墙体、怪物和配置副本。运行前应保存场景。

测试包括状态停止/重启、旧计时器回调、玩家动画切换、FSM/BT 战斗、前摇中出现墙体、阻塞追击与回巢、多个攻击者、血量/血条隔离、事件订阅生命周期、死亡与复活、高优先级反应，以及 30/60/120 Hz 模拟步长下的位移。

## 边界

- 后续导航实现已提供按体型烘焙的绕路、多怪避让、动态障碍与战斗站位，见 [NavigationGuide.md](NavigationGuide.md)。本文件的旧战斗回归会主动禁用 ActorNavigation，独立验证原有碰撞、遮挡与超时脱战；启用导航的联动场景另由 Navigation.Tests 验证。
- 帧率回归向位移入口注入等价的逐帧根位移，并改变模拟步长；它验证时间单位与碰撞一致性，不代表 GPU 性能或所有动画片段在实际 30/60/120 FPS 下的完整手感验收。
- 玩家缺少专用受击/死亡动画资源，本轮完成生命值、停止控制、UI 和复活闭环，未补造动画。
- 本轮不包含移动平台、完整跳跃/台阶边界、联网同步和大规模敌人性能验收。
