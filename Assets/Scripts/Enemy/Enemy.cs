using FSM;
using UnityEngine;

public class Enemy : EnemyControllerBase
{
    public const string PlayerOneStrokeCommandEvent = "PlayerOneStrokeCommand";

    [Header("AI Architecture")]
    [Tooltip("使用行为树（BT）驱动敌人 AI；FSM 代码保留在 EnemyState 下，取消勾选可回退到 FSM。")]
    [SerializeField] private bool useBehaviorTree = true;

    [Header("Debug")]
    [SerializeField] private string currentStateName;
    [SerializeField] private float damageTakenMultiplier = 1f;

    private EnemyHeath health;

    public EnemyStateMachine stateMachine { get; private set; }
    public EnemyBehaviorTree behaviorTree { get; private set; }
    public Vector3 PatrolAreaCenter { get; private set; }
    public Transform ChaseTarget { get; private set; }
    public bool HasChaseLock => ChaseTarget != null;
    public float DamageTakenMultiplier => damageTakenMultiplier;
    public bool IsInBreakState => useBehaviorTree
        ? behaviorTree != null && behaviorTree.IsBreakActive
        : stateMachine != null && stateMachine.currentState.Value == stateMachine.breakState;
    public bool IsDead => health != null && health.IsDead;
    /// <summary>当前是否正在播放攻击动画（撕咬/爪击/咆哮），用于防止攻击动画中途被切换。</summary>
    public bool IsAttackAnimationPlaying
    {
        get
        {
            if (characterAnimator == null)
            {
                return false;
            }

            var info = characterAnimator.GetCurrentAnimatorStateInfo(0);
            var data = Data;
            if (data == null)
            {
                return false;
            }

            // 兼容子状态机：状态名可能带路径前缀，优先用片段名判断（片段名 = 状态名）。
            var clipInfos = characterAnimator.GetCurrentAnimatorClipInfo(0);
            if (clipInfos.Length > 0)
            {
                var clipName = clipInfos[0].clip.name;
                if (clipName == data.biteStateName || clipName == data.clawStateName || clipName == data.roarStateName)
                {
                    return true;
                }
            }

            return info.IsName(data.biteStateName) || info.IsName(data.clawStateName) || info.IsName(data.roarStateName);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        health = GetComponent<EnemyHeath>();
        PatrolAreaCenter = transform.position;
        // FSM 状态机保留创建，但默认不再启用。
        stateMachine = new EnemyStateMachine(this);
        if (useBehaviorTree)
        {
            behaviorTree = new EnemyBehaviorTree(this);
        }
    }

    protected override void Start()
    {
        base.Start();
        if (!useBehaviorTree)
        {
            stateMachine.ChangeState(stateMachine.idleState);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (useBehaviorTree)
        {
            if (behaviorTree != null)
            {
                behaviorTree.Update();
                currentStateName = behaviorTree.CurrentBehavior;
            }

            return;
        }

        stateMachine.Update();
    }

    public void OnPlayerOneStrokeCommand()
    {
        var duration = Data != null ? Data.retreatDuration : 0.35f;
        if (duration <= 0f)
        {
            return;
        }

        var player = ChaseTarget != null ? ChaseTarget : target != null ? target : FindObjectOfType<Player>() != null ? FindObjectOfType<Player>().transform : null;
        if (useBehaviorTree)
        {
            behaviorTree?.RequestRetreat(player);
            return;
        }

        if (stateMachine == null)
        {
            return;
        }

        stateMachine.retreatState.BeginRetreat(player);
    }

    public Vector3 GetRandomPatrolPoint()
    {
        var size = Data != null ? Data.patrolAreaSize : new Vector2(8f, 8f);
        var halfX = size.x * 0.5f;
        var halfZ = size.y * 0.5f;
        var currentPos = transform.position;
        var backward = -transform.forward;
        backward.y = 0f;
        if (backward.sqrMagnitude > 0.0001f)
        {
            backward.Normalize();
        }

        var bestPoint = PatrolAreaCenter;
        var bestScore = float.NegativeInfinity;
        const int sampleCount = 8;
        for (var i = 0; i < sampleCount; i++)
        {
            var randomX = Random.Range(-halfX, halfX);
            var randomZ = Random.Range(-halfZ, halfZ);
            var candidate = new Vector3(PatrolAreaCenter.x + randomX, PatrolAreaCenter.y, PatrolAreaCenter.z + randomZ);

            var toCandidate = candidate - currentPos;
            toCandidate.y = 0f;
            var distanceScore = toCandidate.sqrMagnitude;
            var directionScore = 0f;
            if (toCandidate.sqrMagnitude > 0.0001f && backward.sqrMagnitude > 0.0001f)
            {
                directionScore = Vector3.Dot(toCandidate.normalized, backward);
            }

            // 优先远距离，同时偏向“当前前方反方向”。
            var score = distanceScore + directionScore * 6f;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestPoint = candidate;
        }

        return bestPoint;
    }

    public void BeginChaseLock()
    {
        BeginChaseLock(target);
    }

    public void BeginChaseLock(Transform chaseTarget)
    {
        target = chaseTarget;
        ChaseTarget = chaseTarget;
    }

    public void EndChaseLock()
    {
        ChaseTarget = null;
    }

    public void SetDamageTakenMultiplier(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            damageTakenMultiplier = 1f;
            return;
        }

        damageTakenMultiplier = Mathf.Max(0f, value);
    }

    /// <summary>受击通知（由 EnemyHeath 转发），行为树进入 Hit 状态。</summary>
    public void NotifyHit(Transform attacker)
    {
        if (useBehaviorTree)
        {
            behaviorTree?.RequestHit(attacker);
            return;
        }

        // FSM 回退路径：保持原逻辑，受击动画退出事件会触发追击。
    }

    public void EnterBreakState()
    {
        if (useBehaviorTree)
        {
            behaviorTree?.RequestBreak();
            return;
        }

        if (stateMachine == null)
        {
            return;
        }

        stateMachine.ChangeState(stateMachine.breakState);
    }

    private void OnEnable()
    {
        if (!useBehaviorTree && stateMachine != null)
        {
            stateMachine.currentState.OnValueChanged += HandleStateChanged;
        }

        if (GameEventsManager.MainInstance != null)
        {
            GameEventsManager.MainInstance.AddEventListening<Transform>(PlayerOneStrokeCommandEvent, OnPlayerOneStrokeCommandEvent);
        }
    }
    
    private void OnDisable()
    {
        if (!useBehaviorTree && stateMachine != null)
        {
            stateMachine.currentState.OnValueChanged -= HandleStateChanged;
        }

        if (GameEventsManager.MainInstance != null)
        {
            GameEventsManager.MainInstance.ReMoveEvent<Transform>(PlayerOneStrokeCommandEvent, OnPlayerOneStrokeCommandEvent);
        }
    }

    private void OnPlayerOneStrokeCommandEvent(Transform enemyTransform)
    {
        if (enemyTransform == null)
        {
            return;
        }

        if (enemyTransform == transform)
        {
            OnPlayerOneStrokeCommand();
        }
    }


    private void HandleStateChanged(IState state)
    {
        var stateName = state == null ? "None" : state.GetType().Name;
        if (currentStateName == stateName)
        {
            return;
        }

        currentStateName = stateName;
        Debug.Log($"[EnemyFSM] frame={Time.frameCount} time={Time.time:F2} {name} -> {stateName}", this);
    }
    
    public void OnAnimationExitEvent(AnimationState playerState)
    {
        if (useBehaviorTree)
        {
            behaviorTree?.OnAnimationExitEvent(playerState);
            return;
        }

        switch (playerState)
        {
            case AnimationState.Idle:
                stateMachine.ChangeState(stateMachine.idleState);
                break;
            case AnimationState.Hit:
                if (stateMachine.currentState.Value != stateMachine.breakState)
                {
                    var player = FindObjectOfType<Player>();
                    if (player == null)
                    {
                        break;
                    }

                    target = player.transform;
                    BeginChaseLock();
                    stateMachine.ChangeState(stateMachine.chaseState);
                }
                break;
        }
    }
    

    private void OnDrawGizmosSelected()
    {
        if (Data == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.85f);
        var center = Application.isPlaying ? PatrolAreaCenter : transform.position;
        var size = new Vector3(Data.patrolAreaSize.x, 0.05f, Data.patrolAreaSize.y);
        Gizmos.DrawWireCube(center, size);

        var origin = transform.position + Vector3.up * Data.eyeHeight;
        var halfFov = Data.viewAngle * 0.5f;
        var leftBound = Quaternion.Euler(0f, -halfFov, 0f) * transform.forward;
        var rightBound = Quaternion.Euler(0f, halfFov, 0f) * transform.forward;

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(origin, origin + leftBound * Data.viewDistance);
        Gizmos.DrawLine(origin, origin + rightBound * Data.viewDistance);
        Gizmos.DrawWireSphere(origin, Data.viewDistance);

        var rays = Mathf.Max(3, Data.scanRayCount);
        for (var i = 0; i < rays; i++)
        {
            var t = rays == 1 ? 0f : (float)i / (rays - 1);
            var angle = Mathf.Lerp(-halfFov, halfFov, t);
            var dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            Gizmos.DrawRay(origin, dir * Data.viewDistance);
        }

        if (Application.isPlaying && (target != null || ChaseTarget != null))
        {
            Gizmos.color = HasTargetInSight ? Color.green : Color.yellow;
            var debugTarget = target != null ? target : ChaseTarget;
            Gizmos.DrawLine(origin, debugTarget.position + Vector3.up * Data.eyeHeight);
        }
    }
}
