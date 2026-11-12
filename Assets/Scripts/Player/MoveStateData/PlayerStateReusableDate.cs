using UnityEngine;

namespace PlayerState
{
    public class PlayerStateReusableDate 
    {
        public float inputMult { get; set; }

        public bool shouldWalk { get; set; }
        
        public float dashCooldownEndTime { get; set; }
        public bool canDash => Time.time >= dashCooldownEndTime;
        
        public Vector2 inputDirection { get; set; }

        public float rotationTime { get; set; }

        public float targetAngle { get; set; }

    }
}
