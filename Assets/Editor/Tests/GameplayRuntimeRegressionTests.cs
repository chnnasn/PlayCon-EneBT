using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
public class GameplayRuntimeRegressionTests
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        // The legacy runner can restart this enumerator across a domain reload.
        if (!Application.isPlaying)
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            yield return new EnterPlayMode();
        }
            foreach (var navigation in Object.FindObjectsOfType<ActorNavigation>()) navigation.enabled = false;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.captureDeltaTime = 0f;
        Time.timeScale = 1f;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    private static void DisableEnemies()
    {
        foreach (var enemy in Object.FindObjectsOfType<Enemy>()) enemy.enabled = false;
    }

    private static void Teleport(Component actor, Vector3 point)
    {
        var controller = actor.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        actor.transform.position = point;
        if (controller != null) controller.enabled = true;
        Physics.SyncTransforms();
    }

    private static GameObject Box(string name, Vector3 position, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.position = position;
        box.transform.localScale = size;
        Physics.SyncTransforms();
        return box;
    }

    [UnityTest]
    public IEnumerator RootMotionDisplacementMatchesAt30_60_120HzAndStopsAtWall()
    {
        DisableEnemies();
        var player = Object.FindObjectOfType<Player>();
        player.enabled = false;
        player.GetAnimator().enabled = false;
        var move = typeof(ControllerBase).GetMethod("UpdateCharacterVelocity", InstanceFields);
        Assert.NotNull(move);
        var origin = new Vector3(1000f, 50f, 0f);
        foreach (var rate in new[] { 30, 60, 120 })
        {
            Time.captureDeltaTime = 1f / rate;
            Teleport(player, origin);
            yield return null;
            for (var frame = 0; frame < rate; frame++)
            {
                move.Invoke(player, new object[] { Vector3.forward * (3f / rate) });
                yield return null;
            }
            var distance = player.transform.position.z - origin.z;
            Assert.That(distance, Is.EqualTo(3f).Within(0.02f), "Root-motion displacement at " + rate + " Hz");
            TestContext.WriteLine(rate + " Hz simulated frame step: " + distance.ToString("F4") + " m");
        }

        Teleport(player, origin);
        var wall = Box("Regression wall", origin + new Vector3(0f, 1f, 2f), new Vector3(8f, 4f, 0.2f));
        for (var i = 0; i < 120; i++) move.Invoke(player, new object[] { Vector3.forward * 0.025f });
        Assert.Less(player.transform.position.z - origin.z, 1.85f, "CharacterController must stop animation displacement at the wall");
        Object.Destroy(wall);
    }

    [UnityTest]
    public IEnumerator ExitedRunningStateCannotFireItsDelayedIdleTransition()
    {
        DisableEnemies();
        var player = Object.FindObjectOfType<Player>();
        var machine = player.movementStateMachine;
        machine.ChangeState(machine.runningState);
        var release = machine.runningState.GetType().GetMethod("OnEnterIdle", InstanceFields);
        Assert.NotNull(release);
        release.Invoke(machine.runningState, new object[] { default(InputAction.CallbackContext) });
        machine.ChangeState(machine.playerMovementNullState);
        player.GetAnimator().enabled = false;
        yield return GameplayTestWait.ForSeconds(player.playerSO.movementData.bufferToIdleTime + 0.2f);
        Assert.AreSame(machine.playerMovementNullState, machine.currentState.Value,
            "A timer from the exited running state must not overwrite the attack movement state");
    }

    [UnityTest]
    public IEnumerator TimerCallbacksCanCancelAndScheduleWithoutSkippingOtherTimers()
    {
        DisableEnemies();
        var manager = TimerManager.MainInstance;
        var first = 0;
        var second = 0;
        var cancelled = 0;
        GameTimer cancelledTimer = null;
        manager.GetTimer(0f, () =>
        {
            first++;
            manager.UnregisterTimer(cancelledTimer);
            manager.GetTimer(0f, () => second++);
        });
        cancelledTimer = manager.GetTimer(0.1f, () => cancelled++);
        yield return GameplayTestWait.ForSeconds(0.25f);
        Assert.AreEqual(1, first);
        Assert.AreEqual(1, second);
        Assert.AreEqual(0, cancelled);
    }

    [UnityTest]
    public IEnumerator BothArchitecturesDamagePlayerAndStopAfterDeath()
    {
        var original = Object.FindObjectOfType<Enemy>();
        DisableEnemies();
        var player = Object.FindObjectOfType<Player>();
        var playerHealth = player.GetComponent<PlayerHealth>();
        Assert.NotNull(playerHealth);
        player.movementStateMachine.Stop();
        player.comboStateMachine.Stop();
        player.GetAnimator().enabled = false;
        var origin = new Vector3(1000f, 50f, 0f);
        Box("Regression floor", origin + new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f));

        foreach (var useTree in new[] { false, true })
        {
            playerHealth.Revive();
            player.movementStateMachine.Stop();
            player.comboStateMachine.Stop();
            Teleport(player, origin + Vector3.forward * 1.2f);
            var enemy = Object.Instantiate(original, origin, Quaternion.identity);
            var data = Object.Instantiate(original.Data);
            typeof(EnemyControllerBase).GetField("enemySO", InstanceFields).SetValue(enemy, data);
            typeof(Enemy).GetField("useBehaviorTree", InstanceFields).SetValue(enemy, useTree);
            data.repositionRetreatThreshold = 0f;
            data.repositionAdvanceThreshold = 3f;
            data.biteWindup = 0.1f;
            data.clawWindup = 0.1f;
            data.biteDuration = 0.4f;
            data.clawDuration = 0.4f;
            data.spawnDuration = 0.05f;
            enemy.enabled = true;
            enemy.ResetAIForRevive();
            enemy.BeginChaseLock(player.transform);
            var before = playerHealth.CurrentHP;
            var deadline = Time.time + 5f;
            while (playerHealth.CurrentHP >= before && Time.time < deadline) yield return null;
            Assert.Less(playerHealth.CurrentHP, before, (useTree ? "BT" : "FSM") + " must deliver damage");

            var health = enemy.GetComponent<CharacterHealthBase>();
            health.ReceiveDamage(1000000f, player.transform);
            yield return null;
            Assert.IsTrue(enemy.IsDead);
            Assert.IsFalse(enemy.IsAttacking);
            Assert.IsFalse(health.ReceiveDamage(10f, player.transform));
            var afterDeath = playerHealth.CurrentHP;
            yield return GameplayTestWait.ForSeconds(0.6f);
            Assert.AreEqual(afterDeath, playerHealth.CurrentHP, "Dead enemy must not finish a pending strike");
            Object.Destroy(enemy.gameObject);
            Object.Destroy(data);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator EnemyAttackRechecksObstaclesAndCancelsWhenDisabled()
    {
        var original = Object.FindObjectOfType<Enemy>();
        DisableEnemies();
        var player = Object.FindObjectOfType<Player>();
        player.movementStateMachine.Stop();
        player.comboStateMachine.Stop();
        player.GetAnimator().enabled = false;
        var health = player.GetComponent<PlayerHealth>();
        var origin = new Vector3(1000f, 50f, 0f);
        Box("Regression floor", origin + new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f));
        Teleport(player, origin + Vector3.forward * 1.2f);
        var enemy = Object.Instantiate(original, origin, Quaternion.identity);
        typeof(Enemy).GetField("useBehaviorTree", InstanceFields).SetValue(enemy, false);
        enemy.enabled = true;
        enemy.ResetAIForRevive();
        enemy.stateMachine.Stop();
        enemy.BeginChaseLock(player.transform);
        Assert.IsTrue(enemy.TryStartAttack(EnemyAttackKind.Bite));
        var wall = Box("Attack obstruction", origin + new Vector3(0f, 1.2f, 0.6f), new Vector3(6f, 4f, 0.1f));
        var before = health.CurrentHP;
        yield return GameplayTestWait.ForSeconds(enemy.Data.biteWindup + 0.1f);
        Assert.AreEqual(before, health.CurrentHP, "Wall introduced during windup must block the strike");
        Assert.IsFalse(enemy.HasLineOfSight(player.transform));
        enemy.CancelAttack();
        Assert.IsFalse(enemy.TryStartAttack(EnemyAttackKind.Claw), "Cannot start attack through a wall");
        Object.Destroy(wall);
        yield return null;
        Physics.SyncTransforms();
        Assert.IsTrue(enemy.TryStartAttack(EnemyAttackKind.Claw));
        enemy.enabled = false;
        yield return GameplayTestWait.ForSeconds(enemy.Data.clawWindup + 0.1f);
        Assert.IsFalse(enemy.IsAttacking);
        Assert.AreEqual(before, health.CurrentHP, "Disable cancels a pending strike");
        Object.Destroy(enemy.gameObject);
    }

    [UnityTest]
    public IEnumerator BothArchitecturesDisengageAtWallsAndRestartWithoutResurrection()
    {
        var original = Object.FindObjectOfType<Enemy>();
        DisableEnemies();
        var player = Object.FindObjectOfType<Player>();
        player.movementStateMachine.Stop();
        player.comboStateMachine.Stop();
        player.GetAnimator().enabled = false;
        var origin = new Vector3(1000f, 50f, 0f);
        Box("Regression floor", origin + new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f));
        Box("Blocked pursuit", origin + new Vector3(0f, 1.5f, 1.4f), new Vector3(8f, 4f, 0.1f));
        Box("Blocked return", origin + new Vector3(0f, 1.5f, -1.4f), new Vector3(8f, 4f, 0.1f));
        Teleport(player, origin + Vector3.forward * 3f);
        foreach (var useTree in new[] { false, true })
        {
            var enemy = Object.Instantiate(original, origin - Vector3.forward * 3f, Quaternion.identity);
            var data = Object.Instantiate(original.Data);
            typeof(EnemyControllerBase).GetField("enemySO", InstanceFields).SetValue(enemy, data);
            typeof(Enemy).GetField("useBehaviorTree", InstanceFields).SetValue(enemy, useTree);
            data.lostSightTimeout = 0.15f;
            data.movementBlockedTimeout = 0.15f;
            data.returnTimeout = 1f;
            data.reacquireDelay = 10f;
            data.spawnDuration = 0f;
            Teleport(enemy, origin);
            enemy.enabled = true;
            enemy.ResetAIForRevive();
            enemy.BeginChaseLock(player.transform);
            yield return GameplayTestWait.ForSeconds(2f);
            Assert.IsFalse(enemy.HasChaseLock, "An occluded target must be released");
            Assert.IsFalse(enemy.IsReturningToLair, "An obstructed return must time out safely");
            Assert.That(enemy.transform.position.z, Is.InRange(-1.4f, 1.4f), "Do not cross or teleport through either wall");

            var health = enemy.GetComponent<CharacterHealthBase>();
            health.ReceiveDamage(10f, player.transform);
            var retainedHP = health.CurrentHP;
            enemy.enabled = false;
            Assert.IsFalse(enemy.IsAttacking);
            enemy.enabled = true;
            yield return null;
            Assert.AreEqual(retainedHP, health.CurrentHP, "Re-enabling AI preserves health");
            Assert.IsFalse(enemy.IsReturningToLair);
            if (!useTree) Assert.IsTrue(enemy.stateMachine.IsRunning);
            health.ReceiveDamage(1000000f, player.transform);
            enemy.enabled = false;
            enemy.enabled = true;
            yield return null;
            Assert.IsTrue(enemy.IsDead, "Re-enabling AI must not revive a dead enemy");
            Assert.IsFalse(enemy.IsAttacking);
            health.Revive();
            yield return null;
            Assert.IsFalse(enemy.IsDead);
            if (!useTree) Assert.IsTrue(enemy.stateMachine.IsRunning);
            Object.Destroy(enemy.gameObject);
            Object.Destroy(data);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator TwoEnemiesHaveIndependentAttackCooldownsAndDeathCancellation()
    {
        var original = Object.FindObjectOfType<Enemy>();
        DisableEnemies();
        var player = Object.FindObjectOfType<Player>();
        player.movementStateMachine.Stop();
        player.comboStateMachine.Stop();
        player.GetAnimator().enabled = false;
        var health = player.GetComponent<PlayerHealth>();
        var origin = new Vector3(1000f, 50f, 0f);
        Box("Regression floor", origin + new Vector3(0f, -0.5f, 0f), new Vector3(30f, 1f, 30f));
        Teleport(player, origin + Vector3.forward * 1.2f);
        var enemies = new Enemy[2];
        for (var index = 0; index < enemies.Length; index++)
        {
            var enemy = Object.Instantiate(original, origin + Vector3.right * (index == 0 ? -0.6f : 0.6f), Quaternion.identity);
            enemies[index] = enemy;
            typeof(Enemy).GetField("useBehaviorTree", InstanceFields).SetValue(enemy, false);
            enemy.enabled = true;
            enemy.ResetAIForRevive();
            enemy.stateMachine.Stop();
            enemy.BeginChaseLock(player.transform);
            Assert.IsTrue(enemy.TryStartAttack(EnemyAttackKind.Bite));
        }
        var before = health.CurrentHP;
        yield return GameplayTestWait.ForSeconds(original.Data.biteWindup + 0.1f);
        Assert.That(health.CurrentHP, Is.EqualTo(before - original.Data.biteDamage * 2f).Within(0.001f));
        foreach (var enemy in enemies) enemy.CancelAttack();
        foreach (var enemy in enemies) Assert.IsTrue(enemy.TryStartAttack(EnemyAttackKind.Claw));
        enemies[0].GetComponent<CharacterHealthBase>().ReceiveDamage(1000000f, player.transform);
        before = health.CurrentHP;
        yield return GameplayTestWait.ForSeconds(original.Data.clawWindup + 0.1f);
        Assert.That(health.CurrentHP, Is.EqualTo(before - original.Data.clawDamage).Within(0.001f),
            "Only the surviving enemy may finish its strike");
        foreach (var enemy in enemies) Object.Destroy(enemy.gameObject);
    }
}

}
