using FSM;
using UnityEngine;
using Tools;

[RequireComponent(typeof(CharacterController))]
public class Enemy : ControllerBase
{
    private const int MaxScanTargets = 16;
    private const float FacingEpsilonDegrees = 0.5f;

    [Header("Debug")]
    [SerializeField] private string currentStateName;
    
    [Header("Data")]
    [SerializeField] private EnemySO enemySO;

    [Header("Runtime")]
    [SerializeField] private Transform target;

    public EnemyStateMachine stateMachine { get; private set; }
    public bool HasTargetInSight { get; private set; }
    public Vector3 PatrolAreaCenter { get; private set; }
    public Transform ChaseTarget { get; private set; }
    public bool HasChaseLock => ChaseTarget != null;

    public Transform Target => target;
    public EnemySO Data => enemySO;
    private readonly Collider[] scanHits = new Collider[MaxScanTargets];
    private float lastRootMotionSqr;

    protected override void Awake()
    {
        base.Awake();
        if (enemySO == null)
        {
            Debug.LogError($"{nameof(Enemy)} missing {nameof(EnemySO)} reference.", this);
        }
        PatrolAreaCenter = transform.position;
        stateMachine = new EnemyStateMachine(this);
    }

    protected override void Start()
    {
        base.Start();
        stateMachine.ChangeState(stateMachine.idleState);
    }

    protected override void Update()
    {
        base.Update();
        UpdatePerception();
        stateMachine.Update();
    }

    public void MoveTowards(Vector3 worldPoint, float speed)
    {
        var flatDirection = worldPoint - transform.position;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var direction = flatDirection.normalized;
        characterController.Move(direction * speed * Time.deltaTime);
        RotateTowards(direction);
    }

    public void MoveInDirection(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        characterController.Move(direction.normalized * speed * Time.deltaTime);
    }

