using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
    public class EnemyPriorityRegressionTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly Vector3 origin = new Vector3(1400f, 50f, 0f);
        private Player player;
        private Enemy source;
        private Enemy enemy;
        private EnemySO data;
        private bool usingTree;

        [UnitySetUp]
        public IEnumerator EnterScene()
        {
            if (!Application.isPlaying)
            {
                EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
                yield return new EnterPlayMode();
            }
            foreach (var navigation in Object.FindObjectsOfType<ActorNavigation>()) navigation.enabled = false;

            source = Object.FindObjectOfType<Enemy>();
            foreach (var actor in Object.FindObjectsOfType<Enemy>()) actor.enabled = false;
            player = Object.FindObjectOfType<Player>();
            Assert.NotNull(source);
            Assert.NotNull(player);
            Assert.NotNull(player.GetComponent<PlayerHealth>());
            player.movementStateMachine.Stop();
            player.comboStateMachine.Stop();
            player.GetAnimator().enabled = false;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Enemy priority regression floor";
            floor.transform.position = origin + Vector3.down * 0.5f;
            floor.transform.localScale = new Vector3(50f, 1f, 50f);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator ExitScene()
        {
            if (Application.isPlaying)
            {
                if (enemy != null) Object.Destroy(enemy.gameObject);
                if (data != null) Object.Destroy(data);
                yield return new ExitPlayMode();
            }
        }

        private IEnumerator PrepareEnemy(bool useTree)
        {
            if (enemy != null) Object.Destroy(enemy.gameObject);
            if (data != null) Object.Destroy(data);
            yield return null;

            usingTree = useTree;
            enemy = Object.Instantiate(source, origin, Quaternion.identity);
            data = Object.Instantiate(source.Data);
            data.spawnDuration = 0f;
            data.patrolWaitTime = 60f;
            data.breakDuration = 0.6f;
            data.hitDuration = 0.3f;
            data.retreatDuration = 0.4f;
            data.retreatSpeed = 0f;
            data.retreatStopDistanceToPlayer = 0f;
            data.chaseGiveUpDistance = 3f;
            data.homeLeashDistance = 100f;
            data.lostSightTimeout = 10f;
            data.lairReturnSpeed = 0.5f;
            data.returnTimeout = 10f;
            data.playerLayerMask = 0; // Each test controls the lock explicitly; no incidental reacquisition.
            typeof(EnemyControllerBase).GetField("enemySO", Fields).SetValue(enemy, data);
            typeof(Enemy).GetField("useBehaviorTree", Fields).SetValue(enemy, useTree);
            enemy.enabled = true;
            enemy.GetAnimator().enabled = false; // State timers are the completion fallback being exercised here.
            yield return null;
            yield return WaitForBehavior(nameof(EnemyIdleState));

            Teleport(enemy, origin + Vector3.right * 3f); // Returning cannot finish immediately at its home point.
            Teleport(player, enemy.transform.position + Vector3.forward * 1.2f);
            enemy.BeginChaseLock(player.transform);
            Assert.IsTrue(enemy.HasChaseLock);
        }

        private string Behavior => usingTree ? enemy.behaviorTree.CurrentBehavior : enemy.stateMachine.currentState.Value.GetType().Name;

        private IEnumerator WaitForBehavior(string expected, float timeout = 2f)
        {
            var deadline = Time.time + timeout;
            while (Behavior != expected && Time.time < deadline) yield return null;
            Assert.AreEqual(expected, Behavior, (usingTree ? "BT" : "FSM") + " priority transition");
        }

        private static void Teleport(Component actor, Vector3 point)
        {
            var controller = actor.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            actor.transform.position = point;
            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator LeashReturnWaitsForBreakAndKeepsItsDamageMultiplier()
        {
            foreach (var useTree in new[] { false, true })
            {
                yield return PrepareEnemy(useTree);
                enemy.EnterBreakState();
                yield return WaitForBehavior(nameof(EnemyBreakState));
                Teleport(player, enemy.transform.position + Vector3.forward * 10f);
                yield return null;

                Assert.IsTrue(enemy.IsReturningToLair, "Exceeding the leash must record the return request immediately.");
                Assert.IsTrue(enemy.IsInBreakState, "The return request cannot interrupt the higher-priority break.");
                Assert.AreEqual(data.breakDamageTakenMultiplier, enemy.DamageTakenMultiplier);
                enemy.NotifyHit(player.transform);
                Assert.IsTrue(enemy.IsInBreakState, "A normal hit cannot replace break either.");

                yield return WaitForBehavior(nameof(EnemyReturnToLairState));
                Assert.IsFalse(enemy.IsInBreakState);
                Assert.AreEqual(1f, enemy.DamageTakenMultiplier);
            }
        }

        [UnityTest]
        public IEnumerator RetreatQueuesBreakBeforeReturnAndDiscardsLowerPriorityHit()
        {
            foreach (var useTree in new[] { false, true })
            {
                yield return PrepareEnemy(useTree);
                enemy.OnPlayerOneStrokeCommand();
                yield return WaitForBehavior(nameof(EnemyRetreatState));
                enemy.RequestReturnToLair();
                enemy.EnterBreakState();
                enemy.NotifyHit(player.transform);

                Assert.AreEqual(nameof(EnemyRetreatState), Behavior, "Return, break and hit must not interrupt retreat.");
                Assert.IsTrue(enemy.IsHitReactionDeferred);
                yield return WaitForBehavior(nameof(EnemyBreakState));
                Assert.IsTrue(enemy.IsReturningToLair, "The return intent must survive the queued break.");
                Assert.AreEqual(data.breakDamageTakenMultiplier, enemy.DamageTakenMultiplier);
                yield return WaitForBehavior(nameof(EnemyReturnToLairState));
                Assert.AreEqual(1f, enemy.DamageTakenMultiplier);
            }
        }

        [UnityTest]
        public IEnumerator RetreatQueuesHitBeforeReturnAndOldAnimationExitCannotConsumeIt()
        {
            foreach (var useTree in new[] { false, true })
            {
                yield return PrepareEnemy(useTree);
                enemy.OnPlayerOneStrokeCommand();
                yield return WaitForBehavior(nameof(EnemyRetreatState));
                enemy.NotifyHit(player.transform);
                enemy.RequestReturnToLair();
                enemy.OnAnimationExitEvent(AnimationState.Hit);

                Assert.AreEqual(nameof(EnemyRetreatState), Behavior);
                yield return WaitForBehavior(nameof(EnemyHitState));
                Assert.IsTrue(enemy.IsReturningToLair);
                yield return WaitForBehavior(nameof(EnemyReturnToLairState));
            }
        }

        [UnityTest]
        public IEnumerator DeathOverridesPendingReactionsAndReviveClearsThem()
        {
            foreach (var useTree in new[] { false, true })
            {
                yield return PrepareEnemy(useTree);
                enemy.OnPlayerOneStrokeCommand();
                yield return WaitForBehavior(nameof(EnemyRetreatState));
                enemy.EnterBreakState();
                enemy.RequestReturnToLair();
                var health = enemy.GetComponent<CharacterHealthBase>();
                // Normal armor keeps only 20% of raw damage; use an unambiguously lethal hit.
                Assert.IsTrue(health.ReceiveDamage(1000000f, player.transform));
                yield return WaitForBehavior(nameof(EnemyDeadState));
                Assert.IsFalse(enemy.IsAttacking);
                Assert.AreEqual(1f, enemy.DamageTakenMultiplier);
                yield return GameplayTestWait.ForSeconds(data.retreatDuration + data.breakDuration);
                Assert.AreEqual(nameof(EnemyDeadState), Behavior);

                health.Revive();
                yield return null;
                yield return WaitForBehavior(nameof(EnemyIdleState));
                Assert.IsFalse(enemy.IsReturningToLair);
                Assert.IsFalse(enemy.IsInBreakState);
                Assert.AreEqual(1f, enemy.DamageTakenMultiplier);
            }
        }
    }
}
