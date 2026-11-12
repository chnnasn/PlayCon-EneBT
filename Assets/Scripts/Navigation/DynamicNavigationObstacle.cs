using UnityEngine;
using UnityEngine.AI;

/// <summary>Moving boxes use local avoidance; stationary boxes carve paths. Call SetBlocking for doors.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider), typeof(NavMeshObstacle))]
public sealed class DynamicNavigationObstacle : MonoBehaviour
{
    [Min(0f)] public float stationaryDelay = 0.15f;
    [Min(0.01f)] public float movementThreshold = 0.1f;
    private BoxCollider solid;
    private NavMeshObstacle obstacle;
    public bool IsBlocking => solid != null && solid.enabled;

    private void Awake() => Configure();
    public void Configure()
    {
        solid = GetComponent<BoxCollider>();
        obstacle = GetComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = solid.center;
        obstacle.size = solid.size;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.carvingMoveThreshold = movementThreshold;
        obstacle.carvingTimeToStationary = stationaryDelay;
        solid.isTrigger = false;
    }
    public void SetBlocking(bool blocking)
    {
        if (solid == null) Configure();
        solid.enabled = blocking;
        obstacle.enabled = blocking && isActiveAndEnabled;
    }
    private void OnEnable() { Configure(); obstacle.enabled = solid.enabled; }
    private void OnDisable() { if (obstacle != null) obstacle.enabled = false; }
}
