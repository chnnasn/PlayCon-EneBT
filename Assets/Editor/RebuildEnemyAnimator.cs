using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 重构敌人动画：生成与行为树状态一一对应的 AnimatorController（怪兽BT.controller）。
/// 结构：按"同类动作"使用 sub-state machine 分组 ——
///   Base Layer:
///     Spawn（出生，一次性）
///     Dead（死亡，终态）
///     Locomotion 子状态机：Idle / WalkStart / Locomotion / Retreat
///     Attack 子状态机：Goblin_Ani_Attack_01 / 02 / 03
///     HitReaction 子状态机：Hit_2 / BrokenDefense
/// - 一次性片段（出生/攻击/受击/破防）播完通过 exit 自然回到 Locomotion，
///   避免"攻击中途被打断、切换另一个攻击"。
/// - 行走片段（Walk_F / Walk_B）设为循环，避免播一遍后定格。
/// - 给 Idle / Hit_2 补挂 EnemyAnimatorTranslate（巡逻收尾/受击事件）。
/// 菜单：Tools > Enemy Flow Test > Rebuild Enemy Animator (BT)
/// </summary>
public static class RebuildEnemyAnimator
{
    private const string FbxPath = "Assets/Enemy/怪兽/怪兽.fbx";
    private const string ControllerPath = "Assets/Enemy/怪兽/怪兽BT.controller";
    private const string ScenePath = "Assets/Scenes/Free Look character.unity";

    private static readonly string[] NeededClips /* v2 */ =
    {
        "Goblin_Ani_Idle",
        "Goblin_Ani_Walk_F_Start",
        "Goblin_Ani_Walk_F",
        "Goblin_Ani_Walk_B",
        "Goblin_Ani_Run",
        "Goblin_Ani_Born",
        "Goblin_Ani_Attack_01",
        "Goblin_Ani_Attack_02",
        "Goblin_Ani_Attack_03",
        "Goblin_Ani_Hit_L_Front",
        "Goblin_Ani_Stun_Hit_H_Front",
        "Goblin_Ani_Death_Stay",
    };

