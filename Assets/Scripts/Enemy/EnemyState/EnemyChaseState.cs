using UnityEngine;

public class EnemyChaseState : EnemyState
{
    private bool running;
    public EnemyChaseState(EnemyStateMachine machine) : base(machine) { }
    public override void Enter()
    {
        enemy.ResetMovementProgress();
        running = false;
        enemy.CrossFadeState(enemy.Data.moveStartStateName, enemy.Data.moveStartCrossFade);
        enemy.SetOffset(true);
        enemy.SetLock(enemy.Data.chaseLockValue);
        enemy.SetMovement(0f);
    }
    public override void Update()
    {
        if (!enemy.HasChaseLock)
        {
            stateMachine.ResumeCombatOrReturn();
            return;
        }
        if (enemy.CanStartAttack(EnemyAttackKind.Bite) || enemy.CanStartAttack(EnemyAttackKind.Claw))
        {
            stateMachine.ChangeState(stateMachine.attackState);
            return;
        }
        var data = enemy.Data;
        var direction = enemy.ChaseTarget.position - enemy.transform.position;
        direction.y = 0f;
        var distance = direction.magnitude;
        if (!enemy.HasNavigation) enemy.RotateTowards(direction, data.combatRotationTime);
        var shouldRun = distance > data.chaseRunDistance;
        if (running != shouldRun)
        {
            running = shouldRun;
            enemy.CrossFadeState(running ? data.runStateName : data.moveStartStateName, data.moveStartCrossFade);
        }
        // Wait out cooldowns at striking distance instead of pushing into the player's controller.
        enemy.MoveInPursuit(running ? data.chaseSpeed : data.chaseWalkSpeed);
        if (enemy.MovementIsBlocked) enemy.RequestReturnToLair();
    }
}
