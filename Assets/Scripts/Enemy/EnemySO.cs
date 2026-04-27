using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/EnemySO", fileName = "EnemySO")]
public class EnemySO : ScriptableObject
{
    [Header("Perception")]
    public LayerMask playerLayerMask;
    public LayerMask obstacleMask;
    public float viewDistance = 12f;
    [Range(1f, 180f)] public float viewAngle = 90f;
    public float eyeHeight = 1.2f;

    [Header("Movement")]
    public float rotationTime = 0.12f;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float arriveDistance = 0.2f;

    [Header("Timers")]
    public float patrolWaitTime = 1f;
    public float alertDuration = 0.75f;
    public float alertToChaseDelay = 1.2f;

    [Header("Patrol Area (fixed on Awake)")]
    public Vector2 patrolAreaSize = new Vector2(8f, 8f);

    [Header("Animator Parameters")]
    public float lockValueIdle = 1f;
    public float lockValueMove = 0f;
    public float patrolMovementValue = 1f;
    public float chaseMovementValue = 1f;

    [Header("Animator States")]
    public string idleStateName = "Idle";
    public float idleCrossFade = 0.05f;
    public string moveStartStateName = "WalkStart";
    public float moveStartCrossFade = 0.1f;
    public string breakStateName = "Hit";
    public float breakCrossFade = 0.05f;

    [Header("Break Settings")]
    public float breakDuration = 1f;
    public float breakDamageTakenMultiplier = 2f;

    [Header("Debug")]
    public bool drawScanRays = true;
    [Range(3, 30)] public int scanRayCount = 7;
}

