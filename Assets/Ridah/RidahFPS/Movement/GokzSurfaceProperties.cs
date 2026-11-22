using System;
using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Source contents flags used by MASK_PLAYERSOLID and related movement
    /// traces. Values match public/bspflags.h from the local CS:GO source.
    /// </summary>
    [Flags]
    public enum GokzSourceContents
    {
        Empty = 0,
        Solid = 0x00000001,
        Window = 0x00000002,
        Aux = 0x00000004,
        Grate = 0x00000008,
        Slime = 0x00000010,
        Water = 0x00000020,
        BlockLos = 0x00000040,
        Opaque = 0x00000080,
        TestFogVolume = 0x00000100,
        Unused = 0x00000200,
        BlockLight = 0x00000400,
        Team1 = 0x00000800,
        Team2 = 0x00001000,
        IgnoreNoDrawOpaque = 0x00002000,
        Moveable = 0x00004000,
        AreaPortal = 0x00008000,
        PlayerClip = 0x00010000,
        MonsterClip = 0x00020000,
        BrushPaint = 0x00040000,
        GrenadeClip = 0x00080000,
        Unused2 = 0x00100000,
        Unused3 = 0x00200000,
        Unused4 = 0x00400000,
        Unused5 = 0x00800000,
        Origin = 0x01000000,
        Monster = 0x02000000,
        Debris = 0x04000000,
        Detail = 0x08000000,
        Translucent = 0x10000000,
        Ladder = 0x20000000,
        Hitbox = 0x40000000
    }

    /// <summary>
    /// Source collision groups used by CS:GO's CGameRules::ShouldCollide.
    /// Numeric values match Collision_Group_t in the local CS:GO source.
    /// </summary>
    public enum GokzSourceCollisionGroup
    {
        None = 0,
        Debris = 1,
        DebrisTrigger = 2,
        InteractiveDebris = 3,
        Interactive = 4,
        Player = 5,
        BreakableGlass = 6,
        Vehicle = 7,
        PlayerMovement = 8,
        Npc = 9,
        InVehicle = 10,
        Weapon = 11,
        VehicleClip = 12,
        Projectile = 13,
        DoorBlocker = 14,
        PassableDoor = 15,
        Dissolving = 16,
        PushAway = 17,
        NpcActor = 18,
        NpcScripted = 19,
        PzClip = 20,
        DebrisBlockProjectile = 21
    }

    /// <summary>
    /// Entity categories used by Source's StandardFilterRules. A brush-only
    /// movement trace filters non-brush entities unless CONTENTS_MONSTER is
    /// present in the trace mask.
    /// </summary>
    public enum GokzSourceEntityKind
    {
        WorldOrStaticProp,
        BrushEntity,
        NonBrushEntity
    }

    public struct GokzSourceCollisionData
    {
        public GokzSourceContents contents;
        public GokzSourceCollisionGroup collisionGroup;
        public GokzSourceEntityKind entityKind;
        public Transform owner;

        public GokzSourceCollisionData(
            GokzSourceContents contents,
            GokzSourceCollisionGroup collisionGroup,
            GokzSourceEntityKind entityKind,
            Transform owner = null)
        {
            this.contents = contents;
            this.collisionGroup = collisionGroup;
            this.entityKind = entityKind;
            this.owner = owner;
        }

        public static GokzSourceCollisionData DefaultFor(Collider collider)
        {
            GokzSourceContents contents = GokzSourceContents.Solid;
            GokzSourceCollisionGroup collisionGroup =
                GokzSourceCollisionGroup.None;
            GokzSourceEntityKind entityKind =
                GokzSourceEntityKind.WorldOrStaticProp;
            bool isPlayer = collider != null &&
                HasTagInParents(collider, "Player");
            bool isEnemy = collider != null &&
                HasTagInParents(collider, "Enemy");
            if (isPlayer || isEnemy)
            {
                contents = GokzSourceContents.Monster;
                entityKind = GokzSourceEntityKind.NonBrushEntity;
            }
            if (isEnemy)
                collisionGroup = GokzSourceCollisionGroup.Npc;
            else if (isPlayer)
                collisionGroup = GokzSourceCollisionGroup.Player;
            else if (collider != null && collider.attachedRigidbody != null)
            {
                // CS:GO marks MOVETYPE_PUSH entities as MOVEABLE. Both
                // MASK_PLAYERSOLID variants include this contents bit.
                contents = GokzSourceContents.Moveable;
                entityKind = GokzSourceEntityKind.NonBrushEntity;
            }

            if (collider != null &&
                (collider.GetComponentInParent<GokzLadder>() != null ||
                 GokzSurfaceProperties.Resolve(collider).sourceClimbable))
                contents |= GokzSourceContents.Ladder;

            return new GokzSourceCollisionData(
                contents,
                collisionGroup,
                entityKind);
        }

        private static bool HasTagInParents(Collider collider, string tag)
        {
            for (Transform current = collider.transform;
                 current != null;
                 current = current.parent)
            {
                if (current.CompareTag(tag))
                    return true;
            }

            return false;
        }
    }

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
        [Header("Source collision filtering")]
        [SerializeField]
        [Tooltip("Use explicit Source contents and collision group for movement traces. By default, static colliders map to SOLID, Rigidbody colliders to MOVEABLE, and Player/Enemy tags to MONSTER.")]
        private bool overrideSourceCollisionData;

        [SerializeField]
        [Tooltip("Source CONTENTS_* flags used by player movement traces. For example, use PLAYERCLIP for a player-only clip brush.")]
        private GokzSourceContents sourceContents = GokzSourceContents.Solid;

        [SerializeField]
        [Tooltip("Source COLLISION_GROUP_* used by CGameRules::ShouldCollide, such as WEAPON or PUSH_AWAY.")]
        private GokzSourceCollisionGroup sourceCollisionGroup =
            GokzSourceCollisionGroup.None;

        [SerializeField]
        [Tooltip("With Override Source Collision Data enabled, set the Source category for StandardFilterRules. Use BrushEntity for a Source-style BSP door or platform; Rigidbody colliders otherwise default to NonBrushEntity.")]
        private GokzSourceEntityKind sourceEntityKind =
            GokzSourceEntityKind.WorldOrStaticProp;

        [SerializeField]
        [Tooltip("Optional Source owner entity. Movement traces skip the owner and its owned collider, matching PassServerEntityFilter.")]
        private Transform sourceOwner;

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

        public GokzSourceCollisionData GetSourceCollisionData(Collider collider)
        {
            if (overrideSourceCollisionData)
            {
                return new GokzSourceCollisionData(
                    sourceContents,
                    sourceCollisionGroup,
                    sourceEntityKind,
                    sourceOwner);
            }

            GokzSourceCollisionData defaultData =
                GokzSourceCollisionData.DefaultFor(collider);
            defaultData.owner = sourceOwner;
            return defaultData;
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

        public static GokzSourceCollisionData ResolveCollisionData(
            Collider collider)
        {
            if (collider == null)
                return new GokzSourceCollisionData(
                    GokzSourceContents.Empty,
                    GokzSourceCollisionGroup.None,
                    GokzSourceEntityKind.WorldOrStaticProp);

            GokzSurfaceProperties properties =
                collider.GetComponentInParent<GokzSurfaceProperties>();
            return properties != null
                ? properties.GetSourceCollisionData(collider)
                : GokzSourceCollisionData.DefaultFor(collider);
        }
    }
}
