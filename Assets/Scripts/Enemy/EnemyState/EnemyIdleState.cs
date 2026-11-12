using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private float remaining;
    public EnemyIdleState(EnemyStateMachine machine) : base(machine) { }
    public override void Enter()
    {
        var data = enemy.Data;
        remaining = Mathf.Max(0f, data.patrolWaitTime);
        enemy.CrossFadeState(data.idleStateName, data.idleCrossFade);
        enemy.SetOffset(false);
        enemy.SetLock(data.lockValueIdle);
        enemy.SetMovement(0f);
        enemy.ResetMovementProgress();
    }
    public override void Update()
    {
        if (enemy.HasTargetInSight)
        {
            stateMachine.ChangeState(stateMachine.alertState);
            return;
        }
        remaining -= Time.deltaTime;
        if (remaining <= 0f) stateMachine.ChangeState(stateMachine.patrolState);
    }
}
