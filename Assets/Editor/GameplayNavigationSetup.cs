using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class GameplayNavigationSetup
{
    public const string SmallProfilePath = "Assets/Navigation/Small Ground.asset";
    public const string LargeProfilePath = "Assets/Navigation/Large Ground.asset";
    public const string EnemyPrefabPath = "Assets/Navigation/Navigation Enemy.prefab";
    public const string DemoScenePath = "Assets/Scenes/Navigation Playground.unity";

    [MenuItem("Tools/Gameplay/Navigation/Configure And Bake Current Scene")]
    public static void ConfigureCurrentScene()
    {
        RequireEditMode();
        EnsureFolder("Assets/Navigation");
        var small = Profile(SmallProfilePath, "Gameplay Small", 0.5f, 2.75f);
        var large = Profile(LargeProfilePath, "Gameplay Large", 0.9f, 3.6f);
        var scene = SceneManager.GetActiveScene();
        var world = Object.FindObjectOfType<NavigationWorld>();
        if (world == null) world = new GameObject("Navigation World").AddComponent<NavigationWorld>();
        world.profiles = new[] { small, large };
        foreach (var player in Object.FindObjectsOfType<Player>(true))
            if (player.gameObject.scene == scene && player.GetComponent<CombatPositionRing>() == null)
                Undo.AddComponent<CombatPositionRing>(player.gameObject);
        foreach (var enemy in Object.FindObjectsOfType<Enemy>(true))
        {
            if (enemy.gameObject.scene != scene) continue;
            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent == null) agent = Undo.AddComponent<NavMeshAgent>(enemy.gameObject);
            agent.enabled = false;
            var navigation = enemy.GetComponent<ActorNavigation>();
            if (navigation == null) navigation = Undo.AddComponent<ActorNavigation>(enemy.gameObject);
            navigation.Configure(navigation.Profile != null ? navigation.Profile : small);
            EditorUtility.SetDirty(navigation);
            EditorUtility.SetDirty(agent);
            EditorUtility.SetDirty(enemy.GetComponent<CharacterController>());
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath))
                PrefabUtility.SaveAsPrefabAsset(enemy.gameObject, EnemyPrefabPath);
        }
        Bake(world);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Navigation] Configured actors, combat positions and baked both ground agent sizes.");
    }

    public static NavigationProfile Profile(string path, string agentName, float radius, float height)
    {
        var profile = AssetDatabase.LoadAssetAtPath<NavigationProfile>(path);
        if (profile != null) return profile;
        var agentId = -1;
        for (var index = 0; index < NavMesh.GetSettingsCount(); index++)
        {
            var candidate = NavMesh.GetSettingsByIndex(index).agentTypeID;
            if (NavMesh.GetSettingsNameFromID(candidate) == agentName) agentId = candidate;
        }
        if (agentId == -1)
        {
            agentId = NavMesh.CreateSettings().agentTypeID;
        }
        profile = ScriptableObject.CreateInstance<NavigationProfile>();
        profile.agentTypeId = agentId;
        profile.radius = radius;
        profile.height = height;
        profile.sampleDistance = 0.75f;
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    public static void Bake(NavigationWorld world)
    {
        var scene = world.gameObject.scene;
        var markups = new List<NavMeshBuildMarkup>();
        foreach (var actor in Object.FindObjectsOfType<CharacterController>(true))
            markups.Add(new NavMeshBuildMarkup { root = actor.transform, ignoreFromBuild = true });
        foreach (var obstacle in Object.FindObjectsOfType<DynamicNavigationObstacle>(true))
            markups.Add(new NavMeshBuildMarkup { root = obstacle.transform, ignoreFromBuild = true });
        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(null, world.geometryLayers, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
        sources.RemoveAll(source => source.component != null && source.component.gameObject.scene != scene);
        var bounds = new Bounds(Vector3.zero, Vector3.one);
        var hasBounds = false;
        foreach (var collider in Object.FindObjectsOfType<Collider>())
        {
            if (collider.gameObject.scene != scene || collider.isTrigger || collider is CharacterController ||
                collider.GetComponentInParent<ActorNavigation>() != null || ((1 << collider.gameObject.layer) & world.geometryLayers) == 0) continue;
            if (!hasBounds) { bounds = collider.bounds; hasBounds = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        if (!hasBounds || sources.Count == 0) throw new System.InvalidOperationException("No navigation geometry in the current scene.");
        bounds.Expand(4f);
        var folder = "Assets/Navigation/" + scene.name;
        EnsureFolder(folder);
        var data = new List<NavMeshData>();
        foreach (var profile in world.profiles)
        {
            var baked = NavMeshBuilder.BuildNavMeshData(profile.BuildSettings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (baked == null) throw new System.InvalidOperationException("Failed to bake " + profile.name);
            var path = folder + "/" + profile.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(baked, existing);
                Object.DestroyImmediate(baked);
                EditorUtility.SetDirty(existing);
                data.Add(existing);
            }
            else { AssetDatabase.CreateAsset(baked, path); data.Add(baked); }
        }
        world.bakedData = data.ToArray();
        EditorUtility.SetDirty(world);
    }

    [MenuItem("Tools/Gameplay/Navigation/Create Five Stage Playground")]
    public static void CreatePlayground()
    {
        RequireEditMode();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) != null)
        {
            Debug.Log("[Navigation] Playground already exists. Open it and use Configure And Bake Current Scene to rebake edits.");
            return;
        }
        EnsureFolder("Assets/Navigation");
        var small = Profile(SmallProfilePath, "Gameplay Small", 0.5f, 2.75f);
        var large = Profile(LargeProfilePath, "Gameplay Large", 0.9f, 3.6f);
        var originalScene = SceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ground = Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(50f, 1f, 24f));
        ground.layer = LayerMask.NameToLayer("Ground");
        for (var x = -25; x <= 25; x += 10) Box("Lane divider", new Vector3(x, 1.5f, 0f), new Vector3(0.3f, 3f, 26f));
        Box("Static wall", new Vector3(-20f, 1.5f, 0f), new Vector3(6f, 3f, 0.5f));
        Walker("1 Static wall", small, new Vector3(-20f, 0f, -8f), new Vector3(-20f, 0f, 8f), Color.cyan);

        Box("Narrow gate left", new Vector3(-12.85f, 2f, 0f), new Vector3(4.3f, 4f, 0.5f));
        Box("Narrow gate right", new Vector3(-7.15f, 2f, 0f), new Vector3(4.3f, 4f, 0.5f));
        Walker("2 Small fits", small, new Vector3(-11f, 0f, -8f), new Vector3(-11f, 0f, 8f), Color.cyan);
        Walker("2 Large waits", large, new Vector3(-9f, 0f, -6f), new Vector3(-9f, 0f, 8f), new Color(1f, 0.55f, 0.2f));
        Walker("3 Passing A", small, new Vector3(-1.5f, 0f, -8f), new Vector3(1.5f, 0f, 8f), Color.cyan);
        Walker("3 Passing B", small, new Vector3(-1.5f, 0f, 8f), new Vector3(1.5f, 0f, -8f), Color.green);

        Box("Door frame left", new Vector3(6.75f, 1.5f, 0f), new Vector3(3.5f, 3f, 0.5f));
        Box("Door frame right", new Vector3(13.25f, 1.5f, 0f), new Vector3(3.5f, 3f, 0.5f));
        var door = Box("4 Dynamic door", new Vector3(10f, 1.5f, 0f), new Vector3(3f, 3f, 0.5f));
        door.AddComponent<DynamicNavigationObstacle>().Configure();
        door.AddComponent<NavigationDemoDoor>();
        Walker("4 Door traveller", small, new Vector3(10f, 0f, -8f), new Vector3(10f, 0f, 8f), Color.yellow);

        var target = new GameObject("5 Combat target").AddComponent<CombatPositionRing>();
        target.transform.position = new Vector3(20f, 0f, 1f);
        var targetVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        targetVisual.name = "Target marker";
        targetVisual.transform.position = target.transform.position + Vector3.up * 0.5f;
        targetVisual.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
        Object.DestroyImmediate(targetVisual.GetComponent<Collider>());
        for (var index = 0; index < 6; index++)
        {
            var walker = Walker("5 Surround " + index, small, new Vector3(17f + index, 0f, -8f), target.transform.position, Color.green);
            walker.combatTarget = target;
        }
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0f, 40f, -26f);
        camera.transform.LookAt(Vector3.zero);
        camera.orthographic = true;
        camera.orthographicSize = 19f;
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var world = new GameObject("Navigation World").AddComponent<NavigationWorld>();
        world.profiles = new[] { small, large };
        EditorSceneManager.SaveScene(scene, DemoScenePath);
        Bake(world);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(originalScene)) EditorSceneManager.OpenScene(originalScene);
        Debug.Log("[Navigation] Five-stage playground saved: " + DemoScenePath);
    }

    private static NavigationDemoWalker Walker(string name, NavigationProfile profile, Vector3 from, Vector3 to, Color color)
    {
        var actor = new GameObject(name);
        actor.layer = LayerMask.NameToLayer("Enemy");
        actor.transform.position = from;
        actor.AddComponent<CharacterController>();
        var agent = actor.AddComponent<NavMeshAgent>();
        agent.enabled = false;
        var navigation = actor.AddComponent<ActorNavigation>();
        navigation.Configure(profile);
        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.transform.SetParent(actor.transform, false);
        visual.transform.localPosition = Vector3.up * profile.height * 0.5f;
        visual.transform.localScale = new Vector3(profile.radius * 2f, profile.height * 0.5f, profile.radius * 2f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        var materialPath = "Assets/Navigation/" + (color == Color.cyan ? "Cyan" : color == Color.green ? "Green" : color == Color.yellow ? "Yellow" : "Orange") + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            AssetDatabase.CreateAsset(material, materialPath);
        }
        visual.GetComponent<Renderer>().sharedMaterial = material;
        var walker = actor.AddComponent<NavigationDemoWalker>();
        walker.waypoints = new[] { to, from };
        return walker;
    }

    private static GameObject Box(string name, Vector3 position, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = position;
        box.transform.localScale = size;
        return box;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Configure navigation in Edit Mode.");
        if (SceneManager.GetActiveScene().isDirty) throw new System.InvalidOperationException("Save the current scene before navigation setup.");
    }
}
