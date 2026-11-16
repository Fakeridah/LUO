using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Optional Source-style water metadata for existing Unity trigger water.
    /// Without this component a collider tagged Water is treated as an Eyes
    /// volume, preserving the project's previous behavior.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Water Volume")]
    [RequireComponent(typeof(Collider))]
    public sealed class GokzWaterVolume : MonoBehaviour
    {
        public bool useTransformHeight = true;
        public GokzWaterType waterType = GokzWaterType.Water;
        [Tooltip("World-space water surface when useTransformHeight is disabled.")]
        public float surfaceHeight;

        public float SurfaceHeight => useTransformHeight
            ? transform.position.y
            : surfaceHeight;

        public GokzWaterLevel GetLevel(
            Vector3 feetPosition,
            bool crouched = false,
            float eyeDuckAmount = 0f)
        {
            float depth = SurfaceHeight - feetPosition.y;
            // CGameMovement::CheckWater samples one HU above the feet, not
            // the origin itself. A surface that only reaches the origin is
            // therefore still WL_NotInWater.
            if (depth <= 1f / GokzMovementRules.HammerUnitsPerUnityUnit)
                return GokzWaterLevel.NotInWater;

            // The waist sample is the center of the active Source hull. The
            // eye sample follows CS:GO's continuous duck eye offset while a
            // crouch transition is in progress.
            float waistHeight = (crouched ? 27f : 36f) /
                GokzMovementRules.HammerUnitsPerUnityUnit;
            if (depth <= waistHeight)
                return GokzWaterLevel.Feet;

            float eyeHeight = Mathf.Lerp(64f, 46f, Mathf.Clamp01(eyeDuckAmount)) /
                GokzMovementRules.HammerUnitsPerUnityUnit;
            if (depth <= eyeHeight)
                return GokzWaterLevel.Waist;

            return GokzWaterLevel.Eyes;
        }
    }
}
