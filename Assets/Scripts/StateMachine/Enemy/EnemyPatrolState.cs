using UnityEngine;

public class EnemyPatrolState : EnemyState
{
    private Vector3 patrolTarget;

    public EnemyPatrolState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        enemy.EndChaseLock();
        patrolTarget = enemy.GetRandomPatrolPoint();
        var data = enemy.Data;
        enemy.CrossFadeState(data.moveStartStateName, data.moveStartCrossFade);
        enemy.SetOffset(true);
        enemy.SetLock(data.lockValueMove);
        enemy.SetMovement(data.patrolMovementValue);
    }

    public override void Update()
    {
        if (enemy.HasTargetInSight)
        {
            stateMachine.ChangeState(stateMachine.alertState);
            return;
        }

        enemy.SetOffset(true);
        enemy.SetLock(enemy.Data.lockValueMove);
        enemy.SetMovement(enemy.Data.patrolMovementValue);
        enemy.MoveRootMotionTowards(patrolTarget, enemy.Data.patrolSpeed);
        if (!enemy.HasReached(patrolTarget))
        {
            return;
        }
        
        // 到达目标点后立即回到 Idle，避免在目标点附近持续转向打转。
        stateMachine.ChangeState(stateMachine.idleState);
    }
}
