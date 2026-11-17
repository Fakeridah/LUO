using UnityEngine;

namespace RidahFPS.Controllers
{
    public enum GokzMovementVolumeType
    {
        Default,
        Ice,
        Water,
        Gravity,
        Push,
        Ladder,
        CustomSurface
    }

    /// <summary>
    /// One Unity authoring component for a movement object or volume. A single
    /// object selects one Source/GOKZ type; different objects may still
    /// overlap at runtime, just as Source trigger entities can.
    ///
    /// Slope is intentionally not a type. Source obtains it from the trace
    /// plane normal, so the active movement core calculates it automatically.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Movement Volume")]
    [RequireComponent(typeof(Collider))]
    public sealed class GokzMovementVolume : MonoBehaviour
    {
        [Header("Movement type")]
        [SerializeField]
        private GokzMovementVolumeType movementType;

        [SerializeField, Min(0f)]
        private float sourceFriction = 1f;

        [SerializeField, Min(0f)]
        private float sourceMaxSpeedFactor = 1f;

        [SerializeField, Min(0f)]
        private float sourceJumpFactor = 1f;

        [SerializeField]
        private bool sourceClimbable;

        [Header("Water")]
        [SerializeField]
        private bool useTransformHeight = true;

        [SerializeField]
        private GokzWaterType waterType = GokzWaterType.Water;

        [SerializeField]
        [Tooltip("World-space water surface when Use Transform Height is disabled.")]
        private float surfaceHeight;

        [Header("Gravity")]
        [SerializeField]
        [Tooltip("Source trigger_gravity scale. 1 = normal, 0 = weightless.")]
        private float gravityScale = 1f;

        [SerializeField]
        [Tooltip("Overlapping gravity volumes use the highest priority.")]
        private int priority;

        [Header("Push")]
        [SerializeField]
        [Tooltip("Source pushdir expressed in this object's local axes.")]
        private Vector3 localPushDirection = Vector3.up;

        [SerializeField]
        [Tooltip("Source pushspeed in Hammer units per second. Source defaults to 100.")]
        private float pushSpeedHU = 100f;

        [SerializeField]
        [Tooltip("Matches SF_TRIG_PUSH_ONCE: apply one impulse and consume this volume for the player.")]
        private bool pushOnce;

        public GokzMovementVolumeType MovementType => movementType;
        public int Priority => priority;
        public bool IsWater => movementType == GokzMovementVolumeType.Water;
        public bool IsGravity => movementType == GokzMovementVolumeType.Gravity;
        public bool IsPush => movementType == GokzMovementVolumeType.Push;
        public bool IsLadder => movementType == GokzMovementVolumeType.Ladder;
        public bool PushOnce => pushOnce;
        public float GravityScale => gravityScale;
        public GokzWaterType WaterType => waterType;

        public GokzSourceSurfaceData GetSourceSurfaceData()
        {
            switch (movementType)
            {
                case GokzMovementVolumeType.Ice:
                    return GokzSourceSurfaceData.For(GokzSurfacePreset.Ice);

                case GokzMovementVolumeType.Ladder:
                    return GokzSourceSurfaceData.For(GokzSurfacePreset.Ladder);

                case GokzMovementVolumeType.CustomSurface:
                    return new GokzSourceSurfaceData
                    {
                        sourceFriction = Mathf.Max(0f, sourceFriction),
                        sourceMaxSpeedFactor = Mathf.Max(0f, sourceMaxSpeedFactor),
                        sourceJumpFactor = Mathf.Max(0f, sourceJumpFactor),
                        sourceClimbable = sourceClimbable
                    };

                default:
                    return GokzSourceSurfaceData.Default;
            }
        }

        public float SurfaceHeight => useTransformHeight
            ? transform.position.y
            : surfaceHeight;

        public GokzWaterLevel GetWaterLevel(
            Vector3 feetPosition,
            bool crouched = false,
            float eyeDuckAmount = 0f)
        {
            if (!IsWater)
                return GokzWaterLevel.NotInWater;

            float depth = SurfaceHeight - feetPosition.y;
            float unitScale = 1f /
                GokzMovementRules.HammerUnitsPerUnityUnit;
            if (depth <= unitScale)
                return GokzWaterLevel.NotInWater;

            float waistHeight = (crouched ? 27f : 36f) * unitScale;
            if (depth <= waistHeight)
                return GokzWaterLevel.Feet;

            float eyeHeight = Mathf.Lerp(64f, 46f, Mathf.Clamp01(eyeDuckAmount)) *
                unitScale;
            if (depth <= eyeHeight)
                return GokzWaterLevel.Waist;

            return GokzWaterLevel.Eyes;
        }

        public Vector3 WorldPushVelocity
        {
            get
            {
                Vector3 direction = localPushDirection.sqrMagnitude > 0.000001f
                    ? transform.TransformDirection(localPushDirection.normalized)
                    : Vector3.zero;
                return direction *
                    (pushSpeedHU / SourceMovementCore.HammerUnitsPerUnityUnit);
            }
        }

        private void Reset()
        {
            ConfigureTriggerCollider();
        }

        private void Awake()
        {
            ConfigureTriggerCollider();
        }

        private void OnValidate()
        {
            ConfigureTriggerCollider();
        }

        private void ConfigureTriggerCollider()
        {
            Collider volumeCollider = GetComponent<Collider>();
            if (volumeCollider == null)
                return;

            if (IsWater || IsGravity || IsPush)
                volumeCollider.isTrigger = true;
        }
    }
}
