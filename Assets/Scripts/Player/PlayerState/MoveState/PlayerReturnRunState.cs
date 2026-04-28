using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerState;

namespace  FSM
{
    public class PlayerReturnRunState : PlayerMovementState
    {
        public PlayerReturnRunState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {
        }
        public override void Enter()
        {
            base.Enter();

            animator.SetBool(AnimID.TurnBackID, false);

            animator.SetBool(AnimID.HasInputID, true);

            reusableDate.inputMult = playerMovementData.returnRunData.inputMult;

            reusableDate.rotationTime =playerMovementData.returnRunData.rotationTime;   

        }
        public override void Update()
        {
            //留有一定转向空间
            if (animator.AnimationAtTag("TurnRun") && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.08f) { return; }
           
            CharacterRotation(GetPlayerMovementInputDirection());
       
        }
        public override void Exit()
        {
            base.Exit();

            animator.SetBool(AnimID.TurnBackID, false);
        }
        
        /// 实现状态的退出idle或Sprinting
        public override void OnAnimationExitEvent()
        {
            if (PlayerInputSystem.MainInstance.PlayerMove == Vector2.zero)
            {
                movementStateMachine.ChangeState(movementStateMachine.idlingState);
                return;
            }
            movementStateMachine.ChangeState(movementStateMachine.sprintingState);

        }
         

    }
}

