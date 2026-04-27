using UnityEngine;
public class EnemyHeath : CharacterHealthBase
{
    /// 敌人的伤害处理
    protected override void CharacterHitAction(float damage, string hitName, string parryName)
    {
        base.CharacterHitAction(damage, hitName, parryName);
        // 韧性/防御已移除：受击只做表现，实际扣血在基类事件链路里统一处理。
        animator.CrossFadeInFixedTime(hitName, 0.1f, 0);
    }
}