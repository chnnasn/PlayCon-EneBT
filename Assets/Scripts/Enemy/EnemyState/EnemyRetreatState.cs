using UnityEngine;

public class EnemyRetreatState : EnemyState
{
    private float retreatEndTime;
    private Transform retreatPlayer;

    public EnemyRetreatState(EnemyStateMachine machine) : base(machine)
    {
    }

    public void BeginRetreat(Transform player)
    {
        retreatPlayer = player;
        var duration = enemy.Data != null ? enemy.Data.retreatDuration : 0.35f;
        retreatEndTime = Time.time + Mathf.Max(0f, duration);
        enemy.SetMovement(0f);
        stateMachine.ChangeState(this);
    }

    public override void Enter()
    {
        var data = enemy.Data;
        enemy.SetOffset(true);
        enemy.SetLock(data != null ? data.chaseLockValue : 1f);
        enemy.SetMovement(data != null ? data.retreatMovementValue : 1f);
    }

    public override void Update()
    {
        var data = enemy.Data;

        var stopDistance = data != null ? data.retreatStopDistanceToPlayer : 0f;
        if (retreatPlayer != null && stopDistance > 0f)
        {
            var a = enemy.transform.position;
            var b = retreatPlayer.position;
            a.y = 0f;
            b.y = 0f;
            if ((a - b).sqrMagnitude >= stopDistance * stopDistance)
            {
                FinishRetreat();
                return;
            }
        }

        if (Time.time >= retreatEndTime)
        {
            FinishRetreat();
            return;
        }

        enemy.SetOffset(true);
        enemy.SetLock(data != null ? data.chaseLockValue : 1f);
        enemy.SetMovement(data != null ? data.retreatMovementValue : 1f);

        var back = -enemy.transform.forward;
        back.y = 0f;
        if (back.sqrMagnitude > 0.0001f)
        {
            back.Normalize();
            var speed = data != null ? data.retreatSpeed : 2.5f;
            enemy.MoveInDirection(back, speed);
        }
    }

    public override void Exit()
    {
        base.Exit();    
    }

    private void FinishRetreat()
    {
        retreatPlayer = null;

        if (enemy.HasChaseLock)
        {
            stateMachine.ChangeState(stateMachine.chaseState);
            return;
        }
    }
}

