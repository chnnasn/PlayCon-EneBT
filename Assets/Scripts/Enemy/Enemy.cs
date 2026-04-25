using FSM;
using UnityEngine;

public class Enemy : EnemyControllerBase
{
    [Header("Debug")]
    [SerializeField] private string currentStateName;

    public EnemyStateMachine stateMachine { get; private set; }
    public Vector3 PatrolAreaCenter { get; private set; }
    public Transform ChaseTarget { get; private set; }
    public bool HasChaseLock => ChaseTarget != null;

    protected override void Awake()
    {
        base.Awake();
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
        stateMachine.Update();
    }

    public Vector3 GetRandomPatrolPoint()
    {
        var size = Data != null ? Data.patrolAreaSize : new Vector2(8f, 8f);
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
