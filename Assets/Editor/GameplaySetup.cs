using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Idempotent scene wiring for the existing third-person demo.</summary>
public static class GameplaySetup
{
    public const string ScenePath = "Assets/Scenes/Free Look character.unity";

    [MenuItem("Tools/Gameplay/Configure Current Scene")]
    public static void ConfigureCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Configure gameplay in Edit Mode.");
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var player in Object.FindObjectsOfType<Player>(true))
        {
            if (player.gameObject.scene != scene) continue;
            if (player.GetComponent<PlayerHealth>() == null)
                Undo.AddComponent<PlayerHealth>(player.gameObject);
            var animator = player.GetComponent<Animator>();
            Undo.RecordObject(animator, "Configure player root motion");
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(animator);
        }
        foreach (var enemy in Object.FindObjectsOfType<Enemy>(true))
        {
            if (enemy.gameObject.scene != scene) continue;
            var serialized = new SerializedObject(enemy);
            var groundRadius = serialized.FindProperty("GroundDetectionRadius");
            if (groundRadius != null && groundRadius.floatValue <= 0f)
                groundRadius.floatValue = 0.25f;
            serialized.ApplyModifiedProperties();
            if (enemy.Data != null && enemy.Data.obstacleMask.value == 0)
            {
                Undo.RecordObject(enemy.Data, "Enable enemy obstacle detection");
                enemy.Data.obstacleMask = LayerMask.GetMask("Default", "Ground");
                EditorUtility.SetDirty(enemy.Data);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[GameplaySetup] Player health, root motion and enemy obstacle detection configured.");
    }
}
