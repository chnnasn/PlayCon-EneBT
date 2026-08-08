public class EnemyChaseState : EnemyState
{
    public EnemyChaseState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        enemy.SetMovement(0f);
        var data = enemy.Data;
        enemy.CrossFadeState(data.moveStartStateName, data.moveStartCrossFade);
        enemy.SetOffset(true);
        enemy.SetLock(data.chaseLockValue);
        enemy.SetMovement(0f);
    }

    public override void Update()
    {
        if (!enemy.HasChaseLock)
        {
            stateMachine.ChangeState(stateMachine.patrolState);
            return;
        }

        enemy.SetOffset(true);
        enemy.SetLock(enemy.Data.chaseLockValue);
        MoveTo(enemy.ChaseTarget.position, enemy.Data.chaseSpeed);
        enemy.SetMovement(0f);
    }
}
