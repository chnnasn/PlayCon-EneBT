using UnityEngine;
using UnityEngine.AI;

/// <summary>Shared authoring data only. Paths, reservations and timers belong to each actor.</summary>
[CreateAssetMenu(menuName = "Gameplay/Navigation Profile", fileName = "NavigationProfile")]
public sealed class NavigationProfile : ScriptableObject
{
    public int agentTypeId;
    [Min(0.1f)] public float radius = 0.45f;
    [Min(0.2f)] public float height = 2f;
    [Min(0f)] public float stepHeight = 0.35f;
    [Range(0f, 60f)] public float maxSlope = 45f;
    public int areaMask = NavMesh.AllAreas;
    [Min(0.1f)] public float acceleration = 12f;
    [Min(1f)] public float turnSpeed = 540f;
    [Min(0.05f)] public float repathInterval = 0.35f;
    [Min(0.01f)] public float targetMoveThreshold = 0.3f;
    [Min(0.05f)] public float sampleDistance = 0.8f;
    [Min(0.5f)] public float stuckTimeout = 4f;
    public ObstacleAvoidanceType avoidance = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    public LayerMask solidMask = 9;

    public NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = areaMask };

    public NavMeshBuildSettings BuildSettings
    {
        get
        {
            var settings = NavMesh.GetSettingsByID(agentTypeId);
            settings.agentRadius = radius;
            settings.agentHeight = height;
            settings.agentClimb = stepHeight;
            settings.agentSlope = maxSlope;
            settings.overrideVoxelSize = true;
            settings.voxelSize = Mathf.Max(0.025f, radius / 4f);
            settings.minRegionArea = 0f;
            return settings;
        }
    }
}
