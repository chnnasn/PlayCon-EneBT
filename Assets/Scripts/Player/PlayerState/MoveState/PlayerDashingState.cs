using PlayerState;
using UnityEngine;

namespace FSM
{
    public class PlayerDashingState : PlayerMovementState
    {
        PlayerDashData dashData;

        public PlayerDashingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {
            dashData = playerMovementData.dashData;
        }
        //实现内部逻辑
        public override void Enter()
        {
            base.Enter();
            reusableDate.rotationTime = playerMovementData.dashData.rotationTime;
        
            reusableDate.canDash = false;
            TimerManager.MainInstance.GetOneTimer(playerMovementData.dashData.coldTime, ResetDash);
            //播放音效
            movementStateMachine.player.PlayDodgeSound();
        }
   
        public override void Update() 
        {
            base.Update();
        }
   
   
        #region Dash转到 Idle?Sprint
        public override void OnAnimationExitEvent()
        {
            if (PlayerInputSystem.MainInstance.PlayerMove == Vector2.zero)
            {
                movementStateMachine.ChangeState(movementStateMachine.idlingState);
                return;
            }
            movementStateMachine.ChangeState(movementStateMachine.sprintingState);
        }
        #endregion

    }
}

