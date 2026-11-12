using System;
using PlayerState;
using UnityEngine;

public enum HealthState
{
    TriggerDamage,
    GenerateDamage, // Legacy name only: damage without a recipient must not be broadcast to every character.
    BrokenDefense,
}

[DisallowMultipleComponent]
public class CharacterHealthBase : MonoBehaviour
{
    [SerializeField] private float currentHP;
    [SerializeField, Min(1f)] private float fallbackMaxHP = 100f;
    [SerializeField] protected CharacterHealthInfo characterHealthInfo;
    protected CharacterHealthInfo healthInfo;
    protected Transform currentEnemy;
    protected Animator animator;

    private bool wasDead;
    private bool healthSubscribed;
    private GameEventsManager eventManager;
    private UIManager barOwner;
    private StateBarUI healthBar;
    private Transform barAnchor;
    private float nextBarLookupTime;

    public event Action<float, float> HealthChanged;
    public event Action Died;
    public event Action Revived;

    public float CurrentHP { get { EnsureInitialized(); return healthInfo.currentHP.Value; } }
    public float MaxHP { get { EnsureInitialized(); return healthInfo.maxHP; } }
    public bool IsDead => CurrentHP <= 0f;
    protected virtual bool ShowWorldHealthBar => true;

    protected virtual void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (healthInfo != null)
            return;

        animator = GetComponent<Animator>();
        barAnchor = transform.Find("BarUI");
        healthInfo = characterHealthInfo != null
            ? Instantiate(characterHealthInfo)
            : ScriptableObject.CreateInstance<CharacterHealthInfo>();
        healthInfo.hideFlags = HideFlags.DontSave;
        healthInfo.InitHealthData(fallbackMaxHP);
        currentHP = healthInfo.currentHP.Value;
        wasDead = false;
    }

    protected virtual void OnEnable()
    {
        EnsureInitialized();
        if (!healthSubscribed)
        {
            healthInfo.currentHP.OnValueChanged += OnHealthValueChanged;
            healthSubscribed = true;
        }

        eventManager = GameEventsManager.MainInstance;
        if (eventManager != null)
        {
            eventManager.AddEventListening<float, string, string, Transform, Transform, CharacterComboBase>(HealthState.TriggerDamage.ToString(), OnCharacterHitEventHandler);
            eventManager.AddEventListening<Transform>(HealthState.BrokenDefense.ToString(), OnBrokenDefenseEvent);
        }

        nextBarLookupTime = 0f;
        OnHealthValueChanged(healthInfo.currentHP.Value);
    }

    protected virtual void OnDisable()
    {
        // Use the registered instance: a teardown must never create a new singleton.
        if (eventManager != null)
        {
            eventManager.ReMoveEvent<float, string, string, Transform, Transform, CharacterComboBase>(HealthState.TriggerDamage.ToString(), OnCharacterHitEventHandler);
            eventManager.ReMoveEvent<Transform>(HealthState.BrokenDefense.ToString(), OnBrokenDefenseEvent);
        }
        eventManager = null;

        if (healthInfo != null && healthSubscribed)
            healthInfo.currentHP.OnValueChanged -= OnHealthValueChanged;
        healthSubscribed = false;
        ReleaseHealthBar();
    }

    protected virtual void OnDestroy()
    {
        ReleaseHealthBar();
        if (healthInfo != null)
            Destroy(healthInfo);
    }

    /// <summary>Apply one targeted hit. Returns false for dead, disabled or invalid recipients.</summary>
    public bool ReceiveDamage(float damage, Transform attacker = null, string hitName = null)
    {
        return ApplyDamage(damage, attacker, hitName, null);
    }

    private bool ApplyDamage(float damage, Transform attacker, string hitName, string parryName)
    {
        if (!isActiveAndEnabled || IsDead || !IsPositiveFinite(damage))
            return false;

        var finalDamage = damage * GetDamageTakenMultiplier();
        if (!IsPositiveFinite(finalDamage))
            return false;

        currentEnemy = attacker;
        healthInfo.TakeDamage(finalDamage);
        // Lethal hits enter death directly; their hit animation must not overwrite death.
        if (!IsDead)
        {
            CharacterHitAction(finalDamage, hitName, parryName);
            OnCharacterHit(finalDamage, hitName, parryName, attacker);
        }
        return true;
    }

    public void Revive()
    {
        EnsureInitialized();
        currentEnemy = null;
        healthInfo.RestoreFullHealth();
        if (!healthSubscribed)
            OnHealthValueChanged(healthInfo.currentHP.Value);
    }

    public void Heal(float amount)
    {
        if (!isActiveAndEnabled || IsDead || !IsPositiveFinite(amount))
            return;
        healthInfo.RePH(amount);
    }

    private static bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private bool IsRecipient(Transform recipient)
    {
        return recipient != null && recipient.GetComponentInParent<CharacterHealthBase>() == this;
    }

    private void OnCharacterHitEventHandler(float damage, string hitName, string parryName, Transform attacker, Transform bearer, CharacterComboBase characterCombo)
    {
        if (IsRecipient(bearer))
            ApplyDamage(damage, attacker, hitName, parryName);
    }

    private void OnBrokenDefenseEvent(Transform bearer)
    {
        if (isActiveAndEnabled && !IsDead && IsRecipient(bearer))
            EnterBreakState();
    }

    protected virtual void EnterBreakState() { }
    protected virtual float GetDamageTakenMultiplier() => 1f;
    protected virtual void CharacterHitAction(float damage, string hitName, string parryName) { }
    protected virtual void OnCharacterHit(float damage, string hitName, string parryName, Transform attacker) { }
    protected virtual void OnDied() { }
    protected virtual void OnRevived() { }

    private void OnHealthValueChanged(float value)
    {
        currentHP = value;
        var dead = value <= 0f;
        healthInfo.onDead.Value = dead;
        if (healthBar != null)
        {
            healthBar.UpdateBlood(value / MaxHP);
            healthBar.SetVisible(!dead);
        }
        if (dead == wasDead)
        {
            HealthChanged?.Invoke(currentHP, MaxHP);
            return;
        }
        wasDead = dead;
        if (dead)
        {
            currentEnemy = null;
            OnDied();
            Died?.Invoke();
        }
        else
        {
            OnRevived();
            Revived?.Invoke();
        }
        HealthChanged?.Invoke(currentHP, MaxHP);
    }

    protected virtual void Update()
    {
        if (IsDead || currentEnemy == null || animator == null || animator.runtimeAnimatorController == null)
            return;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (!state.IsTag("Hit") || state.normalizedTime >= 0.3f)
            return;
        var direction = currentEnemy.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 720f * Time.deltaTime);
    }

    protected virtual void LateUpdate()
    {
        if (!ShowWorldHealthBar)
            return;
        if (healthBar == null && !IsDead && Time.unscaledTime >= nextBarLookupTime)
        {
            nextBarLookupTime = Time.unscaledTime + 1f;
            if (UIManager.TryGetExistingInstance(out var manager))
            {
                barOwner = manager;
                healthBar = manager.GetOrCreateEnemyHealthBar(this);
                if (healthBar != null)
                    healthBar.UpdateBlood(CurrentHP / MaxHP, true);
            }
        }
        if (healthBar == null)
            return;
        if (IsDead)
        {
            healthBar.SetVisible(false);
            return;
        }
        healthBar.ShowAt(barAnchor != null ? barAnchor.position : transform.position + Vector3.up * 2f);
    }

    private void ReleaseHealthBar()
    {
        if (barOwner != null)
            barOwner.ReleaseEnemyHealthBar(this);
        healthBar = null;
        barOwner = null;
    }
}
