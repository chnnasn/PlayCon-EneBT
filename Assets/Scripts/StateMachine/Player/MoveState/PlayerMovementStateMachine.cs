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
        

        public PlayerMovementNullState playerMovementNullState { get; }

        public PlayerMovementStateMachine(Player P)
        {
            player = P;

            reusableDate =new PlayerStateReusableDate();

            idlingState = new PlayerIdlingState(this);

            walkingState = new PlayerWalkingState(this);

            runningState = new PlayerRunningState(this);
            

            playerMovementNullState=new PlayerMovementNullState(this);  

        }
            
    }
}

