
using PlayerState;
using UnityEngine;

namespace FSM
{
    public class PlayerSkillState : PlayerComboState
    {
        public PlayerSkillState(PlayerComboStateMachine comboStateMachine) : base(comboStateMachine)
        {
        }

        /// 通过输入事件和执行条件触发
        public override void Enter()
        {
            base.Enter();
            comboStateMachine.Player.movementStateMachine.ChangeState(comboStateMachine.Player.movementStateMachine.playerMovementNullState);
      
        }

        public override void Update()
        {
            characterCombo.UpdateEnemy();
            characterCombo.UpdateAttackLookAtEnemy();
        }
        
        public override void Exit()
        {
            base.Exit();
      
        }
        /// 通过动画脚本触发：动画播放完退出
        public override void OnAnimationExitEvent()
        {
            comboStateMachine.ChangeState(comboStateMachine.NullState);
       
        }
        
        /// 动画退出
        public override void OnAnimationTranslateEvent(IState state)
        {
            comboStateMachine.ChangeState(state);
        }
    }
}


