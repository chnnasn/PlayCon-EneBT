using UnityEngine;
using Tools;

[RequireComponent(typeof(CharacterController))]
public abstract class EnemyControllerBase : ControllerBase
{
    private const int maxScanTargets = 16;
    private const float facingSnapEpsilonDegrees = 0.5f;

    [Header("Data")]
    [SerializeField] protected EnemySO enemySO;

    [Header("Runtime")]
    [SerializeField] protected Transform target;

    public bool HasTargetInSight { get; protected set; }
    public Transform Target => target;
    public EnemySO Data => enemySO;
    public ActorNavigation Navigation { get; private set; }
    public bool HasNavigation => Navigation != null && Navigation.isActiveAndEnabled && Navigation.IsConfigured;

    private readonly Collider[] scanHits = new Collider[maxScanTargets];
    private readonly RaycastHit[] sightHits = new RaycastHit[32];
    private float threatSuppressedUntil;
    public float MovementBlockedFor { get; private set; }
    public bool MovementIsBlocked => HasNavigation && Navigation.HasFailed ||
        MovementBlockedFor >= Mathf.Max(0.1f, Data != null ? Data.movementBlockedTimeout : 1.5f);

    protected override void Awake()
    {
        base.Awake();
        Navigation = GetComponent<ActorNavigation>();
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
        direction.y = 0f;
        if (!CanMove || characterController == null || !characterController.enabled || direction.sqrMagnitude < 0.0001f || speed <= 0f)
        {
            return;
        }
        var displacement = direction.normalized * speed * Time.deltaTime;
        var before = transform.position;
        characterController.Move(displacement);
        var progress = Vector3.Dot(transform.position - before, displacement.normalized);
        MovementBlockedFor = progress < displacement.magnitude * 0.1f ? MovementBlockedFor + Time.deltaTime : 0f;
    }

    public void ResetMovementProgress()
    {
        MovementBlockedFor = 0f;
        Navigation?.ResetProgress();
    }

    public void SuppressThreats(float seconds)
    {
        threatSuppressedUntil = Time.time + Mathf.Max(0f, seconds);
        target = null;
        HasTargetInSight = false;
    }

    public static bool IsValidTarget(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        var health = candidate.GetComponentInParent<CharacterHealthBase>();
        if (health == null || !health.isActiveAndEnabled || health.IsDead) return false;
        var controller = health.GetComponent<ControllerBase>();
        return controller == null || controller.isActiveAndEnabled;
    }

    public bool HasLineOfSight(Transform candidate)
    {
        if (!IsValidTarget(candidate))
        {
            return false;
        }

        // A zero mask on the existing asset means use solid scene geometry, not "see through everything".
        var mask = Data != null && Data.obstacleMask.value != 0 ? Data.obstacleMask.value : Physics.DefaultRaycastLayers;
        var eyeHeight = Mathf.Max(0.5f, Data != null ? Data.eyeHeight : 1f);
        var origin = transform.position + Vector3.up * eyeHeight;
        var destination = candidate.position + Vector3.up * eyeHeight;
        var delta = destination - origin;
        if (delta.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        var count = Physics.RaycastNonAlloc(origin, delta.normalized, sightHits, delta.magnitude, mask, QueryTriggerInteraction.Ignore);
        var targetHealth = candidate.GetComponentInParent<CharacterHealthBase>();
        for (var i = 0; i < count; i++)
        {
            var hit = sightHits[i].transform;
            if (hit == null || hit == transform || hit.IsChildOf(transform))
            {
                continue;
            }
            if (hit.GetComponentInParent<CharacterHealthBase>() == targetHealth)
            {
                continue;
            }
            return false;
        }
        return count < sightHits.Length; // A saturated buffer must not allow a hit through an unexamined wall.
    }
    

    public void FaceTargetThenMove(Vector3 worldPoint, float speed)
    {
        FaceTargetThenMove(worldPoint, speed, enemySO != null ? enemySO.rotationTime : 0.12f);
    }

    public void FaceTargetThenMove(Vector3 worldPoint, float speed, float rotationTime)
    {
        if (HasNavigation)
        {
            if (CanMove) Navigation.MoveTo(worldPoint, speed, Mathf.Min(0.2f, Data.arriveDistance));
            return;
        }
        var flatDirection = worldPoint - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var direction = flatDirection.normalized;
        RotateTowards(direction, rotationTime);
        MoveInDirection(direction, speed);
    }

    public void CrossFadeState(string stateName, float transitionDuration)
    {
        if (characterAnimator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        characterAnimator.CrossFadeInFixedTime(stateName, transitionDuration);
    }

    public void SetOffset(bool value)
    {
        if (characterAnimator != null) characterAnimator.SetBool(AnimID.OffsetID, value);
    }

    public void SetBreak(bool value)
    {
        if (characterAnimator != null) characterAnimator.SetBool(AnimID.BreakD, value);
    }

    public void SetLock(float value)
    {
        SetAnimatorFloat(AnimID.LockID, value);
    }

    public void SetMovement(float value)
    {
        SetAnimatorFloat(AnimID.MoveID, value);
    }

    public void MoveRootMotionTowards(Vector3 worldPoint, float fallbackSpeed)
    {
        FaceTargetThenMove(worldPoint, fallbackSpeed);
    }

    public void RotateTowards(Vector3 direction)
    {
        RotateTowards(direction, enemySO != null ? enemySO.rotationTime : 0.12f);
    }

    public void RotateTowards(Vector3 direction, float rotationTime)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var timer = rotationTime;
        transform.LookAt(transform.position + direction, timer);

        var flat = direction;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
        {
            var desired = Quaternion.LookRotation(flat.normalized);
            if (Quaternion.Angle(transform.rotation, desired) <= facingSnapEpsilonDegrees)
            {
                transform.rotation = desired;
            }
        }
    }

    /// <summary>面向目标并向后移动（策略性后撤/拉开距离）。</summary>
    public void RetreatFrom(Vector3 worldPoint, float speed, float rotationTime)
    {
        var flatDirection = worldPoint - transform.position;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        RotateTowards(flatDirection, rotationTime);
        MoveInDirection(-flatDirection.normalized, speed);
    }

    /// <summary>立即朝向目标方向（无插值，用于攻击/战斗）。</summary>
    public void SnapRotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        var flat = direction;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(flat.normalized);
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
        if (Time.time < threatSuppressedUntil || !CanMove)
        {
            target = null;
            return false;
        }
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

            var health = hit.GetComponentInParent<CharacterHealthBase>();
            var candidate = health != null ? health.transform : null;
            if (!IsValidTarget(candidate) || candidate == transform || candidate.GetComponent<Player>() == null)
            {
                continue;
            }
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

            var blocked = !HasLineOfSight(candidate);

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
        // Enemy locomotion is authored in metres/second by the AI and applied once through CharacterController.
        // Applying animation displacement here as well would bypass action cancellation and double movement.
    }

    protected override void OnAnimatorMove()
    {
        // Enemy steering owns both movement and facing; animation must not rotate it off the route.
    }

    private void SetAnimatorFloat(int parameterId, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            value = 0f;
        }

        if (characterAnimator != null) characterAnimator.SetFloat(parameterId, value);
    }
}
