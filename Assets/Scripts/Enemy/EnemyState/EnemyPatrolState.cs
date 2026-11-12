using UnityEngine;

public class EnemyPatrolState : EnemyState
{
    private Vector3 patrolTarget;
    private bool waitingForIdleTranslate;
    private float idleReturnTimer;

    public EnemyPatrolState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        enemy.EndChaseLock();
        patrolTarget = enemy.GetRandomPatrolPoint();
        waitingForIdleTranslate = false;
        idleReturnTimer = 0f;
        enemy.ResetMovementProgress();
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

        if (waitingForIdleTranslate)
        {
            // 等待动画收尾期间保持静止，避免继续朝巡逻点纠正导致原地打转。
            enemy.SetOffset(false);
            enemy.SetLock(enemy.Data.lockValueIdle);
            enemy.SetMovement(0f);
            idleReturnTimer += Time.deltaTime;
            if (idleReturnTimer >= EnemyBehaviorTree.PatrolIdleReturnTimeout)
                stateMachine.ChangeState(stateMachine.idleState);
            return;
        }

        enemy.SetOffset(true);
        enemy.SetLock(enemy.Data.lockValueMove);
        enemy.SetMovement(enemy.Data.patrolMovementValue);
        enemy.MoveRootMotionTowards(patrolTarget, enemy.Data.patrolSpeed);
        if (enemy.MovementIsBlocked)
        {
            stateMachine.ChangeState(stateMachine.idleState);
            return;
        }
        if (!enemy.HasReached(patrolTarget))
        {
            return;
        }

        if (waitingForIdleTranslate)
        {
            return;
        }


        waitingForIdleTranslate = true;
        enemy.SetOffset(false);
        enemy.SetLock(enemy.Data.lockValueIdle);
        enemy.SetMovement(0f);
    }

    public override void OnAnimationExitEvent()
    {
        if (waitingForIdleTranslate) idleReturnTimer = EnemyBehaviorTree.PatrolIdleReturnTimeout;
    }
    
}
