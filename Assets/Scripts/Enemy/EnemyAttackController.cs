using UnityEngine;

public enum EnemyAttackKind
{
    Bite,
    Claw
}

/// <summary>Shared, interruptible attack execution for both AI architectures.</summary>
public sealed class EnemyAttackController
{
    private readonly Enemy enemy;
    private Transform victim;
    private EnemyAttackKind kind;
    private float elapsed;
    private float windup;
    private float duration;
    private float biteReadyAt;
    private float clawReadyAt;
    private bool hitAttempted;

    public bool IsActive { get; private set; }
    public int Version { get; private set; }

    public EnemyAttackController(Enemy owner) => enemy = owner;

    public bool CanStart(EnemyAttackKind attack)
    {
        if (IsActive || enemy.IsDead || enemy.IsInBreakState || enemy.IsReturningToLair ||
            !enemy.isActiveAndEnabled || enemy.Data == null || !enemy.HasChaseLock || !enemy.CanOccupyAttackPosition)
        {
            return false;
        }
        var readyAt = attack == EnemyAttackKind.Bite ? biteReadyAt : clawReadyAt;
        return Time.time >= readyAt && CanHit(enemy.ChaseTarget, attack);
    }

    public bool TryStart(EnemyAttackKind attack)
    {
        if (!CanStart(attack))
        {
            return false;
        }
        kind = attack;
        victim = enemy.ChaseTarget;
        elapsed = 0f;
        hitAttempted = false;
        var data = enemy.Data;
        windup = Mathf.Max(0f, attack == EnemyAttackKind.Bite ? data.biteWindup : data.clawWindup);
        duration = Mathf.Max(windup + 0.15f, attack == EnemyAttackKind.Bite ? data.biteDuration : data.clawDuration);
        IsActive = true;
        Version++;
        enemy.CrossFadeState(attack == EnemyAttackKind.Bite ? data.biteStateName : data.clawStateName,
            attack == EnemyAttackKind.Bite ? data.biteCrossFade : data.clawCrossFade);
        enemy.SetOffset(false);
        enemy.SetLock(data.lockValueIdle);
        enemy.SetMovement(0f);
        enemy.ResetMovementProgress();
        return true;
    }

    public void Tick()
    {
        if (!IsActive)
        {
            return;
        }
        if (enemy.IsDead || !enemy.isActiveAndEnabled || !EnemyControllerBase.IsValidTarget(victim))
        {
            Cancel();
            return;
        }
        elapsed += Time.deltaTime;
        enemy.SetMovement(0f);
        if (!hitAttempted)
        {
            // Tracking ends at the strike; recovery cannot rotate a missed hit back onto the player.
            var toTarget = victim.position - enemy.transform.position;
            toTarget.y = 0f;
            enemy.RotateTowards(toTarget, enemy.Data.combatRotationTime);
            if (elapsed >= windup)
            {
                hitAttempted = true;
                if (CanHit(victim, kind))
                {
                    var health = victim.GetComponentInParent<CharacterHealthBase>();
                    var damage = kind == EnemyAttackKind.Bite ? enemy.Data.biteDamage : enemy.Data.clawDamage;
                    health.ReceiveDamage(damage, enemy.transform, enemy.Data.playerHitStateName);
                }
            }
        }
        if (elapsed >= duration)
        {
            Cancel();
        }
    }

    public void Cancel()
    {
        if (IsActive)
        {
            var data = enemy.Data;
            if (kind == EnemyAttackKind.Bite)
                biteReadyAt = Time.time + Mathf.Max(0f, data.biteCooldown);
            else
                clawReadyAt = Time.time + Mathf.Max(0f, data.clawCooldown);
        }
        IsActive = false;
        victim = null;
        hitAttempted = false;
    }

    public void Reset()
    {
        Cancel();
        biteReadyAt = 0f;
        clawReadyAt = 0f;
    }

    private bool CanHit(Transform candidate, EnemyAttackKind attack)
    {
        if (!EnemyControllerBase.IsValidTarget(candidate) || !enemy.HasLineOfSight(candidate))
        {
            return false;
        }
        var data = enemy.Data;
        var delta = candidate.position - enemy.transform.position;
        if (Mathf.Abs(delta.y) > Mathf.Max(0f, data.attackHeightTolerance))
        {
            return false;
        }
        delta.y = 0f;
        var range = Mathf.Max(0f, attack == EnemyAttackKind.Bite ? data.biteRange : data.clawRange);
        return delta.sqrMagnitude <= range * range &&
               (delta.sqrMagnitude < 0.0001f || Vector3.Angle(enemy.transform.forward, delta) <= data.attackAngle * 0.5f);
    }
}
