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

    /// <summary>
    /// Per-command state exchanged between LUO and the Source/GOKZ movement core.
    /// Only project-specific state (gravity toggles, water volumes, and event
    /// locks) sits beside the Source user-command and movement fields.
    /// </summary>
    public sealed class MoveData
    {
        public Transform playerTransform;
        public Transform groundTransform;

        public Vector3 Origin;
        public float ViewYawDelta;
        public Vector3 Velocity;
        public int Buttons;
        public int OldButtons;
        public float ForwardMove;
        public float SideMove;
        public float UpMove;

        public float SurfaceFriction = 1f;
        public float SurfaceMaxSpeedFactor = 1f;
        public float SurfaceJumpFactor = 1f;

        public float verticalAxis;
        public float horizontalAxis;
        public bool moveLeft;
        public bool moveRight;
        public bool moveForward;
        public bool moveBack;
        public bool wishJump;
        public bool jumpHeld;
        public bool crouching;
        public bool sprinting;

        public bool climbingLadder;
        public Vector3 ladderNormal = Vector3.zero;
        public Vector3 ladderDirection = Vector3.forward;
        public Vector3 ladderClimbDir = Vector3.up;
        public Vector3 ladderVelocity = Vector3.zero;

        public bool underwater;
        public GokzWaterLevel waterLevel = GokzWaterLevel.NotInWater;
        public GokzWaterType waterType = GokzWaterType.Water;
        public bool antiBhop;
        public bool antiCheckpoint;
        public bool antiPause;
        public bool antiJumpstats;

        // LUO gravity and trigger extension; Source surface gravity remains
        // controlled by GokzMovementVolume inside the movement core.
        public bool isGravity = true;
        public bool LoseWeight;
        public float reverseGravity = 1f;

        public bool hasJump;
        public bool grounded;
        public bool gameCodeMovedPlayer;
    }
}
