using System;
using UnityEngine;
using Tools;

public static class ExpandAnimator
{

    /// 旋转Y轴，让Forward指向目标
    public static void LookAt(this Transform transform, Vector3 target,float timer)
    {
        var direction = (target - transform.position).normalized;
        direction.y = 0f;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation,lookRotation,MathToos.UnTetheredLerp(timer));
    }
        
    /// 判断当前动画片段是否是指定Tag
    public static bool AnimationAtTag(this Animator animator, string tagName,int indexLayer = 0)
    {
        return animator.GetCurrentAnimatorStateInfo(indexLayer).IsTag(tagName);
           
          
    }
}

