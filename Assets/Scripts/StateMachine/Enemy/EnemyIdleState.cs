using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private GameTimer patrolTimer;

    public EnemyIdleState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        var data = enemy.Data;
        enemy.CrossFadeState(data.idleStateName, data.idleCrossFade);
        enemy.SetOffset(false);
        enemy.SetLock(data.lockValueIdle);
        enemy.SetMovement(0f);

        if (TimerManager.MainInstance != null)
        {
            patrolTimer = TimerManager.MainInstance.GetTimer(data.patrolWaitTime, () =>
            {
                stateMachine.ChangeState(stateMachine.patrolState);
            });
        }
    }

    public override void Update()
    {
        if (enemy.HasTargetInSight)
        {
            stateMachine.ChangeState(stateMachine.alertState);
            return;
        }
    }

    public override void Exit()
    {
        base.Exit();
        if (TimerManager.MainInstance != null)
        {
            TimerManager.MainInstance.UnregisterTimer(patrolTimer);
        }

        patrolTimer = null;
    }
}
