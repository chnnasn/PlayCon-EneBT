using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerState;
using UnityEngine.InputSystem;

namespace FSM
{
    public class PlayerRunningState : PlayerMovementState
    {
        GameTimer gameTimer = null;
        public PlayerRunningState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {
        }
        public override void Enter()
        { 
            base.Enter();
            animator.CrossFadeInFixedTime("WalkStart", 0.14f);

            reusableDate.rotationTime = playerMovementData.runData.rotationTime;

            animator.SetBool(AnimID.HasInputID,true);

            reusableDate.inputMult = playerMovementData.runData.inputMult;
        
        }
        public override void Update()
        {
            base.Update();
            
        }
        #region 转换Idling
        protected override void AddInputActionCallBacks()
        {
            base.AddInputActionCallBacks();
            PlayerInputSystem.MainInstance.inputActions.Player.Movement.canceled += OnEnterIdle;
            PlayerInputSystem.MainInstance.inputActions.Player.Movement.started += OnKeepRunning;
        }

        protected override void RemoveInputActionCallBacks()
        {
            base.RemoveInputActionCallBacks();
            PlayerInputSystem.MainInstance.inputActions.Player.Movement.canceled -= OnEnterIdle;
            PlayerInputSystem.MainInstance.inputActions.Player.Movement.started -= OnKeepRunning;
        }

        private void OnEnterIdle(InputAction.CallbackContext context)
        {
            gameTimer = TimerManager.MainInstance.GetTimer(playerMovementData.bufferToIdleTime, StartToIdle);
        }

        private void StartToIdle()
        {
            movementStateMachine.ChangeState(movementStateMachine.idlingState);
        }
        private void OnKeepRunning(InputAction.CallbackContext context)
        {
            TimerManager.MainInstance.UnregisterTimer(gameTimer);
            animator.CrossFadeInFixedTime("WalkStart", 0.14f);
        }


        #endregion

        #region 转换Walking

        protected override void OnWalkStart(InputAction.CallbackContext context)
        {
            base.OnWalkStart(context);

            movementStateMachine.ChangeState(movementStateMachine.walkingState);
        }
        #endregion
    }
}
