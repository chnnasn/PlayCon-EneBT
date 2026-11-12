using FSM;
using System.Collections.Generic;
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

    private CharacterHealthBase health;
    private readonly List<AnimatorClipInfo> attackClipInfo = new List<AnimatorClipInfo>(4);
    private EnemyAttackController attacks;
    private float lostSightTime;
    private bool engaged;
    private bool activeUseBehaviorTree;
    private GameEventsManager subscribedEvents;
    private CombatPositionRing combatPositions;
    private Vector3 lastKnownTargetPosition;
    private float nextPositionRequest;
    private bool hasCombatPosition;
    private Vector3 combatPosition;
    public bool CanOccupyAttackPosition => !HasNavigation || combatPositions == null || !combatPositions.isActiveAndEnabled ||
        (TryCombatPosition(out var point) && Vector3.Distance(transform.position, point) <= 0.5f);
    public Vector3 PursuitPosition => HasNavigation && lostSightTime > 0f ? lastKnownTargetPosition : ChaseTarget.position;

    public EnemyStateMachine stateMachine { get; private set; }
    public EnemyBehaviorTree behaviorTree { get; private set; }
    public Vector3 PatrolAreaCenter { get; private set; }
    public Transform ChaseTarget { get; private set; }
    public bool HasChaseLock => IsValidTarget(ChaseTarget);
    public bool IsAttacking => attacks != null && attacks.IsActive;
    public int AttackVersion => attacks != null ? attacks.Version : 0;
    public bool IsReturningToLair { get; private set; }
    public float DamageTakenMultiplier => damageTakenMultiplier;
    public bool IsInBreakState => useBehaviorTree
        ? behaviorTree != null && behaviorTree.IsBreakActive
        : stateMachine != null && stateMachine.currentState.Value == stateMachine.breakState;
    public bool IsHitReactionDeferred => useBehaviorTree
        ? behaviorTree != null && behaviorTree.IsHitReactionDeferred
        : stateMachine != null && stateMachine.IsHitReactionDeferred;
    public bool IsDead => health != null && health.IsDead;
    protected override bool CanMove => base.CanMove && !IsDead;
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
            characterAnimator.GetCurrentAnimatorClipInfo(0, attackClipInfo);
            if (attackClipInfo.Count > 0)
            {
                var clipName = attackClipInfo[0].clip.name;
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
        health = GetComponent<CharacterHealthBase>();
        PatrolAreaCenter = transform.position;
        attacks = new EnemyAttackController(this);
        stateMachine = new EnemyStateMachine(this);
        behaviorTree = new EnemyBehaviorTree(this);
        activeUseBehaviorTree = useBehaviorTree;
    }

    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();

        if (Data == null) return;
        if (activeUseBehaviorTree != useBehaviorTree)
        {
            ResetAIForRevive();
        }

        if (IsDead)
        {
            CancelAttack();
            EndChaseLock();
            if (!useBehaviorTree && stateMachine.currentState.Value != stateMachine.deadState)
                stateMachine.ChangeState(stateMachine.deadState);
        }
        else if (engaged)
        {
            if (!HasChaseLock)
            {
                RequestReturnToLair();
            }
            else
            {
                if (HasLineOfSight(ChaseTarget))
                {
                    lostSightTime = 0f;
                    lastKnownTargetPosition = ChaseTarget.position;
                }
                else lostSightTime += Time.deltaTime;
                var delta = ChaseTarget.position - transform.position;
                var fromHome = transform.position - PatrolAreaCenter;
                delta.y = fromHome.y = 0f;
                if ((Data.chaseGiveUpDistance > 0f && delta.sqrMagnitude > Data.chaseGiveUpDistance * Data.chaseGiveUpDistance) ||
                    (Data.homeLeashDistance > 0f && fromHome.sqrMagnitude > Data.homeLeashDistance * Data.homeLeashDistance) ||
                    lostSightTime > Mathf.Max(0.1f, HasNavigation ? Data.navigationSearchTimeout : Data.lostSightTimeout))
                    RequestReturnToLair();
            }
        }

        if (useBehaviorTree)
        {
            if (behaviorTree != null)
            {
                behaviorTree.Update();
                currentStateName = behaviorTree.CurrentBehavior;
            }

        }
        else stateMachine.Update();
        attacks.Tick();
    }

    public void OnPlayerOneStrokeCommand()
    {
        if (IsDead || !isActiveAndEnabled) return;
        CancelAttack();
        Navigation?.Cancel();
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

        stateMachine.RequestRetreat(player);
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
        if (IsDead || !IsValidTarget(chaseTarget) || IsReturningToLair) return;
        var targetHealth = chaseTarget.GetComponentInParent<CharacterHealthBase>();
        chaseTarget = targetHealth.transform;
        if (ChaseTarget != chaseTarget)
        {
            combatPositions?.Release(Navigation);
            combatPositions = chaseTarget.GetComponent<CombatPositionRing>();
            hasCombatPosition = false;
            nextPositionRequest = 0f;
            lostSightTime = 0f;
            lastKnownTargetPosition = chaseTarget.position;
        }
        target = chaseTarget;
        ChaseTarget = chaseTarget;
        engaged = true;
    }

    public void EndChaseLock()
    {
        combatPositions?.Release(Navigation);
        combatPositions = null;
        hasCombatPosition = false;
        ChaseTarget = null;
        engaged = false;
        lostSightTime = 0f;
    }

    private bool TryCombatPosition(out Vector3 position)
    {
        position = combatPosition;
        if (combatPositions == null || !HasNavigation || !combatPositions.isActiveAndEnabled) return false;
        if (Time.time >= nextPositionRequest)
        {
            nextPositionRequest = Time.time + 0.25f;
            var radius = Mathf.Min(Data.biteRange, Data.clawRange) * 0.85f;
            hasCombatPosition = combatPositions.TryReserve(Navigation, radius, out combatPosition);
        }
        position = combatPosition;
        return hasCombatPosition;
    }

    public void MoveInPursuit(float speed)
    {
        if (!HasChaseLock) return;
        var point = PursuitPosition;
        if (HasNavigation)
        {
            var stopDistance = 0.2f;
            if (lostSightTime == 0f && combatPositions != null && combatPositions.isActiveAndEnabled)
            {
                if (TryCombatPosition(out var reserved)) point = reserved;
                else point = combatPositions.WaitingPosition(Navigation, Mathf.Min(Data.biteRange, Data.clawRange));
            }
            else if (lostSightTime == 0f) stopDistance = Mathf.Min(Data.biteRange, Data.clawRange) * 0.85f;
            Navigation.MoveTo(point, speed, stopDistance);
            if (Vector3.Distance(transform.position, point) <= stopDistance + 0.1f)
                RotateTowards(ChaseTarget.position - transform.position, Data.combatRotationTime);
            return;
        }
        var direction = point - transform.position;
        direction.y = 0f;
        RotateTowards(direction, Data.combatRotationTime);
        if (direction.magnitude > Mathf.Max(0.2f, Mathf.Min(Data.biteRange, Data.clawRange) * 0.9f))
            MoveInDirection(direction, speed);
        else ResetMovementProgress();
    }

    private void OnNavigationMoved(Vector3 velocity)
    {
        if (isActiveAndEnabled && !IsDead && Data != null)
            SetMovement(velocity.magnitude > 0.05f ? Data.chaseMovementValue : 0f);
    }

    public bool CanStartAttack(EnemyAttackKind kind) => attacks != null && attacks.CanStart(kind);
    public bool TryStartAttack(EnemyAttackKind kind) => attacks != null && attacks.TryStart(kind);
    public void CancelAttack() => attacks?.Cancel();
    public void CancelAttack(int version)
    {
        if (attacks != null && attacks.Version == version) attacks.Cancel();
    }

    public void RequestReturnToLair()
    {
        if (IsDead || IsReturningToLair) return;
        CancelAttack();
        Navigation?.Cancel();
        EndChaseLock();
        IsReturningToLair = true;
        SuppressThreats(float.PositiveInfinity);
        ResetMovementProgress();
        if (useBehaviorTree) behaviorTree.RequestReturnToLair();
        else if (!stateMachine.IsInPriorityReaction) stateMachine.ChangeState(stateMachine.returnState);
    }

    public void FinishReturnToLair()
    {
        IsReturningToLair = false;
        EndChaseLock();
        SuppressThreats(Data != null ? Data.reacquireDelay : 3f);
        ResetMovementProgress();
        SetOffset(false);
        SetMovement(0f);
    }

    public void ResetAIForRevive()
    {
        if (stateMachine == null || behaviorTree == null) return;
        stateMachine.Stop();
        stateMachine.HasRoared = false;
        stateMachine.ClearPendingReactions();
        behaviorTree.Reset();
        attacks.Reset();
        Navigation?.Cancel();
        EndChaseLock();
        IsReturningToLair = false;
        SuppressThreats(0f);
        ResetMovementProgress();
        SetDamageTakenMultiplier(1f);
        SetBreak(false);
        SetOffset(false);
        SetMovement(0f);
        activeUseBehaviorTree = useBehaviorTree;
        if (!useBehaviorTree && isActiveAndEnabled && Data != null)
            stateMachine.Start(IsDead ? (IState)stateMachine.deadState : stateMachine.spawnState);
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
        if (IsDead || !isActiveAndEnabled || IsInBreakState) return;
        CancelAttack();
        Navigation?.Cancel();
        BeginChaseLock(attacker);
        if (useBehaviorTree)
        {
            behaviorTree?.RequestHit(attacker);
            return;
        }

        stateMachine.RequestHit();
    }

    public void EnterBreakState()
    {
        if (IsDead || !isActiveAndEnabled) return;
        CancelAttack();
        Navigation?.Cancel();
        if (useBehaviorTree)
        {
            behaviorTree?.RequestBreak();
            return;
        }

        if (stateMachine == null)
        {
            return;
        }

        stateMachine.RequestBreak();
    }

    private void OnEnable()
    {
        if (health != null) health.Died += OnHealthDied;
        if (Navigation != null) Navigation.Moved += OnNavigationMoved;
        if (stateMachine != null)
        {
            stateMachine.currentState.OnValueChanged += HandleStateChanged;
        }
        ResetAIForRevive();

        subscribedEvents = GameEventsManager.MainInstance;
        if (subscribedEvents != null)
        {
            subscribedEvents.AddEventListening<Transform>(PlayerOneStrokeCommandEvent, OnPlayerOneStrokeCommandEvent);
        }
    }
    
    private void OnDisable()
    {
        if (health != null) health.Died -= OnHealthDied;
        if (Navigation != null)
        {
            Navigation.Moved -= OnNavigationMoved;
            Navigation.Cancel();
        }
        if (stateMachine != null)
        {
            stateMachine.currentState.OnValueChanged -= HandleStateChanged;
        }
        CancelAttack();
        behaviorTree?.Reset();
        stateMachine?.Stop();
        stateMachine?.ClearPendingReactions();
        EndChaseLock();

        if (subscribedEvents != null)
        {
            subscribedEvents.ReMoveEvent<Transform>(PlayerOneStrokeCommandEvent, OnPlayerOneStrokeCommandEvent);
        }
        subscribedEvents = null;
    }

    private void OnHealthDied()
    {
        // Damage can arrive after Update has already submitted movement for this frame.
        Navigation?.Cancel();
        CancelAttack();
        EndChaseLock();
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
        if (!isActiveAndEnabled || IsDead) return;
        if (useBehaviorTree)
        {
            behaviorTree?.OnAnimationExitEvent(playerState);
            return;
        }

        switch (playerState)
        {
            case AnimationState.Idle:
                if (stateMachine.currentState.Value == stateMachine.patrolState)
                    stateMachine.patrolState.OnAnimationExitEvent();
                break;
            case AnimationState.Hit:
                if (stateMachine.currentState.Value == stateMachine.hitState)
                    stateMachine.hitState.OnAnimationExitEvent();
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
