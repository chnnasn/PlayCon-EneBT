using UnityEngine;

public class EnemyHeath : CharacterHealthBase
{
    private Enemy enemy;

    protected override void Awake()
    {
        base.Awake();
        enemy = GetComponent<Enemy>();
    }

    private Enemy Owner => enemy != null ? enemy : (enemy = GetComponent<Enemy>());

    protected override float GetDamageTakenMultiplier()
    {
        var owner = Owner;
        return owner == null ? 1f : (owner.IsInBreakState ? 1f : 0.2f) * owner.DamageTakenMultiplier;
    }

    protected override void EnterBreakState()
    {
        Owner?.EnterBreakState();
    }

    protected override void CharacterHitAction(float damage, string hitName, string parryName)
    {
        // Damage is already applied; higher-priority reactions own the current animation.
        if (Owner != null && Owner.IsHitReactionDeferred)
            return;
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(hitName))
            return;
        var state = Animator.StringToHash(hitName);
        if (animator.HasState(0, state))
            animator.CrossFadeInFixedTime(state, 0.1f, 0);
    }

    protected override void OnCharacterHit(float damage, string hitName, string parryName, Transform attacker)
    {
        Owner?.NotifyHit(attacker);
    }

    protected override void OnRevived()
    {
        Owner?.ResetAIForRevive();
    }
}
