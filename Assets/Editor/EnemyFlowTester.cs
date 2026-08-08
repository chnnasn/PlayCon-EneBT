using PlayerState;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 敌人 AI 全套流程测试驱动：通过编辑器菜单在 Play 模式下控制玩家位置、
/// 施加伤害/破防/后撤/击杀事件，用于验证行为树各状态流转。
/// 菜单入口：Tools > Enemy Flow Test > ...
/// </summary>
public static class EnemyFlowTester
{
    private const string HitName = "Hit_2";

    private static Enemy FindEnemy()
    {
        var enemy = Object.FindObjectOfType<Enemy>();
        if (enemy == null)
        {
            Debug.LogError("[EnemyFlowTest] 场景中找不到 Enemy。");
        }

        return enemy;
    }

    private static Player FindPlayer()
    {
        var player = Object.FindObjectOfType<Player>();
        if (player == null)
        {
            Debug.LogError("[EnemyFlowTest] 场景中找不到 Player。");
        }

        return player;
    }

    [MenuItem("Tools/Enemy Flow Test/Teleport Player Near Enemy")]
    public static void TeleportPlayerNearEnemy()
    {
        var enemy = FindEnemy();
        var player = FindPlayer();
        if (enemy == null || player == null)
        {
            return;
        }

        var forward = enemy.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        var target = enemy.transform.position + forward.normalized * 2.5f;
        target.y = 1f; // 固定在地面高度，避免传送到无地板区域掉出世界
        player.transform.position = target;
        Debug.Log($"[EnemyFlowTest] 玩家传送到敌人前方 2.5m: {target}");
    }

    [MenuItem("Tools/Enemy Flow Test/Teleport Player Far (20m)")]
    public static void TeleportPlayerFar()
    {
        var enemy = FindEnemy();
        var player = FindPlayer();
        if (enemy == null || player == null)
        {
            return;
        }

        var target = enemy.transform.position + new Vector3(0f, 0f, -20f);
        target.y = 1f;
        player.transform.position = target;
        Debug.Log($"[EnemyFlowTest] 玩家传送到 20m 外: {target}");
    }

    [MenuItem("Tools/Enemy Flow Test/Teleport Player Behind Enemy")]
    public static void TeleportPlayerBehindEnemy()
    {
        var enemy = FindEnemy();
        var player = FindPlayer();
        if (enemy == null || player == null)
        {
            return;
        }

        var backward = -enemy.transform.forward;
        backward.y = 0f;
        if (backward.sqrMagnitude < 0.0001f)
        {
            backward = Vector3.back;
        }

        var target = enemy.transform.position + backward.normalized * 3f;
        target.y = 1f;
        player.transform.position = target;
        Debug.Log($"[EnemyFlowTest] 玩家传送到敌人身后 3m: {target}");
    }

    [MenuItem("Tools/Enemy Flow Test/Force Investigate")]
    public static void ForceInvestigate()
    {
        var enemy = FindEnemy();
        if (enemy == null)
        {
            return;
        }

        enemy.EndChaseLock();
        var point = enemy.transform.position + enemy.transform.forward * 3f;
        point.y = 1f;
        enemy.behaviorTree?.RequestInvestigate(point);
        Debug.Log($"[EnemyFlowTest] 强制调查点: {point}");
    }

    [MenuItem("Tools/Enemy Flow Test/Dump Animator States")]
    public static void DumpAnimatorStates()
    {
        var enemy = FindEnemy();
        if (enemy == null)
        {
            return;
        }

        var animator = enemy.GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("[EnemyFlowTest] Animator/controller 为空。");
            return;
        }

