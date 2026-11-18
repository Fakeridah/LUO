using System;
using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Surface identifiers used by the Unity adapter. In Source these are
    /// surfaceprop names which are resolved through the surface data table;
    /// they are not renderer materials.
    /// </summary>
    public enum GokzSurfacePreset
    {
        Default,
        Ice,
        Ladder
    }

    /// <summary>
    /// The movement fields read from Source's surfacedata_t. The current
    /// movement port uses the physics friction and the three game movement
    /// fields; audio, penetration and impact data are deliberately outside
    /// the player movement solver.
    /// </summary>
    [Serializable]
    public struct GokzSourceSurfaceData
    {
        public float sourceFriction;
        public float sourceMaxSpeedFactor;
        public float sourceJumpFactor;
        public bool sourceClimbable;

        public static GokzSourceSurfaceData Default => new GokzSourceSurfaceData
        {
            sourceFriction = 1f,
            sourceMaxSpeedFactor = 1f,
            sourceJumpFactor = 1f,
            sourceClimbable = false
        };

        public static GokzSourceSurfaceData For(GokzSurfacePreset preset)
        {
            switch (preset)
            {
                case GokzSurfacePreset.Ice:
                    // Unity-authored compatibility mapping. The local
                    // cstrike15_src tree contains surfacedata_t's fields but
                    // not CS:GO's shipped surface-property data table, so
                    // 0.05 is not claimed to be the game's exact ice value.
                    // Keep the game movement multipliers at their defaults;
                    // this preset only changes the editable physics friction.
                    return new GokzSourceSurfaceData
                    {
                        sourceFriction = 0.05f,
                        sourceMaxSpeedFactor = 1f,
                        sourceJumpFactor = 1f,
                        sourceClimbable = false
                    };

                case GokzSurfacePreset.Ladder:
                    // Ladder movement is a separate Source movement branch;
                    // this flag lets a surface preset identify a ladder
                    // without relying on a Unity tag.
                    return new GokzSourceSurfaceData
                    {
                        sourceFriction = 1f,
                        sourceMaxSpeedFactor = 1f,
                        sourceJumpFactor = 1f,
                        sourceClimbable = true
                    };

                default:
                    return Default;
            }
        }
    }

    /// <summary>
    /// Source-style movement surface data for a Unity collider.
    ///
    /// A component is the primary mapping, because one collider can carry
    /// more information than a single Unity tag. The active GOKZ controller
    /// also accepts an Ice tag as a compatibility fallback for existing map
    /// objects that already use the old project convention.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Surface Properties")]
    [DisallowMultipleComponent]
    public sealed class GokzSurfaceProperties : MonoBehaviour
    {
        [Tooltip("Source surface data preset used by this collider.")]
        public GokzSurfacePreset preset = GokzSurfacePreset.Default;

        [Tooltip("Keep this enabled when the numeric values below are intentionally customized.")]
        public bool overridePreset;

        [Min(0f)]
        [Tooltip("Source physics.friction. This affects ground friction and acceleration.")]
        public float sourceFriction = 1f;

        [Min(0f)]
        [Tooltip("Source game.maxSpeedFactor. This scales the player's movement max speed on this surface.")]
        public float sourceMaxSpeedFactor = 1f;

        [Min(0f)]
        [Tooltip("Source game.jumpFactor. This scales jump impulse while standing on this surface.")]
        public float sourceJumpFactor = 1f;

        [Tooltip("Source game.climbable. Ladder movement uses this when no GokzLadder component is present.")]
        public bool sourceClimbable;

        public GokzSourceSurfaceData GetSourceData()
        {
            if (!overridePreset)
                return GokzSourceSurfaceData.For(preset);

            return new GokzSourceSurfaceData
            {
                sourceFriction = Mathf.Max(0f, sourceFriction),
                sourceMaxSpeedFactor = Mathf.Max(0f, sourceMaxSpeedFactor),
                sourceJumpFactor = Mathf.Max(0f, sourceJumpFactor),
                sourceClimbable = sourceClimbable
            };
        }

        public void ApplyPreset()
        {
            if (overridePreset)
                return;

            GokzSourceSurfaceData data =
                GokzSourceSurfaceData.For(preset);
            sourceFriction = data.sourceFriction;
            sourceMaxSpeedFactor = data.sourceMaxSpeedFactor;
            sourceJumpFactor = data.sourceJumpFactor;
            sourceClimbable = data.sourceClimbable;
        }

        private void OnValidate()
        {
            ApplyPreset();
        }

        /// <summary>
        /// Resolves the surface attached to a collision result. This mirrors
        /// Source's trace-to-surfacedata lookup while keeping Unity collision
        /// filtering independent from movement surface behavior.
        /// </summary>
        public static GokzSourceSurfaceData Resolve(Collider collider)
        {
            if (collider == null)
                return GokzSourceSurfaceData.Default;

            GokzMovementVolume movementVolume =
                collider.GetComponentInParent<GokzMovementVolume>();
            if (movementVolume != null)
                return movementVolume.GetSourceSurfaceData();

            GokzSurfaceProperties properties =
                collider.GetComponentInParent<GokzSurfaceProperties>();
            if (properties != null)
                return properties.GetSourceData();

            // Existing LUO map objects use Ice/Ladder tags. Keep that content
            // working without requiring a manual component on every collider.
            for (Transform current = collider.transform;
                 current != null;
                 current = current.parent)
            {
                switch (current.gameObject.tag)
                {
                    case "Ice":
                        return GokzSourceSurfaceData.For(
                            GokzSurfacePreset.Ice);
                    case "Ladder":
                        return GokzSourceSurfaceData.For(
                            GokzSurfacePreset.Ladder);
                }
            }

            return GokzSourceSurfaceData.Default;
        }
    }
}
