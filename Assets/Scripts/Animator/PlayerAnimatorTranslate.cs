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
    Null,
    Hit,
}

public class PlayerAnimatorTranslate : StateMachineBehaviour
{
    Player player;
    private readonly Queue<Player.AnimationStateBinding> entries = new Queue<Player.AnimationStateBinding>();
    private int animationSession = -1;

    [SerializeField] public  AnimationState onEnterAnimationState;
        
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
        if (layerIndex == 0 && animator.TryGetComponent<Player>(out player) && player.CanAcceptInput)
        {
            if (animationSession != player.AnimationSession)
            {
                entries.Clear();
                animationSession = player.AnimationSession;
            }
            player.OnAnimationEnterEvent(onEnterAnimationState);
            entries.Enqueue(player.CaptureAnimationBinding());
        }
    }


    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        
        if (layerIndex == 0 && animator.TryGetComponent<Player>(out player) &&
            animationSession == player.AnimationSession && entries.Count > 0)
        {
            player.OnAnimationExitEvent(entries.Dequeue());
        }
    }
    
    
    

}
