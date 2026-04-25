using UnityEngine;
using PlayerState;
using Tools;
using UnityEngine.InputSystem;

namespace FSM
{
    public class PlayerSprintingState : PlayerMovementState
    {
        GameTimer gameTimer;
        Vector3 targetDir;
        float turnDeltaAngle;
        public PlayerSprintingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {
        }
        public override void Enter()
        {
            base.Enter();
            reusableDate.rotationTime = playerMovementData.sprintData.rotationTime;

            animator.SetBool(AnimID.HasInputID, true);

            reusableDate.inputMult = playerMovementData.sprintData.inputMult;


        }
        public override void Update()
        {
            base.Update();
            targetDir = Quaternion.Euler(0, reusableDate.targetAngle, 0) * Vector3.forward;
            turnDeltaAngle = MathToos.GetDeltaAngle(playerTransform, targetDir);

            if (Mathf.Abs(turnDeltaAngle) > playerMovementData.turnBackAngle)
            {
                animator.SetBool(AnimID.TurnBackID, true);
              
            }
           
           
        }
        public override void Exit()
        {
            base.Exit();
           
        }
        #region 转换Idling
        protected override void AddInputActionCallBacks()
        {
            base.AddInputActionCallBacks();

            PlayerInputSystem.MainInstance.inputActions.Player.Movement.canceled += OnBufferToIdle;

        }
        protected override void RemoveInputActionCallBacks()
        {
            base.RemoveInputActionCallBacks();
            Debug.Log("移除run到idle的委托");
            PlayerInputSystem.MainInstance.inputActions.Player.Movement.canceled -= OnBufferToIdle;
            PlayerInputSystem.MainInstance.inputActions.Player.Movement.started -= OnUnregisterBufferTimer;

        }

        private void OnBufferToIdle(InputAction.CallbackContext context)
        {
            gameTimer = TimerManager.MainInstance.GetTimer(playerMovementData.bufferToIdleTime, IdleStart);
            PlayerInputSystem.MainInstance.inputActions.Player.Movement.started += OnUnregisterBufferTimer;
        }



        private void IdleStart()
        {           
            movementStateMachine.ChangeState(movementStateMachine.idlingState);          
        }
        private void OnUnregisterBufferTimer(InputAction.CallbackContext context)
        {
            Debug.Log("注销Timer");
            TimerManager.MainInstance.UnregisterTimer(gameTimer);
        }
        #endregion

        #region 转换到walk
        protected override void OnWalkStart(InputAction.CallbackContext context)
        {
            base.OnWalkStart(context);
            movementStateMachine.ChangeState(movementStateMachine.walkingState);
        }
        #endregion
    }
}