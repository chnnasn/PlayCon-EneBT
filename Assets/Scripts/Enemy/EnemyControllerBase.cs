using UnityEngine;
using Tools;

[RequireComponent(typeof(CharacterController))]
public abstract class EnemyControllerBase : ControllerBase
{
    private const int MaxScanTargets = 16;
    private const float FacingSnapEpsilonDegrees = 0.5f;

    [Header("Data")]
    [SerializeField] protected EnemySO enemySO;

    [Header("Runtime")]
    [SerializeField] protected Transform target;

    public bool HasTargetInSight { get; protected set; }
    public Transform Target => target;
    public EnemySO Data => enemySO;

    private readonly Collider[] scanHits = new Collider[MaxScanTargets];
    private float lastRootMotionSqr;

    protected override void Awake()
    {
        base.Awake();
        if (enemySO == null)
        {
            Debug.LogError($"{GetType().Name} missing {nameof(EnemySO)} reference.", this);
        }
    }

    protected override void Update()
    {
        base.Update();
        HasTargetInSight = ScanForThreat();
    }

    public void MoveInDirection(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        characterController.Move(direction.normalized * speed * Time.deltaTime);
    }

    public void MoveToTarget(Vector3 worldPoint, float speed)
    {
        var flatDirection = worldPoint - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        MoveInDirection(flatDirection.normalized, speed);
    }

    public void FaceTargetThenMove(Vector3 worldPoint, float speed)
    {
        var flatDirection = worldPoint - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var direction = flatDirection.normalized;
        RotateTowards(direction);
        MoveInDirection(direction, speed);
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
        SetAnimatorFloatSafe(AnimID.MoveID, value);
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

        var timer = enemySO != null ? enemySO.rotationTime : 0.12f;
        transform.LookAt(transform.position + direction, timer);

        var flat = direction;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
        {
            var desired = Quaternion.LookRotation(flat.normalized);
            if (Quaternion.Angle(transform.rotation, desired) <= FacingSnapEpsilonDegrees)
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

    public bool ScanForThreat()
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

    protected override void UpdateCharacterVelocity(Vector3 movement)
    {
        var dir = ResetVelocityOnSlop(movement);
        lastRootMotionSqr = dir.sqrMagnitude;
        if (characterAnimator.AnimationAtTag("Movement"))
        {
            characterController.Move(dir);
        }
        else
        {
            lastRootMotionSqr = 0f;
        }
    }

    protected override void OnAnimatorMove()
    {
        // Root motion is always active; state classes decide behavior.
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
}
