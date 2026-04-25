using FSM;
using UnityEngine;

public abstract class EnemyState : IState
{
    protected EnemyStateMachine stateMachine { get; }
    protected Enemy enemy { get; }

    protected EnemyState(EnemyStateMachine machine)
    {
        stateMachine = machine;
        enemy = machine.enemy;
    }

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    public virtual void HandInput()
    {
    }

    public virtual void Update()
    {
    }

    public virtual void OnAnimationTranslateEvent(IState state)
    {
        stateMachine.ChangeState(state);
    }

    public virtual void OnAnimationExitEvent()
    {
    }

    protected void MoveTo(Vector3 point, float speed)
    {
        enemy.FaceTargetThenMove(point, speed);
    }
}