    public void CrossFadeState(string stateName, float transitionDuration)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        characterAnimator.CrossFadeInFixedTime(stateName, transitionDuration);
    }

    public void SetOffset(bool value)
    {
        characterAnimator.SetBool(AnimID.OffsetID, value);
    }

    public void SetLock(float value)
    {
        SetAnimatorFloatSafe(AnimID.LockID, value);
    }

    public void SetMovement(float value)
    {
        SetAnimatorFloatSafe(AnimID.MovementID, value);
    }

    public void MoveRootMotionTowards(Vector3 worldPoint, float fallbackSpeed)
    {
        var flatDirection = worldPoint - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var direction = flatDirection.normalized;
        RotateTowards(direction);

        if (lastRootMotionSqr <= 0.000001f)
        {
            characterController.Move(direction * fallbackSpeed * Time.deltaTime);
        }
    }

    public void RotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // Prefer project-provided rotation helper (see `Animator/ExpandAnimator.cs`)
        // `rotationTime` here acts as the smoothing "timer" parameter used by UnTetheredLerp.
        var timer = enemySO != null ? enemySO.rotationTime : 0.12f;
        transform.LookAt(transform.position + direction, timer);

        // Slerp can asymptotically approach; snap when close enough so "face-then-move" completes.
        var flat = direction;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
        {
            var desired = Quaternion.LookRotation(flat.normalized);
            if (Quaternion.Angle(transform.rotation, desired) <= FacingEpsilonDegrees)
            {
                transform.rotation = desired;
            }
        }
    }

    public bool HasReached(Vector3 worldPoint, float arriveThreshold = -1f)
    {
        var threshold = arriveThreshold > 0f ? arriveThreshold : (enemySO != null ? enemySO.arriveDistance : 0.2f);
        var flatDelta = worldPoint - transform.position;
        flatDelta.y = 0f;
        return flatDelta.sqrMagnitude <= threshold * threshold;
    }

    public Vector3 GetRandomPatrolPoint()
    {
        var size = enemySO != null ? enemySO.patrolAreaSize : new Vector2(8f, 8f);
        var randomX = Random.Range(-size.x * 0.5f, size.x * 0.5f);
        var randomZ = Random.Range(-size.y * 0.5f, size.y * 0.5f);
        return new Vector3(PatrolAreaCenter.x + randomX, PatrolAreaCenter.y, PatrolAreaCenter.z + randomZ);
    }

    public void BeginChaseLock()
    {
        ChaseTarget = target;
    }

    public void EndChaseLock()
    {
        ChaseTarget = null;
    }

    private void UpdatePerception()
    {
        HasTargetInSight = CanSeeTarget();
        if (!HasTargetInSight)
        {
            return;
        }
    }

    private bool CanSeeTarget()
    {
        var eye = enemySO != null ? enemySO.eyeHeight : 1.2f;
        var origin = transform.position + Vector3.up * eye;
        var hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            enemySO != null ? enemySO.viewDistance : 12f,
            scanHits,
            enemySO != null ? enemySO.playerLayerMask : default,
            QueryTriggerInteraction.Collide);

        var hasVisibleTarget = false;
        var bestDistance = float.MaxValue;
        Transform bestTarget = null;

        for (var i = 0; i < hitCount; i++)
        {
            var hit = scanHits[i];
            if (hit == null)
            {
                continue;
            }

            var candidate = hit.transform;
            var candidatePos = candidate.position + Vector3.up * eye;
            var toCandidate = candidatePos - origin;
            var distance = toCandidate.magnitude;

            var viewDist = enemySO != null ? enemySO.viewDistance : 12f;
            if (distance <= 0.001f || distance > viewDist)
            {
                continue;
            }

            var viewAng = enemySO != null ? enemySO.viewAngle : 90f;
            if (Vector3.Angle(transform.forward, toCandidate) > viewAng * 0.5f)
            {
                continue;
            }

            var blocked = Physics.Raycast(
                origin,
                toCandidate.normalized,
                distance,
                enemySO != null ? enemySO.obstacleMask : default,
                QueryTriggerInteraction.Ignore);

            if (enemySO != null && enemySO.drawScanRays)
            {
                Debug.DrawRay(origin, toCandidate.normalized * distance, blocked ? Color.red : Color.green);
            }

            if (blocked || distance >= bestDistance)
            {
                continue;
            }

            hasVisibleTarget = true;
            bestDistance = distance;
            bestTarget = candidate;
        }

        target = bestTarget;
        return hasVisibleTarget;
    }

    private void OnEnable()
    {
        if (stateMachine == null)
        {
            return;
        }

        stateMachine.currentState.OnValueChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (stateMachine == null)
        {
            return;
        }

        stateMachine.currentState.OnValueChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(IState state)
    {
        currentStateName = state == null ? "None" : state.GetType().Name;
    }

    protected override void UpdateCharacterVelocity(Vector3 movement)
    {
        var dir = ResetVelocityOnSlop(movement);
        lastRootMotionSqr = dir.sqrMagnitude;
        if (characterAnimator.AnimationAtTag("Movement"))
        {
            // `movement` comes from animator.deltaPosition, already frame-based displacement.
            // Multiplying deltaTime again will almost freeze movement.
            characterController.Move(dir);
        }
        else
        {
            lastRootMotionSqr = 0f;
        }
    }

    protected override void OnAnimatorMove()
    {
        // Root motion is always active; states only decide animation/rotation.
        base.OnAnimatorMove();
    }

    private void SetAnimatorFloatSafe(int parameterId, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            value = 0f;
        }

        characterAnimator.SetFloat(parameterId, value);
    }

    private void OnDrawGizmosSelected()
    {
        if (enemySO == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.85f);
        var center = Application.isPlaying ? PatrolAreaCenter : transform.position;
        var size = new Vector3(enemySO.patrolAreaSize.x, 0.05f, enemySO.patrolAreaSize.y);
        Gizmos.DrawWireCube(center, size);

        var origin = transform.position + Vector3.up * enemySO.eyeHeight;
        var halfFov = enemySO.viewAngle * 0.5f;
        var leftBound = Quaternion.Euler(0f, -halfFov, 0f) * transform.forward;
        var rightBound = Quaternion.Euler(0f, halfFov, 0f) * transform.forward;

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(origin, origin + leftBound * enemySO.viewDistance);
        Gizmos.DrawLine(origin, origin + rightBound * enemySO.viewDistance);
        Gizmos.DrawWireSphere(origin, enemySO.viewDistance);

        var rays = Mathf.Max(3, enemySO.scanRayCount);
        for (var i = 0; i < rays; i++)
        {
            var t = rays == 1 ? 0f : (float)i / (rays - 1);
            var angle = Mathf.Lerp(-halfFov, halfFov, t);
            var dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            Gizmos.DrawRay(origin, dir * enemySO.viewDistance);
        }

        if (Application.isPlaying && (target != null || ChaseTarget != null))
        {
            Gizmos.color = HasTargetInSight ? Color.green : Color.yellow;
            var debugTarget = target != null ? target : ChaseTarget;
            Gizmos.DrawLine(origin, debugTarget.position + Vector3.up * enemySO.eyeHeight);
        }
    }
}
