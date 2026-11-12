using FSM;
using UnityEngine;

public class EnemyStateMachine : StateMachine
{
    public Enemy enemy { get; }
    public EnemyIdleState idleState { get; }
    public EnemyPatrolState patrolState { get; }
    public EnemyAlertState alertState { get; }
    public EnemyChaseState chaseState { get; }
    public EnemyBreakState breakState { get; }
    public EnemyRetreatState retreatState { get; }
    public EnemyAttackState attackState { get; }
    public EnemyHitState hitState { get; }
    public EnemyDeadState deadState { get; }
    public EnemyReturnToLairState returnState { get; }
    public EnemyInvestigateState investigateState { get; }
    public EnemySpawnState spawnState { get; }
    public EnemyRoarState roarState { get; }
    public bool HasRoared { get; set; }
    private bool breakRequested;
    private bool hitRequested;
    private Transform pendingRetreatTarget;
    private float retreatRequestedUntil;

    public bool IsInPriorityReaction => currentState.Value == spawnState || currentState.Value == retreatState ||
                                       currentState.Value == breakState || currentState.Value == hitState;
    public bool IsHitReactionDeferred => currentState.Value == spawnState || currentState.Value == retreatState ||
                                        currentState.Value == breakState || breakRequested || Time.time < retreatRequestedUntil;

    public EnemyStateMachine(Enemy enemyController)
    {
        enemy = enemyController;
        idleState = new EnemyIdleState(this);
        patrolState = new EnemyPatrolState(this);
        alertState = new EnemyAlertState(this);
        chaseState = new EnemyChaseState(this);
        breakState = new EnemyBreakState(this);
        retreatState = new EnemyRetreatState(this);
        attackState = new EnemyAttackState(this);
        hitState = new EnemyHitState(this);
        deadState = new EnemyDeadState(this);
        returnState = new EnemyReturnToLairState(this);
        investigateState = new EnemyInvestigateState(this);
        spawnState = new EnemySpawnState(this);
        roarState = new EnemyRoarState(this);
    }

    public void ResumeCombatOrReturn()
    {
        if (enemy.IsDead) ChangeState(deadState);
        else if (TryEnterPendingReaction()) return;
        else if (enemy.IsReturningToLair) ChangeState(returnState);
        else if (enemy.HasChaseLock) ChangeState(chaseState);
        else
        {
            enemy.RequestReturnToLair();
            ChangeState(returnState); // The current reaction has completed, so consume the deferred intent now.
        }
    }

    public void ResumeAfterSpawn()
    {
        if (enemy.IsDead) ChangeState(deadState);
        else if (TryEnterPendingReaction()) return;
        else if (enemy.IsReturningToLair) ChangeState(returnState);
        else ChangeState(idleState);
    }

    public void RequestBreak()
    {
        hitRequested = false;
        if (currentState.Value == spawnState || currentState.Value == retreatState)
        {
            breakRequested = true;
            return;
        }
        breakRequested = false;
        ChangeState(breakState);
    }

    public void RequestHit()
    {
        if (breakRequested || currentState.Value == breakState) return;
        if (currentState.Value == spawnState || currentState.Value == retreatState)
        {
            hitRequested = true;
            return;
        }
        ChangeState(hitState);
    }

    public void RequestRetreat(Transform player)
    {
        if (currentState.Value == spawnState)
        {
            pendingRetreatTarget = player;
            retreatRequestedUntil = Time.time + Mathf.Max(0f, enemy.Data.retreatDuration);
            return;
        }
        retreatState.BeginRetreat(player);
    }

    public void ClearPendingReactions()
    {
        breakRequested = hitRequested = false;
        pendingRetreatTarget = null;
        retreatRequestedUntil = 0f;
    }

    private bool TryEnterPendingReaction()
    {
        var remainingRetreat = retreatRequestedUntil - Time.time;
        retreatRequestedUntil = 0f;
        var retreatTarget = pendingRetreatTarget;
        pendingRetreatTarget = null;
        if (remainingRetreat > 0f)
        {
            retreatState.BeginRetreat(retreatTarget, remainingRetreat);
            return true;
        }
        if (breakRequested)
        {
            breakRequested = false;
            ChangeState(breakState);
            return true;
        }
        if (hitRequested)
        {
            hitRequested = false;
            ChangeState(hitState);
            return true;
        }
        return false;
    }
}
