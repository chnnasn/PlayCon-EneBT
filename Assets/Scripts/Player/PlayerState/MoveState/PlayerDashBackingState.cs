using UnityEngine;
using PlayerState;

namespace FSM
{
    public class PlayerDashBackingState : PlayerMovementState
    {
        PlayerDashData dashData;
        public PlayerDashBackingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {
            dashData = playerMovementData.dashData;
        }
        //实现内部逻辑
        public override void Enter()
        {
            base.Enter();
            reusableDate.rotationTime = playerMovementData.dashData.rotationTime;

            reusableDate.dashCooldownEndTime = Time.time + playerMovementData.dashData.coldTime;
            //播放音效
            movementStateMachine.player.PlayDodgeSound();
        }
        public override void Update()
        {
            if (dashData.dodgeBackApplyRotation)
            {
                base.Update();
            }
        }
        #region Dash转到 Idle或Run
        public override void OnAnimationExitEvent()
        {
            if (PlayerInputSystem.MainInstance.PlayerMove == Vector2.zero)
            {
                movementStateMachine.ChangeState(movementStateMachine.idlingState);
                return;
            }
            movementStateMachine.ChangeState(movementStateMachine.runningState);
        }
        #endregion

    }
}
