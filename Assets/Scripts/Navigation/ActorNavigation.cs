using UnityEngine;
using UnityEngine.AI;

public enum NavigationStatus { Idle, Pending, Moving, Arrived, Paused, PartialPath, Unreachable, Stuck, OffNavMesh }

/// <summary>
/// Reusable ground navigation, independent of Enemy/FSM/BT. Refresh MoveTo each frame while
/// movement is desired. No command this frame means pause; Cancel releases all path state.
/// Agent supplies steering/avoidance, CharacterMotor exclusively executes displacement.
/// </summary>
[DisallowMultipleComponent, RequireComponent(typeof(CharacterMotor), typeof(NavMeshAgent))]
public sealed class ActorNavigation : MonoBehaviour
{
    [SerializeField] private NavigationProfile profile;
    private NavMeshAgent agent;
    private CharacterMotor motor;
    private Vector3 destination, submittedDestination, progressAnchor;
    private float speed, stoppingDistance, nextRepath, stalledFor, progressElapsed;
    private int commandFrame = -1;
    private bool submitted, configured;
    public NavigationProfile Profile => profile;
    public NavigationStatus Status { get; private set; }
    public Vector3 DesiredVelocity { get; private set; }
    public Vector3 ActualVelocity => motor != null ? motor.ActualVelocity : Vector3.zero;
    public Vector3 Destination => destination;
    public bool HasFailed => Status == NavigationStatus.Unreachable || Status == NavigationStatus.PartialPath ||
                             Status == NavigationStatus.Stuck || Status == NavigationStatus.OffNavMesh;
    public bool IsConfigured => profile != null;
    public int PathRequests { get; private set; }
    public event System.Action<Vector3> Moved;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        motor = GetComponent<CharacterMotor>();
        agent.enabled = false;
        Configure(profile);
    }

    public void Configure(NavigationProfile settings)
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (motor == null) motor = GetComponent<CharacterMotor>();
        Cancel();
        profile = settings;
        configured = false;
        if (profile == null) return;
        motor.Configure(profile);
        agent.agentTypeID = profile.agentTypeId;
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.autoTraverseOffMeshLink = false;
        agent.radius = profile.radius;
        agent.height = profile.height;
        agent.baseOffset = 0f;
        agent.areaMask = profile.areaMask;
        agent.acceleration = profile.acceleration;
        agent.obstacleAvoidanceType = profile.avoidance;
        agent.avoidancePriority = 30 + (int)((uint)GetInstanceID() % 50);
        agent.autoRepath = true;
        configured = true;
    }

    public void MoveTo(Vector3 point, float metresPerSecond, float stopDistance = 0.2f)
    {
        if (!isActiveAndEnabled || !IsFinite(point) || metresPerSecond <= 0f) return;
        if (!submitted || (destination - point).sqrMagnitude > 1f) ResetProgress();
        destination = point;
        speed = metresPerSecond;
        stoppingDistance = Mathf.Max(0.05f, stopDistance);
        commandFrame = Time.frameCount;
    }

    public bool TrySample(Vector3 point, out Vector3 sampled)
    {
        sampled = point;
        if (profile == null || !NavMesh.SamplePosition(point, out var hit, profile.sampleDistance, profile.Filter)) return false;
        sampled = hit.position;
        return true;
    }

    public bool CanReach(Vector3 point, NavMeshPath path)
    {
        return profile != null && TrySample(transform.position, out var start) && TrySample(point, out var end) &&
               NavMesh.CalculatePath(start, end, profile.Filter, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private bool Bind()
    {
        if (!configured || !motor.Controller.enabled) return false;
        if (agent.enabled && agent.isOnNavMesh) return true;
        if (!TrySample(transform.position, out var point)) return false;
        // Do not silently teleport an actor to the other side of a nearby wall.
        var lift = Vector3.up * Mathf.Min(profile.height * 0.5f, 1f);
        if (Physics.Linecast(transform.position + lift, point + lift, profile.solidMask, QueryTriggerInteraction.Ignore)) return false;
        agent.enabled = true;
        if (!agent.isOnNavMesh || !agent.Warp(point))
        {
            agent.enabled = false;
            return false;
        }
        agent.nextPosition = transform.position;
        submitted = false;
        ResetProgress();
        return true;
    }

    private void LateUpdate()
    {
        if (commandFrame != Time.frameCount)
        {
            Pause();
            return;
        }
        // Clear last frame's movement before any pending/failed/arrived early return.
        DesiredVelocity = Vector3.zero;
        motor.Stop();
        Moved?.Invoke(Vector3.zero);
        if (!Bind()) { Status = NavigationStatus.OffNavMesh; return; }
        agent.nextPosition = transform.position;
        if ((agent.nextPosition - transform.position).sqrMagnitude > profile.sampleDistance * profile.sampleDistance)
        {
            Cancel();
            Status = NavigationStatus.OffNavMesh;
            return;
        }
        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;
        agent.isStopped = false;
        if (Time.time >= nextRepath && (!submitted || !agent.pathPending))
        {
            var changed = !submitted || (destination - submittedDestination).sqrMagnitude > profile.targetMoveThreshold * profile.targetMoveThreshold;
            // Periodic refresh also responds to carving and formerly unreachable destinations.
            if (changed || !agent.hasPath || agent.isPathStale || Status != NavigationStatus.Arrived)
            {
                nextRepath = Time.time + profile.repathInterval + ((uint)GetInstanceID() % 7) * 0.007f;
                if (!TrySample(destination, out var sampled))
                {
                    agent.ResetPath();
                    Status = NavigationStatus.Unreachable;
                    return;
                }
                submittedDestination = destination;
                submitted = agent.SetDestination(sampled);
                PathRequests++;
                if (!submitted) { Status = NavigationStatus.Unreachable; return; }
            }
        }
        if (agent.pathPending) { Status = NavigationStatus.Pending; return; }
        if (!agent.hasPath)
        {
            Status = FlatDistance(transform.position, destination) <= stoppingDistance + 0.1f
                ? NavigationStatus.Arrived : NavigationStatus.Unreachable;
            return;
        }
        if (agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            Status = agent.pathStatus == NavMeshPathStatus.PathPartial ? NavigationStatus.PartialPath : NavigationStatus.Unreachable;
            agent.isStopped = true;
            return;
        }
        if (agent.remainingDistance <= stoppingDistance + 0.05f)
        {
            Status = NavigationStatus.Arrived;
            agent.isStopped = true;
            DesiredVelocity = Vector3.zero;
            ResetProgress();
            return;
        }

        DesiredVelocity = Vector3.ClampMagnitude(agent.desiredVelocity, speed);
        DesiredVelocity = new Vector3(DesiredVelocity.x, 0f, DesiredVelocity.z);
        var displacement = DesiredVelocity * Time.deltaTime;
        // The simulation setter constrains movement to connected NavMesh polygons, including
        // sliding at corners. A raycast-only clamp can deadlock on the exact polygon boundary.
        // updatePosition is false: only the motor below changes the actual Transform.
        agent.nextPosition = transform.position + displacement;
        var constrained = agent.nextPosition - transform.position;
        constrained.y = 0f;
        DesiredVelocity = Vector3.ClampMagnitude(constrained / Mathf.Max(0.0001f, Time.deltaTime), speed);
        motor.Move(DesiredVelocity, Time.deltaTime);
        motor.Face(DesiredVelocity, profile.turnSpeed, Time.deltaTime);
        Moved?.Invoke(motor.ActualVelocity);
        agent.nextPosition = transform.position;
        progressElapsed += Time.deltaTime;
        if (progressElapsed >= 0.4f)
        {
            stalledFor = FlatDistance(progressAnchor, transform.position) < 0.03f ? stalledFor + progressElapsed : 0f;
            progressAnchor = transform.position;
            progressElapsed = 0f;
        }
        Status = stalledFor >= profile.stuckTimeout ? NavigationStatus.Stuck : NavigationStatus.Moving;
    }

    public void ResetProgress()
    {
        stalledFor = progressElapsed = 0f;
        progressAnchor = transform.position;
        if (HasFailed)
        {
            Status = NavigationStatus.Idle;
            submitted = false;
            nextRepath = 0f;
        }
    }

    public void Pause()
    {
        commandFrame = -1;
        DesiredVelocity = Vector3.zero;
        motor?.Stop();
        Moved?.Invoke(Vector3.zero);
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        if (Status == NavigationStatus.Moving || Status == NavigationStatus.Pending) Status = NavigationStatus.Paused;
        ResetProgress();
    }

    public void Cancel()
    {
        commandFrame = -1;
        submitted = false;
        nextRepath = 0f;
        DesiredVelocity = Vector3.zero;
        motor?.Stop();
        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
        }
        Status = NavigationStatus.Idle;
        ResetProgress();
    }

    private void OnDisable() => Cancel();
    private static bool IsFinite(Vector3 point) => !float.IsNaN(point.sqrMagnitude) && !float.IsInfinity(point.sqrMagnitude);
    private static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
}
