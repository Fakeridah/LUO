using System;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// The movement convars replicated by gokz-core for each official GOKZ
    /// mode. Values are kept in Source/Hammer units; the movement solver
    /// converts them once at the Unity boundary.
    /// </summary>
    [Serializable]
    public struct GokzModeSettings
    {
        public float accelerate;
        public float airAccelerate;
        public float airMaxWishSpeed;
        public float friction;
        public float stopSpeed;
        public float gravity;
        public float jumpImpulse;
        public float maxSpeed;
        // Source keeps m_flClientMaxSpeed (sv_maxspeed, normally 320 HU/s)
        // separate from the movement max-speed hook used by GOKZ (250 HU/s
        // times the pre-strafe modifier). WaterMove uses the former for its
        // jump/up intent before capping the wishspeed with the latter.
        public float clientMaxSpeed;
        public float maxVelocity;
        public float walkableNormal;
        public float waterAccelerate;
        public float waterMoveSpeedMultiplier;
        public bool enableBunnyhopping;
        public bool autoBhop;
        public bool useWeaponSpeed;
        public bool prestrafe;
        public bool tweakPerfJump;
        public bool allowCrouchJump;
        public float perfSpeedCap;
        public float preVelModMax;
        public float ladderScaleSpeed;
        // sv_timebetweenducks. GOKZ KZTimer/SimpleKZ set this to 0; the
        // vanilla CS:GO movement profile keeps the engine default.
        public float timeBetweenDucks;

        public static GokzModeSettings For(GokzMode mode)
        {
            switch (mode)
            {
                case GokzMode.Vanilla:
                    return new GokzModeSettings
                    {
                        accelerate = 5.5f,
                        airAccelerate = 12f,
                        airMaxWishSpeed = 30f,
                        friction = 5.2f,
                        stopSpeed = 80f,
                        gravity = 800f,
                        jumpImpulse = 301.993377f,
                        maxSpeed = 250f,
                        clientMaxSpeed = 320f,
                        maxVelocity = 3500f,
                        walkableNormal = 0.7f,
                        waterAccelerate = 10f,
                        waterMoveSpeedMultiplier = 0.8f,
                        enableBunnyhopping = false,
                        autoBhop = false,
                        useWeaponSpeed = true,
                        prestrafe = false,
                        tweakPerfJump = false,
                        allowCrouchJump = false,
                        perfSpeedCap = 380f,
                        preVelModMax = 1f,
                        timeBetweenDucks = 0.4f,
                        ladderScaleSpeed = 0.78f
                    };

                case GokzMode.SimpleKz:
                    return new GokzModeSettings
                    {
                        accelerate = 6.5f,
                        airAccelerate = 100f,
                        airMaxWishSpeed = 30f,
                        friction = 5.2f,
                        stopSpeed = 80f,
                        gravity = 800f,
                        jumpImpulse = 301.993377f,
                        maxSpeed = 250f,
                        clientMaxSpeed = 320f,
                        maxVelocity = 3500f,
                        walkableNormal = 0.7f,
                        waterAccelerate = 10f,
                        waterMoveSpeedMultiplier = 0.8f,
                        enableBunnyhopping = true,
                        autoBhop = false,
                        useWeaponSpeed = false,
                        prestrafe = true,
                        tweakPerfJump = false,
                        allowCrouchJump = true,
                        perfSpeedCap = 380f,
                        preVelModMax = 1f,
                        timeBetweenDucks = 0f,
                        ladderScaleSpeed = 1f
                    };

                default:
                    // These are the values from gokz-mode-kztimer.sp. The
                    // default mode in GOKZ is KZTimer.
                    return new GokzModeSettings
                    {
                        accelerate = 6.5f,
                        airAccelerate = 100f,
                        airMaxWishSpeed = 30f,
                        friction = 5f,
                        stopSpeed = 80f,
                        gravity = 800f,
                        jumpImpulse = 301.993377f,
                        maxSpeed = 250f,
                        clientMaxSpeed = 320f,
                        maxVelocity = 2000f,
                        walkableNormal = 0.7f,
                        waterAccelerate = 10f,
                        waterMoveSpeedMultiplier = 0.8f,
                        enableBunnyhopping = true,
                        autoBhop = false,
                        useWeaponSpeed = false,
                        prestrafe = true,
                        tweakPerfJump = true,
                        allowCrouchJump = true,
                        perfSpeedCap = 380f,
                        preVelModMax = 1.104f,
                        timeBetweenDucks = 0f,
                        ladderScaleSpeed = 1f
                    };
            }
        }
    }
}
