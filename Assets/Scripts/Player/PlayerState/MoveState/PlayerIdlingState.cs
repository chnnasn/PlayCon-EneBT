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

        }
        public override void Update()
        {
            base.Update();
        }
        protected override void AddInputActionCallBacks()
        {
            base.AddInputActionCallBacks();
            PlayerInputSystem.MainInstance.inputActions.Player.Movement.started += bufferToRun;
        }
        protected override void RemoveInputActionCallBacks() 
        {
            base.RemoveInputActionCallBacks();
            PlayerInputSystem.MainInstance.inputActions.Player.Movement.started -= bufferToRun;
            TimerManager.MainInstance.UnregisterTimer(GameTimer);
        }

        private void bufferToRun(InputAction.CallbackContext context)
        {          
          GameTimer=TimerManager.MainInstance.GetTimer(0.11f,CheckMoveInput);
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
