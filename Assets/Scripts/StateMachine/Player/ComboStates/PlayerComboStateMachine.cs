using UnityEngine;
using PlayerState;

namespace FSM
{
    public class PlayerComboStateMachine : StateMachine
    {
        public Player Player { get; }
        public GenericProperty ATKIngState { get; }
        public PlayerNullState NullState { get; }
        public PlayerComboReusableData ReusableData { get; }

        public PlayerSkillState SkillState { get; }
        public PlayerComboStateMachine(Player player)
        {
            Player = player;

            ReusableData = new PlayerComboReusableData();

            ATKIngState =new GenericProperty(this);

            NullState=new PlayerNullState(this);

            SkillState =new PlayerSkillState(this);
        }

    }
}
