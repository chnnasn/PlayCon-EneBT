using UnityEngine;

public class EnemyBreakState : EnemyState
{
    private float remaining;
    public EnemyBreakState(EnemyStateMachine machine) : base(machine) { }
    public override void Enter()
    {
        var data = enemy.Data;
        enemy.CancelAttack();
        enemy.CrossFadeState(data.breakStateName, data.breakCrossFade);
        enemy.SetOffset(false);
        enemy.SetLock(data.lockValueIdle);
        enemy.SetMovement(0f);
        enemy.SetDamageTakenMultiplier(data.breakDamageTakenMultiplier);
        enemy.SetBreak(true);
        remaining = Mathf.Max(0f, data.breakDuration);
    }
    public override void Update()
    {
        enemy.SetMovement(0f);
        remaining -= Time.deltaTime;
        if (remaining <= 0f) stateMachine.ResumeCombatOrReturn();
    }
    public override void Exit()
    {
        remaining = 0f;
        enemy.SetBreak(false);
        enemy.SetDamageTakenMultiplier(1f);
    }
}
