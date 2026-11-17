using UnityEngine;

namespace RidahFPS.Controllers
{
    public enum GokzWaterLevel
    {
        NotInWater,
        Feet,
        Waist,
        Eyes
    }

    public enum GokzWaterType
    {
        Water,
        Slime
    }


    public enum MoveType
    {
        None,
        Walk,
        Noclip, // Source noclip is outside the walk movement core
        Ladder,
    }

    public class MoveData
    {
        public Transform playerTransform;
        public Transform viewTransform;
        public Vector3 viewTransformDefaultLocalPos;
        public Vector3 viewTransformDefaultWorldPos;
        public Transform groundTransform;
        ///// Fields /////

        public Vector3 Origin;
        public Vector3 ViewAngles;
        // Accumulated view-yaw change since the previous rendered input
        // update. The 128-tick movement solver consumes it once per tick.
        public float ViewYawDelta;
        public Vector3 Velocity;
        public int Buttons;
        public int OldButtons;
        public float ForwardMove;
        public float SideMove;
        public float UpMove;
        public float SurfaceFriction = 1f;
        // Source surface data also carries game.maxSpeedFactor. Keep it
        // separate from friction: CheckParameters scales maxspeed with this
        // factor, while Accelerate uses surface friction.
        public float SurfaceMaxSpeedFactor = 1f;
        // Source game.jumpFactor scales the jump impulse on the current
        // ground surface. It is independent from physics.friction.
        public float SurfaceJumpFactor = 1f;
        public bool isGravity = true;
        public float GravityFactor = 1f;
        public float WalkFactor = 1f;

        public float verticalAxis = 0f;
        public float horizontalAxis = 0f;
        // Keep the raw Source button state separate from the summed axes.
        // A+D therefore remains two pressed buttons even though the resulting
        // sidemove value is zero, which is required by KZTimer's pre-strafe
        // calculation.
        public bool moveLeft = false;
        public bool moveRight = false;
        public bool moveForward = false;
        public bool moveBack = false;
        public bool wishJump = false;
        public bool jumpHeld = false;
        public bool crouching = false;
        // Source IN_SPEED. The historical field name is kept for existing
        // callers; in the CS:GO movement path it means the walk modifier,
        // not a project-specific sprint boost.
        public bool sprinting = false;

        public float slopeLimit = 45f;

        public float rigidbodyPushForce = 1f;

        public float defaultHeight = 2f;
        public float crouchingHeight = 1f;
        public float crouchingSpeed = 10f;
        //public bool toggleCrouch = false;

        public bool jumpEnabled = false;
        public bool crouchEnabled = false;
        public bool slidingEnabled = false;
        public bool laddersEnabled = false;
        public bool IceEnabled = false;
        public bool ModifierEnabled = false;

        public bool climbingLadder = false;
        public Vector3 ladderNormal = Vector3.zero;
        public Vector3 ladderDirection = Vector3.forward;
        public Vector3 ladderClimbDir = Vector3.up;
        public Vector3 ladderVelocity = Vector3.zero;

        public bool underwater = false;
        public GokzWaterLevel waterLevel = GokzWaterLevel.NotInWater;
        public GokzWaterType waterType = GokzWaterType.Water;
        public bool antiBhop = false;
        public bool antiCheckpoint = false;
        public bool antiPause = false;
        public bool antiJumpstats = false;
        public bool Booster = false;
        public bool Bouncer = false;
        public bool LoseWeight = false;

        //public bool groundedTemp = false;
        public float fallingVelocity = 0f;
        public float friction;

        //Audio
        public bool hasJump = false;
        public bool isReset = false;

        public float reverseGravity = 1f;
        public bool grounded = false;
        public float airAccel = 1500;

        public float scale = 1;

    }
}
