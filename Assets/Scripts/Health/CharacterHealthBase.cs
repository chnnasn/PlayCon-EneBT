using System;
using Tools;
using UnityEngine;
using PlayerState;
using static UnityEngine.Rendering.DebugUI;

public enum HealthState
{
    TriggerDamage,
    GenerateDamage,
    BrokenDefense,
}

public class CharacterHealthBase : MonoBehaviour
{
    [SerializeField] private float currentHP;
    protected Transform currentEnemy;
    [SerializeField]protected CharacterHealthInfo characterHealthInfo;
    protected CharacterHealthInfo healthInfo;
    protected Animator animator;

    private Transform BarUI;
    protected virtual void Awake()
    {
        animator = GetComponent<Animator>();
        healthInfo=Instantiate(characterHealthInfo);

        healthInfo.currentHP.OnValueChanged += OnUpdatePH;
    }

 
    private void Start()
    {
        healthInfo.InitHealthData();
        
        BarUI = transform.Find("BarUI");
    }
    protected virtual void Update()
    {
        LookAtAttacker();
    }
    protected virtual void OnEnable()
    {
        if (GameEventsManager.MainInstance != null)
        {
            GameEventsManager.MainInstance.AddEventListening<float, string, string, Transform, Transform, CharacterComboBase>(HealthState.TriggerDamage.ToString(), OnCharacterHitEventHandler);
            GameEventsManager.MainInstance.AddEventListening<float>(HealthState.GenerateDamage.ToString(), OnCharacterDamageAction);
            GameEventsManager.MainInstance.AddEventListening<Transform>(HealthState.BrokenDefense.ToString(), OnBrokenDefenseEvent);
        }

    }

 

    protected virtual void OnDisable()
    {
        if (GameEventsManager.MainInstance != null)
        {
            GameEventsManager.MainInstance.ReMoveEvent<float, string, string, Transform, Transform, CharacterComboBase>(HealthState.TriggerDamage.ToString(), OnCharacterHitEventHandler);
            GameEventsManager.MainInstance.ReMoveEvent<float>(HealthState.GenerateDamage.ToString(), OnCharacterDamageAction);
            GameEventsManager.MainInstance.ReMoveEvent<Transform>(HealthState.BrokenDefense.ToString(), OnBrokenDefenseEvent);
        }

        if (healthInfo == null)
        {
            return;
        }

        healthInfo.currentHP.OnValueChanged -= OnUpdatePH;

    }

    private void OnBrokenDefenseEvent(Transform bearer)
    {
        if (bearer != transform)
        {
            return;
        }

        EnterBreakState();
    }

    protected virtual void EnterBreakState()
    {
        // 仅对敌人生效：进入破防状态
        if (TryGetComponent<Enemy>(out var enemy))
        {
            enemy.EnterBreakState();
        }
    }

    //事件注册
    private void OnCharacterHitEventHandler(float Damage, string HitName, string ParryName, Transform Attacker, Transform Bearer, CharacterComboBase characterCombo)
    {
      
        if (Bearer != this.transform) { return; }
        Damage *= GetDamageTakenMultiplier();
        SetEnemy(Attacker);
        CharacterHitAction(Damage, HitName, ParryName);
        OnCharacterDamageAction(Damage);
    }

 
    protected void OnCharacterDamageAction(float damage)
    {
        healthInfo.TakeDamage(damage);
    }

    protected virtual void CharacterHitAction(float damage, string hitName, string parryName)
    {
        
    }

    protected virtual float GetDamageTakenMultiplier()
    {
        if (TryGetComponent<Enemy>(out var enemy))
        {
            // 非破防：只吃 20% 伤害；破防：吃满额伤害。
            var baseMultiplier = 0.2f;
            var machine = enemy.stateMachine;
            if (machine != null && machine.currentState.Value == machine.breakState)
            {
                baseMultiplier = 1f;
            }

            return baseMultiplier * enemy.DamageTakenMultiplier;
        }

        return 1f;
    }

    protected virtual void SetEnemy(Transform attacker)
    {
        if (currentEnemy != attacker || currentEnemy == null)
        { 
            currentEnemy = attacker;    
        }
    }
    private void LookAtAttacker()
    {
        if (currentEnemy == null) { return; }
        if (animator.AnimationAtTag("Hit")&&animator.GetCurrentAnimatorStateInfo(0).normalizedTime<0.3f)
        {
            transform.LookAt(currentEnemy.position, 50);
        }
        
    
    }
    protected void SetHitFVX(Transform attacker,Transform hitter)
    { 
        Vector3 hitDir = (attacker.position- hitter.position).normalized;
        Vector3 targetPos = hitter.position + hitDir*0.8f+Vector3.up*1f;
    }

    private void OnUpdatePH(float value)
    {
        Debug.Log("敌人的血量为"+value);
        currentHP = value;
        if (value > 0)
        {
            healthInfo.onDead.Value = false;
            UIManager.MainInstance.EnemyBarUI.UpdateBlood(currentHP / healthInfo.maxHP);
            return;
        }
        healthInfo.onDead.Value = true;


    }

    private void LateUpdate()
    {
        UIManager.MainInstance.EnemyBarUI.ShowAt(BarUI.transform.position);
    }
}
