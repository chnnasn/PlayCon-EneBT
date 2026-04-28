using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerState;

namespace FSM
{
    public class PlayerMovementStateMachine : StateMachine
    {
        public PlayerStateReusableDate reusableDate { get;  }
        public Player player { get; }
        public PlayerIdlingState idlingState { get; }
        public PlayerWalkingState walkingState { get; }
        public PlayerRunningState runningState { get; }
        public PlayerSprintingState sprintingState { get; }

        public PlayerDashingState dashingState { get;  }

        public PlayerDashBackingState dashBackingState { get; }

        public PlayerReturnRunState returnRunState{ get; }
        

        public PlayerMovementNullState playerMovementNullState { get; }

        public PlayerMovementStateMachine(Player P)
        {
            player = P;

            reusableDate =new PlayerStateReusableDate();
            
            idlingState = new PlayerIdlingState(this);

            walkingState = new PlayerWalkingState(this);

            runningState = new PlayerRunningState(this);

            sprintingState = new PlayerSprintingState(this);

            dashingState = new PlayerDashingState(this);

            dashBackingState = new PlayerDashBackingState(this);

            returnRunState = new PlayerReturnRunState(this);
            

            playerMovementNullState=new PlayerMovementNullState(this);  

        }
            
    }
}