    [MenuItem("Tools/Enemy Flow Test/Rebuild Enemy Animator (BT)")]
    public static void Run()
    {
        // 1) 行走片段设为循环（否则播一遍就定格）
        FixWalkLoop();
        AssetDatabase.Refresh();

        // 2) 加载 FBX 内嵌片段
        var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .ToDictionary(c => c.name, c => c);

        foreach (var name in NeededClips)
        {
            if (!clips.ContainsKey(name))
            {
                Debug.LogWarning($"[RebuildAnim] 缺少片段: {name}");
            }
        }

        // 3) 重建控制器（sub-state machine 分组）
        if (File.Exists(ControllerPath))
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AssetDatabase.Refresh();
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = controller.layers[0].stateMachine;

        controller.AddParameter("Move", AnimatorControllerParameterType.Float);
        controller.AddParameter("Lock", AnimatorControllerParameterType.Float);
        controller.AddParameter("Offset", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Break", AnimatorControllerParameterType.Bool);

        // --- 顶层状态：出生 / 死亡 ---
        var spawn = AddState(sm, "Spawn", Clip(clips, "Goblin_Ani_Born"));
        var dead = AddState(sm, "Dead", Clip(clips, "Goblin_Ani_Death_Stay"));

        // --- Locomotion 子状态机：移动类动作 ---
        var locoSM = sm.AddStateMachine("Locomotion");
        var idle = AddState(locoSM, "Idle", Clip(clips, "Goblin_Ani_Idle"));
        var walkStart = AddState(locoSM, "WalkStart", Clip(clips, "Goblin_Ani_Walk_F_Start"));
        var locomotion = AddState(locoSM, "Locomotion", Clip(clips, "Goblin_Ani_Walk_F"));
        var run = AddState(locoSM, "Run", Clip(clips, "Goblin_Ani_Run"));
        var retreat = AddState(locoSM, "Retreat", Clip(clips, "Goblin_Ani_Walk_B"));
        locoSM.defaultState = idle;
        AddTransition(walkStart, locomotion, 0.9f); // 起步动画播完进入行走

        // --- Attack 子状态机：攻击类动作 ---
        var atkSM = sm.AddStateMachine("Attack");
        var bite = AddState(atkSM, "Goblin_Ani_Attack_01", Clip(clips, "Goblin_Ani_Attack_01"));
        var claw = AddState(atkSM, "Goblin_Ani_Attack_02", Clip(clips, "Goblin_Ani_Attack_02"));
        var roar = AddState(atkSM, "Goblin_Ani_Attack_03", Clip(clips, "Goblin_Ani_Attack_03"));
        atkSM.defaultState = bite;
        AddExitTransition(bite, 0.95f);
        AddExitTransition(claw, 0.95f);
        AddExitTransition(roar, 0.95f);

        // --- HitReaction 子状态机：受击/破防类动作 ---
        var hitSM = sm.AddStateMachine("HitReaction");
        var hit = AddState(hitSM, "Hit_2", Clip(clips, "Goblin_Ani_Hit_L_Front"));
        var brk = AddState(hitSM, "BrokenDefense", Clip(clips, "Goblin_Ani_Stun_Hit_H_Front"));
        hitSM.defaultState = hit;
        AddExitTransition(hit, 0.9f);
        AddExitTransition(brk, 0.9f);

        // --- 动画事件 ---
        AddBehaviour(idle, AnimationState.Idle);
        AddBehaviour(hit, AnimationState.Hit);

        // --- 顶层过渡 ---
        sm.defaultState = spawn;

        // 出生播完 → 进入移动子状态机（默认 Idle）
        AddTransitionToSubMachine(spawn, locoSM, 1f);

        // 攻击子状态机播完（内部状态 exit）→ 回移动子状态机
        sm.AddStateMachineTransition(atkSM, locoSM);

        // 受击/破防子状态机播完（内部状态 exit）→ 回移动子状态机
        sm.AddStateMachineTransition(hitSM, locoSM);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[RebuildAnim] 控制器已生成（sub-state machine 分组）: " + ControllerPath);

        // 4) 绑定到场景敌人
        var scene = GetTargetScene();
        var enemy = Object.FindObjectOfType<Enemy>();
        if (enemy == null)
        {
            Debug.LogError("[RebuildAnim] 场景中未找到 Enemy。");
            return;
        }

        var animator = enemy.GetComponent<Animator>();
        if (animator == null)
        {
            animator = enemy.gameObject.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        EditorUtility.SetDirty(enemy.gameObject);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[RebuildAnim] 完成：敌人 {enemy.name} 已绑定新控制器，场景已保存。");
    }

    private static Scene GetTargetScene()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.path == ScenePath)
        {
            return active;
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
    {
        var state = sm.AddState(name);
        state.motion = clip;
        state.speed = 1f;
        state.writeDefaultValues = true;
        return state;
    }

    private static void AddBehaviour(AnimatorState state, AnimationState animationState)
    {
        if (state == null)
        {
            return;
        }

        var behaviour = state.AddStateMachineBehaviour<EnemyAnimatorTranslate>();
        behaviour.onAnimationState = animationState;
        Debug.Log($"[RebuildAnim] 给状态 {state.name} 添加 EnemyAnimatorTranslate({animationState})。");
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, float exitTime)
    {
        if (from == null || to == null)
        {
            return;
        }

        var transition = from.AddTransition(to);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = 0.1f;
        transition.canTransitionToSelf = false;
    }

    private static void AddExitTransition(AnimatorState state, float exitTime)
    {
        if (state == null)
        {
            return;
        }

        // 内部状态 → 子状态机 Exit（isExit 过渡），由子状态机的出口过渡决定去向。
        var transition = state.AddTransition((AnimatorState)null);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = 0.1f;
        transition.canTransitionToSelf = false;
        transition.isExit = true;
    }

    private static void AddTransitionToSubMachine(AnimatorState from, AnimatorStateMachine to, float exitTime)
    {
        if (from == null || to == null)
        {
            return;
        }

        // 状态 → 子状态机（目的地设为子状态机，进入其默认状态）。
        var transition = from.AddTransition((AnimatorState)null);
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = 0.1f;
        transition.canTransitionToSelf = false;
        transition.destinationStateMachine = to;
    }

    private static AnimationClip Clip(Dictionary<string, AnimationClip> clips, string name)
    {
        clips.TryGetValue(name, out var clip);
        return clip;
    }

    private static void FixWalkLoop()
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[RebuildAnim] 无法获取 ModelImporter。");
            return;
        }

        var clipAnimations = importer.clipAnimations;
        var changed = false;
        foreach (var clip in clipAnimations)
        {
            if ((clip.name == "Goblin_Ani_Walk_F" || clip.name == "Goblin_Ani_Walk_B" || clip.name == "Goblin_Ani_Run") && !clip.loopTime)
            {
                clip.loopTime = true;
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = clipAnimations;
            importer.SaveAndReimport();
            Debug.Log("[RebuildAnim] 行走/跑步片段已设为循环（重新导入 FBX）。");
        }
    }
}
