using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Scene-owned baked navigation. Each agent type has its own data and clearance.</summary>
[DefaultExecutionOrder(-200), DisallowMultipleComponent]
public sealed class NavigationWorld : MonoBehaviour
{
    public NavigationProfile[] profiles;
    public NavMeshData[] bakedData;
    public LayerMask geometryLayers = 9;
    private readonly List<NavMeshDataInstance> instances = new List<NavMeshDataInstance>();

    private void OnEnable()
    {
        if (bakedData == null) return;
        foreach (var data in bakedData)
            if (data != null) instances.Add(NavMesh.AddNavMeshData(data));
    }

    private void OnDisable()
    {
        foreach (var instance in instances) if (instance.valid) instance.Remove();
        instances.Clear();
    }
}
