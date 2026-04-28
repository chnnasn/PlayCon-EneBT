using FSM;

public class EnemyStateMachine : StateMachine
{
    public Enemy enemy { get; }
    public EnemyIdleState idleState { get; }
    public EnemyPatrolState patrolState { get; }
    public EnemyAlertState alertState { get; }
    public EnemyChaseState chaseState { get; }
    public EnemyBreakState breakState { get; }

    public EnemyStateMachine(Enemy enemyController)
    {
        enemy = enemyController;
        idleState = new EnemyIdleState(this);
        patrolState = new EnemyPatrolState(this);
        alertState = new EnemyAlertState(this);
        chaseState = new EnemyChaseState(this);
        breakState = new EnemyBreakState(this);
    }
}
