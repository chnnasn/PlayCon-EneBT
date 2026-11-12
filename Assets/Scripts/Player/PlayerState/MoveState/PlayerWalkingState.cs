using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerState;
using UnityEngine.InputSystem;

namespace FSM
{
        public class PlayerWalkingState : PlayerMovementState
    {
        public PlayerWalkingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {
        }
        GameTimer gameTimer;

        public override void Enter()
        {
            base.Enter();
            reusableDate.rotationTime = playerMovementData.walkData.rotationTime;

            animator.SetBool(AnimID.HasInputID, true);

            reusableDate.inputMult = playerMovementData.walkData.inputMult;

        }
        public override void Update()
        {
            base.Update();
            
        }
        #region 转换Idling
        protected override void AddInputActionCallBacks()
        {
            base.AddInputActionCallBacks();

            inputActions.Player.Movement.canceled += OnBufferToIdle;

        }
        protected override void RemoveInputActionCallBacks()
        {
            base.RemoveInputActionCallBacks();
            Debug.Log("移除run到idle的委托");
            inputActions.Player.Movement.canceled -= OnBufferToIdle;
            inputActions.Player.Movement.started -= OnUnregisterBufferTimer;

        }

        private void OnBufferToIdle(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;
            CancelTimer(gameTimer);
            gameTimer = ScheduleTimer(playerMovementData.bufferToIdleTime, IdleStart);
            inputActions.Player.Movement.started -= OnUnregisterBufferTimer;
            inputActions.Player.Movement.started += OnUnregisterBufferTimer;
        }



        private void IdleStart()
        {
           inputActions.Player.Movement.started -= OnUnregisterBufferTimer;
            movementStateMachine.ChangeState(movementStateMachine.idlingState);
        }
        private void OnUnregisterBufferTimer(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;
            Debug.Log("注销Timer");
            CancelTimer(gameTimer);
        }
        #endregion

        #region 转换Running

        protected override void OnWalkStart(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;
            base.OnWalkStart(context);
         
            movementStateMachine.ChangeState(movementStateMachine.runningState);
        }
        #endregion
    }
}
