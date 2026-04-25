public class EnemyChaseState : EnemyState
{
    public EnemyChaseState(EnemyStateMachine machine) : base(machine)
    {
    }

    public override void Enter()
    {
        var data = enemy.Data;
        enemy.CrossFadeState(data.moveStartStateName, data.moveStartCrossFade);
        enemy.SetOffset(true);
        enemy.SetLock(data.lockValueMove);
        enemy.SetMovement(data.chaseMovementValue);
    }

    public override void Update()
    {
        if (!enemy.HasChaseLock)
        {
            stateMachine.ChangeState(stateMachine.patrolState);
            return;
        }

        enemy.SetOffset(true);
        enemy.SetLock(enemy.Data.lockValueMove);
        MoveTo(enemy.ChaseTarget.position, enemy.Data.chaseSpeed);
        enemy.SetMovement(enemy.Data.chaseMovementValue);
    }
}
