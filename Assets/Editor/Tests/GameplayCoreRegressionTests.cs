using BT;
using FSM;
using NUnit.Framework;

namespace Gameplay.Tests
{
public class GameplayCoreRegressionTests
{
    private sealed class Machine : StateMachine { }
    private sealed class State : IState
    {
        public int enters, exits, updates;
        public void Enter() => enters++;
        public void Exit() => exits++;
        public void Update() => updates++;
        public void HandInput() { }
        public void OnAnimationTranslateEvent(IState state) { }
        public void OnAnimationExitEvent() { }
    }
    private sealed class Node : BTNode
    {
        public NodeStatus result = NodeStatus.Failure;
        public int exits;
        protected override NodeStatus OnExecute() => result;
        protected override void OnExit() => exits++;
    }

    [Test]
    public void StoppedStateMachineRejectsLateTransitionsAndRestartsOnce()
    {
        var machine = new Machine();
        var first = new State();
        var late = new State();
        machine.Start(first);
        machine.ChangeState(first);
        Assert.AreEqual(1, first.enters);
        machine.Stop();
        machine.Stop();
        machine.ChangeState(late);
        machine.Update();
        Assert.AreEqual(1, first.exits);
        Assert.AreEqual(0, late.enters);
        Assert.AreEqual(0, first.updates);
        machine.Start(late);
        Assert.IsTrue(machine.IsRunning);
        Assert.AreEqual(1, late.enters);
    }

    [Test]
    public void SelectorInterruptsRunningLowerPriorityAndCanRestartIt()
    {
        var high = new Node();
        var low = new Node { result = NodeStatus.Running };
        var root = new Selector(high, low);
        Assert.AreEqual(NodeStatus.Running, root.Tick());
        high.result = NodeStatus.Running;
        root.Tick();
        Assert.AreEqual(1, low.exits);
        Assert.AreEqual(NodeStatus.Inactive, low.Status);
        high.result = NodeStatus.Failure;
        root.Tick();
        Assert.AreEqual(NodeStatus.Running, low.Status);
        root.Reset();
        Assert.AreEqual(2, low.exits);
    }

    [Test]
    public void TimerCallbackCanRestartTheSameTimerWithoutBeingMarkedCompleted()
    {
        var timer = new GameTimer();
        var calls = 0;
        timer.StartTimer(false, 0f, () =>
        {
            calls++;
            timer.StartTimer(false, 100f, () => calls++);
        });
        timer.UpdateTimer();
        Assert.AreEqual(1, calls);
        Assert.AreEqual(TimerStation.DoWorking, timer.TimerStation);
    }
}

}