        var ac = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ac == null)
        {
            return;
        }

        foreach (var layer in ac.layers)
        {
            DumpStateMachine(layer.stateMachine, "", layer.name);
        }
    }

    private static void DumpStateMachine(UnityEditor.Animations.AnimatorStateMachine sm, string indent, string layerName)
    {
        if (sm == null)
        {
            return;
        }

        foreach (var st in sm.states)
        {
            var motion = st.state.motion;
            if (motion == null)
            {
                Debug.Log($"[EnemyFlowTest] {indent}[{layerName}] {st.state.name} -> (no motion)");
                continue;
            }

            Debug.Log($"[EnemyFlowTest] {indent}[{layerName}] {st.state.name} -> MOTION type={motion.GetType().Name} name={motion.name}");
            if (motion is UnityEditor.Animations.BlendTree tree)
            {
                DumpTree(tree, indent + "    ");
            }
        }

        foreach (var sub in sm.stateMachines)
        {
            DumpStateMachine(sub.stateMachine, indent + "    ", layerName);
        }
    }

    private static void DumpTree(UnityEditor.Animations.BlendTree tree, string indent)
    {
        Debug.Log($"[EnemyFlowTest] {indent}TREE name={tree.name} param='{tree.blendParameter}' paramY='{tree.blendParameterY}' min={tree.minThreshold} max={tree.maxThreshold} children={tree.children.Length}");
        foreach (var ch in tree.children)
        {
            if (ch.motion is UnityEditor.Animations.BlendTree sub)
            {
                DumpTree(sub, indent + "    ");
            }
            else
            {
                Debug.Log($"[EnemyFlowTest] {indent}    CLIP name={ch.motion?.name} threshold={ch.threshold}");
            }
        }
    }

    [MenuItem("Tools/Enemy Flow Test/Dump Current Anim State")]
    public static void DumpCurrentAnimState()
    {
        var enemy = FindEnemy();
        if (enemy == null)
        {
            return;
        }

        var animator = enemy.GetComponent<Animator>();
        if (animator == null)
        {
            return;
        }

        var info = animator.GetCurrentAnimatorStateInfo(0);
        var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        var stateName = "?";
        if (controller != null)
        {
            foreach (var layer in controller.layers)
            {
                stateName = FindStateName(layer.stateMachine, info.shortNameHash) ?? stateName;
            }
        }

        var clipName = animator.GetCurrentAnimatorClipInfo(0).Length > 0 ? animator.GetCurrentAnimatorClipInfo(0)[0].clip.name : "(none)";
        Debug.Log($"[EnemyFlowTest] currentState={stateName} clip={clipName} normalizedTime={info.normalizedTime:F2} inTransition={animator.IsInTransition(0)}");
    }

    private static string FindStateName(UnityEditor.Animations.AnimatorStateMachine sm, int shortNameHash)
    {
        if (sm == null)
        {
            return null;
        }

        foreach (var s in sm.states)
        {
            if (s.state.nameHash == shortNameHash)
            {
                return s.state.name;
            }
        }

        foreach (var sub in sm.stateMachines)
        {
            var n = FindStateName(sub.stateMachine, shortNameHash);
            if (n != null)
            {
                return n;
            }
        }

        return null;
    }

    [MenuItem("Tools/Enemy Flow Test/Teleport Player To Start")]
    public static void TeleportPlayerToStart()
    {
        var player = FindPlayer();
        if (player == null)
        {
            return;
        }

        player.transform.position = new Vector3(4.63f, 1f, -3.82f);
        Debug.Log("[EnemyFlowTest] 玩家已重置到出生点 (4.63, 1, -3.82)。");
    }

    [MenuItem("Tools/Enemy Flow Test/Deal 1000 Damage")]
    public static void DealDamage()
    {
        var enemy = FindEnemy();
        var player = FindPlayer();
        if (enemy == null || player == null)
        {
            return;
        }

        GameEventsManager.MainInstance.CallEvent<float, string, string, Transform, Transform, CharacterComboBase>(
            HealthState.TriggerDamage.ToString(),
            1000f, HitName, string.Empty,
            player.transform, enemy.transform, null);
        Debug.Log("[EnemyFlowTest] 对敌人造成 1000 伤害（TriggerDamage）。");
    }

    [MenuItem("Tools/Enemy Flow Test/Trigger Break")]
    public static void TriggerBreak()
    {
        var enemy = FindEnemy();
        if (enemy == null)
        {
            return;
        }

        GameEventsManager.MainInstance.CallEvent(HealthState.BrokenDefense.ToString(), enemy.transform);
        Debug.Log("[EnemyFlowTest] 触发破防（BrokenDefense）。");
    }

    [MenuItem("Tools/Enemy Flow Test/Trigger Retreat")]
    public static void TriggerRetreat()
    {
        var enemy = FindEnemy();
        if (enemy == null)
        {
            return;
        }

        GameEventsManager.MainInstance.CallEvent(Enemy.PlayerOneStrokeCommandEvent, enemy.transform);
        Debug.Log("[EnemyFlowTest] 触发一笔画后撤（PlayerOneStrokeCommand）。");
    }

    [MenuItem("Tools/Enemy Flow Test/Kill Enemy")]
    public static void KillEnemy()
    {
        var enemy = FindEnemy();
        var player = FindPlayer();
        if (enemy == null || player == null)
        {
            return;
        }

        GameEventsManager.MainInstance.CallEvent<float, string, string, Transform, Transform, CharacterComboBase>(
            HealthState.TriggerDamage.ToString(),
            999999f, HitName, string.Empty,
            player.transform, enemy.transform, null);
        Debug.Log("[EnemyFlowTest] 击杀敌人（999999 伤害）。");
    }
}
