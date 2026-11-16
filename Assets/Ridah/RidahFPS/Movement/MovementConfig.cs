using UnityEngine;
namespace RidahFPS.Controllers
{
    [System.Serializable]
    public class MovementConfig
    {
        [Header("Jumping and gravity")]
        public float Gravity = 15.24f;
        //public float Bounce;//what?
        public bool AutoBhop = true;
        public float JumpPower = 5.112f;

        [Header("General physics")]
        public float Friction = 4f;
        [Tooltip("clamp air movement")]
        public float MaxSpeed = 6f;
        [Tooltip("clamp the real veclocity")]
        public float MaxVelocity = 100f;

        [Header("Air movement")]
        public bool ClampAirSpeed;
        public float AirCap = 0.575f;
        public float AirAccel = 1500;
        public float AirFriction = 0.25f;

        [Header("Ground movement")] 
        public float StopSpeed = 1.905f;
        public float walkSpeed = 7f;
        public float sprintSpeed = 12f;
        public float acceleration = 14f;
        public float deceleration = 10f;

        [Header("Crouch movement")]
        public float crouchSpeed = 4f;
        public float crouchAcceleration = 8f;
        public float crouchDeceleration = 4f;
        public float crouchFriction = 3f;

        [Header("Sliding")]
        public float minimumSlideSpeed = 9f;
        public float maximumSlideSpeed = 18f;
        public float slideSpeedMultiplier = 1.75f;
        public float slideFriction = 14f;
        public float downhillSlideSpeedMultiplier = 2.5f;
        public float slideDelay = 0.5f;

        [Header("Underwater")]
        public float swimUpSpeed = 12f;
        public float underwaterSwimSpeed = 3f;
        public float underwaterAcceleration = 6f;
        public float underwaterDeceleration = 3f;
        public float underwaterFriction = 2f;
        public float underwaterGravity = 6f;
        public float underwaterVelocityDampening = 2f;
    }
}
