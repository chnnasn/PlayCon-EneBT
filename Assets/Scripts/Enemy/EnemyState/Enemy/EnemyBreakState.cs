using UnityEngine;

public class EnemyBreakState : EnemyState
{
    private GameTimer breakTimer;
    private float fallbackCountdown;

    public EnemyBreakState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        var data = enemy.Data;
        enemy.CrossFadeState(data.breakStateName, data.breakCrossFade);
        enemy.SetOffset(false);
        enemy.SetLock(data.lockValueIdle);
        enemy.SetMovement(0f);
        enemy.SetDamageTakenMultiplier(data.breakDamageTakenMultiplier);
        enemy.SetBreak(true);

        if (TimerManager.MainInstance != null)
        {
            breakTimer = TimerManager.MainInstance.GetTimer(data.breakDuration, () =>
            {
                var player = Object.FindObjectOfType<Player>();
                if (player != null)
                {
                    enemy.BeginChaseLock(player.transform);
                    stateMachine.ChangeState(stateMachine.chaseState);
                    return;
                }

                stateMachine.ChangeState(stateMachine.idleState);
            });
            fallbackCountdown = 0f;
            return;
        }

        breakTimer = null;
        fallbackCountdown = data.breakDuration;
    }

    public override void Update()
    {
        // 破防阶段：原地不动、也不自动转向目标。
        enemy.SetMovement(0f);

        if (breakTimer == null && fallbackCountdown > 0f)
        {
            fallbackCountdown -= Time.deltaTime;
            if (fallbackCountdown <= 0f)
            {
                fallbackCountdown = 0f;
                var player = Object.FindObjectOfType<Player>();
                if (player != null)
                {
                    enemy.BeginChaseLock(player.transform);
                    stateMachine.ChangeState(stateMachine.chaseState);
                    return;
                }

                stateMachine.ChangeState(stateMachine.idleState);
            }
        }
    }

    public override void Exit()
    {
        enemy.SetBreak(false);
        
        if (TimerManager.MainInstance != null)
        {
            TimerManager.MainInstance.UnregisterTimer(breakTimer);
        }

        breakTimer = null;
        fallbackCountdown = 0f;
        enemy.SetDamageTakenMultiplier(1f);
    }
}
