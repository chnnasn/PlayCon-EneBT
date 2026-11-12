using Tools;
using System;
using FSM;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Mesh;
using System.Collections.Generic;

namespace PlayerState
{
    public class PlayerMovementState : IState
    {
        protected PlayerMovementStateMachine movementStateMachine { get; }
        protected Animator animator { get; }
        protected Transform playerTransform { get; }
        protected PlayerMovementData playerMovementData { get; }
        protected PlayerStateReusableDate reusableDate { get; }
        private readonly List<GameTimer> ownedTimers = new List<GameTimer>();
        protected CharacterInput inputActions;
        protected bool IsCurrentState => movementStateMachine.IsRunning &&
            ReferenceEquals(movementStateMachine.currentState.Value, this) &&
            movementStateMachine.player.CanAcceptInput;

        public PlayerMovementState(PlayerMovementStateMachine playerMovementStateMachine)
        {
            movementStateMachine=playerMovementStateMachine;
            if (playerTransform == null)
            {
                playerTransform = movementStateMachine.player.transform;
            }
            if (animator == null)
            {
                animator = movementStateMachine.player.characterAnimator;
            }
            if (playerMovementData == null)
            {
                playerMovementData = movementStateMachine.player.playerSO.movementData;
            }
            if (reusableDate == null)
            {
                reusableDate=movementStateMachine.reusableDate;
            }
           
        }
        public virtual void Enter()
        {    
            AddInputActionCallBacks();
        }

       
        public virtual void Exit()
        {
            RemoveInputActionCallBacks();
            if (TimerManager.TryGetExistingInstance(out var timerManager))
            {
                foreach (var timer in ownedTimers) timerManager.UnregisterTimer(timer);
            }
            ownedTimers.Clear();
        }

        public virtual void HandInput()
        {
            //不同的动画
            animator.SetFloat(AnimID.MovementID,
                GetPlayerMovementInputDirection().magnitude * reusableDate.inputMult, 0.35f, Time.deltaTime);
        }

        protected GameTimer ScheduleTimer(float duration, Action action)
        {
            GameTimer timer = null;
            timer = TimerManager.MainInstance.GetTimer(duration, () =>
            {
                ownedTimers.Remove(timer);
                if (IsCurrentState) action();
            });
            ownedTimers.Add(timer);
            return timer;
        }

        protected void CancelTimer(GameTimer timer)
        {
            if (TimerManager.TryGetExistingInstance(out var timerManager)) timerManager.UnregisterTimer(timer);
            ownedTimers.Remove(timer);
        }
        
        public virtual void Update()
        {
            CharacterRotation(GetPlayerMovementInputDirection());

        }
        public virtual void OnAnimationTranslateEvent(IState state)
        {
            movementStateMachine.ChangeState(state);
        }
        public virtual void OnAnimationExitEvent()
        {
            
        }
        protected Vector2 GetPlayerMovementInputDirection()
        {
            return movementStateMachine.player.CanAcceptInput ? PlayerInputSystem.MainInstance.PlayerMove : Vector2.zero;

        }

        float currentVelocity=0;
        protected void CharacterRotation(Vector2 movementDirection)
        {

            if (GetPlayerMovementInputDirection() == Vector2.zero) { return; }

            var cameraYaw = movementStateMachine.player.camera != null ? movementStateMachine.player.camera.eulerAngles.y : 0f;
            reusableDate.targetAngle= Mathf.Atan2(movementDirection.x, movementDirection.y) *Mathf.Rad2Deg + cameraYaw;

            movementStateMachine.player.transform.eulerAngles = Vector3.up * Mathf.SmoothDampAngle(movementStateMachine.player.transform.eulerAngles.y, reusableDate.targetAngle, ref currentVelocity, reusableDate.rotationTime);
            
        }
        #region 输入回调
        protected virtual void AddInputActionCallBacks()
        {
            inputActions = PlayerInputSystem.MainInstance?.inputActions;
            if (inputActions == null) return;
            //角色walk委托  
            inputActions.Player.Walk.started += OnWalkStart;
            inputActions.Player.Run.started += OnDashStart;
            inputActions.Player.Movement.canceled += OnMovementCanceled;
            inputActions.Player.Movement.performed += OnMovementPerformed;
            inputActions.Player.CameraLook.started += OnMouseMovementStarted;
        }

       

