using PlayerState;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FSM
{
    public class PlayerIdlingState : PlayerMovementState
    {
        GameTimer GameTimer { get; set; }
        //调用父类的构造函数
        public PlayerIdlingState(PlayerMovementStateMachine playerMovementStateMachine) : base(playerMovementStateMachine)
        {

        }
        public override void Enter()
        {  
            base.Enter();
            reusableDate.rotationTime= playerMovementData.idleData.rotationTime;
            animator.SetBool(AnimID.HasInputID, false);
            reusableDate.inputMult = playerMovementData.idleData.inputMult;
            if (GetPlayerMovementInputDirection() != Vector2.zero)
                GameTimer = ScheduleTimer(0.11f, CheckMoveInput);

        }
        public override void Update()
        {
            base.Update();
        }
        protected override void AddInputActionCallBacks()
        {
            base.AddInputActionCallBacks();
            inputActions.Player.Movement.started += bufferToRun;
        }
        protected override void RemoveInputActionCallBacks() 
        {
            base.RemoveInputActionCallBacks();
            inputActions.Player.Movement.started -= bufferToRun;
            CancelTimer(GameTimer);
        }

        private void bufferToRun(InputAction.CallbackContext context)
        {
           if (!IsCurrentState) return;
           CancelTimer(GameTimer);
           GameTimer = ScheduleTimer(0.11f, CheckMoveInput);
        }

        private void CheckMoveInput()
        {
            
            if (PlayerInputSystem.MainInstance.PlayerMove == Vector2.zero)
            {
                 animator.CrossFadeInFixedTime("Run_Start_End",0.13f);
            }
            else
            {
                Move();
            }
        }

        private void Move()
        {
            if (movementStateMachine.reusableDate.shouldWalk)
            {
                //切换到Walk状态
                movementStateMachine.ChangeState(movementStateMachine.walkingState);
                return;
            }
            //否则执行Run移动
            movementStateMachine.ChangeState(movementStateMachine.runningState);
        }

        public override void HandInput()
        {
            base.HandInput();

        }
        public override void Exit()
        {
            base.Exit();
        }
    }
}
