using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Pure Source movement equations used by the runtime controller. Keeping
    /// these equations free of Physics queries makes their constants and
    /// order testable without entering Play Mode.
    /// </summary>
    public static class GokzMovementRules
    {
        public const float SpeedNormalHu = 250f;
        public const float HammerUnitsPerUnityUnit = 52.4934383202f;
        public const float PreVelModMax = 1.104f;
        public const float PerfSpeedCapHu = 380f;
        public const float AirMaxWishSpeedHu = 30f;
        public const float TickInterval = 1f / 128f;

        public static Vector3 ApplyFriction(
            Vector3 velocity,
            float stopSpeed,
            float friction,
            float surfaceFriction,
            float tickInterval = TickInterval)
        {
            float speed = velocity.magnitude;
            if (speed < 0.1f / HammerUnitsPerUnityUnit)
                return velocity;

            float control = Mathf.Max(speed, stopSpeed);
            float drop = control * friction * Mathf.Max(0f, surfaceFriction) * tickInterval;
            float newSpeed = Mathf.Max(speed - drop, 0f);
            return velocity * (newSpeed / speed);
        }

        public static Vector3 ApplyWaterFriction(
            Vector3 velocity,
            float friction,
            float surfaceFriction,
            float tickInterval = TickInterval)
        {
            float speed = velocity.magnitude;
            if (speed < 0.1f / HammerUnitsPerUnityUnit)
                return Vector3.zero;

            float newSpeed = Mathf.Max(
                0f,
                speed - tickInterval * speed * friction *
                Mathf.Max(0f, surfaceFriction));
            // CGameMovement::WaterMove zeros the velocity when the remaining
            // speed falls below the same 0.1 HU threshold used by its
            // post-friction check; it does not leave a tiny residual drift.
            if (newSpeed < 0.1f / HammerUnitsPerUnityUnit)
                return Vector3.zero;
            return velocity * (newSpeed / speed);
        }

        public static Vector3 Accelerate(
            Vector3 velocity,
            Vector3 wishDirection,
            float wishSpeed,
            float acceleration,
            float surfaceFriction,
            float tickInterval = TickInterval)
        {
            if (wishDirection.sqrMagnitude < 0.000001f || wishSpeed <= 0f)
                return velocity;

            float currentSpeed = Vector3.Dot(velocity, wishDirection);
            float addSpeed = wishSpeed - currentSpeed;
            if (addSpeed <= 0f)
                return velocity;

            // CGameMovement::Accelerate uses max(250, wishspeed), while the
            // passed values at this layer are Unity-space values.
            float accelerationScale = Mathf.Max(
                SpeedNormalHu / HammerUnitsPerUnityUnit,
                wishSpeed);
            float accelerationSpeed = acceleration * tickInterval * accelerationScale *
                Mathf.Max(0f, surfaceFriction);
            accelerationSpeed = Mathf.Min(accelerationSpeed, addSpeed);
            return velocity + wishDirection * accelerationSpeed;
        }

        /// <summary>
        /// CGameMovement::WaterMove uses sv_accelerate * wishspeed rather
        /// than CCSGameMovement::Accelerate's max(250, wishspeed) scale.
        /// Keep that branch separate so fixing ground acceleration cannot
        /// silently change Source water movement.
        /// </summary>
        public static Vector3 AccelerateWater(
            Vector3 velocity,
            Vector3 wishDirection,
            float wishSpeed,
            float acceleration,
            float surfaceFriction,
            float tickInterval = TickInterval)
        {
            if (wishDirection.sqrMagnitude < 0.000001f || wishSpeed < 0.1f / HammerUnitsPerUnityUnit)
                return velocity;

            float speed = velocity.magnitude;
            float addSpeed = wishSpeed - speed;
            if (addSpeed <= 0f)
                return velocity;

            float accelerationSpeed = acceleration * wishSpeed * tickInterval *
                Mathf.Max(0f, surfaceFriction);
            accelerationSpeed = Mathf.Min(accelerationSpeed, addSpeed);
            return velocity + wishDirection * accelerationSpeed;
        }

        /// <summary>
        /// CCSGameMovement::Accelerate's normal CS:GO branch. Source does not
        /// use wishspeed as the acceleration scale when wishspeed is below
        /// the 250 HU run speed; it uses max(250, wishspeed). Ducking and
        /// walking then scale both that acceleration scale and the goal
        /// speed. The caller supplies the already-cropped wishspeed.
        /// </summary>
        public static Vector3 AccelerateCsGo(
            Vector3 velocity,
            Vector3 wishDirection,
            float wishSpeed,
            float acceleration,
            float surfaceFriction,
            float movementScale = 1f,
            float tickInterval = TickInterval,
            bool isWalking = false,
            bool isDucking = false,
            bool useWeaponSpeed = false,
            float weaponMaxSpeedHu = SpeedNormalHu)
        {
            if (wishDirection.sqrMagnitude < 0.000001f || wishSpeed <= 0f)
                return velocity;

            float unmodifiedSpeed = Vector3.Dot(velocity, wishDirection);
            float addSpeed = wishSpeed - unmodifiedSpeed;
            if (addSpeed <= 0f)
                return velocity;

            // CCSGameMovement keeps the signed projection for the add-speed
            // test, then clamps it to zero before applying the duck/walk
            // acceleration branch. Reusing the signed value here makes a
            // reverse strafe use a different acceleration scale than Source.
            float currentSpeed = Mathf.Max(0f, unmodifiedSpeed);

            // Source performs this calculation in Hammer units.  wishSpeed
            // is already expressed in Unity units at this boundary, so the
            // 250-HU floor must be converted once, not multiplied by the
            // HU-per-Unity conversion factor.
            float baseAccelerationScaleHu = Mathf.Max(
                SpeedNormalHu,
                wishSpeed * HammerUnitsPerUnityUnit);
            float weaponSpeedScale = useWeaponSpeed
                ? Mathf.Min(1f, weaponMaxSpeedHu / SpeedNormalHu)
                : 1f;
            float goalSpeedHu = baseAccelerationScaleHu * weaponSpeedScale;
            // Source only scales the acceleration term by weapon speed when
            // neither ducking nor walking. The goal speed is always scaled,
            // then both terms receive the duck/walk modifier.
            float accelerationScaleHu = baseAccelerationScaleHu;
            if (!isDucking && !isWalking)
                accelerationScaleHu *= weaponSpeedScale;

            float accelerationScale = accelerationScaleHu /
                HammerUnitsPerUnityUnit * movementScale;
            float goalSpeed = goalSpeedHu /
                HammerUnitsPerUnityUnit * movementScale;
            float storedAcceleration = acceleration;
            if (isWalking && currentSpeed > goalSpeed -
                5f / HammerUnitsPerUnityUnit)
            {
                // CCSGameMovement::Accelerate only suppresses the walk
                // acceleration when it is already within 5 HU of the walk
                // goal. Above that range the player is allowed to decelerate
                // naturally instead of being hard-clamped.
                float denominator = 5f / HammerUnitsPerUnityUnit;
                storedAcceleration *= Mathf.Clamp01(
                    1f - Mathf.Max(0f, currentSpeed -
                        (goalSpeed - denominator)) / denominator);
            }
            float accelerationSpeed = storedAcceleration * tickInterval *
                accelerationScale * Mathf.Max(0f, surfaceFriction);
            accelerationSpeed = Mathf.Min(accelerationSpeed, addSpeed);
            return velocity + wishDirection * accelerationSpeed;
        }

        public static Vector3 AirAccelerate(
            Vector3 velocity,
            Vector3 wishDirection,
            float wishSpeed,
            float acceleration,
            float airMaxWishSpeed,
            float surfaceFriction,
            float tickInterval = TickInterval)
        {
            if (wishDirection.sqrMagnitude < 0.000001f || wishSpeed <= 0f)
                return velocity;

            // Source caps wishspd at sv_air_max_wishspeed for the add-speed
            // test, but the acceleration formula keeps the original wishspeed.
            float wishSpeedCap = Mathf.Min(wishSpeed, airMaxWishSpeed);
            float currentSpeed = Vector3.Dot(velocity, wishDirection);
            float addSpeed = wishSpeedCap - currentSpeed;
            if (addSpeed <= 0f)
                return velocity;

            float accelerationSpeed = acceleration * wishSpeed * tickInterval *
                Mathf.Max(0f, surfaceFriction);
            accelerationSpeed = Mathf.Min(accelerationSpeed, addSpeed);
            return velocity + wishDirection * accelerationSpeed;
        }

        /// <summary>
        /// Source's GetPlayerMaxSpeed detour point. KZTimer changes the
        /// engine's max-wish-speed to SPEED_NORMAL * realPreVelMod; it does
        /// not clamp the velocity that comes out of movement.
        /// </summary>
        public static float GetPlayerMaxSpeed(
            float speedNormalHu,
            float realPreVelMod)
        {
            return speedNormalHu * Mathf.Max(0f, realPreVelMod);
        }

        public static float ApplyPerfSpeedCap(float horizontalSpeedHu)
        {
            return Mathf.Min(horizontalSpeedHu, PerfSpeedCapHu);
        }

        /// <summary>
        /// GOKZ KZTimer's Movement_OnJumpPre/TweakJump rule. This is only
        /// called for a HitPerf jump; it is not a general movement cap.
        /// </summary>
        public static Vector3 ApplyPerfJumpSpeedCap(
            Vector3 velocity,
            Vector3 up,
            float capHu)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, up);
            float speed = horizontal.magnitude;
            if (speed <= capHu || speed < 0.000001f)
                return velocity;

            Vector3 capped = horizontal * (capHu / speed);
            return capped + up * Vector3.Dot(velocity, up);
        }
    }
}
