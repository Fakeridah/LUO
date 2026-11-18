using System;
using UnityEngine;

namespace RidahFPS.Controllers
{
    public enum GokzJumpType
    {
        LongJump,
        Bhop,
        MultiBhop,
        WeirdJump,
        LadderJump,
        LadderHop,
        Jumpbug,
        LowPreWeirdJump,
        LowPreBhop
    }

    public enum GokzJumpTier
    {
        None,
        Meh,
        Impressive,
        Perfect,
        Godlike,
        Ownage,
        Wrecker
    }

    [Serializable]
    public struct GokzJumpResult
    {
        public int number;
        public GokzJumpType type;
        public GokzJumpTier tier;
        public float takeoffSpeed;
        public float landingSpeed;
        public float airtime;
        public float distance;
        public bool perfect;
        public bool ducked;
        public bool deadstrafed;
    }

    /// <summary>
    /// Local equivalent of gokz-jumpstats/jump_tracking and jump_reporting.
    /// It observes the authoritative GOKZ movement state instead of creating
    /// another movement implementation.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Jump Stats")]
    public sealed class GokzJumpStats : MonoBehaviour
    {
        [SerializeField] private LZM player;
        [SerializeField] private bool enabledInGame = true;

        public GokzJumpResult LastJump { get; private set; }
        public event Action<GokzJumpResult> JumpCompleted;
        public event Action<GokzJumpResult> JumpStarted;

        private int _lastJumpCount;
        private bool _wasGrounded;
        private float _takeoffTime;
        private Vector3 _takeoffPosition;
        private float _takeoffSpeed;
        private bool _takeoffPerfect;
        private bool _takeoffDucked;

        private void Awake()
        {
            ResolvePlayer();
        }

        private void FixedUpdate()
        {
            if (!enabledInGame)
                return;
            if (GokzProfile.Instance != null && !GokzProfile.Instance.JumpstatsEnabled)
                return;
            ResolvePlayer();
            if (player == null || player.MoveData.antiJumpstats)
                return;

            if (_lastJumpCount != player.GokzJumpCount)
            {
                _lastJumpCount = player.GokzJumpCount;
                _takeoffTime = Time.fixedTime;
                _takeoffPosition = player.transform.position;
                _takeoffSpeed = player.GokzLastTakeoffSpeed;
                _takeoffPerfect = player.GokzLastJumpWasPerfect;
                _takeoffDucked = player.GokzLastJumpWasDuck;
                JumpStarted?.Invoke(new GokzJumpResult
                {
                    number = _lastJumpCount,
                    type = _lastJumpCount > 1 ? GokzJumpType.Bhop : GokzJumpType.LongJump,
                    takeoffSpeed = _takeoffSpeed,
                    perfect = _takeoffPerfect,
                    ducked = _takeoffDucked
                });
            }

            bool grounded = player.GokzGrounded || player.MoveData.grounded;
            if (!_wasGrounded && grounded && _lastJumpCount > 0 && _takeoffTime > 0f)
            {
                Vector3 offset = player.transform.position - _takeoffPosition;
                Vector3 up = player.MoveData.reverseGravity < 0f ? Vector3.down : Vector3.up;
                Vector3 horizontalOffset = Vector3.ProjectOnPlane(offset, up);
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(player.MoveData.Velocity, up);
                float landingSpeed = horizontalVelocity.magnitude *
                    SourceMovementCore.HammerUnitsPerUnityUnit;
                float verticalSpeed = Mathf.Abs(Vector3.Dot(player.MoveData.Velocity, up)) *
                    SourceMovementCore.HammerUnitsPerUnityUnit;
                GokzJumpType jumpType = _lastJumpCount > 1
                    ? GokzJumpType.Bhop
                    : GokzJumpType.LongJump;
                float distance = horizontalOffset.magnitude *
                    SourceMovementCore.HammerUnitsPerUnityUnit;

                LastJump = new GokzJumpResult
                {
                    number = _lastJumpCount,
                    type = jumpType,
                    tier = GetTier(jumpType, distance),
                    takeoffSpeed = _takeoffSpeed,
                    landingSpeed = landingSpeed,
                    airtime = Time.fixedTime - _takeoffTime,
                    distance = distance,
                    perfect = _takeoffPerfect,
                    ducked = _takeoffDucked,
                    deadstrafed = verticalSpeed > 0f && verticalSpeed < 140f
                };
                JumpCompleted?.Invoke(LastJump);
            }

            _wasGrounded = grounded;
        }

        private GokzJumpTier GetTier(GokzJumpType type, float distance)
        {
            GokzMode mode = GokzRuntime.Instance != null
                ? GokzRuntime.Instance.Mode
                : GokzMode.KzTimer;

            // Exact values from cfg/sourcemod/gokz/gokz-jumpstats-tiers.cfg
            // for the two jump classes the local controller can currently
            // classify without Source ladder/trigger metadata.
            float[] thresholds;
            if (type == GokzJumpType.LongJump)
            {
                thresholds = mode == GokzMode.Vanilla
                    ? new[] { 210f, 235f, 240f, 245f, 248f, 250f }
                    : new[] { 215f, 265f, 270f, 275f, 280f, 282f };
            }
            else
            {
                if (mode == GokzMode.Vanilla)
                    thresholds = new[] { 150f, 230f, 233f, 235f, 238f, 240f };
                else if (mode == GokzMode.SimpleKz)
                    thresholds = new[] { 215f, 265f, 270f, 275f, 280f, 282f };
                else
                    thresholds = new[] { 215f, 340f, 345f, 350f, 355f, 357f };
            }

            if (distance >= thresholds[5]) return GokzJumpTier.Wrecker;
            if (distance >= thresholds[4]) return GokzJumpTier.Ownage;
            if (distance >= thresholds[3]) return GokzJumpTier.Godlike;
            if (distance >= thresholds[2]) return GokzJumpTier.Perfect;
            if (distance >= thresholds[1]) return GokzJumpTier.Impressive;
            if (distance >= thresholds[0]) return GokzJumpTier.Meh;
            return GokzJumpTier.None;
        }

        private void ResolvePlayer()
        {
            if (player != null)
                return;
            player = LZM.Player;
            if (player == null)
                player = FindFirstObjectByType<LZM>();
        }
    }
}
