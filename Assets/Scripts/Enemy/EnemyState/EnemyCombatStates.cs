using UnityEngine;

public sealed class EnemySpawnState : EnemyState
{
    private float remaining;
    public EnemySpawnState(EnemyStateMachine machine) : base(machine) { }
    public override void Enter()
    {
        remaining = Mathf.Max(0f, enemy.Data.spawnDuration);
        enemy.SetMovement(0f);
        enemy.SetOffset(false);
        enemy.CrossFadeState(enemy.Data.spawnStateName, enemy.Data.spawnCrossFade);
    }
    public override void Update()
    {
        remaining -= Time.deltaTime;
        if (remaining <= 0f) stateMachine.ResumeAfterSpawn();
    }
}

public sealed class EnemyRoarState : EnemyState
{
    private float remaining;
    public EnemyRoarState(EnemyStateMachine machine) : base(machine) { }
    public override void Enter()
    {
        stateMachine.HasRoared = true;
        remaining = Mathf.Max(0f, enemy.Data.roarDuration);
        enemy.SetMovement(0f);
        enemy.SetOffset(false);
        enemy.CrossFadeState(enemy.Data.roarStateName, enemy.Data.roarCrossFade);
    }
    public override void Update()
    {
        if (!enemy.HasChaseLock)
        {
            stateMachine.ResumeCombatOrReturn();
            return;
        }
        var direction = enemy.ChaseTarget.position - enemy.transform.position;
        direction.y = 0f;
        enemy.RotateTowards(direction, enemy.Data.combatRotationTime);
        remaining -= Time.deltaTime;
        if (remaining <= 0f) stateMachine.ChangeState(stateMachine.chaseState);
    }
}

public sealed class EnemyAttackState : EnemyState
{
    private bool started;
    private int attackVersion;
    public EnemyAttackState(EnemyStateMachine machine) : base(machine) { }
    public override void Enter()
    {
        started = enemy.TryStartAttack(EnemyAttackKind.Bite) || enemy.TryStartAttack(EnemyAttackKind.Claw);
        attackVersion = enemy.AttackVersion;
    }
    public override void Update()
    {
        if (!started || !enemy.IsAttacking || enemy.AttackVersion != attackVersion)
            stateMachine.ResumeCombatOrReturn();
    }
    public override void Exit()
    {
        if (started) enemy.CancelAttack(attackVersion);
        started = false;
    }
}

public sealed class EnemyHitState : EnemyState
{
    private float remaining;
    public EnemyHitState(EnemyStateMachine machine) : base(machine) { }
    public override void Enter()
    {
        enemy.CancelAttack();
        remaining = Mathf.Max(0f, enemy.Data.hitDuration);
        enemy.SetMovement(0f);
        enemy.SetOffset(false);
        enemy.CrossFadeState(enemy.Data.hitStateName, enemy.Data.hitCrossFade);
    }
    public override void Update()
    {
        remaining -= Time.deltaTime;
        if (remaining <= 0f) stateMachine.ResumeCombatOrReturn();
    }
    public override void OnAnimationExitEvent() => remaining = 0f;
}

public sealed class EnemyDeadState : EnemyState
{
    public EnemyDeadState(EnemyStateMachine machine) : base(machine) { }
    public override void Enter()
    {
        enemy.CancelAttack();
        enemy.EndChaseLock();
        enemy.SetOffset(false);
        enemy.SetMovement(0f);
        enemy.SetBreak(false);
        enemy.SetDamageTakenMultiplier(1f);
        enemy.CrossFadeState(enemy.Data.deadStateName, enemy.Data.deadCrossFade);
    }
}

public sealed class EnemyReturnToLairState : EnemyState
{
    private float elapsed;
    public EnemyReturnToLairState(EnemyStateMachine machine) : base(machine) { }
    public override void Enter()
    {
        elapsed = 0f;
        stateMachine.HasRoared = false;
        enemy.EndChaseLock();
        enemy.ResetMovementProgress();
        enemy.SetOffset(true);
        enemy.SetLock(enemy.Data.lockValueMove);
        enemy.SetMovement(enemy.Data.patrolMovementValue);
        enemy.CrossFadeState(enemy.Data.moveStartStateName, enemy.Data.moveStartCrossFade);
    }
    public override void Update()
    {
        elapsed += Time.deltaTime;
        if (enemy.HasReached(enemy.PatrolAreaCenter, enemy.Data.lairArriveDistance) ||
            enemy.MovementIsBlocked || elapsed >= Mathf.Max(1f, enemy.Data.returnTimeout))
        {
            enemy.FinishReturnToLair();
            stateMachine.ChangeState(stateMachine.idleState);
            return;
        }
        enemy.FaceTargetThenMove(enemy.PatrolAreaCenter, enemy.Data.lairReturnSpeed);
    }
}

public sealed class EnemyInvestigateState : EnemyState
{
    private Vector3 point;
    private float lookTime;
    private float elapsed;
    private bool arrived;
    public EnemyInvestigateState(EnemyStateMachine machine) : base(machine) { }
    public void SetPoint(Vector3 position) => point = position;
    public override void Enter()
    {
        arrived = false;
        lookTime = elapsed = 0f;
        enemy.ResetMovementProgress();
        enemy.SetOffset(true);
        enemy.SetMovement(enemy.Data.patrolMovementValue);
        enemy.CrossFadeState(enemy.Data.moveStartStateName, enemy.Data.moveStartCrossFade);
    }
    public override void Update()
    {
        if (enemy.HasTargetInSight)
        {
            stateMachine.ChangeState(stateMachine.alertState);
            return;
        }
        elapsed += Time.deltaTime;
        if (enemy.MovementIsBlocked || elapsed >= Mathf.Max(1f, enemy.Data.returnTimeout))
        {
            enemy.RequestReturnToLair();
            return;
        }
        if (!arrived)
        {
            enemy.FaceTargetThenMove(point, enemy.Data.investigateSpeed);
            if (enemy.HasReached(point, enemy.Data.investigateArriveDistance))
            {
                arrived = true;
                enemy.SetOffset(false);
                enemy.SetMovement(0f);
                enemy.CrossFadeState(enemy.Data.idleStateName, enemy.Data.idleCrossFade);
            }
            return;
        }
        lookTime += Time.deltaTime;
        if (lookTime >= enemy.Data.investigateDuration) enemy.RequestReturnToLair();
    }
}
