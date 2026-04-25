using UnityEngine;
public class EnemyHeath : CharacterHealthBase
{
    /// 敌人的伤害处理
    protected override void CharacterHitAction(float damage, string hitName, string parryName)
    {
        base.CharacterHitAction(damage, hitName, parryName);
        if (healthInfo.hasStrength.Value)//格挡
        {
            healthInfo.TakeStrength(damage);
            animator.CrossFadeInFixedTime(parryName, 0.1f, 0);
            
        }
        else//挨打
        {
         
            healthInfo.TakeDamage(damage);
            animator.CrossFadeInFixedTime(hitName, 0.1f, 0);
        }
        healthInfo.TakeDefenseValue(damage);
    }
    /// 处理敌人的破防逻辑   
    protected override void OnUpdateDefenseValue(float value)
    {
        base.OnUpdateDefenseValue(value);
        if (currentEnemy == null) { return; }
        if (value <= 0)
        {
            GameEventsManager.MainInstance.CallEvent("达到QTE条件", currentEnemy);
            healthInfo.ReDefenseValue();
            return;
        }
    }
}