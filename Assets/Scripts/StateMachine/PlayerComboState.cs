
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Tools;
using PlayerState;
using System.Collections.Generic;

namespace FSM
{

    public class PlayerComboState : IState
    {
        protected Player player{ get; }
        protected PlayerComboStateMachine comboStateMachine { get; }
        protected CharacterCombo characterCombo { get; }
        protected PlayerComboReusableData reusableData { get;  }
        protected PlayerComboData playerComboData { get; }
        protected Animator animator { get; }
        private readonly List<GameTimer> ownedTimers = new List<GameTimer>();
        private CharacterInput inputActions;
        protected bool IsCurrentState => comboStateMachine.IsRunning &&
            ReferenceEquals(comboStateMachine.currentState.Value, this) && player.CanAcceptInput;
        public PlayerComboState(PlayerComboStateMachine comboStateMachine)
        {
           this.comboStateMachine = comboStateMachine;

            if (player == null)
            {
                player = comboStateMachine.Player;
            }  
            if (animator == null)
            {
                animator = comboStateMachine.Player.characterAnimator;
            }
            if (playerComboData == null)
            {
                playerComboData = player.playerSO.ComboData;
            }
            if (reusableData == null)
            {
                reusableData = this.comboStateMachine.ReusableData;
            }
            if (characterCombo == null)
            {
                characterCombo = new CharacterCombo(animator, player.transform,player.camera, reusableData, playerComboData.comboData,player);
            }
           
        }
        public virtual void Enter()
        {
            AddInputActionEvent();
        }

        public virtual void Exit()
        {
            RemoveInputActionEvent();
            if (TimerManager.TryGetExistingInstance(out var timerManager))
            {
                foreach (var timer in ownedTimers) timerManager.UnregisterTimer(timer);
            }
            ownedTimers.Clear();
        }

        protected void ScheduleTimer(float duration, Action action)
        {
            GameTimer timer = null;
            timer = TimerManager.MainInstance.GetTimer(duration, () =>
            {
                ownedTimers.Remove(timer);
                if (IsCurrentState) action();
            });
            ownedTimers.Add(timer);
        }

        public virtual void HandInput()
        {
          
        }

        public virtual void OnAnimationExitEvent()
        {
            
        }

        public virtual void OnAnimationTranslateEvent(IState state)
        {

        }

        public virtual void Update()
        {
            characterCombo.UpdateComboAnimation();
            characterCombo.UpdateEnemy();
            characterCombo.CheckCanLinkCombo();
        }
        public void DrawEnemyDetectionGizmos()
        {
            characterCombo.DrawDetectionGizmos();
        }
        protected virtual void AddInputActionEvent()
        {
            inputActions = PlayerInputSystem.MainInstance?.inputActions;
            if (inputActions == null) return;
            inputActions.Player.L_AtK.started += OnAttackInput;
            inputActions.Player.FinishSkill.started += OnFinishSkill;
            inputActions.Player.Execute.started += OnSkill;
            characterCombo.AddEventAction();
        }


        protected virtual void RemoveInputActionEvent()
        {
            if (inputActions == null) return;
            inputActions.Player.L_AtK.started -= OnAttackInput;
            inputActions.Player.FinishSkill.started -= OnFinishSkill;
            inputActions.Player.Execute.started -= OnSkill;
            characterCombo.RemoveEventActon();
        }
        private void OnAttackInput(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;

            if (characterCombo.CanBaseComboInput())
            {
                if (player.currentMovementState == "PlayerSprintingState" || animator.AnimationAtTag("Dodge"))
                {
                    characterCombo.DodgeComboInput();
                    Debug.Log("闪避攻击");
                }
                else
                {
                  
                    characterCombo.LightComboInput();
                }

            }
        }
        private void OnFinishSkill(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;
            if (characterCombo.CanFinishSkillInput())
            {
                characterCombo.FinishSkillInput();
                comboStateMachine.ChangeState(comboStateMachine.SkillState);
            }
        }
        private void OnSkill(InputAction.CallbackContext context)
        {
            if (!IsCurrentState) return;
            if (characterCombo.CanSkillInput())
            {
                characterCombo.SkillInput();
                comboStateMachine.ChangeState(comboStateMachine.SkillState);
            }
        }

    }
}
