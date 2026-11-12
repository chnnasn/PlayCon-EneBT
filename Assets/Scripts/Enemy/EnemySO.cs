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
    [Tooltip("追击/战斗时的转向速度（越大越快，UnTetheredLerp 速率参数，建议 6~12）")] public float combatRotationTime = 8f;
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float arriveDistance = 0.2f;

    [Header("Chase Strategy (run / walk)")]
    [Tooltip("与玩家距离大于此值→跑步追击，否则→步行逼近（跑过去/走过去）")] public float chaseRunDistance = 6f;
    [Tooltip("步行逼近时的速度")] public float chaseWalkSpeed = 1.5f;
    public string runStateName = "Run";

    [Header("Pre-Attack Reposition")]
    [Tooltip("低于此距离→后撤拉开（攻击前策略后退）")] public float repositionRetreatThreshold = 1.4f;
    [Tooltip("高于此距离且低于最大接敌距离→前进逼近（攻击前策略前进）")] public float repositionAdvanceThreshold = 2.6f;
    [Tooltip("超过此距离不再做攻击前调整，由追击负责接近")] public float repositionMaxEngageDistance = 4.5f;
    [Tooltip("定位移动速度")] public float repositionSpeed = 2f;
    [Tooltip("单次定位移动的时长上限")] public float repositionDuration = 0.8f;
    [Tooltip("两次定位之间的冷却")] public float repositionCooldown = 1.2f;

    [Header("Timers")]
    public float patrolWaitTime = 1f;
    public float alertDuration = 0.75f;
    public float alertToChaseDelay = 1.2f;

    [Header("Patrol Area (fixed on Awake)")]
    public Vector2 patrolAreaSize = new Vector2(8f, 8f);

    [Header("Animator Parameters")]
    public float lockValueIdle = 1f;
    public float lockValueMove = 0f;
    public float chaseLockValue = 1f;
    public float patrolMovementValue = 1f;
    public float chaseMovementValue = 1f;
    public float retreatMovementValue = 1f;

    [Header("Retreat Response (player one-stroke)")]
    [Min(0f)] public float retreatDuration = 0.35f;
    [Min(0f)] public float retreatSpeed = 2.5f;
    [Min(0f)] public float retreatStopDistanceToPlayer = 4.5f;

    [Header("Animator States")]
    public string idleStateName = "Idle";
    public float idleCrossFade = 0.05f;
    public string moveStartStateName = "WalkStart";
    public float moveStartCrossFade = 0.1f;
    public string retreatStateName = "Retreat";
    public string breakStateName = "Hit";
    public float breakCrossFade = 0.05f;

    [Header("Break Settings")]
    public float breakDuration = 1f;
    public float breakDamageTakenMultiplier = 2f;

    [Header("Spawn")]
    public string spawnStateName = "Spawn";
    public float spawnCrossFade = 0.1f;
    [Min(0f)] public float spawnDuration = 1f;

    [Header("Combat (Bite/Claw/Roar)")]
    public string biteStateName = "Bite";
    public string clawStateName = "Claw";
    public string roarStateName = "Roar";
    public float biteCrossFade = 0.05f;
    public float clawCrossFade = 0.05f;
    public float roarCrossFade = 0.05f;
    [Min(0f)] public float biteRange = 1.5f;
    [Min(0f)] public float clawRange = 2.5f;
    [Min(0f)] public float biteCooldown = 1.2f;
    [Min(0f)] public float clawCooldown = 0.8f;
    [Min(0f)] public float biteDuration = 0.8f;
    [Min(0f)] public float clawDuration = 0.8f;
    [Min(0f)] public float roarDuration = 1f;

    [Header("Attack Gameplay")]
    [Min(0f)] public float biteDamage = 12f;
    [Min(0f)] public float clawDamage = 8f;
    [Min(0f)] public float biteWindup = 0.4f;
    [Min(0f)] public float clawWindup = 0.3f;
    [Range(1f, 180f)] public float attackAngle = 100f;
    [Min(0f)] public float attackHeightTolerance = 1.5f;
    public string playerHitStateName = "Hit";

    [Header("Investigate")]
    [Min(0f)] public float investigateSpeed = 2.5f;
    [Min(0f)] public float investigateDuration = 2f;
    [Min(0f)] public float investigateArriveDistance = 0.3f;

    [Header("Hit Reaction")]
    public string hitStateName = "Hit";
    public float hitCrossFade = 0.1f;
    [Min(0f)] public float hitDuration = 0.5f;

    [Header("Chase Give Up (leash)")]
    [Min(0f)] public float chaseGiveUpDistance = 18f;
    [Min(0f)] public float homeLeashDistance = 20f;
    [Min(0f)] public float lostSightTimeout = 2f;
    [Tooltip("导航追击时先走向最后目击点，再搜索；不会隔墙持续读取玩家位置。")]
    [Min(0.1f)] public float navigationSearchTimeout = 10f;
    [Min(0.1f)] public float movementBlockedTimeout = 1.5f;
    [Min(0f)] public float reacquireDelay = 3f;
    [Min(1f)] public float returnTimeout = 12f;

    [Header("Lair (ReturnToLair)")]
    [Min(0f)] public float lairReturnSpeed = 3f;
    [Min(0f)] public float lairArriveDistance = 0.5f;

    [Header("Dead")]
    public string deadStateName = "Dead";
    public float deadCrossFade = 0.1f;

    [Header("Debug")]
    public bool drawScanRays = true;
    [Range(3, 30)] public int scanRayCount = 7;
}

