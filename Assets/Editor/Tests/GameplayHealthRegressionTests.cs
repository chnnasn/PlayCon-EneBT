using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Gameplay.Tests
{
public class GameplayHealthRegressionTests
{
    private Player player;
    private PlayerHealth playerHealth;
    private Enemy sourceEnemy;
    private GameObject clonedEnemy;

    [UnitySetUp]
    public IEnumerator EnterGameplayScene()
    {
        if (!Application.isPlaying)
        {
            EditorSceneManager.OpenScene(GameplaySetup.ScenePath);
            yield return new EnterPlayMode();
        }
            foreach (var navigation in Object.FindObjectsOfType<ActorNavigation>()) navigation.enabled = false;

        // Health tests must not race natural attacks or enemy movement.
        foreach (var enemy in Object.FindObjectsOfType<Enemy>())
            enemy.enabled = false;
        yield return null;

        player = Object.FindObjectOfType<Player>();
        sourceEnemy = Object.FindObjectOfType<Enemy>();
        Assert.That(player, Is.Not.Null, "The configured gameplay scene must contain a player.");
        Assert.That(sourceEnemy, Is.Not.Null, "The gameplay scene must contain an enemy for isolation tests.");
        playerHealth = player.GetComponent<PlayerHealth>();
        Assert.That(playerHealth, Is.Not.Null, "Run GameplaySetup before the regression suite.");
    }

    [UnityTearDown]
    public IEnumerator ExitGameplayScene()
    {
        if (Application.isPlaying)
        {
            if (clonedEnemy != null)
            {
                Object.Destroy(clonedEnemy);
                yield return null;
            }
            yield return new ExitPlayMode();
        }
    }

    [UnityTest]
    public IEnumerator PlayerStartsAliveWithDefaultHealthAndRejectsInvalidDamage()
    {
        Assert.That(playerHealth.MaxHP, Is.EqualTo(100f));
        Assert.That(playerHealth.CurrentHP, Is.EqualTo(playerHealth.MaxHP));
        Assert.That(playerHealth.IsDead, Is.False);
        Assert.That(player.IsDead, Is.False);
        Assert.That(player.CanAcceptInput, Is.True);

        Assert.That(playerHealth.ReceiveDamage(0f), Is.False);
        Assert.That(playerHealth.ReceiveDamage(-1f), Is.False);
        Assert.That(playerHealth.ReceiveDamage(float.NaN), Is.False);
        Assert.That(playerHealth.ReceiveDamage(float.PositiveInfinity), Is.False);
        Assert.That(playerHealth.CurrentHP, Is.EqualTo(playerHealth.MaxHP));
        yield break;
    }

    [UnityTest]
    public IEnumerator DisableReenableRestoresHealthNotificationsWithoutDuplicateListeners()
    {
        var notifications = 0;
        var lastReportedHP = -1f;
        var lastReportedMaximum = -1f;
        playerHealth.HealthChanged += (hp, maximum) =>
        {
            notifications++;
            lastReportedHP = hp;
            lastReportedMaximum = maximum;
        };

        var damage = playerHealth.MaxHP * 0.1f;
        Assert.That(playerHealth.ReceiveDamage(damage), Is.True);
        Assert.That(notifications, Is.EqualTo(1));
        var hpBeforeDisable = playerHealth.CurrentHP;

        playerHealth.enabled = false;
        Assert.That(playerHealth.ReceiveDamage(damage), Is.False);
        Assert.That(playerHealth.CurrentHP, Is.EqualTo(hpBeforeDisable));
        playerHealth.enabled = true;
        playerHealth.enabled = false;
        playerHealth.enabled = true;

        // Re-enable publishes a snapshot; only the following hit should add one notification.
        var notificationsBeforeHit = notifications;
        Assert.That(playerHealth.ReceiveDamage(damage), Is.True);
        Assert.That(notifications, Is.EqualTo(notificationsBeforeHit + 1));
        Assert.That(playerHealth.CurrentHP, Is.EqualTo(hpBeforeDisable - damage).Within(0.001f));
        Assert.That(lastReportedHP, Is.EqualTo(playerHealth.CurrentHP));
        Assert.That(lastReportedMaximum, Is.EqualTo(playerHealth.MaxHP));
        yield break;
    }

    [UnityTest]
    public IEnumerator LethalDamageNotifiesOnceAndReviveRestartsBothPlayerStateMachines()
    {
        var deaths = 0;
        var revivals = 0;
        playerHealth.Died += () => deaths++;
        playerHealth.Revived += () => revivals++;

        Assert.That(playerHealth.ReceiveDamage(playerHealth.MaxHP), Is.True);
        Assert.That(playerHealth.CurrentHP, Is.Zero);
        Assert.That(playerHealth.IsDead, Is.True);
        Assert.That(player.IsDead, Is.True);
        Assert.That(player.CanAcceptInput, Is.False);
        Assert.That(player.movementStateMachine.IsRunning, Is.False);
        Assert.That(player.comboStateMachine.IsRunning, Is.False);
        Assert.That(deaths, Is.EqualTo(1));

        Assert.That(playerHealth.ReceiveDamage(1f, sourceEnemy.transform, "MissingHitState"), Is.False);
        Assert.That(playerHealth.CurrentHP, Is.Zero);
        Assert.That(deaths, Is.EqualTo(1));

        playerHealth.Revive();
        Assert.That(revivals, Is.EqualTo(1));
        Assert.That(playerHealth.CurrentHP, Is.EqualTo(playerHealth.MaxHP));
        Assert.That(playerHealth.IsDead, Is.False);
        Assert.That(player.IsDead, Is.False);
        AssertPlayerControlRestarted();

        playerHealth.Revive();
        Assert.That(revivals, Is.EqualTo(1), "Restoring an already living player must not emit a second revival.");
        yield break;
    }

    [UnityTest]
    public IEnumerator RevivalWhileDisabledWaitsForPlayerEnableBeforeRestartingControl()
    {
        Assert.That(playerHealth.ReceiveDamage(playerHealth.MaxHP), Is.True);
        player.enabled = false;
        playerHealth.enabled = false;

        playerHealth.Revive();
        Assert.That(playerHealth.IsDead, Is.False);
        Assert.That(player.IsDead, Is.False);
        Assert.That(player.movementStateMachine.IsRunning, Is.False);
        Assert.That(player.comboStateMachine.IsRunning, Is.False);
        Assert.That(player.CanAcceptInput, Is.False);

        playerHealth.enabled = true;
        player.enabled = true;
        AssertPlayerControlRestarted();
        var beforeHit = playerHealth.CurrentHP;
        Assert.That(playerHealth.ReceiveDamage(playerHealth.MaxHP * 0.1f), Is.True);
        Assert.That(playerHealth.CurrentHP, Is.LessThan(beforeHit));
        yield break;
    }

    [UnityTest]
    public IEnumerator EnemyHealthAndViewsAreIndependentAndDisableReleasesOnlyItsOwnView()
    {
        Assert.That(sourceEnemy.enabled, Is.False);
        var firstHealth = sourceEnemy.GetComponent<EnemyHeath>();
        Assert.That(firstHealth, Is.Not.Null);
        clonedEnemy = Object.Instantiate(sourceEnemy.gameObject, sourceEnemy.transform.position + Vector3.right * 3f, sourceEnemy.transform.rotation);
        clonedEnemy.name = "Health regression enemy";
        var secondEnemy = clonedEnemy.GetComponent<Enemy>();
        var secondHealth = clonedEnemy.GetComponent<EnemyHeath>();
        Assert.That(secondEnemy.enabled, Is.False, "The cloned AI must remain disabled throughout the health test.");
        Assert.That(secondHealth, Is.Not.Null);
        yield return null;
        yield return null;

        Assert.That(UIManager.TryGetExistingInstance(out var ui), Is.True);
        Assert.That(ui.EnemyBarUI, Is.Not.Null);
        Assert.That(ui.EnemyBarUI.gameObject.activeSelf, Is.False, "The authored blood bar is a hidden template.");
        var firstView = ui.GetOrCreateEnemyHealthBar(firstHealth);
        var secondView = ui.GetOrCreateEnemyHealthBar(secondHealth);
        Assert.That(firstView, Is.Not.Null);
        Assert.That(secondView, Is.Not.Null);
        Assert.That(firstView, Is.Not.SameAs(secondView));
        Assert.That(firstView, Is.Not.SameAs(ui.EnemyBarUI));
        Assert.That(secondView, Is.Not.SameAs(ui.EnemyBarUI));
        var firstFill = GetHealthFill(firstView);
        var secondFill = GetHealthFill(secondView);
        Assert.That(firstFill, Is.Not.SameAs(secondFill));

        var secondHP = secondHealth.CurrentHP;
        var secondDisplayedHealth = secondFill.fillAmount;
        var firstHP = firstHealth.CurrentHP;
        Assert.That(firstHealth.ReceiveDamage(firstHealth.MaxHP * 0.1f), Is.True);
        Assert.That(firstHealth.CurrentHP, Is.LessThan(firstHP));
        Assert.That(secondHealth.CurrentHP, Is.EqualTo(secondHP));
        Assert.That(firstFill.fillAmount, Is.EqualTo(firstHealth.CurrentHP / firstHealth.MaxHP).Within(0.001f));
        Assert.That(secondFill.fillAmount, Is.EqualTo(secondDisplayedHealth));

        var retainedHP = firstHealth.CurrentHP;
        firstHealth.enabled = false;
        yield return null;
        Assert.That(firstView == null, Is.True, "Disabling health must destroy only that enemy's runtime view.");
        Assert.That(secondView != null, Is.True);
        Assert.That(ui.GetOrCreateEnemyHealthBar(secondHealth), Is.SameAs(secondView));
        Assert.That(secondHealth.CurrentHP, Is.EqualTo(secondHP));

        firstHealth.enabled = true;
        yield return null;
        yield return null;
        var restoredView = ui.GetOrCreateEnemyHealthBar(firstHealth);
        Assert.That(restoredView, Is.Not.Null);
        Assert.That(restoredView, Is.Not.SameAs(secondView));
        Assert.That(firstHealth.CurrentHP, Is.EqualTo(retainedHP));
        Assert.That(GetHealthFill(restoredView).fillAmount, Is.EqualTo(retainedHP / firstHealth.MaxHP).Within(0.001f));
        Assert.That(ui.GetOrCreateEnemyHealthBar(secondHealth), Is.SameAs(secondView));
    }

    [UnityTest]
    public IEnumerator TargetedDamageAndBreakEventsRespectRecipientAndSubscriptionLifecycle()
    {
        var firstHealth = sourceEnemy.GetComponent<EnemyHeath>();
        Assert.That(firstHealth, Is.Not.Null);
        clonedEnemy = Object.Instantiate(sourceEnemy.gameObject, sourceEnemy.transform.position + Vector3.right * 3f, sourceEnemy.transform.rotation);
        clonedEnemy.name = "Targeted event regression enemy";
        var secondEnemy = clonedEnemy.GetComponent<Enemy>();
        var secondHealth = clonedEnemy.GetComponent<EnemyHeath>();
        Assert.That(secondHealth, Is.Not.Null);
        Assert.That(sourceEnemy.enabled, Is.False);
        Assert.That(secondEnemy.enabled, Is.False);
        Assert.That(GameEventsManager.TryGetExistingInstance(out var events), Is.True);

        var rawDamage = firstHealth.MaxHP * 0.1f;
        var secondHP = secondHealth.CurrentHP;
        var beforeFirstEvent = firstHealth.CurrentHP;
        SendTargetedDamage(events, rawDamage, sourceEnemy.transform);
        var damagePerEvent = beforeFirstEvent - firstHealth.CurrentHP;
        Assert.That(damagePerEvent, Is.GreaterThan(0f));
        Assert.That(secondHealth.CurrentHP, Is.EqualTo(secondHP));

        firstHealth.enabled = false;
        var disabledHP = firstHealth.CurrentHP;
        SendTargetedDamage(events, rawDamage, sourceEnemy.transform);
        Assert.That(firstHealth.CurrentHP, Is.EqualTo(disabledHP));
        Assert.That(secondHealth.CurrentHP, Is.EqualTo(secondHP));

        firstHealth.enabled = true;
        firstHealth.enabled = false;
        firstHealth.enabled = true;
        var beforeReenabledEvent = firstHealth.CurrentHP;
        SendTargetedDamage(events, rawDamage, sourceEnemy.transform);
        Assert.That(beforeReenabledEvent - firstHealth.CurrentHP, Is.EqualTo(damagePerEvent).Within(0.001f),
            "Repeated enabling must register exactly one damage listener, independent of the enemy's configured multiplier.");
        Assert.That(secondHealth.CurrentHP, Is.EqualTo(secondHP));

        // Arrange an eligible reaction state without yielding to natural AI or changing the shared EnemySO.
        SetRuntimeFsmIdle(sourceEnemy);
        SetRuntimeFsmIdle(secondEnemy);
        try
        {
            firstHealth.enabled = false;
            events.CallEvent(HealthState.BrokenDefense.ToString(), sourceEnemy.transform);
            Assert.That(sourceEnemy.IsInBreakState, Is.False);
            Assert.That(secondEnemy.IsInBreakState, Is.False);

            firstHealth.enabled = true;
            events.CallEvent(HealthState.BrokenDefense.ToString(), sourceEnemy.transform);
            Assert.That(sourceEnemy.IsInBreakState, Is.True);
            Assert.That(secondEnemy.IsInBreakState, Is.False, "The other listener must ignore a break addressed to the first enemy.");
            Assert.That(secondHealth.CurrentHP, Is.EqualTo(secondHP));

            events.CallEvent(HealthState.BrokenDefense.ToString(), secondEnemy.transform);
            Assert.That(secondEnemy.IsInBreakState, Is.True);
            Assert.That(sourceEnemy.IsInBreakState, Is.True);
        }
        finally
        {
            sourceEnemy.enabled = false;
            secondEnemy.enabled = false;
        }
        yield break;
    }

    private void SendTargetedDamage(GameEventsManager events, float damage, Transform recipient)
    {
        events.CallEvent<float, string, string, Transform, Transform, PlayerState.CharacterComboBase>(
            HealthState.TriggerDamage.ToString(), damage, string.Empty, string.Empty, player.transform, recipient, null);
    }

    private static void SetRuntimeFsmIdle(Enemy enemy)
    {
        var serializedEnemy = new SerializedObject(enemy);
        serializedEnemy.FindProperty("useBehaviorTree").boolValue = false;
        serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
        enemy.enabled = true;
        enemy.stateMachine.Start(enemy.stateMachine.idleState);
    }

    private void AssertPlayerControlRestarted()
    {
        Assert.That(player.CanAcceptInput, Is.True);
        Assert.That(player.movementStateMachine.IsRunning, Is.True);
        Assert.That(player.comboStateMachine.IsRunning, Is.True);
        Assert.That(player.movementStateMachine.currentState.Value, Is.SameAs(player.movementStateMachine.idlingState));
        Assert.That(player.comboStateMachine.currentState.Value, Is.SameAs(player.comboStateMachine.NullState));
    }

    private static Image GetHealthFill(StateBarUI view)
    {
        var serializedView = new SerializedObject(view);
        var fill = serializedView.FindProperty("GreenBloodBar").objectReferenceValue as Image;
        Assert.That(fill, Is.Not.Null, "Each cloned bar must retain its own green fill image reference.");
        return fill;
    }
}

}
