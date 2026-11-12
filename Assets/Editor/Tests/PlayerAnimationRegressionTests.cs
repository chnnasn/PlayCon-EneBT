using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
    public class PlayerAnimationRegressionTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (!Application.isPlaying)
            {
                EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
                yield return new EnterPlayMode();
            }
            foreach (var navigation in Object.FindObjectsOfType<ActorNavigation>()) navigation.enabled = false;
            yield return null;
            foreach (var enemy in Object.FindObjectsOfType<Enemy>()) enemy.enabled = false;
            Object.FindObjectOfType<Player>().GetAnimator().enabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator OutgoingDodgeCannotEndTheNewSkill()
        {
            var player = Object.FindObjectOfType<Player>();
            player.OnAnimationEnterEvent(AnimationState.Dash);
            var dodge = player.CaptureAnimationBinding();
            player.comboStateMachine.ChangeState(player.comboStateMachine.SkillState);
            var skill = player.CaptureAnimationBinding();

            player.OnAnimationExitEvent(dodge);
            yield return GameplayTestWait.ForSeconds(0.25f);
            Assert.AreSame(player.comboStateMachine.SkillState, player.comboStateMachine.currentState.Value);
            Assert.AreSame(player.movementStateMachine.playerMovementNullState, player.movementStateMachine.currentState.Value);

            player.OnAnimationExitEvent(skill);
            Assert.AreSame(player.comboStateMachine.NullState, player.comboStateMachine.currentState.Value);
        }

        [UnityTest]
        public IEnumerator CancelledAttackCannotEndDodgeOrProduceAnotherHit()
        {
            var player = Object.FindObjectOfType<Player>();
            player.comboStateMachine.ReusableData.currentCombo = player.playerSO.ComboData.comboData.lightCombo;
            player.OnAnimationEnterEvent(AnimationState.ATK);
            var attack = player.CaptureAnimationBinding();
            player.OnAnimationEnterEvent(AnimationState.Dash);
            player.comboStateMachine.ReusableData.ATKIndex = 0;

            player.OnAnimationExitEvent(attack);
            player.ATK();
            yield return null;
            Assert.AreSame(player.movementStateMachine.dashingState, player.movementStateMachine.currentState.Value);
            Assert.AreSame(player.comboStateMachine.NullState, player.comboStateMachine.currentState.Value);
            Assert.AreEqual(0, player.comboStateMachine.ReusableData.ATKIndex, "A faded-out attack must not open a new hit window after cancellation.");
        }

        [UnityTest]
        public IEnumerator RestartedDodgeRejectsThePreviousAnimationExit()
        {
            var player = Object.FindObjectOfType<Player>();
            player.OnAnimationEnterEvent(AnimationState.Dash);
            var previous = player.CaptureAnimationBinding();
            player.OnAnimationEnterEvent(AnimationState.Dash);
            var current = player.CaptureAnimationBinding();
            Assert.AreNotEqual(previous.MovementRevision, current.MovementRevision);

            player.OnAnimationExitEvent(previous);
            yield return null;
            Assert.AreSame(player.movementStateMachine.dashingState, player.movementStateMachine.currentState.Value);
            player.OnAnimationExitEvent(current);
            Assert.AreSame(player.movementStateMachine.idlingState, player.movementStateMachine.currentState.Value);
        }
    }
}
