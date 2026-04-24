using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FSM;

public  enum AnimationState
{
    Idle,
    Walk,
    Run,
    Dash,
    DashBack,
    TurnBack,
    ATK,
    Null
}

public class AnimatorTranslate : StateMachineBehaviour
{
    Player player;

    [SerializeField] public  AnimationState onEnterAnimationState;
        
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
        if (onEnterAnimationState == AnimationState.Null)
        {
            return;
        }

        if (animator.TryGetComponent<Player>(out player))
        {
            player.OnAnimationEnterEvent(onEnterAnimationState);
        }
    }


    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
        if (animator.TryGetComponent<Player>(out player))
        {
            player.OnAnimationExitEvent();
        }
    }


}