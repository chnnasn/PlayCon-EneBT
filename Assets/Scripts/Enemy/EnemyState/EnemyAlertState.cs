using UnityEngine;

public class EnemyAlertState : EnemyState
{
    private float visibleTime;
    private float lostTime;
    private Vector3 lastSeen;
    public EnemyAlertState(EnemyStateMachine machine) : base(machine) { }
    public override void Enter()
    {
        visibleTime = lostTime = 0f;
        lastSeen = enemy.Target != null ? enemy.Target.position : enemy.transform.position;
        var data = enemy.Data;
        enemy.CrossFadeState(data.idleStateName, data.idleCrossFade);
        enemy.SetOffset(false);
        enemy.SetLock(data.lockValueIdle);
        enemy.SetMovement(0f);
    }
    public override void Update()
    {
        if (enemy.HasTargetInSight && EnemyControllerBase.IsValidTarget(enemy.Target))
        {
            lostTime = 0f;
            visibleTime += Time.deltaTime;
            lastSeen = enemy.Target.position;
            var direction = lastSeen - enemy.transform.position;
            direction.y = 0f;
            enemy.RotateTowards(direction);
            if (visibleTime >= enemy.Data.alertToChaseDelay)
            {
                enemy.BeginChaseLock();
                stateMachine.ChangeState(stateMachine.HasRoared ? (FSM.IState)stateMachine.chaseState : stateMachine.roarState);
            }
            return;
        }
        visibleTime = 0f;
        lostTime += Time.deltaTime;
        if (lostTime >= enemy.Data.alertDuration)
        {
            stateMachine.investigateState.SetPoint(lastSeen);
            stateMachine.ChangeState(stateMachine.investigateState);
        }
    }
}
