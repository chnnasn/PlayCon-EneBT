using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在 "Free Look character" 场景中完成敌人的位置与挂载：
/// 1) 确保敌人根节点有 Animator（怪兽.controller + FBX Avatar + Apply Root Motion）；
/// 2) 把敌人放到 patrolCenter 标记处；
/// 3) 确保 EnemySO / EnemyHealthInfo / BarUI / 图层引用完整；
/// 4) 给动画控制器补挂 EnemyAnimatorTranslate（Idle->Idle 事件、Hit_2->Hit 事件）；
/// 5) 保存场景与资源。
/// 可通过菜单 Tools/Setup Enemy In Scene 手动运行，也可用 Unity 批处理 -executeMethod 调用。
/// </summary>
public static class SetupEnemyScene
{
    private const string ScenePath = "Assets/Scenes/Free Look character.unity";
    private const string ControllerPath = "Assets/Enemy/怪兽/怪兽.controller";
    private const string FbxPath = "Assets/Enemy/怪兽/怪兽.fbx";
    private const string EnemySoPath = "Assets/Enemy/EnemySO.asset";
    private const string HealthInfoPath = "Assets/Enemy/EnemyHealthInfo.asset";

    [MenuItem("Tools/Setup Enemy In Free Look Scene")]
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var enemy = UnityEngine.Object.FindObjectOfType<Enemy>();
            if (enemy == null)
            {
                Debug.LogError("[SetupEnemy] 场景中未找到 Enemy 组件，中止。");
                return;
            }

            var root = enemy.transform;
            Debug.Log($"[SetupEnemy] 找到敌人: {root.name}  当前位置={root.position}");

            // 1) Animator：没有则添加，并绑定控制器与 Avatar
            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                animator = root.gameObject.AddComponent<Animator>();
                Debug.Log("[SetupEnemy] 敌人缺少 Animator，已添加。");
            }

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(FbxPath);
            if (controller != null) animator.runtimeAnimatorController = controller;
            if (avatar != null) animator.avatar = avatar;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Debug.Log($"[SetupEnemy] Animator: controller={controller != null} avatar={avatar != null} rootMotion={animator.applyRootMotion}");

            // 2) 位置：放到 patrolCenter 标记处（没有则用默认坐标）
            var patrolCenter = GameObject.Find("patrolCenter");
            if (patrolCenter != null)
            {
                root.position = patrolCenter.transform.position;
                Debug.Log($"[SetupEnemy] 已把敌人放到 patrolCenter: {root.position}");
            }
            else
            {
                root.position = new Vector3(0.93f, -2.07f, 0.45f);
                Debug.Log($"[SetupEnemy] 未找到 patrolCenter，使用默认位置: {root.position}");
            }

            // 3) 图层与引用
            root.gameObject.layer = LayerMask.NameToLayer("Enemy");

            var enemySo = AssetDatabase.LoadAssetAtPath<EnemySO>(EnemySoPath);
            if (enemySo != null)
            {
                var so = new SerializedObject(enemy);
                var prop = so.FindProperty("enemySO");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = enemySo;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[SetupEnemy] 已补上 enemySO 引用。");
                }
            }

            var heath = root.GetComponent<EnemyHeath>();
            if (heath != null)
            {
                var healthInfo = AssetDatabase.LoadAssetAtPath<CharacterHealthInfo>(HealthInfoPath);
                var hso = new SerializedObject(heath);
                var prop = hso.FindProperty("characterHealthInfo");
                if (prop != null && (prop.objectReferenceValue == null || healthInfo != null))
                {
                    prop.objectReferenceValue = healthInfo;
                    hso.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 4) BarUI 子物体（血条锚点）
            if (root.Find("BarUI") == null)
            {
                var bar = new GameObject("BarUI");
                bar.transform.SetParent(root, false);
                bar.transform.localPosition = new Vector3(0f, 2.03f, 0f);
                Debug.Log("[SetupEnemy] 已创建 BarUI 子物体。");
            }

            // 5) 动画控制器状态补挂 EnemyAnimatorTranslate
            AddAnimatorBehaviours(controller);

            EditorUtility.SetDirty(root.gameObject);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SetupEnemy] 完成。敌人={root.name} pos={root.position} children={root.childCount}");
        }
        catch (Exception e)
        {
            Debug.LogError("[SetupEnemy] 执行失败: " + e);
            throw;
        }
    }

    private static void AddAnimatorBehaviours(RuntimeAnimatorController controller)
    {
        if (controller == null) return;
        var ac = controller as AnimatorController;
        if (ac == null) return;

        foreach (var layer in ac.layers)
        {
            AddBehavioursToStateMachine(layer.stateMachine);
        }

        EditorUtility.SetDirty(ac);
        Debug.Log("[SetupEnemy] 已检查并补挂 EnemyAnimatorTranslate（Idle / Hit_2）。");
    }

    private static void AddBehavioursToStateMachine(AnimatorStateMachine stateMachine)
    {
        if (stateMachine == null) return;

        foreach (var child in stateMachine.stateMachines)
        {
            AddBehavioursToStateMachine(child.stateMachine);
        }

        foreach (var child in stateMachine.states)
        {
            var state = child.state;
            if (state == null) continue;

            var hasIdle = false;
            var hasHit = false;
            foreach (var behaviour in state.behaviours)
            {
                if (behaviour is EnemyAnimatorTranslate translate)
                {
                    if (translate.onAnimationState == AnimationState.Idle) hasIdle = true;
                    if (translate.onAnimationState == AnimationState.Hit) hasHit = true;
                }
            }

            if (state.name == "Idle" && !hasIdle)
            {
                var b = state.AddStateMachineBehaviour<EnemyAnimatorTranslate>();
                b.onAnimationState = AnimationState.Idle;
                Debug.Log("[SetupEnemy] 给 Idle 状态添加 EnemyAnimatorTranslate(Idle)。");
            }

            if (state.name == "Hit_2" && !hasHit)
            {
                var b = state.AddStateMachineBehaviour<EnemyAnimatorTranslate>();
                b.onAnimationState = AnimationState.Hit;
                Debug.Log("[SetupEnemy] 给 Hit_2 状态添加 EnemyAnimatorTranslate(Hit)。");
            }
        }
    }
}
