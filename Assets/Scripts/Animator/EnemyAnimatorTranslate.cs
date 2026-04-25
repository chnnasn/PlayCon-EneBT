using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FSM;


public class EnemyAnimatorTranslate : StateMachineBehaviour
{
    Enemy enemy;

    [SerializeField] public  AnimationState onEnterAnimationState;
        
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
        if (onEnterAnimationState == AnimationState.Null)
        {
            return;
        }

        if (animator.TryGetComponent<Enemy>(out enemy))
        {
            enemy.OnAnimationEnterEvent(onEnterAnimationState);
        }
    }


    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
        if (animator.TryGetComponent<Enemy>(out enemy))
        {
            enemy.OnAnimationExitEvent();
        }
    }
    
    
    

}