        protected virtual void RemoveInputActionCallBacks()
        { 
            if (inputActions == null) return;
            inputActions.Player.Walk.started -= OnWalkStart;
            inputActions.Player.Run.started -= OnDashStart;
            inputActions.Player.Movement.canceled -= OnMovementCanceled;
            inputActions.Player.Movement.performed -= OnMovementPerformed;
            inputActions.Player.CameraLook.started -= OnMouseMovementStarted;
        }
        #endregion
        protected virtual void OnWalkStart(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;
            Debug.Log(movementStateMachine.reusableDate.shouldWalk);
            //为什么要用bool？ 确保在idle状态记录walk的切换
            reusableDate.shouldWalk = !reusableDate.shouldWalk; ;
        }
        
        private void OnDashStart(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;
            if (movementStateMachine.player.comboStateMachine.currentState.Value == movementStateMachine.player.comboStateMachine.SkillState) { return; }
            if (reusableDate.canDash)
            {
                Debug.Log("进入闪避状态");
                if (PlayerInputSystem.MainInstance.PlayerMove != Vector2.zero)
                {
                    animator.CrossFadeInFixedTime(playerMovementData. dashData.frontDushAnimationName, playerMovementData. dashData.fadeTime);
                }
                else
                {
                    animator.CrossFadeInFixedTime(playerMovementData.dashData.backDushAnimationName, playerMovementData.dashData.fadeTime);
                }
            }
           
        }
        
        
        public virtual void ResetDash()
        {
           
             reusableDate.dashCooldownEndTime = 0f;
        }
        
        #region 相机的水平居中
        public void UpdateCameraRecenteringState(Vector2 movementInput)
        {
            if (movementInput == Vector2.zero) { return; }
            if (movementStateMachine.player.camera == null) return;
            //如果玩家按住W，也取消水平居中
            if (movementInput == Vector2.up)
            {
                DisableCameraRecentering();
                return;
            }
            //根据相机的垂直角来设置居中的速度
            //根据玩家的输入的按键来设置是否居中相机
            //得到相机的垂直角度（上下角）
            float cameraVerticalAngle = movementStateMachine.player.camera.localEulerAngles.x;
            //欧拉角返回的都是一个正数-90=>270，所以减去
            if (cameraVerticalAngle > 270f)
            {
                cameraVerticalAngle -= 360f;
            }
            cameraVerticalAngle= Mathf.Abs(cameraVerticalAngle);

            if (movementInput == Vector2.down)
            {
                SetCameraRecentering(cameraVerticalAngle,playerMovementData.BackWardsCameraRecenteringData);
                return;
            }
            //执行到这里就是带有水平方向的输入了：
            SetCameraRecentering(cameraVerticalAngle, playerMovementData.SidewaysCameraRecenteringData);
        }

        protected void SetCameraRecentering(float cameraVerticalAngle,List<PlayerCameraRecenteringData> playerCameraRecenteringDates)
        {
         
            foreach (PlayerCameraRecenteringData recenteringData in playerCameraRecenteringDates)
            {
                if (!recenteringData.IsWithInAngle(cameraVerticalAngle))
                {
                    //直接退出这个元素，进行下一个元素
                    continue;
                }
                //如果在范围内：
                EnableCameraRecentering(recenteringData.waitingTime, recenteringData.recenteringTime);
                return;
            }

            //如果循环完了没有匹配的范围就关闭水平居中
            DisableCameraRecentering();
        }

        public void EnableCameraRecentering(float waitTime = -1f, float recenteringTime = -1)
        { 
         movementStateMachine.player.playerCameraUtility.EnableRecentering(waitTime, recenteringTime);
        }
        public void DisableCameraRecentering()
        {
            movementStateMachine.player.playerCameraUtility.DisableRecentering();
        }
        private void OnMovementCanceled(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;
            //输入停止则禁用居中（水平、垂直）
            DisableCameraRecentering();
        }
        //当有鼠标输入或者移动方向键时调用
        private void OnMouseMovementStarted(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;
            UpdateCameraRecenteringState(GetPlayerMovementInputDirection());
        }

        private void OnMovementPerformed(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;
            //这里直接用事件得到的最新值
            UpdateCameraRecenteringState(context.ReadValue<Vector2>());
        }

        #endregion
    }
} 
