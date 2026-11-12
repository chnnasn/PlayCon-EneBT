using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Navigation.Tests
{
    public class NavigationRegressionTests
    {
        private readonly List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        private readonly List<NavMeshDataInstance> instances = new List<NavMeshDataInstance>();
        private readonly List<NavMeshData> data = new List<NavMeshData>();
        private NavigationProfile small, large;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (!Application.isPlaying)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                yield return new EnterPlayMode();
            }
            sources.Clear();
            instances.Clear();
            data.Clear();
            small = AssetDatabase.LoadAssetAtPath<NavigationProfile>(GameplayNavigationSetup.SmallProfilePath);
            large = AssetDatabase.LoadAssetAtPath<NavigationProfile>(GameplayNavigationSetup.LargeProfilePath);
            Assert.NotNull(small);
            Assert.NotNull(large);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            if (Application.isPlaying)
            {
                foreach (var actor in Object.FindObjectsOfType<ActorNavigation>()) actor.Cancel();
                foreach (var instance in instances) if (instance.valid) instance.Remove();
                foreach (var item in data) if (item != null) Object.Destroy(item);
                yield return new ExitPlayMode();
            }
        }

        private GameObject Box(string name, Vector3 position, Vector3 size, bool bake = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = size;
            if (bake) sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = box.transform.localToWorldMatrix, size = Vector3.one, area = 0 });
            Physics.SyncTransforms();
            return box;
        }

        private void Floor(float width = 20f)
        {
            Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(width, 1f, 20f)).layer = 3;
        }

        private void Bake(params NavigationProfile[] profiles)
        {
            foreach (var profile in profiles)
            {
                var baked = NavMeshBuilder.BuildNavMeshData(profile.BuildSettings, sources,
                    new Bounds(Vector3.zero, new Vector3(40f, 12f, 40f)), Vector3.zero, Quaternion.identity);
                Assert.NotNull(baked);
                data.Add(baked);
                instances.Add(NavMesh.AddNavMeshData(baked));
            }
        }

        private ActorNavigation Actor(NavigationProfile profile, Vector3 position)
        {
            var actor = new GameObject(profile.name + " test actor");
            actor.SetActive(false);
            actor.layer = 7;
            actor.transform.position = position;
            actor.AddComponent<CharacterController>();
            actor.AddComponent<NavMeshAgent>().enabled = false;
            var navigation = actor.AddComponent<ActorNavigation>();
            navigation.Configure(profile);
            actor.SetActive(true);
            return navigation;
        }

        private static IEnumerator Wait(float seconds)
        {
            var until = Time.time + seconds;
            var realDeadline = Time.realtimeSinceStartup + seconds + 10f;
            while (Time.time < until)
            {
                Assert.Less(Time.realtimeSinceStartup, realDeadline, "Simulation time stalled");
                yield return null;
            }
        }

        private static IEnumerator Travel(ActorNavigation actor, Vector3 destination, float timeout = 12f)
        {
            var deadline = Time.time + timeout;
            do
            {
                actor.MoveTo(destination, 4f, 0.2f);
                yield return null;
            } while (actor.Status != NavigationStatus.Arrived && Time.time < deadline);
            Assert.AreEqual(NavigationStatus.Arrived, actor.Status, "Destination " + destination + "; actual " + actor.transform.position);
            Assert.Less(Vector3.Distance(actor.transform.position, destination), 0.4f);
        }

        [UnityTest]
        public IEnumerator SingleActorRoutesAroundStaticWallAndCancelsMovement()
        {
            Floor();
            Box("Wall", new Vector3(0f, 2f, 0f), new Vector3(5f, 4f, 0.6f));
            Bake(small);
            var actor = Actor(small, new Vector3(0f, 0f, -6f));
            var goal = new Vector3(0f, 0f, 6f);
            var deadline = Time.time + 12f;
            var maximumDetour = 0f;
            do
            {
                actor.MoveTo(goal, 4f);
                yield return null;
                maximumDetour = Mathf.Max(maximumDetour, Mathf.Abs(actor.transform.position.x));
                if (Mathf.Abs(actor.transform.position.z) < 0.7f) Assert.Greater(Mathf.Abs(actor.transform.position.x), 2.8f);
            } while (actor.Status != NavigationStatus.Arrived && Time.time < deadline);
            Assert.AreEqual(NavigationStatus.Arrived, actor.Status);
            Assert.Greater(maximumDetour, 3f, "The actor must route around the end of the wall");
            actor.Cancel();
            var stopped = actor.transform.position;
            yield return Wait(0.3f);
            Assert.Less(Vector3.Distance(stopped, actor.transform.position), 0.01f);
            Assert.Less(actor.PathRequests, 60, "Do not calculate a path every frame");
        }

        [UnityTest]
        public IEnumerator NarrowGateAdmitsSmallBodyButRejectsLargeBody()
        {
            Floor(12f);
            Box("Gate left", new Vector3(-3.35f, 2f, 0f), new Vector3(5.3f, 4f, 0.6f));
            Box("Gate right", new Vector3(3.35f, 2f, 0f), new Vector3(5.3f, 4f, 0.6f));
            Bake(small, large);
            var smallActor = Actor(small, new Vector3(0f, 0f, -6f));
            var largeActor = Actor(large, new Vector3(3f, 0f, -6f));
            var goal = new Vector3(0f, 0f, 6f);
            Assert.IsTrue(smallActor.CanReach(goal, new NavMeshPath()));
            Assert.IsFalse(largeActor.CanReach(goal, new NavMeshPath()));
            largeActor.MoveTo(goal, 4f);
            yield return null;
            yield return null;
            Assert.Less(largeActor.transform.position.z, -5f);
            yield return Travel(smallActor, goal);
        }

        [UnityTest]
        public IEnumerator TwoActorsPassEachOtherWithoutOverlapping()
        {
            Floor();
            Bake(small);
            var first = Actor(small, new Vector3(-1.5f, 0f, -6f));
            var second = Actor(small, new Vector3(-1.5f, 0f, 6f));
            var firstGoal = new Vector3(1.5f, 0f, 6f);
            var secondGoal = new Vector3(1.5f, 0f, -6f);
            var deadline = Time.time + 12f;
            var minimumDistance = float.MaxValue;
            do
            {
                first.MoveTo(firstGoal, 3f);
                second.MoveTo(secondGoal, 3f);
                yield return null;
                minimumDistance = Mathf.Min(minimumDistance, Vector3.Distance(first.transform.position, second.transform.position));
            } while ((first.Status != NavigationStatus.Arrived || second.Status != NavigationStatus.Arrived) && Time.time < deadline);
            Assert.AreEqual(NavigationStatus.Arrived, first.Status);
            Assert.AreEqual(NavigationStatus.Arrived, second.Status);
            Assert.GreaterOrEqual(minimumDistance, small.radius * 1.7f);
        }

        [UnityTest]
        public IEnumerator DynamicDoorBlocksThenReopensAndReclosesTheRoute()
        {
            Floor(12f);
            Box("Frame left", new Vector3(-3.75f, 2f, 0f), new Vector3(4.5f, 4f, 0.6f));
            Box("Frame right", new Vector3(3.75f, 2f, 0f), new Vector3(4.5f, 4f, 0.6f));
            Bake(small);
            var door = Box("Door", new Vector3(0f, 2f, 0f), new Vector3(3f, 4f, 0.6f), false).AddComponent<DynamicNavigationObstacle>();
            yield return Wait(0.7f);
            var actor = Actor(small, new Vector3(0f, 0f, -5f));
            var goal = new Vector3(0f, 0f, 5f);
            Assert.IsFalse(actor.CanReach(goal, new NavMeshPath()), "The stationary obstacle must carve the passage");
            var until = Time.time + 0.6f;
            while (Time.time < until) { actor.MoveTo(goal, 4f); yield return null; }
            Assert.Less(actor.transform.position.z, -1f);
            door.SetBlocking(false);
            yield return Travel(actor, goal);
            door.SetBlocking(true);
            yield return Wait(0.7f);
            var other = Actor(small, new Vector3(0f, 0f, -5f));
            Assert.IsFalse(other.CanReach(goal, new NavMeshPath()), "Closing the door must invalidate the passage again");
        }

        [UnityTest]
        public IEnumerator MovingObstacleIsAvoidedAndTheActorStillReachesItsGoal()
        {
            Floor();
            Bake(small);
            var obstacle = Box("Moving crate", new Vector3(0f, 1.5f, 0f), new Vector3(2f, 3f, 2f), false);
            obstacle.AddComponent<DynamicNavigationObstacle>();
            var actor = Actor(small, new Vector3(0f, 0f, -6f));
            var goal = new Vector3(0f, 0f, 6f);
            var deadline = Time.time + 15f;
            do
            {
                obstacle.transform.position = new Vector3(Mathf.Sin(Time.time * 0.8f) * 2.5f, 1.5f, 0f);
                Physics.SyncTransforms();
                actor.MoveTo(goal, 3f);
                yield return null;
                var delta = actor.transform.position - obstacle.transform.position;
                Assert.IsTrue(Mathf.Abs(delta.x) >= 0.95f || Mathf.Abs(delta.z) >= 0.95f, "Actor center must not pass through the moving solid");
            } while (actor.Status != NavigationStatus.Arrived && Time.time < deadline);
            Assert.AreEqual(NavigationStatus.Arrived, actor.Status);
        }

        [UnityTest]
        public IEnumerator CombatPositionsAreDistinctReachableAndReleasedOnDisable()
        {
            Floor();
            Bake(small);
            var ring = new GameObject("Combat target").AddComponent<CombatPositionRing>();
            var actors = new List<ActorNavigation>();
            var points = new Vector3[6];
            for (var index = 0; index < points.Length; index++)
                actors.Add(Actor(small, new Vector3(-4f + index * 1.5f, 0f, -6f)));
            var deadline = Time.time + 15f;
            var arrived = false;
            do
            {
                arrived = true;
                for (var index = 0; index < actors.Count; index++)
                {
                    Assert.IsTrue(ring.TryReserve(actors[index], 2.5f, out points[index]));
                    actors[index].MoveTo(points[index], 3f);
                    if (Vector3.Distance(actors[index].transform.position, points[index]) > 0.4f) arrived = false;
                }
                yield return null;
            } while (!arrived && Time.time < deadline);
            Assert.IsTrue(arrived, "Every actor must reach its own combat position");
            Assert.AreEqual(6, ring.ReservationCount);
            for (var first = 0; first < points.Length; first++)
                for (var second = first + 1; second < points.Length; second++)
                    Assert.Greater(Vector3.Distance(points[first], points[second]), small.radius * 2f);
            actors[0].enabled = false;
            Assert.AreEqual(5, ring.ReservationCount);
            var replacement = Actor(small, new Vector3(4f, 0f, -6f));
            Assert.IsTrue(ring.TryReserve(replacement, 2.5f, out var replacementPoint));
            Assert.AreEqual(6, ring.ReservationCount);
            ring.gameObject.SetActive(false);
            Assert.AreEqual(0, ring.ReservationCount);
        }

        [UnityTest]
        public IEnumerator BothEnemyArchitecturesRouteAroundWallAndDamageTheirTarget()
        {
            Floor();
            Box("Wall", new Vector3(0f, 2f, 0f), new Vector3(5f, 4f, 0.6f));
            Bake(small);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayNavigationSetup.EnemyPrefabPath);
            Assert.NotNull(prefab);
            foreach (var useTree in new[] { false, true })
            foreach (var side in new[] { -1f, 1f })
            {
                var target = new GameObject("Target");
                target.layer = 6;
                target.transform.position = new Vector3(0f, 0f, 6f);
                var health = target.AddComponent<CharacterHealthBase>();
                target.AddComponent<CombatPositionRing>();
                var enemy = Object.Instantiate(prefab, new Vector3(side * 0.25f, 0f, -6f), Quaternion.identity).GetComponent<Enemy>();
                enemy.GetAnimator().enabled = false;
                var config = Object.Instantiate(enemy.Data);
                config.spawnDuration = config.roarDuration = 0f;
                config.chaseSpeed = 4f;
                config.chaseWalkSpeed = 3f;
                config.navigationSearchTimeout = 15f;
                config.homeLeashDistance = 50f;
                typeof(EnemyControllerBase).GetField("enemySO", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, config);
                typeof(Enemy).GetField("useBehaviorTree", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, useTree);
                enemy.ResetAIForRevive();
                // A remembered attacker is a real gameplay trigger even while a wall blocks sight.
                enemy.NotifyHit(target.transform);
                var before = health.CurrentHP;
                var deadline = Time.time + 15f;
                var detour = 0f;
                while (health.CurrentHP == before && Time.time < deadline)
                {
                    detour = Mathf.Max(detour, Mathf.Abs(enemy.transform.position.x));
                    yield return null;
                }
                Assert.Less(health.CurrentHP, before, (useTree ? "BT" : "FSM") + " must navigate and deliver damage; navigation=" + enemy.Navigation.Status + "; position=" + enemy.transform.position);
                Assert.Greater(detour, 3f);
                var reactionPosition = enemy.transform.position;
                enemy.Navigation.MoveTo(enemy.transform.position + Vector3.right * 4f, 4f);
                enemy.NotifyHit(target.transform);
                yield return null;
                Assert.Less(Vector3.Distance(reactionPosition, enemy.transform.position), 0.03f, "Hit must cancel a command submitted in the same frame");
                enemy.Navigation.MoveTo(enemy.transform.position + Vector3.right * 4f, 4f);
                enemy.EnterBreakState();
                yield return null;
                Assert.Less(Vector3.Distance(reactionPosition, enemy.transform.position), 0.03f, "Break must cancel already submitted movement");
                enemy.Navigation.MoveTo(enemy.transform.position + Vector3.right * 4f, 4f);
                var enemyHealth = enemy.GetComponent<CharacterHealthBase>();
                enemyHealth.ReceiveDamage(100000f, target.transform);
                Assert.IsTrue(enemyHealth.IsDead);
                Assert.AreEqual(NavigationStatus.Idle, enemy.Navigation.Status, "Death cancels navigation synchronously");
                Assert.AreEqual(0, target.GetComponent<CombatPositionRing>().ReservationCount);
                yield return null;
                Assert.Less(Vector3.Distance(reactionPosition, enemy.transform.position), 0.03f, "Death must not apply stale movement in LateUpdate");
                enemy.enabled = false;
                Assert.AreEqual(0, target.GetComponent<CombatPositionRing>().ReservationCount);
                Object.Destroy(enemy.gameObject);
                Object.Destroy(config);
                Object.Destroy(target);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator NavigationAroundCornersIsConsistentAt30And60And120Hz()
        {
            Floor();
            Box("Wall", new Vector3(0f, 2f, 0f), new Vector3(5f, 4f, 0.6f));
            Bake(small);
            var fastest = float.PositiveInfinity;
            var slowest = 0f;
            foreach (var rate in new[] { 30, 60, 120 })
            {
                Time.captureDeltaTime = 1f / rate;
                var actor = Actor(small, new Vector3(0f, 0f, -6f));
                var start = Time.time;
                yield return Travel(actor, new Vector3(0f, 0f, 6f));
                fastest = Mathf.Min(fastest, Time.time - start);
                slowest = Mathf.Max(slowest, Time.time - start);
                actor.Cancel();
                Object.Destroy(actor.gameObject);
                yield return null;
            }
            Assert.Less(slowest - fastest, 0.35f, "Frame step must not materially change movement speed");
        }

        [UnityTest]
        public IEnumerator SavedPlaygroundRunsAllFiveStagesWithItsBakedData()
        {
            EditorSceneManager.LoadSceneInPlayMode(GameplayNavigationSetup.DemoScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var world = Object.FindObjectOfType<NavigationWorld>();
            Assert.NotNull(world);
            Assert.AreEqual(2, world.bakedData.Length);
            var wall = GameObject.Find("1 Static wall").GetComponent<ActorNavigation>();
            var smallWalker = GameObject.Find("2 Small fits").GetComponent<ActorNavigation>();
            var largeWalker = GameObject.Find("2 Large waits").GetComponent<ActorNavigation>();
            var passingA = GameObject.Find("3 Passing A").GetComponent<ActorNavigation>();
            var passingB = GameObject.Find("3 Passing B").GetComponent<ActorNavigation>();
            var doorWalker = GameObject.Find("4 Door traveller").GetComponent<ActorNavigation>();
            var ring = Object.FindObjectOfType<CombatPositionRing>();
            var wallPassed = false;
            var smallPassed = false;
            var aPassed = false;
            var bPassed = false;
            var doorPassed = false;
            Time.captureDeltaTime = 1f / 60f;
            var until = Time.time + 14f;
            while (Time.time < until)
            {
                wallPassed |= wall.transform.position.z > 2f;
                smallPassed |= smallWalker.transform.position.z > 2f;
                aPassed |= passingA.transform.position.z > 2f;
                bPassed |= passingB.transform.position.z < -2f;
                doorPassed |= doorWalker.transform.position.z > 2f;
                Assert.Less(largeWalker.transform.position.z, -1f, "Large body cannot cross the narrow gate");
                yield return null;
            }
            Assert.IsTrue(wallPassed && smallPassed && aPassed && bPassed && doorPassed,
                $"wall={wallPassed}, small={smallPassed}, passing={aPassed}/{bPassed}, door={doorPassed}");
            Assert.AreEqual(6, ring.ReservationCount);
        }

        [UnityTest]
        public IEnumerator DisableRestartAndOffMeshDisplacementNeverRestoreAnOldPathOrTeleport()
        {
            Floor();
            Bake(small);
            var actor = Actor(small, new Vector3(0f, 0f, -5f));
            var goal = new Vector3(0f, 0f, 5f);
            var until = Time.time + 0.5f;
            while (Time.time < until) { actor.MoveTo(goal, 3f); yield return null; }
            actor.enabled = false;
            var stopped = actor.transform.position;
            yield return Wait(0.25f);
            Assert.Less(Vector3.Distance(stopped, actor.transform.position), 0.01f);
            actor.enabled = true;
            yield return Wait(0.25f);
            Assert.Less(Vector3.Distance(stopped, actor.transform.position), 0.01f, "Enable alone must not resume stale pursuit");
            yield return Travel(actor, goal);
            actor.Cancel();
            var controller = actor.GetComponent<CharacterController>();
            controller.enabled = false;
            actor.transform.position = new Vector3(100f, 0f, 100f);
            controller.enabled = true;
            actor.MoveTo(goal, 3f);
            yield return null;
            Assert.AreEqual(NavigationStatus.OffNavMesh, actor.Status);
            Assert.AreEqual(new Vector3(100f, 0f, 100f), actor.transform.position);
        }
    }
}
