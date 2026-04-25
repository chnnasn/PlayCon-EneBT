using UnityEngine;

public class EnemyAlertState : EnemyState
{
    private GameTimer chaseTimer;
    private GameTimer lostTimer;

    public EnemyAlertState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        var data = enemy.Data;
        enemy.CrossFadeState(data.idleStateName, data.idleCrossFade);
        enemy.SetOffset(false);
        enemy.SetLock(data.lockValueIdle);
        enemy.SetMovement(0f);
    }

    public override void Update()
    {
        if (enemy.HasTargetInSight)
        {
            if (TimerManager.MainInstance != null)
            {
                TimerManager.MainInstance.UnregisterTimer(lostTimer);
                lostTimer = null;

                if (chaseTimer == null)
                {
                    chaseTimer = TimerManager.MainInstance.GetTimer(enemy.Data.alertToChaseDelay, () =>
                    {
                        enemy.BeginChaseLock();
                        stateMachine.ChangeState(stateMachine.chaseState);
                    });
                }
            }

            if (enemy.Target != null)
            {
                var lookDir = enemy.Target.position - enemy.transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    enemy.RotateTowards(lookDir);
                }
            }
        }
        else
        {
            if (TimerManager.MainInstance != null)
            {
                TimerManager.MainInstance.UnregisterTimer(chaseTimer);
                chaseTimer = null;

                if (lostTimer == null)
                {
                    lostTimer = TimerManager.MainInstance.GetTimer(enemy.Data.alertDuration, () =>
                    {
                        stateMachine.ChangeState(stateMachine.patrolState);
                    });
                }
            }
        }
    }

    public override void Exit()
    {
        base.Exit();
        if (TimerManager.MainInstance != null)
        {
            TimerManager.MainInstance.UnregisterTimer(chaseTimer);
            TimerManager.MainInstance.UnregisterTimer(lostTimer);
        }

        chaseTimer = null;
        lostTimer = null;
    }
}
