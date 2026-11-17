using System.Collections.Generic;
using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// KZTimer movement port.
    ///
    /// This is deliberately independent from MovementConfig. The values and
    /// the order of operations come from GOKZ KZTimer and the Source
    /// CGameMovement implementation. The active hull is an axis-aligned
    /// Source player box and is swept manually; CharacterController is not
    /// used by this path.
    /// </summary>
    public sealed class SourceMovementCore
    {
        public const int TickRate = 128;
        public const float TickInterval = 1f / TickRate;
        public const float HammerUnitsPerUnityUnit = 52.4934383202f;

        private const float UnitScale = 1f / HammerUnitsPerUnityUnit;
        // Source's default client command speeds (in HU/s). These are the
        // values that CGameMovement receives in CMoveData; m_flMaxSpeed is a
        // separate server-side crop applied by CheckParameters.
        private const float SourceClientMoveSpeed = 450f;

        // Official GOKZ mode values. The KZTimer profile is the default;
        // SimpleKZ and Vanilla use the same Source call path with their own
        // replicated mode cvars.
        private GokzMode _mode = GokzMode.KzTimer;
        private GokzModeSettings _settings = GokzModeSettings.For(GokzMode.KzTimer);

        private float SpeedNormal => _settings.maxSpeed * UnitScale;
        private float GetPlayerMaxSpeed(float realPreVelMod) =>
            GokzMovementRules.GetPlayerMaxSpeed(
                _settings.maxSpeed,
                realPreVelMod) * UnitScale * GetStaminaSpeedScale();
        private float PreVelModMax => _settings.preVelModMax;
        private float PerfSpeedCap => _settings.perfSpeedCap * UnitScale;
        private float Accelerate => _settings.accelerate;
        private float AirAccelerate => _settings.airAccelerate;
        private float AirMaxWishSpeed => _settings.airMaxWishSpeed * UnitScale;
        private float Friction => _settings.friction;
        private float StopSpeed => _settings.stopSpeed * UnitScale;
        private float Gravity => _settings.gravity * UnitScale;
        private float JumpImpulse => _settings.jumpImpulse * UnitScale;
        private float ClientMaxSpeed => _settings.clientMaxSpeed * UnitScale;
        private float MaxVelocity => _settings.maxVelocity * UnitScale;
        private float WalkableNormal => _settings.walkableNormal;
        private float WaterAccelerate => _settings.waterAccelerate;
        private float WaterMoveSpeedMultiplier => _settings.waterMoveSpeedMultiplier;

        // Source player hull: {-16, -16, 0} to {16, 16, 72}; duck max Z 54.
        private const float PlayerHullHeight = 72f * UnitScale;
        private const float DuckHullHeight = 54f * UnitScale;
        private const float HullRadius = 16f * UnitScale;
        private const float StepHeight = 18f * UnitScale;
        private const float SourceStepEpsilon = 0.03125f * UnitScale;
        private const float CollisionSkin = 0.001f;
        private const float SourceMinimumMoveFraction = 0.0001f;
        private const float SourceCheckStuckInterval = 1f;
        private const float SourceStaminaRange = 100f;

        // CS:GO's continuous duck state. GOKZ does not replace Source's duck
        // animation; it only prevents the engine's duck-spam slowdown from
        // dropping below the one-duck value.
        private const float DuckSpeedNormal = 8f;
        private const float DuckSpeedMinimum = 6.0234375f;
        private const float DuckSpeedAlwaysMinimum = 1.5f;
        private const float DuckSpamPenalty = 2f;
        private const float DuckSpeedModifier = 0.34f;
        private const float WalkSpeedModifier = 0.52f;
        private const float DuckEyeStanding = 64f;
        private const float DuckEyeCrouched = 46f;
        // Kept only by UpdateDuckStateLegacy below as a reference to the
        // generic Source CGameMovement duck routine. The active path uses
        // CCSGameMovement::Duck and does not use this timer.
        private const float DuckTransitionTimeMilliseconds = 1000f;
        private const float TimeToDuckMilliseconds = 200f;
        private const float TimeToUnduckMilliseconds = 200f;
        private const float DuckAirOriginOffset = 9f * UnitScale;
        private const float WaterJumpDurationMilliseconds = 2000f;
        private const float WaterJumpVelocity = 256f * UnitScale;
        private const float WaterJumpForwardDistance = 24f * UnitScale;
        private const float WaterJumpHeight = 8f * UnitScale;
        private const float WaterJumpTraceDistance = 1024f * UnitScale;

        // KZTimerGlobal pre-strafe algorithm.
        private const float PreSpeedThreshold = 248.9f * UnitScale;
        private const float PreTickIncrement = 0.0009f;
        private const float PreTickIncrementFast = 0.001f;
        private const float PreTickDecrement = 0.04f;
        private const float PreTickTurnDecay = 0.0045f;
        private const int PreTickTurnWindow = 75;
        private const float SimpleMaxRewardTurnRate = 0.703125f;
        private const float SimpleMaxTurnRateDecrement = 0.015625f;
        private const float SimpleSpeedMax = 26.54321f;
        private const float SimpleSpeedIncrement = 0.35f;
        private const float SimpleSpeedDecrementMidair = 0.2824f;
        private const int SimpleGraceTicks = 3;

        private ISurfControllable _surfer;
        private Vector3 _movementUp = Vector3.up;
        private Vector3 _colliderUp = Vector3.up;
        private Vector3 _groundNormal = Vector3.up;
        private RaycastHit _groundHit;
        private Vector3 _previousViewForward = Vector3.forward;
        private bool _hasPreviousView;

        private float _preVelMod = 1f;
        private float _preVelModLastChange;
        private int _preTickCounter;
        private float _elapsed;

        private bool _landedThisTick;
        private bool _landingAvailable;
        private bool _startGrounded;
        private bool _tickStarted;
        private bool _isCrouched;
        private bool _ducking;
        private float _duckAmount;
        // GOKZ changes the Source duck state through movement hooks, but it
        // does not drive the first-person eye from m_bDucked. Keep the eye
        // presentation separate from the physical hull so an airborne duck
        // can crouch the lower collision box without moving the view.
        private float _eyeDuckAmount;
        private float _duckSpeed = DuckSpeedNormal;
        private Vector3 _lastFullDuckSpeedPosition;
        // Reference-only state for UpdateDuckStateLegacy.
        private float _duckTimeMilliseconds;
        private bool _rawDuckHeld;
        private bool _duckInputInitialized;
        private float _lastDuckTime = float.NegativeInfinity;
        private bool _inDuckJump;
        private bool _duckJumpTimeActive;
        private bool _wasPhysicallyCrouched;
        private bool _duckedAtProcessStart;
        private Vector3 _velocityBeforeDuckState;
        private int _movementTick;
        private int _landingTick = -1;
        private int _lastJumpRequestTick = int.MinValue;
        private bool _hullInitialized;
        private bool _wasCrouching;
        private bool _wasJumping;
        private bool _wasJumpHeld;
        // Source m_nOldButtons contains the previous command's IN_JUMP bit,
        // including one-tick mouse-wheel jump pulses. Keeping only the
        // physical key-held state loses that bit for consecutive wheel
        // commands and can allow a second jump that Source rejects.
        private bool _oldJumpButton;
        private bool _previousWalkMoved;
        private bool _walkMovedThisTick;
        private bool _hasWalkMovedSinceLastJump;
        private bool _onLadderThisTick;
        private bool _wasOnLadder;
        private float _ladderJumpBlockedUntil;
        private float _waterJumpTimeMilliseconds;
        private Vector3 _waterJumpVelocity;
        private bool _waterJumpProcessedThisTick;
        private float _lastGroundSurfaceMaxSpeedFactor = 1f;
        private int _stuckOffsetIndex;
        private float _nextStuckCheckTime;
        private bool _movementBlockedThisTick;
        private float _simpleBonusSpeed;
        private float _simpleTurnRate;
        private int _simpleTicksSinceIncrement;
        private bool _simpleTurningLeft;
        private float _landingPreVelMod = 1f;
        private Vector3 _landingVelocity;
        private float _stamina;

        private Vector3 _movementVelocity;
        private Vector3 _baseVelocity;
        // Source carries the ground entity's absolute velocity through
        // CGameMovement::SetGroundEntity. Unity exposes it directly for a
        // Rigidbody, but transform-driven platforms have no velocity
        // property, so derive one per 128-tick command from their pose.
        private Transform _sampledGroundTransform;
        private Vector3 _sampledGroundPosition;
        private Vector3 _sampledGroundVelocity;
        private bool _hasGroundTransformSample;
        private readonly Collider[] _hullOverlaps = new Collider[32];
        private readonly Collider[] _sourceVolumeOverlaps = new Collider[64];
        private readonly HashSet<GokzMovementVolume> _sourceVolumesThisTick =
            new HashSet<GokzMovementVolume>();
        private readonly HashSet<GokzMovementVolume> _consumedSourcePushVolumes =
            new HashSet<GokzMovementVolume>();
        private readonly RaycastHit[] _moveHits = new RaycastHit[8];
        private BoxCollider _sourceHullCollider;
        private Transform _sourceHullTransform;
        private Rigidbody _sourceRigidbody;
        private bool _collisionReady;
        private float _sourceGravityScale = 1f;
        private Vector3 _sourceTriggerBaseVelocity;
        private bool _sourcePushDetachedThisTick;
        private bool _originalCharacterControllerEnabled;
        private bool _originalRigidbodyKinematic;
        private bool _originalRigidbodyUseGravity;
        private bool _originalRigidbodyDetectCollisions;
        private CollisionDetectionMode _originalCollisionDetectionMode;
        private RigidbodyConstraints _originalRigidbodyConstraints;
        private float _originalBoxContactOffset;

        public bool Grounded { get; private set; }
        public bool Crouched => _isCrouched;
        public float DuckAmount => _duckAmount;
        public float EyeDuckAmount => _eyeDuckAmount;
        public bool LastJumpWasPerfect { get; private set; }
        public bool LastJumpWasDuck { get; private set; }
        public int JumpCount { get; private set; }
        public float LastTakeoffSpeed { get; private set; }
        public float Stamina => _stamina;
        public float Speed { get; private set; }
        public Vector3 MovementVelocity => _movementVelocity;
        public GokzMode Mode => _mode;
        public float SourceGravityScale => _sourceGravityScale;
        public Vector3 SourceTriggerBaseVelocity => _sourceTriggerBaseVelocity;

        public void SetMode(GokzMode mode)
        {
            if (_mode == mode)
                return;

            _mode = mode;
            _settings = GokzModeSettings.For(mode);
            _preVelMod = 1f;
            _preVelModLastChange = _elapsed;
            _preTickCounter = 0;
            _simpleBonusSpeed = 0f;
            _simpleTurnRate = 0f;
            _simpleTicksSinceIncrement = 0;
            _simpleTurningLeft = false;
            _landingPreVelMod = 1f;
            _landingVelocity = Vector3.zero;
            _stamina = 0f;
        }

        public void EnableSourceCollision(ISurfControllable surfer)
        {
            if (_collisionReady)
                return;

            CharacterController character = surfer.CC;
            if (character == null)
                return;

            _originalCharacterControllerEnabled = character.enabled;
            character.enabled = false;

            Transform sourceHull = character.transform.Find("GokzSourceHull");
            _sourceHullCollider = sourceHull != null
                ? sourceHull.GetComponent<BoxCollider>()
                : null;
            if (_sourceHullCollider == null)
            {
                GameObject hullObject = new GameObject("GokzSourceHull");
                hullObject.layer = character.gameObject.layer;
                sourceHull = hullObject.transform;
                sourceHull.SetParent(character.transform, false);
                _sourceHullCollider = hullObject.AddComponent<BoxCollider>();
            }

            _sourceHullTransform = _sourceHullCollider.transform;
            ConfigureSourceHullTransform(character.transform);
            _originalBoxContactOffset = _sourceHullCollider.contactOffset;
            _sourceHullCollider.isTrigger = false;
            _sourceHullCollider.enabled = true;
            _sourceHullCollider.contactOffset = CollisionSkin;

            _sourceRigidbody = character.GetComponent<Rigidbody>();
            if (_sourceRigidbody != null)
            {
                _originalRigidbodyKinematic = _sourceRigidbody.isKinematic;
                _originalRigidbodyUseGravity = _sourceRigidbody.useGravity;
                _originalRigidbodyDetectCollisions = _sourceRigidbody.detectCollisions;
                _originalCollisionDetectionMode = _sourceRigidbody.collisionDetectionMode;
                _originalRigidbodyConstraints = _sourceRigidbody.constraints;
                _sourceRigidbody.isKinematic = true;
                _sourceRigidbody.useGravity = false;
                _sourceRigidbody.detectCollisions = true;
                _sourceRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                _sourceRigidbody.constraints |= RigidbodyConstraints.FreezeRotation;
            }

            _collisionReady = true;
            UpdateBoxCollider(surfer.MoveData.crouching);
            Physics.SyncTransforms();
        }

        public void DisableSourceCollision(ISurfControllable surfer)
        {
            if (!_collisionReady)
                return;

            if (_sourceHullCollider != null)
                _sourceHullCollider.enabled = false;
            if (_sourceRigidbody != null)
            {
                _sourceRigidbody.isKinematic = _originalRigidbodyKinematic;
                _sourceRigidbody.useGravity = _originalRigidbodyUseGravity;
                _sourceRigidbody.detectCollisions = _originalRigidbodyDetectCollisions;
                _sourceRigidbody.collisionDetectionMode = _originalCollisionDetectionMode;
                _sourceRigidbody.constraints = _originalRigidbodyConstraints;
            }
            if (_sourceHullCollider != null)
                _sourceHullCollider.contactOffset = _originalBoxContactOffset;
            if (surfer.CC != null)
                surfer.CC.enabled = _originalCharacterControllerEnabled;
            _collisionReady = false;
        }

        public void Move(ISurfControllable surfer)
        {
            if (!_collisionReady || surfer.MoveData.playerTransform == null)
                return;

            ResolveInitialPenetration(surfer.MoveData.playerTransform);
            Vector3 startPosition = surfer.MoveData.playerTransform.position;
            // MoveData.Velocity is authoritative. The cached velocity is a
            // diagnostic value and must not swallow a velocity written by a
            // checkpoint, replay or teleport between Process and Move.
            Vector3 startVelocity = surfer.MoveData.Velocity + _baseVelocity;
            Vector3 startFinalVelocity = startVelocity;
            Vector3 halfExtents = GetBoxHalfExtents(_isCrouched);

            // Source's TryPlayerMove first attempts a slide. On a grounded
            // wall hit, StepMove tries the same move one player-step higher
            // and keeps that path only when it travels farther. This is the
            // part CharacterController used to hide from the old controller.
            Vector3 slidePosition;
            Vector3 slideVelocity;
            Vector3 slideFinalVelocity;
            bool hitWall;
            TrySlideMove(
                startPosition,
                startVelocity,
                startFinalVelocity,
                TickInterval,
                halfExtents,
                out slidePosition,
                out slideVelocity,
                out slideFinalVelocity,
                out hitWall);

            Vector3 bestPosition = slidePosition;
            Vector3 bestVelocity = slideVelocity;
            Vector3 bestFinalVelocity = slideFinalVelocity;

            if (Grounded && Vector3.Dot(startVelocity, _movementUp) <= 140f * UnitScale && hitWall)
            {
                Vector3 stepPosition;
                Vector3 stepVelocity;
                Vector3 stepFinalVelocity;
                if (TryStepMove(
                    startPosition,
                    startVelocity,
                    startFinalVelocity,
                    halfExtents,
                    out stepPosition,
                    out stepVelocity,
                    out stepFinalVelocity))
                {
                    Vector3 slidePlanar = Vector3.ProjectOnPlane(slidePosition - startPosition, _movementUp);
                    Vector3 stepPlanar = Vector3.ProjectOnPlane(stepPosition - startPosition, _movementUp);
                    if (stepPlanar.sqrMagnitude > slidePlanar.sqrMagnitude + CollisionSkin * CollisionSkin)
                    {
                        bestPosition = stepPosition;
                        bestVelocity = stepVelocity;
                        bestFinalVelocity = stepFinalVelocity;
                    }
                }
            }

            if (Grounded)
                StayOnGround(ref bestPosition, halfExtents);

            bestVelocity -= _baseVelocity;
            bestFinalVelocity -= _baseVelocity;

            surfer.MoveData.playerTransform.position = bestPosition;
            surfer.MoveData.Origin = bestPosition;
            surfer.MoveData.Velocity = bestFinalVelocity;
            _movementVelocity = bestVelocity;
            Physics.SyncTransforms();
        }

        private void TrySlideMove(
            Vector3 startPosition,
            Vector3 startVelocity,
            Vector3 startFinalVelocity,
            float duration,
            Vector3 halfExtents,
            out Vector3 position,
            out Vector3 velocity,
            out Vector3 finalVelocity,
            out bool hitWall)
        {
            position = startPosition;
            velocity = startVelocity;
            finalVelocity = startFinalVelocity;
            hitWall = false;

            // This is the Source CGameMovement::TryPlayerMove plane solver.
            // A sequence of independent clips is not equivalent: in a corner
            // Source tests every accumulated plane, then follows the crease
            // of two planes or stops. That distinction is what keeps wall
            // strafes and edge contacts from gaining/losing velocity.
            Vector3 primalVelocity = startVelocity;
            Vector3 originalVelocity = startVelocity;
            // Source uses MAX_CLIP_PLANES = 5. Four planes are enough for
            // most rooms, but the fifth matters in acute brush corners.
            Vector3[] planes = new Vector3[5];
            int numPlanes = 0;
            float timeLeft = duration;
            float allFraction = 0f;

            // Source keeps MAX_CLIP_PLANES at five but TryPlayerMove makes
            // four bump attempts per tick.
            for (int bump = 0; bump < 4 && timeLeft > 0.000001f; bump++)
            {
                if (velocity.sqrMagnitude <= 0.00000001f)
                    break;

                // Match trace_t::allsolid. Unity's sweep can report a hit
                // without marking an initial overlap, so explicitly test the
                // current Source hull before trying the next bump.
                if (IsHullStuckAt(position, halfExtents))
                {
                    velocity = Vector3.zero;
                    break;
                }

                float distance = velocity.magnitude * timeLeft;
                if (distance <= CollisionSkin)
                {
                    // Do not turn a sub-skin-size movement into zero velocity.
                    // At the apex of a jump the displacement can be smaller than
                    // the collision skin for one 128-tick step. Source still
                    // carries the velocity through that tick; zeroing it here
                    // makes the player hang forever because gravity is then
                    // repeatedly overwritten by this move loop.
                    position += velocity * timeLeft;
                    allFraction += 1f;
                    break;
                }

                Vector3 direction = velocity / velocity.magnitude;
                int hitCount = SweepBox(position, halfExtents, direction, distance);
                RaycastHit nearest;
                if (!TryGetNearestHit(hitCount, out nearest))
                {
                    position += velocity * timeLeft;
                    allFraction += 1f;
                    if (IsHullStuckAt(position, halfExtents))
                        velocity = Vector3.zero;
                    break;
                }

                float safeDistance = Mathf.Clamp(nearest.distance - CollisionSkin, 0f, distance);
                float fraction = distance > 0f ? safeDistance / distance : 0f;
                if (fraction > 0f && fraction < SourceMinimumMoveFraction)
                {
                    // CGameMovement::TryPlayerMove discards extremely small
                    // trace fractions before accumulating time. Keeping a
                    // Unity skin contact as a real move makes acute corners
                    // oscillate at 128 tick.
                    fraction = 0f;
                }
                if (fraction > 0f)
                {
                    position += velocity * (timeLeft * fraction);
                    originalVelocity = velocity;
                    numPlanes = 0;
                }

                allFraction += fraction;
                Vector3 planeNormal = nearest.normal.normalized;
                if (Mathf.Abs(Vector3.Dot(planeNormal, _movementUp)) <
                    0.0001f)
                {
                    planeNormal = Vector3.ProjectOnPlane(
                        planeNormal,
                        _movementUp).normalized;
                }

                if (Vector3.Dot(planeNormal, _movementUp) < WalkableNormal)
                    hitWall = true;

                // A hit at the end of the trace means the whole displacement
                // was covered. There is no remaining time to clip.
                if (fraction >= 0.999999f)
                    break;

                timeLeft -= timeLeft * fraction;
                if (numPlanes >= planes.Length)
                {
                    velocity = Vector3.zero;
                    break;
                }

                planes[numPlanes++] = planeNormal;

                bool foundValidPlane = false;
                Vector3 candidateVelocity = Vector3.zero;
                for (int i = 0; i < numPlanes; i++)
                {
                    candidateVelocity = ClipVelocity(originalVelocity, planes[i], 1f);
                    bool valid = true;
                    for (int j = 0; j < numPlanes; j++)
                    {
                        if (j != i && Vector3.Dot(candidateVelocity, planes[j]) < -0.000001f)
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (valid)
                    {
                        velocity = candidateVelocity;
                        foundValidPlane = true;
                        break;
                    }
                }

                if (!foundValidPlane)
                {
                    if (numPlanes != 2)
                    {
                        velocity = Vector3.zero;
                        break;
                    }

                    Vector3 crease = Vector3.Cross(planes[0], planes[1]);
                    if (crease.sqrMagnitude <= 0.00000001f)
                    {
                        velocity = Vector3.zero;
                        break;
                    }

                    crease.Normalize();
                    velocity = crease * Vector3.Dot(crease, velocity);
                }

                // Source stops when clipping would reverse the original
                // movement. This prevents tiny oscillations in acute corners.
                if (Vector3.Dot(velocity, primalVelocity) <= 0f)
                {
                    velocity = Vector3.zero;
                    break;
                }
            }

            if (allFraction <= 0f)
                velocity = Vector3.zero;

            finalVelocity = velocity;
        }

        private bool TryStepMove(
            Vector3 startPosition,
            Vector3 startVelocity,
            Vector3 startFinalVelocity,
            Vector3 halfExtents,
            out Vector3 position,
            out Vector3 velocity,
            out Vector3 finalVelocity)
        {
            position = startPosition;
            velocity = startVelocity;
            finalVelocity = startFinalVelocity;

            // Source first keeps the ordinary (down) TryPlayerMove result.
            // The stepped path is only allowed to replace it if it travels
            // farther in the floor plane.
            Vector3 downPosition;
            Vector3 downFinalVelocity;
            TrySlideMove(
                startPosition,
                startVelocity,
                startFinalVelocity,
                TickInterval,
                halfExtents,
                out downPosition,
                out _,
                out downFinalVelocity,
                out _);

            // Raise the hull. If the full step is blocked, this is not a
            // stair and the ordinary clipped path remains authoritative. A
            // partial trace is still a valid Source headroom result.
            RaycastHit upHit;
            float stepTraceDistance = StepHeight + SourceStepEpsilon;
            int upHitCount = SweepBox(
                startPosition,
                halfExtents,
                _movementUp,
                stepTraceDistance);
            Vector3 raisedPosition;
            if (TryGetNearestHit(upHitCount, out upHit))
            {
                if (IsHullStuckAt(startPosition, halfExtents))
                    return false;

                float availableRise = Mathf.Max(
                    0f,
                    Mathf.Min(
                        stepTraceDistance,
                        upHit.distance - CollisionSkin));
                if (availableRise <= CollisionSkin)
                    return false;
                raisedPosition = startPosition + _movementUp * availableRise;
            }
            else
            {
                raisedPosition = startPosition + _movementUp * stepTraceDistance;
            }

            Vector3 upPosition;
            Vector3 upFinalVelocity;
            TrySlideMove(
                raisedPosition,
                startVelocity,
                startFinalVelocity,
                TickInterval,
                halfExtents,
                out upPosition,
                out _,
                out upFinalVelocity,
                out _);

            // StepMove lowers the raised path back onto the walkable surface.
            // A missing/steep hit means Source restores the ordinary path.
            RaycastHit downHit;
            int downHitCount = SweepBox(
                upPosition,
                halfExtents,
                -_movementUp,
                stepTraceDistance);
            // Source checks trace.plane.normal[2] before accepting the
            // stepped path.  An empty trace has a zero/default plane normal,
            // so a missing lower hit must restore the ordinary path instead
            // of leaving the player in the air at the full step distance.
            if (!TryGetNearestHit(downHitCount, out downHit) ||
                Vector3.Dot(downHit.normal, _movementUp) < WalkableNormal)
                return false;

            float safeDownDistance = Mathf.Clamp(
                downHit.distance - CollisionSkin,
                0f,
                stepTraceDistance);
            Vector3 steppedPosition = upPosition -
                _movementUp * safeDownDistance;

            Vector3 downPlanar = Vector3.ProjectOnPlane(
                downPosition - startPosition,
                _movementUp);
            Vector3 steppedPlanar = Vector3.ProjectOnPlane(
                steppedPosition - startPosition,
                _movementUp);
            if (downPlanar.sqrMagnitude >
                steppedPlanar.sqrMagnitude + CollisionSkin * CollisionSkin)
                return false;

            position = steppedPosition;
            // Source keeps the horizontal velocity from the stepped path but
            // copies the vertical component from the ordinary path.
            finalVelocity = Vector3.ProjectOnPlane(
                upFinalVelocity,
                _movementUp) +
                _movementUp * Vector3.Dot(downFinalVelocity, _movementUp);
            velocity = finalVelocity;
            return true;
        }

        private int SweepBox(
            Vector3 position,
            Vector3 halfExtents,
            Vector3 direction,
            float distance)
        {
            Vector3 queryHalfExtents = GetQueryHalfExtents(halfExtents);
            Vector3 center = position + _movementUp * halfExtents.y;
            return Physics.BoxCastNonAlloc(
                center,
                queryHalfExtents,
                direction,
                _moveHits,
                Quaternion.identity,
                distance,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);
        }

        private bool TryGetNearestHit(int hitCount, out RaycastHit nearest)
        {
            nearest = default;
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _moveHits[i];
                if (candidate.collider == null || candidate.collider == _sourceHullCollider ||
                    (_surfer != null && _surfer.MoveData.playerTransform != null &&
                     candidate.collider.transform.IsChildOf(_surfer.MoveData.playerTransform)))
                    continue;
                if (candidate.distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = candidate.distance;
                    found = true;
                }
            }
            return found;
        }

        public void Process(ISurfControllable surfer)
        {
            _surfer = surfer;
            _elapsed += TickInterval;
            _movementTick++;
            // GOKZ SimpleKZ records the last command containing IN_JUMP in
            // OnPlayerRunCmdPost, not only a newly pressed jump pulse. This
            // matters when a held space key is used to satisfy the three
            // command SimpleKZ perf window after landing.
            if (_surfer.MoveData.wishJump || _surfer.MoveData.jumpHeld)
                _lastJumpRequestTick = _movementTick;
            LastJumpWasPerfect = false;
            _sourcePushDetachedThisTick = false;

            // The project can temporarily reverse gravity or disable it in a
            // trigger. The normal state remains the exact GOKZ +Z-up path.
            _movementUp = _surfer.MoveData.reverseGravity < 0f ? Vector3.down : Vector3.up;

            // Sample the ground entity before recategorizing. This is the
            // Unity equivalent of Source reading GetAbsVelocity() from the
            // entity attached during the previous command.
            bool groundAtTickStart = Grounded;
            UpdateSourceMovementVolumes(groundAtTickStart);
            Vector3 groundVelocityAtTickStart = groundAtTickStart
                ? GetGroundEntityVelocity(_groundHit)
                : Vector3.zero;
            UpdateGroundEntityVelocitySample();

            // The active controller moves the root Transform directly. Keep
            // the BoxCollider used by the sweep queries synchronized before
            // categorizing the next tick's ground state.
            ConfigureSourceHullTransform(_surfer.MoveData.playerTransform);
            Physics.SyncTransforms();
            ResolveInitialPenetration(_surfer.MoveData.playerTransform);
            _movementBlockedThisTick = false;
            if (TrySourceStuckRecovery())
            {
                // CGameMovement::CheckStuck returns 1 when it cannot free the
                // player and PlayerMove exits before Duck/FullWalkMove. Keep
                // that ordering: do not apply another gravity half-step or
                // let the normal movement branch push deeper into the solid.
                _movementBlockedThisTick = true;
                _movementVelocity = Vector3.zero;
                _surfer.MoveData.Velocity = Vector3.zero;
                Grounded = CategorizePosition(out _groundHit);
                if (_sourcePushDetachedThisTick)
                    Grounded = false;
                _groundNormal = Grounded ? _groundHit.normal : _movementUp;
                _startGrounded = groundAtTickStart;
                _landedThisTick = false;
                _landingAvailable = false;
                _duckedAtProcessStart = _isCrouched;
                _surfer.MoveData.SurfaceFriction = Grounded
                    ? GetSurfaceFriction(_groundHit)
                    : 1f;
                _surfer.MoveData.SurfaceMaxSpeedFactor = Grounded
                    ? (_lastGroundSurfaceMaxSpeedFactor =
                        GetSurfaceMaxSpeedFactor(_groundHit))
                    : _lastGroundSurfaceMaxSpeedFactor;
                _surfer.MoveData.SurfaceJumpFactor = Grounded
                    ? GetSurfaceJumpFactor(_groundHit)
                    : 1f;
                _surfer.MoveData.grounded = Grounded;
                _surfer.MoveData.groundTransform = Grounded
                    ? _groundHit.transform
                    : null;
                _baseVelocity = groundAtTickStart && !Grounded
                    ? _surfer.BaseVelocity + groundVelocityAtTickStart +
                        _sourceTriggerBaseVelocity
                    : GetBaseVelocity(
                        _surfer.MoveData.playerTransform);
                _tickStarted = true;
                return;
            }
            Grounded = CategorizePosition(out _groundHit);
            if (_sourcePushDetachedThisTick)
                Grounded = false;
            _groundNormal = Grounded ? _groundHit.normal : _movementUp;
            _surfer.MoveData.SurfaceFriction = Grounded
                ? GetSurfaceFriction(_groundHit)
                : 1f;
            _surfer.MoveData.SurfaceMaxSpeedFactor = Grounded
                ? (_lastGroundSurfaceMaxSpeedFactor =
                    GetSurfaceMaxSpeedFactor(_groundHit))
                : _lastGroundSurfaceMaxSpeedFactor;
            _surfer.MoveData.SurfaceJumpFactor = Grounded
                ? GetSurfaceJumpFactor(_groundHit)
                : 1f;
            // This is Source's old ground entity state, not the result of the
            // new CategorizePosition trace. Finish() uses it to distinguish a
            // real landing from a player who was already grounded.
            _startGrounded = groundAtTickStart;
            _landedThisTick = _landingAvailable;
            _landingAvailable = false;
            _surfer.MoveData.grounded = Grounded;
            _surfer.MoveData.groundTransform = Grounded ? _groundHit.transform : null;
            // Source's StartGravity consumes the vertical component of the
            // ground entity base velocity before the movement trace. Cache it
            // before any jump/air branch changes the ground flag.
            _baseVelocity = groundAtTickStart && !Grounded
                ? _surfer.BaseVelocity + groundVelocityAtTickStart +
                    _sourceTriggerBaseVelocity
                : GetBaseVelocity(
                    _surfer.MoveData.playerTransform);
            _onLadderThisTick = false;
            _waterJumpProcessedThisTick = false;
            _wasOnLadder = _surfer.MoveData.climbingLadder;
            _surfer.MoveData.climbingLadder = false;
            _walkMovedThisTick = false;

            // GOKZ removes a newly pressed duck bind when jump is pressed on
            // the ground. Holding duck before the jump remains a real crouch
            // jump; only the tick-perfect jump+duck bind is removed.
            if (_mode != GokzMode.Vanilla && Grounded &&
                _surfer.MoveData.wishJump &&
                !_oldJumpButton && _surfer.MoveData.crouching && !_rawDuckHeld)
            {
                _surfer.MoveData.crouching = false;
            }

            _velocityBeforeDuckState = _surfer.MoveData.Velocity;
            // GOKZ's FixDisplacementStuck compares the flags observed at the
            // start of the previous command with the flags at this command's
            // start (gI_OldFlags versus GetEntityFlags). Capture that first
            // state before CCSGameMovement::Duck can change the hull.
            _duckedAtProcessStart = _isCrouched;
            ReduceMovementTimers();

            // GOKZ's OnPlayerRunCmd hooks run before CCSGameMovement::Duck.
            // CalcPrestrafeVelMod is the first hook, followed by the
            // KZTimer/SimpleKZ water and displacement fixes. Vanilla mode has
            // none of these SourceMod hooks.
            float realPreVelMod = UpdatePrestrafeModifier(Grounded);
            if (_mode != GokzMode.Vanilla &&
                _surfer.MoveData.waterLevel >= GokzWaterLevel.Waist &&
                _surfer.MoveData.crouching && !_wasCrouching)
            {
                TryWaterCrouchBoost();
            }

            // Fix a standing hull that was left embedded by the previous
            // unduck before evaluating this command's duck state.
            if (_mode != GokzMode.Vanilla)
                FixDisplacementStuck();

            // Source updates the CS:GO continuous duck amount before movement.
            // The physical hull is changed by FinishDuck/FinishUnDuck, while
            // the fractional amount is used for the eye and speed crop.
            UpdateDuckState();

            // This is the Source CGameMovement::FullWalkMove order:
            // StartGravity -> CheckJumpButton -> Friction -> WalkMove/AirMove.
            // Collision, CategorizePosition and FinishGravity happen in
            // Finish(), after the root transform has been swept.
            if (_waterJumpTimeMilliseconds > 0f)
            {
                // FullWalkMove gives WaterJump priority over the normal
                // water/air branches for the whole tick.
                _waterJumpProcessedThisTick = true;
                WaterJump();
            }
            else if (_surfer.MoveData.underwater)
            {
                // CGameMovement::FullWalkMove does not apply gravity while
                // swimming. It still processes an active water-jump timer,
                // then handles the jump button and regular WaterMove branch.
                TryWaterJump();
                if (_waterJumpTimeMilliseconds > 0f)
                {
                    WaterJump();
                }
                else
                {
                    CheckJumpButton();
                    WaterMove(realPreVelMod);
                }
            }
            else if (_surfer.MoveData.laddersEnabled && TryLadderMove())
            {
                // LadderMove is a complete movement branch in Source:
                // gravity and ground friction do not run for this tick.
            }
            else
            {
                // CGameMovement::FullWalkMove calls CheckWater before
                // StartGravity. CheckWater() returns true only above
                // WL_Feet, because WL_Feet is deliberately not enough for
                // WaterMove(). Therefore a player whose feet are in water
                // still receives both normal gravity half-steps, exactly as
                // the Source `if (!CheckWater())` branches do.
                bool usesWaterMove = _surfer.MoveData.waterLevel >=
                    GokzWaterLevel.Waist;
                if (!usesWaterMove)
                    StartGravity();
                CheckJumpButton();
                if (Grounded)
                    GroundMove(realPreVelMod);
                else
                    AirMove(realPreVelMod);
            }

            CheckVelocity(ref _surfer.MoveData.Velocity);
            CheckVelocity(ref _movementVelocity);
            CheckOrigin();
            _tickStarted = true;
        }

        public void Finish(ISurfControllable surfer)
        {
            if (!_tickStarted || _surfer != surfer)
                return;

            Physics.SyncTransforms();
            RaycastHit finalGroundHit;
            bool finalGrounded = CategorizePosition(out finalGroundHit);
            if (_sourcePushDetachedThisTick)
                finalGrounded = false;
            bool landed = !_startGrounded && finalGrounded;
            if (landed)
            {
                _landingTick = _movementTick;
                _groundHit = finalGroundHit;
                _groundNormal = finalGroundHit.normal;
                Vector3 incomingVelocity = _surfer.MoveData.Velocity;
                AddLandingStamina(Mathf.Max(
                    0f,
                    -Vector3.Dot(incomingVelocity, _movementUp) *
                        HammerUnitsPerUnityUnit));
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                    incomingVelocity,
                    _movementUp);
                ApplySlopeFix(
                    ref horizontalVelocity,
                    _surfer.MoveData.playerTransform.position,
                    landed);
                _surfer.MoveData.Velocity = horizontalVelocity +
                    _movementUp * Vector3.Dot(incomingVelocity, _movementUp);
                _landingVelocity = horizontalVelocity;
                _landingPreVelMod = _preVelMod;
            }
            else if (finalGrounded)
            {
                _groundHit = finalGroundHit;
                _groundNormal = finalGroundHit.normal;
            }

            _surfer.MoveData.SurfaceFriction = finalGrounded
                ? GetSurfaceFriction(finalGroundHit)
                : 1f;
            _surfer.MoveData.SurfaceMaxSpeedFactor = finalGrounded
                ? (_lastGroundSurfaceMaxSpeedFactor =
                    GetSurfaceMaxSpeedFactor(finalGroundHit))
                : _lastGroundSurfaceMaxSpeedFactor;
            _surfer.MoveData.SurfaceJumpFactor = finalGrounded
                ? GetSurfaceJumpFactor(finalGroundHit)
                : 1f;

            Grounded = finalGrounded;
            if (!_movementBlockedThisTick &&
                _surfer.MoveData.waterLevel < GokzWaterLevel.Waist &&
                !_onLadderThisTick &&
                !_waterJumpProcessedThisTick)
                FinishGravity();

            // Source explicitly clears the gravity component after the final
            // ground categorization. This is why a player that walks off an
            // edge starts falling, while a player that remains on a floor has
            // no accumulating downward velocity.
            if (Grounded && !_onLadderThisTick)
                RemoveVerticalVelocity(ref _surfer.MoveData.Velocity);

            _surfer.MoveData.grounded = Grounded;
            _surfer.MoveData.groundTransform = Grounded ? _groundHit.transform : null;
            _landingAvailable = landed;
            // GOKZ FixWaterBoost compares the raw previous IN_DUCK bit from
            // OnPlayerRunCmd, not CCSGameMovement's possibly anti-spam-
            // suppressed effective duck flag.
            _wasCrouching = _rawDuckHeld;
            _wasJumping = _surfer.MoveData.wishJump;
            _wasJumpHeld = _surfer.MoveData.jumpHeld;
            _oldJumpButton = _surfer.MoveData.wishJump ||
                _surfer.MoveData.jumpHeld;
            // Keep the previous command's starting flag for the next
            // OnPlayerRunCmd-equivalent pass. This must not be the final flag
            // after Duck(), otherwise an unduck transition can never be seen
            // by FixDisplacementStuck.
            _wasPhysicallyCrouched = _duckedAtProcessStart;
            _previousWalkMoved = _walkMovedThisTick;

            CheckVelocity(ref _surfer.MoveData.Velocity);
            CheckVelocity(ref _movementVelocity);
            CheckOrigin();
            Speed = new Vector3(
                Vector3.ProjectOnPlane(_surfer.MoveData.Velocity, _movementUp).x,
                Vector3.ProjectOnPlane(_surfer.MoveData.Velocity, _movementUp).y,
                Vector3.ProjectOnPlane(_surfer.MoveData.Velocity, _movementUp).z).magnitude;

            // A rendered input update may cover multiple fixed ticks. Consume
            // the view turn once, exactly as one Source user command is
            // consumed by one movement tick.
            _surfer.MoveData.ViewYawDelta = 0f;
            _movementBlockedThisTick = false;
            _tickStarted = false;
            _surfer = null;
        }

        public void ResetState()
        {
            _preVelMod = 1f;
            _preVelModLastChange = 0f;
            _preTickCounter = 0;
            _elapsed = 0f;
            _hasPreviousView = false;
            _landingAvailable = false;
            _startGrounded = false;
            _tickStarted = false;
            _wasCrouching = false;
            _wasJumping = false;
            _wasJumpHeld = false;
            _oldJumpButton = false;
            _previousWalkMoved = false;
            _walkMovedThisTick = false;
            _hasWalkMovedSinceLastJump = false;
            _onLadderThisTick = false;
            _wasOnLadder = false;
            _ladderJumpBlockedUntil = 0f;
            _waterJumpTimeMilliseconds = 0f;
            _waterJumpVelocity = Vector3.zero;
            _waterJumpProcessedThisTick = false;
            _lastGroundSurfaceMaxSpeedFactor = 1f;
            _stuckOffsetIndex = 0;
            _nextStuckCheckTime = 0f;
            _movementBlockedThisTick = false;
            _simpleBonusSpeed = 0f;
            _simpleTurnRate = 0f;
            _simpleTicksSinceIncrement = 0;
            _simpleTurningLeft = false;
            _landingPreVelMod = 1f;
            _landingVelocity = Vector3.zero;
            _stamina = 0f;
            _isCrouched = false;
            _ducking = false;
            _duckAmount = 0f;
            _eyeDuckAmount = 0f;
            _duckSpeed = DuckSpeedNormal;
            _lastFullDuckSpeedPosition = Vector3.zero;
            _duckTimeMilliseconds = 0f;
            _rawDuckHeld = false;
            _duckInputInitialized = false;
            _lastDuckTime = float.NegativeInfinity;
            _inDuckJump = false;
            _duckJumpTimeActive = false;
            _wasPhysicallyCrouched = false;
            _duckedAtProcessStart = false;
            _velocityBeforeDuckState = Vector3.zero;
            _movementTick = 0;
            _landingTick = -1;
            _lastJumpRequestTick = int.MinValue;
            _hullInitialized = false;
            _movementVelocity = Vector3.zero;
            _baseVelocity = Vector3.zero;
            _sourceGravityScale = 1f;
            _sourceTriggerBaseVelocity = Vector3.zero;
            _sourcePushDetachedThisTick = false;
            _sourceVolumesThisTick.Clear();
            _consumedSourcePushVolumes.Clear();
            _sampledGroundTransform = null;
            _sampledGroundPosition = Vector3.zero;
            _sampledGroundVelocity = Vector3.zero;
            _hasGroundTransformSample = false;
            JumpCount = 0;
            LastTakeoffSpeed = 0f;
            Grounded = false;
            LastJumpWasPerfect = false;
            LastJumpWasDuck = false;
            Speed = 0f;
        }

        private void StartGravity()
        {
            float gravity = GetGravityAcceleration();
            _surfer.MoveData.Velocity -= _movementUp * gravity * TickInterval * 0.5f;

            // CGameMovement::StartGravity applies one frame of the ground
            // entity's vertical base velocity to the player velocity, then
            // removes that vertical component from base velocity. Horizontal
            // base velocity remains available to TryPlayerMove in Move().
            float baseVelocityUp = Vector3.Dot(_baseVelocity, _movementUp);
            if (Mathf.Abs(baseVelocityUp) > 0.000001f)
            {
                _surfer.MoveData.Velocity += _movementUp *
                    (baseVelocityUp * TickInterval);
                _baseVelocity -= _movementUp * baseVelocityUp;
            }
        }

        private void FinishGravity()
        {
            float gravity = GetGravityAcceleration();
            _surfer.MoveData.Velocity -= _movementUp * gravity * TickInterval * 0.5f;
        }

        private void ReduceMovementTimers()
        {
            // CCSGameMovement::ReduceTimers only decrements the legacy
            // m_nDuckTimeMsecs field. CS:GO's overridden Duck() uses
            // m_flDuckAmount/m_flDuckSpeed instead, so there is no duck timer
            // to advance in this port.
            if (_settings.staminaMax > 0f &&
                _settings.staminaRecoveryRate > 0f &&
                _stamina > 0f)
            {
                _stamina = Mathf.Max(
                    0f,
                    _stamina - _settings.staminaRecoveryRate * TickInterval);
            }
        }

        private bool CheckJumpButton()
        {
            bool jumpRequested = _surfer.MoveData.wishJump ||
                (_settings.autoBhop && _surfer.MoveData.jumpHeld);

            // Source treats IN_JUMP in waist-deep water as swimming, not as a
            // normal ground jump. The button can remain held here; unlike a
            // ground jump this branch is intentionally not edge-triggered.
            if (_surfer.MoveData.underwater)
            {
                if (!_surfer.MoveData.wishJump && !_surfer.MoveData.jumpHeld)
                    return false;

                Grounded = false;
                _surfer.MoveData.grounded = false;
                float swimImpulse = (_surfer.MoveData.waterType ==
                    GokzWaterType.Slime ? 80f : 100f) * UnitScale;
                _surfer.MoveData.Velocity -= _movementUp *
                    Vector3.Dot(_surfer.MoveData.Velocity, _movementUp);
                _surfer.MoveData.Velocity += _movementUp * swimImpulse;
                return false;
            }

            if (!jumpRequested || !Grounded || _surfer.MoveData.antiBhop)
                return false;
            // Source checks m_nOldButtons here, not whether the previous
            // command contained a one-shot jump request. Holding IN_JUMP
            // through a landing must not pogo unless sv_autobunnyhopping is
            // enabled.
            if (!_settings.autoBhop && _oldJumpButton)
                return false;

            if (!_settings.enableBunnyhopping)
                PreventBunnyJumping();

            // Source SetGroundEntity(NULL) preserves the ground entity's
            // velocity when a jump starts. StartGravity has already consumed
            // and cleared the vertical base component for this command, so
            // restore only the platform's vertical component here; the
            // horizontal component is already cached in _baseVelocity.
            if (_groundHit.collider != null)
            {
                float groundVelocityUp = Vector3.Dot(
                    GetGroundEntityVelocity(_groundHit),
                    _movementUp);
                _baseVelocity += _movementUp * groundVelocityUp;
            }

            // MovementAPI's normal KZTimer perf is the landing command. The
            // SimpleKZ mode deliberately widens this to its source command
            // window: <=1 command since landing, or <=3 when the jump input
            // itself was within the last three commands.
            ApplyJump(IsHitPerfJump());
            return true;
        }

        private bool IsHitPerfJump()
        {
            if (_wasOnLadder)
                return false;

            if (_mode != GokzMode.SimpleKz)
                return _landedThisTick;

            if (_landingTick < 0)
                return false;

            int cmdsSinceLanding = _movementTick - _landingTick;
            int cmdsSinceJumpInput = _movementTick - _lastJumpRequestTick;
            return cmdsSinceLanding <= 1 ||
                (cmdsSinceLanding <= 3 && cmdsSinceJumpInput <= 3);
        }

        private void RemoveVerticalVelocity(ref Vector3 velocity)
        {
            velocity -= _movementUp * Vector3.Dot(velocity, _movementUp);
        }

        private void PreventBunnyJumping()
        {
            // CCSGameMovement::PreventBunnyJumping crops the complete
            // processing velocity to 1.1 * player maxspeed before adding the
            // jump impulse. Vanilla GOKZ hooks GetPlayerMaxSpeed to 250 HU.
            // CCSGameMovement::PreventBunnyJumping uses the player's
            // m_flMaxspeed (the client max speed, normally 320 HU/s), not
            // the GOKZ GetPlayerMaxSpeed hook (250 HU/s * prestrafe).
            float maxSpeed = 1.1f * ClientMaxSpeed;
            float speed = _surfer.MoveData.Velocity.magnitude;
            if (maxSpeed > 0f && speed > maxSpeed)
                _surfer.MoveData.Velocity *= maxSpeed / speed;
        }

        private bool CategorizePosition(out RaycastHit hit)
        {
            hit = default;
            if (!_collisionReady || _surfer.MoveData.playerTransform == null)
                return false;

            // CGameMovement::CategorizePosition refuses to attach a player
            // moving away from a surface faster than NON_JUMP_VELOCITY. This
            // matters on the first jump tick and prevents a fast upward move
            // from being reclassified as ground contact.
            float groundVelocityUp = Grounded
                ? Vector3.Dot(GetGroundEntityVelocity(_groundHit), _movementUp)
                : 0f;
            if (Vector3.Dot(_surfer.MoveData.Velocity, _movementUp) -
                groundVelocityUp > 140f * UnitScale)
                return false;

            bool crouchedHull = _hullInitialized ? _isCrouched : _surfer.MoveData.crouching;
            Vector3 halfExtents = GetBoxHalfExtents(crouchedHull);
            Vector3 center = _surfer.MoveData.playerTransform.position + _movementUp * halfExtents.y;
            Vector3 queryHalfExtents = GetQueryHalfExtents(halfExtents);
            // CGameMovement extends the ground trace by step size when the
            // player was already walking on ground. This is what keeps a
            // player attached while descending ramps and crossing small
            // displacement seams; a two-HU probe alone makes Unity produce
            // the visible one-tick airborne gap.
            float probeDistance = 2f * UnitScale + CollisionSkin +
                (Grounded && _surfer.MoveData.waterLevel < GokzWaterLevel.Eyes
                    ? StepHeight
                    : 0f);
            int hitCount = Physics.BoxCastNonAlloc(
                center,
                queryHalfExtents,
                -_movementUp,
                _moveHits,
                Quaternion.identity,
                probeDistance,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);

            bool found = TryGetNearestStandableHit(hitCount, out hit);
            if (!found)
            {
                // CGameMovement::TracePlayerBBoxForGround retries four
                // horizontal quadrants when the full hull first hits a steep
                // edge but one corner can still stand on a valid plane. Keep
                // the same half-box layout in Unity's X/Z ground plane.
                Vector3 quadrantHalfExtents = new Vector3(
                    halfExtents.x * 0.5f,
                    halfExtents.y,
                    halfExtents.z * 0.5f);
                Vector3[] quadrantOffsets =
                {
                    new Vector3(-quadrantHalfExtents.x, 0f,
                        -quadrantHalfExtents.z),
                    new Vector3(quadrantHalfExtents.x, 0f,
                        quadrantHalfExtents.z),
                    new Vector3(-quadrantHalfExtents.x, 0f,
                        quadrantHalfExtents.z),
                    new Vector3(quadrantHalfExtents.x, 0f,
                        -quadrantHalfExtents.z)
                };

                for (int quadrant = 0; quadrant < quadrantOffsets.Length;
                     quadrant++)
                {
                    int quadrantHitCount = Physics.BoxCastNonAlloc(
                        center + quadrantOffsets[quadrant],
                        GetQueryHalfExtents(quadrantHalfExtents),
                        -_movementUp,
                        _moveHits,
                        Quaternion.identity,
                        probeDistance,
                        GetCollisionMask(),
                        QueryTriggerInteraction.Ignore);
                    if (!TryGetNearestStandableHit(
                            quadrantHitCount,
                            out RaycastHit quadrantHit))
                        continue;

                    // TracePlayerBBoxForGround returns on the first
                    // standable quadrant in Source's fixed order; it does
                    // not choose the numerically nearest quadrant hit.
                    hit = quadrantHit;
                    return true;
                }
            }

            if (found)
                return true;

            // Do not carry the previous grounded state forward. Source's
            // CategorizePosition is a real hull trace every tick; retaining
            // the old flag makes a player float when walking off an edge.
            hit = default;
            return false;
        }

        private bool TryGetNearestStandableHit(
            int hitCount,
            out RaycastHit nearest)
        {
            nearest = default;
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _moveHits[i];
                if (candidate.collider == null ||
                    candidate.collider == _sourceHullCollider ||
                    (_surfer != null &&
                     _surfer.MoveData.playerTransform != null &&
                     candidate.collider.transform.IsChildOf(
                         _surfer.MoveData.playerTransform)) ||
                    Vector3.Dot(candidate.normal, _movementUp) < WalkableNormal ||
                    candidate.distance >= nearestDistance)
                    continue;

                nearest = candidate;
                nearestDistance = candidate.distance;
                found = true;
            }

            return found;
        }

        private void UpdateDuckState()
        {
            if (!_collisionReady)
                return;

            if (!_hullInitialized)
            {
                _hullInitialized = true;
                _isCrouched = false;
                _ducking = false;
                _duckAmount = 0f;
                _eyeDuckAmount = 0f;
                _duckSpeed = DuckSpeedNormal;
                _lastFullDuckSpeedPosition = _surfer.MoveData.playerTransform.position;
                _rawDuckHeld = false;
                _duckInputInitialized = true;
                _lastDuckTime = float.NegativeInfinity;
            }

            // The active gravity direction changes which side of the feet the
            // Source hull occupies. Source itself is Z-up; this is the only
            // adapter needed for the project's reverse-gravity extension.
            if ((_colliderUp - _movementUp).sqrMagnitude > 0.000001f)
                UpdateBoxCollider(_isCrouched);

            // CCSGameMovement::CheckParameters saves the raw duck bit before
            // anti-spam can clear IN_DUCK. GOKZ's KZTimer hook restores 8.0 on
            // a raw release before Source subtracts the 2.0 edge penalty;
            // SimpleKZ instead enforces the one-duck minimum of 6.0234375.
            bool rawDuckHeld = _surfer.MoveData.crouching;
            if (!_duckInputInitialized)
            {
                _rawDuckHeld = false;
                _duckInputInitialized = true;
            }

            bool rawDuckChanged = rawDuckHeld != _rawDuckHeld;
            bool rawDuckReleased = _rawDuckHeld && !rawDuckHeld;
            if (_mode == GokzMode.SimpleKz)
            {
                if (!_ducking && _duckSpeed < DuckSpeedNormal)
                    _duckSpeed = DuckSpeedNormal;
                else if (_duckSpeed < DuckSpeedMinimum)
                    _duckSpeed = DuckSpeedMinimum;
            }
            else if (_mode == GokzMode.KzTimer && rawDuckReleased)
            {
                _duckSpeed = DuckSpeedNormal;
            }

            if (rawDuckChanged)
                _duckSpeed = Mathf.Max(0f, _duckSpeed - DuckSpamPenalty);

            _duckSpeed = Mathf.MoveTowards(
                _duckSpeed,
                DuckSpeedNormal,
                3f * TickInterval);

            // This is the CS:GO recovery rule from CCSGameMovement::Duck:
            // only a fully transitioning or fully settled player gets the
            // distance-based 6.0 recovery.
            if (_duckAmount <= 0f || _duckAmount >= 1f)
            {
                float distanceFromRecoveryPoint = Vector3.ProjectOnPlane(
                    _surfer.MoveData.playerTransform.position - _lastFullDuckSpeedPosition,
                    _movementUp).magnitude;
                if (distanceFromRecoveryPoint > 64f * UnitScale)
                {
                    _duckSpeed = Mathf.MoveTowards(
                        _duckSpeed,
                        DuckSpeedNormal,
                        6f * TickInterval);
                }
                _lastFullDuckSpeedPosition = _surfer.MoveData.playerTransform.position;
            }

            bool playerTouchingGround = Grounded;
            bool onLadder = _wasOnLadder;
            bool inAir = !playerTouchingGround && !onLadder;

            // CheckParameters can suppress the effective IN_DUCK while raw
            // input remains held. GroundMove still sees _ducking or the
            // physical hull, just as CCSGameMovement does.
            bool duckButtonHeld = rawDuckHeld && DuckingEnabled();
            _surfer.MoveData.crouching = duckButtonHeld;

            if (!duckButtonHeld && _duckAmount > 0f)
            {
                _ducking = true;
            }
            else if (duckButtonHeld && _duckAmount < 1f)
            {
                _ducking = true;
            }

            // CCSGameMovement::Duck does not use the generic one-second
            // CGameMovement duck timer. It advances m_flDuckAmount at
            // m_flDuckSpeed * 0.8 and finishes an air duck immediately.
            if (duckButtonHeld && _ducking)
            {
                float duckSpeed = _duckSpeed * 0.8f;
                _duckAmount = Mathf.MoveTowards(
                    _duckAmount,
                    1f,
                    TickInterval * duckSpeed);

                if (_duckAmount >= 1f || !playerTouchingGround)
                    FinishDuckState();
                else
                    SetDuckedEyeOffset(_duckAmount);
            }

            // CS:GO clears m_bDucked before the remaining grounded eye
            // transition completes. A blocked unduck restores a fully ducked
            // state instead of leaving a half-height hull under a ceiling.
            if (!duckButtonHeld && _ducking)
            {
                if (CanUnduck())
                {
                    float duckSpeed = Mathf.Max(DuckSpeedAlwaysMinimum, _duckSpeed);
                    _duckAmount = Mathf.MoveTowards(
                        _duckAmount,
                        0f,
                        TickInterval * duckSpeed);

                    _isCrouched = false;
                    UpdateBoxCollider(false);

                    if (_duckAmount <= 0f || inAir)
                        FinishUnDuckState();
                    else
                        SetDuckedEyeOffset(_duckAmount);
                }
                else
                {
                    _duckAmount = 1f;
                    _isCrouched = true;
                    _ducking = false;
                    SetDuckedEyeOffset(1f);
                    UpdateBoxCollider(true);
                }
            }

            // rawDuckReleased is used only for the GOKZ slowdown hook above;
            // the CS state machine itself uses the effective button and the
            // continuous m_flDuckAmount/m_bDucked equivalents.
            _rawDuckHeld = rawDuckHeld;
            _wasCrouching = rawDuckHeld;
        }

        // Reference copy of the generic Source CGameMovement duck routine.
        // It is intentionally not called by the GOKZ path; CS:GO dispatches
        // CCSGameMovement::Duck instead.
        private void UpdateDuckStateLegacy()
        {
            if (!_collisionReady)
                return;

            if (!_hullInitialized)
            {
                _hullInitialized = true;
                _isCrouched = false;
                _duckAmount = 0f;
                _duckSpeed = DuckSpeedNormal;
                _lastFullDuckSpeedPosition = _surfer.MoveData.playerTransform.position;
                _duckTimeMilliseconds = 0f;
                _rawDuckHeld = false;
                _duckInputInitialized = true;
                _lastDuckTime = float.NegativeInfinity;
            }

            // Gravity shifts keep the same feet/top anchor but reverse which
            // side of that anchor the Source hull occupies.
            if ((_colliderUp - _movementUp).sqrMagnitude > 0.000001f)
                UpdateBoxCollider(_isCrouched);

            // CCSGameMovement::CheckParameters keeps a raw duck bit so the
            // spam penalty is applied to both press and release, even when a
            // gameplay hook later removes IN_DUCK. GOKZ's KZTimer hook resets
            // the speed on a raw release before Source subtracts its 2.0
            // penalty; SimpleKZ has its own 6.0234375 minimum.
            bool rawDuckHeld = _surfer.MoveData.crouching;
            if (!_duckInputInitialized)
            {
                _rawDuckHeld = false;
                _duckInputInitialized = true;
            }

            bool rawDuckChanged = rawDuckHeld != _rawDuckHeld;
            bool rawDuckReleased = _rawDuckHeld && !rawDuckHeld;
            if (_mode == GokzMode.SimpleKz)
            {
                if (!_ducking && _duckSpeed < DuckSpeedNormal)
                    _duckSpeed = DuckSpeedNormal;
                else if (_duckSpeed < DuckSpeedMinimum)
                    _duckSpeed = DuckSpeedMinimum;
            }
            else if (_mode == GokzMode.KzTimer && rawDuckReleased)
            {
                _duckSpeed = DuckSpeedNormal;
            }

            if (rawDuckChanged)
                _duckSpeed = Mathf.Max(0f, _duckSpeed - DuckSpamPenalty);

            // This is the engine's recovery curve. GOKZ's ReduceDuckSlowdown
            // hook runs before Source's Duck() state machine.
            _duckSpeed = Mathf.MoveTowards(
                _duckSpeed,
                DuckSpeedNormal,
                3f * TickInterval);

            if (_duckAmount <= 0f || _duckAmount >= 1f)
            {
                float distanceFromRecoveryPoint = Vector3.ProjectOnPlane(
                    _surfer.MoveData.playerTransform.position - _lastFullDuckSpeedPosition,
                    _movementUp).magnitude;
                if (distanceFromRecoveryPoint > 64f * UnitScale)
                {
                    _duckSpeed = Mathf.MoveTowards(
                        _duckSpeed,
                        DuckSpeedNormal,
                        6f * TickInterval);
                }
                _lastFullDuckSpeedPosition = _surfer.MoveData.playerTransform.position;
            }

            bool duckButtonHeld = rawDuckHeld && DuckingEnabled();

            // Source starts a one-second duck timer on the press edge. The
            // actual eye transition is only the 200 ms CS:GO segment of that
            // timer; the physical duck amount still follows m_flDuckSpeed.
            if (duckButtonHeld && !_isCrouched && !_ducking &&
                !_inDuckJump && !_duckJumpTimeActive)
            {
                _duckTimeMilliseconds = DuckTransitionTimeMilliseconds;
                _ducking = true;
            }

            if (!duckButtonHeld && _duckAmount > 0f)
            {
                // This mirrors Source's recovery for a state that is still
                // physically ducked after the button was released.
                _ducking = true;
            }
            else if (duckButtonHeld && _duckAmount < 1f)
            {
                _ducking = true;
            }

            if (duckButtonHeld && _ducking && !_isCrouched)
            {
                float speed = _duckSpeed * 0.8f;
                _duckAmount = Mathf.MoveTowards(
                    _duckAmount,
                    1f,
                    TickInterval * speed);

                float duckMilliseconds = DuckTransitionTimeMilliseconds -
                    _duckTimeMilliseconds;
                // CGameMovement::Duck finishes after TIME_TO_DUCK_MSECS or
                // immediately when the player is airborne.
                if (_duckAmount >= 1f || duckMilliseconds > TimeToDuckMilliseconds ||
                    !Grounded)
                    FinishDuckState();
                else
                    SetDuckedEyeOffset(
                        FractionDucked(duckMilliseconds, TimeToDuckMilliseconds));
            }
            else if (!duckButtonHeld && _ducking)
            {
                if (CanUnduck())
                {
                    if (rawDuckReleased)
                    {
                        if (_isCrouched && !_inDuckJump)
                        {
                            _duckTimeMilliseconds = DuckTransitionTimeMilliseconds;
                        }
                        else if (!_isCrouched)
                        {
                            // Source inverts the remaining duck timer when
                            // release happens before full duck.
                            float elapsedMilliseconds =
                                DuckTransitionTimeMilliseconds -
                                _duckTimeMilliseconds;
                            float fractionDucked = Mathf.Clamp01(
                                elapsedMilliseconds / TimeToDuckMilliseconds);
                            float remainingUnduckMilliseconds =
                                fractionDucked * TimeToUnduckMilliseconds;
                            _duckTimeMilliseconds =
                                DuckTransitionTimeMilliseconds -
                                TimeToUnduckMilliseconds +
                                remainingUnduckMilliseconds;
                        }
                    }

                    float speed = Mathf.Max(DuckSpeedAlwaysMinimum, _duckSpeed);
                    _duckAmount = Mathf.MoveTowards(
                        _duckAmount,
                        0f,
                        TickInterval * speed);

                    float duckMilliseconds = DuckTransitionTimeMilliseconds -
                        _duckTimeMilliseconds;
                    if (_duckAmount <= 0f ||
                        duckMilliseconds > TimeToUnduckMilliseconds ||
                        (!Grounded && !_inDuckJump))
                        FinishUnDuckState();
                    else
                    {
                        SetDuckedEyeOffset(
                            1f - FractionUnDucked(
                                duckMilliseconds,
                                TimeToUnduckMilliseconds));
                        _isCrouched = false;
                        UpdateBoxCollider(false);
                    }
                }
                else
                {
                    // Source restores a fully ducked state when the standing
                    // hull cannot fit under the ceiling.
                    _duckAmount = 1f;
                    _isCrouched = true;
                    _ducking = false;
                    SetDuckedEyeOffset(1f);
                    UpdateBoxCollider(true);
                }
            }

            _rawDuckHeld = rawDuckHeld;
            _wasCrouching = rawDuckHeld;
        }

        private void FinishDuckState()
        {
            if (!_isCrouched && !Grounded && !_wasOnLadder)
            {
                _surfer.MoveData.playerTransform.position +=
                    _movementUp * DuckAirOriginOffset;
                _surfer.MoveData.Origin =
                    _surfer.MoveData.playerTransform.position;
                Physics.SyncTransforms();
            }

            _duckAmount = 1f;
            _ducking = false;
            _isCrouched = true;
            _duckTimeMilliseconds = DuckTransitionTimeMilliseconds;
            _lastDuckTime = _elapsed;
            SetDuckedEyeOffset(1f);
            UpdateBoxCollider(true);
            FixPlayerCrouchStuck();

            if (CategorizePosition(out RaycastHit duckGroundHit))
            {
                Grounded = true;
                _groundHit = duckGroundHit;
                _groundNormal = duckGroundHit.normal;
            }
        }

        private void FinishUnDuckState()
        {
            if (!Grounded && !_wasOnLadder)
            {
                _surfer.MoveData.playerTransform.position -=
                    _movementUp * DuckAirOriginOffset;
                _surfer.MoveData.Origin =
                    _surfer.MoveData.playerTransform.position;
                Physics.SyncTransforms();
            }

            _duckAmount = 0f;
            _ducking = false;
            _isCrouched = false;
            _duckTimeMilliseconds = 0f;
            SetDuckedEyeOffset(0f);
            UpdateBoxCollider(false);

            if (CategorizePosition(out RaycastHit unduckGroundHit))
            {
                Grounded = true;
                _groundHit = unduckGroundHit;
                _groundNormal = unduckGroundHit.normal;
            }
        }

        private void UpdateBoxCollider(bool crouched)
        {
            if (_sourceHullCollider == null)
                return;

            float height = crouched ? DuckHullHeight : PlayerHullHeight;
            _sourceHullCollider.size = new Vector3(HullRadius * 2f, height, HullRadius * 2f);
            _sourceHullCollider.center = _movementUp * (height * 0.5f);
            if (_sourceHullTransform != null && _sourceHullTransform.parent != null)
                ConfigureSourceHullTransform(_sourceHullTransform.parent);
            _colliderUp = _movementUp;
        }

        private bool CanFitHull(float height)
        {
            return CanFitHullAt(_surfer.MoveData.playerTransform.position, height);
        }

        private bool CanUnduck()
        {
            if (!_isCrouched && !_ducking)
                return true;

            // GOKZ KZTimer/SimpleKZ detour CCSGameMovement::CanUnduck: a
            // fully ducked player may not unduck on the first command after
            // landing. This is what keeps a landing duck from turning into a
            // one-tick half-height state.
            if (_mode != GokzMode.Vanilla && _landedThisTick &&
                _isCrouched && _duckAmount >= 1f)
                return false;

            Vector3 start = _surfer.MoveData.playerTransform.position;
            Vector3 end = start;
            if (!Grounded && !_wasOnLadder)
                end -= _movementUp * DuckAirOriginOffset;

            Vector3 delta = end - start;
            Vector3 halfExtents = GetBoxHalfExtents(false);
            Vector3 center = start + _movementUp * halfExtents.y;
            if (delta.sqrMagnitude > CollisionSkin * CollisionSkin)
            {
                int hitCount = Physics.BoxCastNonAlloc(
                    center,
                    GetQueryHalfExtents(halfExtents),
                    delta.normalized,
                    _moveHits,
                    Quaternion.identity,
                    delta.magnitude,
                    GetCollisionMask(),
                    QueryTriggerInteraction.Ignore);
                if (TryGetNearestHit(hitCount, out _))
                    return false;
            }

            return CanFitHullAt(end, PlayerHullHeight);
        }

        private bool DuckingEnabled()
        {
            if (_duckSpeed < DuckSpeedAlwaysMinimum)
                return false;

            return _isCrouched ||
                _elapsed >= _lastDuckTime + _settings.timeBetweenDucks;
        }

        private static float FractionDucked(float milliseconds, float duration)
        {
            return Mathf.Clamp01(milliseconds / Mathf.Max(0.0001f, duration));
        }

        private static float FractionUnDucked(float milliseconds, float duration)
        {
            return Mathf.Clamp01(milliseconds / Mathf.Max(0.0001f, duration));
        }

        private static float SimpleSpline(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void SetDuckedEyeOffset(float duckFraction)
        {
            _eyeDuckAmount = SimpleSpline(duckFraction);
        }

        private void FixPlayerCrouchStuck()
        {
            // Source's FixPlayerCrouchStuck moves only upward after the hull
            // becomes short. Keep the same bounded 36 HU correction rather
            // than letting a generic penetration solver change the jump
            // trajectory or horizontal speed.
            if (_surfer == null || _surfer.MoveData.playerTransform == null)
                return;

            Vector3 originalPosition =
                _surfer.MoveData.playerTransform.position;
            for (int i = 0; i < 36; i++)
            {
                if (CanFitHullAt(
                    _surfer.MoveData.playerTransform.position,
                    DuckHullHeight))
                    return;

                _surfer.MoveData.playerTransform.position +=
                    _movementUp * UnitScale;
                _surfer.MoveData.Origin =
                    _surfer.MoveData.playerTransform.position;
                Physics.SyncTransforms();
            }

            // CGameMovement::FixPlayerCrouchStuck restores the original
            // origin when all 36 one-unit probes fail. Leaving the temporary
            // displacement in place changes jump height and is not a valid
            // unstuck result.
            _surfer.MoveData.playerTransform.position = originalPosition;
            _surfer.MoveData.Origin = originalPosition;
            Physics.SyncTransforms();
        }

        private void FixDisplacementStuck()
        {
            // GOKZ's FixDisplacementStuck runs after Source has removed the
            // duck flag. If the standing hull is embedded by a displacement
            // or a precision edge, preserve the old processing velocity and
            // restore the duck transition instead of allowing the custom box
            // to remain stuck in a standing state.
            if (!_wasPhysicallyCrouched || _isCrouched ||
                _surfer == null || _surfer.MoveData.playerTransform == null)
                return;

            if (CanFitHullAt(
                _surfer.MoveData.playerTransform.position,
                PlayerHullHeight))
                return;

            _surfer.MoveData.Velocity = _velocityBeforeDuckState;
            _duckAmount = 1f;
            _isCrouched = true;
            _ducking = true;
            SetDuckedEyeOffset(1f);
            UpdateBoxCollider(true);
        }

        private bool CanFitHullAt(Vector3 basePosition, float height)
        {
            Vector3 center = basePosition +
                _movementUp * (height * 0.5f + 0.01f);
            Vector3 halfExtents = new Vector3(HullRadius, height * 0.5f, HullRadius) * 0.98f;
            int count = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                _hullOverlaps,
                Quaternion.identity,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider collider = _hullOverlaps[i];
                if (collider == null || collider == _sourceHullCollider ||
                    collider.transform.IsChildOf(_surfer.MoveData.playerTransform))
                    continue;
                return false;
            }

            return true;
        }

        private bool IsHullStuckAt(Vector3 basePosition, Vector3 halfExtents)
        {
            if (_surfer == null || _surfer.MoveData.playerTransform == null)
                return false;

            Vector3 center = basePosition + _movementUp * halfExtents.y;
            int count = Physics.OverlapBoxNonAlloc(
                center,
                GetQueryHalfExtents(halfExtents),
                _hullOverlaps,
                Quaternion.identity,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = _hullOverlaps[i];
                if (collider == null || collider == _sourceHullCollider ||
                    collider.transform.IsChildOf(
                        _surfer.MoveData.playerTransform))
                    continue;
                return true;
            }

            return false;
        }

        private void TryWaterCrouchBoost()
        {
            // GOKZ FixWaterBoost is only for a newly pressed duck while at
            // least waist-deep, airborne, and not already crouched. It moves
            // the origin by exactly 9 HU if the duck hull is clear.
            if (_surfer.MoveData.waterLevel < GokzWaterLevel.Waist ||
                Grounded || _isCrouched)
                return;

            Vector3 position = _surfer.MoveData.playerTransform.position;
            Vector3 boostedPosition = position + _movementUp * DuckAirOriginOffset;
            if (!CanFitHullAt(boostedPosition, DuckHullHeight))
                return;

            _surfer.MoveData.playerTransform.position = boostedPosition;
            _surfer.MoveData.Origin = boostedPosition;
            Physics.SyncTransforms();
        }

        private bool TryLadderMove()
        {
            if (!_surfer.MoveData.laddersEnabled)
                return false;

            Vector3 up = _movementUp;
            Vector3 forward = _surfer.Forward.normalized;
            Vector3 right = _surfer.Right.normalized;
            Vector3 wish = forward * _surfer.MoveData.verticalAxis +
                right * _surfer.MoveData.horizontalAxis;
            if (wish.sqrMagnitude < 0.000001f && !_wasOnLadder)
                return false;

            Vector3 probeDirection = _wasOnLadder
                ? -_surfer.MoveData.ladderNormal
                : wish.normalized;
            float probeDistance = (_wasOnLadder ? 10f : 2f) * UnitScale;
            Vector3 center = _surfer.MoveData.playerTransform.position +
                up * GetBoxHalfExtents(_isCrouched).y;
            int hitCount = Physics.BoxCastNonAlloc(
                center,
                GetQueryHalfExtents(GetBoxHalfExtents(_isCrouched)),
                probeDirection,
                _moveHits,
                Quaternion.identity,
                probeDistance,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);

            RaycastHit ladderHit = default;
            bool found = false;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _moveHits[i];
                if (candidate.collider == null || candidate.collider == _sourceHullCollider ||
                    candidate.collider.transform.IsChildOf(_surfer.MoveData.playerTransform) ||
                    Vector3.Dot(candidate.normal, up) >= 0.999999f ||
                    candidate.distance >= nearestDistance)
                    continue;
                GokzLadder ladder = candidate.collider.GetComponentInParent<GokzLadder>();
                bool surfaceClimbable =
                    GokzSurfaceProperties.Resolve(candidate.collider)
                        .sourceClimbable;
                if ((ladder != null && !ladder.Climbable) ||
                    (ladder == null && !surfaceClimbable))
                    continue;
                ladderHit = candidate;
                nearestDistance = candidate.distance;
                found = true;
            }

            if (!found)
            {
                // CGameMovement also checks behind the player after walking
                // off an edge. This is the Source ladder edge-grab path,
                // distinct from a normal forward ladder acquisition.
                if (!TryCatchLadderEdge(out ladderHit))
                {
                    _surfer.MoveData.climbingLadder = false;
                    return false;
                }

                found = true;
            }

            Vector3 ladderNormal = ladderHit.normal.normalized;
            if (!_wasOnLadder)
                _ladderJumpBlockedUntil = _elapsed + 0.2f;

            Vector3 viewForward = _surfer.Forward.normalized;
            Vector3 viewRight = _surfer.Right.normalized;
            float climbSpeed = 200f * UnitScale;
            if (_surfer.MoveData.crouching || _surfer.MoveData.sprinting)
                climbSpeed *= 0.34f;
            float forwardSpeed = 0f;
            float rightSpeed = 0f;
            if (_surfer.MoveData.moveBack)
                forwardSpeed -= climbSpeed;
            if (_surfer.MoveData.moveForward)
                forwardSpeed += climbSpeed;
            if (_surfer.MoveData.moveLeft)
                rightSpeed -= climbSpeed;
            if (_surfer.MoveData.moveRight)
                rightSpeed += climbSpeed;

            Vector3 velocity;
            // LadderMove checks IN_JUMP directly, unlike the ground jump
            // path. A held jump therefore launches as soon as the Source
            // ladder-jump ignore window expires, even if the key was already
            // held when the ladder was acquired.
            bool ladderJumpRequested = _surfer.MoveData.wishJump ||
                _surfer.MoveData.jumpHeld;
            if (ladderJumpRequested && _elapsed >= _ladderJumpBlockedUntil)
            {
                // Source FullLadderMove launches away from the ladder at
                // exactly 270 HU/s and changes back to WALK. Returning false
                // lets Process continue through the ordinary FullWalkMove
                // branch in this same command, just like Source's movement
                // type switch does after LadderMove returns.
                velocity = ladderNormal * (270f * UnitScale);
                _surfer.MoveData.climbingLadder = false;
                _onLadderThisTick = false;
                _movementVelocity = velocity;
                _surfer.MoveData.Velocity = velocity;
                Grounded = false;
                _surfer.MoveData.grounded = false;
                return false;
            }

            Vector3 desired = viewForward * forwardSpeed + viewRight * rightSpeed;
            if (desired.sqrMagnitude > 0.000001f)
            {
                // CGameMovement::LadderMove decomposes desired velocity into
                // the ladder normal and its plane, then converts the normal
                // component into climbing direction.
                Vector3 perp = Vector3.Cross(up, ladderNormal);
                if (perp.sqrMagnitude < 0.000001f)
                    perp = Vector3.right;
                else
                    perp.Normalize();
                Vector3 ladderUp = Vector3.Cross(ladderNormal, perp).normalized;
                float normal = Vector3.Dot(desired, ladderNormal);
                Vector3 cross = ladderNormal * normal;
                Vector3 lateral = desired - cross;

                // Source's CS ladder dampening reduces motion perpendicular
                // to the ladder only when the approach angle is steep.
                float lateralAlongUp = Vector3.Dot(lateral, ladderUp);
                float lateralPerpendicular = Vector3.Dot(lateral, perp);
                Vector3 angleVector =
                    perp * lateralPerpendicular + cross;
                if (angleVector.sqrMagnitude > 0.000001f &&
                    Vector3.Dot(angleVector.normalized, ladderNormal) < -0.707f)
                {
                    lateral = ladderUp * lateralAlongUp +
                        perp * (0.2f * lateralPerpendicular);
                }

                velocity = lateral - ladderUp * normal;
                velocity *= _settings.ladderScaleSpeed;

                if (Grounded && normal > 0f)
                    velocity += ladderNormal * (200f * UnitScale);
            }
            else
            {
                velocity = Vector3.zero;
            }

            _surfer.MoveData.ladderNormal = ladderNormal;
            _surfer.MoveData.ladderDirection = -ladderNormal;
            _surfer.MoveData.ladderClimbDir = up;
            _surfer.MoveData.ladderVelocity = velocity;
            _surfer.MoveData.climbingLadder = true;
            _onLadderThisTick = true;
            _movementVelocity = velocity;
            _surfer.MoveData.Velocity = velocity;
            Grounded = false;
            _surfer.MoveData.grounded = false;
            return true;
        }

        private bool TryCatchLadderEdge(out RaycastHit ladderHit)
        {
            ladderHit = default;
            if (!_hasWalkMovedSinceLastJump || _wasOnLadder || Grounded)
                return false;

            Vector3 velocity = _surfer.MoveData.Velocity;
            float verticalSpeed = Vector3.Dot(velocity, _movementUp) *
                HammerUnitsPerUnityUnit;
            if (verticalSpeed > 0f || verticalSpeed <= -50f)
                return false;

            Vector3 lateral = Vector3.ProjectOnPlane(velocity, _movementUp);
            // Source's edge check requires lateral motion in both horizontal
            // axes. With the Unity player using X/Z as the floor plane, keep
            // that same non-zero-component requirement.
            if (Mathf.Abs(lateral.x) <= 0.000001f ||
                Mathf.Abs(lateral.z) <= 0.000001f)
                return false;

            Vector3 direction = velocity.normalized;
            if (direction.sqrMagnitude < 0.000001f)
                return false;

            Vector3 origin = _surfer.MoveData.playerTransform.position;
            Vector3 traceStart = origin - _movementUp * (6f * UnitScale);
            Vector3 traceEnd = origin - direction * (24f * UnitScale);
            Vector3 halfExtents = GetBoxHalfExtents(_isCrouched);
            if (!TraceBox(
                    traceStart + _movementUp * halfExtents.y,
                    traceEnd + _movementUp * halfExtents.y,
                    halfExtents,
                    out RaycastHit candidate) ||
                candidate.collider == null ||
                !IsLadderCollider(candidate.collider) ||
                Mathf.Abs(Vector3.Dot(candidate.normal, _movementUp) - 1f) <
                    0.000001f)
                return false;

            ladderHit = candidate;
            _surfer.MoveData.playerTransform.position = candidate.point +
                candidate.normal * (HullRadius + CollisionSkin) -
                _movementUp * halfExtents.y;
            _surfer.MoveData.Origin =
                _surfer.MoveData.playerTransform.position;
            _surfer.MoveData.Velocity = Vector3.zero;
            Physics.SyncTransforms();
            return true;
        }

        private static bool IsLadderCollider(Collider collider)
        {
            if (collider == null)
                return false;

            GokzLadder ladder = collider.GetComponentInParent<GokzLadder>();
            bool surfaceClimbable =
                GokzSurfaceProperties.Resolve(collider).sourceClimbable;
            return (ladder != null && ladder.Climbable) ||
                (ladder == null && surfaceClimbable);
        }

        private void StayOnGround(ref Vector3 position, Vector3 halfExtents)
        {
            // Source WalkMove performs a second trace from origin + 2 HU to
            // origin - step height. This keeps the player attached to slopes
            // and moving down small steps instead of leaving a tiny airborne
            // gap after the horizontal move.
            float startOffset = 2f * UnitScale;
            Vector3 originalCenter = position + _movementUp * halfExtents.y;
            int upHitCount = Physics.BoxCastNonAlloc(
                originalCenter,
                GetQueryHalfExtents(halfExtents),
                _movementUp,
                _moveHits,
                Quaternion.identity,
                startOffset,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);
            // TracePlayerBBox(origin, origin + 2 HU) returns origin + 2 HU
            // when clear, or the actual trace end when it hits. The old
            // adapter left the clear path at origin and added the 2 HU again
            // after a hit, which is the opposite of Source in both cases.
            Vector3 safePosition = position + _movementUp * startOffset;
            if (TryGetNearestHit(upHitCount, out RaycastHit upHit))
            {
                safePosition += _movementUp *
                    (Mathf.Max(0f, upHit.distance - CollisionSkin) -
                        startOffset);
            }

            Vector3 endPosition = position - _movementUp * StepHeight;
            Vector3 startCenter = safePosition + _movementUp * halfExtents.y;
            Vector3 endCenter = endPosition + _movementUp * halfExtents.y;
            Vector3 downDelta = endCenter - startCenter;
            float traceDistance = downDelta.magnitude;
            if (traceDistance <= CollisionSkin)
                return;

            int hitCount = Physics.BoxCastNonAlloc(
                startCenter,
                GetQueryHalfExtents(halfExtents),
                downDelta / traceDistance,
                _moveHits,
                Quaternion.identity,
                traceDistance,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);

            if (!TryGetNearestHit(hitCount, out RaycastHit groundHit) ||
                Vector3.Dot(groundHit.normal, _movementUp) < WalkableNormal)
                return;

            float safeDistance = Mathf.Clamp(
                groundHit.distance - CollisionSkin,
                0f,
                traceDistance);
            position = safePosition +
                (downDelta / traceDistance) * safeDistance;
        }

        private Vector3 GetBaseVelocity(Transform playerTransform)
        {
            Vector3 baseVelocity = _surfer != null ? _surfer.BaseVelocity : Vector3.zero;
            // Source trigger_push writes FL_BASEVELOCITY. Keep it in the
            // same base-velocity path as moving ground so StartGravity and
            // Move consume it in the Source order.
            baseVelocity += _sourceTriggerBaseVelocity;
            // Source keeps the ground entity's base velocity when
            // SetGroundEntity(NULL) is called by a jump. At that point
            // Grounded is already false, so use the ground captured at the
            // start of this command as well as the ordinary grounded path.
            if ((!Grounded && !_startGrounded) || _groundHit.collider == null)
                return baseVelocity;

            Rigidbody groundBody = _groundHit.collider.attachedRigidbody;
            if (groundBody == null)
            {
                Transform groundTransform = _groundHit.transform;
                if (_hasGroundTransformSample &&
                    groundTransform == _sampledGroundTransform)
                    return baseVelocity + _sampledGroundVelocity;
                return baseVelocity;
            }

            return baseVelocity + groundBody.GetPointVelocity(playerTransform.position);
        }

        private void UpdateSourceMovementVolumes(bool groundAtTickStart)
        {
            _sourceGravityScale = 1f;
            _sourceTriggerBaseVelocity = Vector3.zero;
            _sourceVolumesThisTick.Clear();

            if (_surfer == null || _surfer.MoveData.playerTransform == null)
                return;

            Vector3 halfExtents = GetBoxHalfExtents(_isCrouched);
            Vector3 center = _surfer.MoveData.playerTransform.position +
                _movementUp * halfExtents.y;
            int count = Physics.OverlapBoxNonAlloc(
                center,
                GetQueryHalfExtents(halfExtents),
                _sourceVolumeOverlaps,
                Quaternion.identity,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            GokzMovementVolume selectedGravity = null;

            for (int i = 0; i < count; i++)
            {
                Collider overlap = _sourceVolumeOverlaps[i];
                if (overlap == null || overlap == _sourceHullCollider ||
                    overlap.transform.IsChildOf(
                        _surfer.MoveData.playerTransform))
                    continue;

                GokzMovementVolume volume =
                    overlap.GetComponentInParent<GokzMovementVolume>();
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                if (!_sourceVolumesThisTick.Add(volume))
                    continue;

                if (volume.IsGravity)
                {
                    // Source normally has one gravity trigger at a time. A
                    // deterministic priority resolves overlapping imported
                    // volumes without depending on PhysX overlap ordering.
                    if (selectedGravity == null ||
                        volume.Priority > selectedGravity.Priority)
                    {
                        selectedGravity = volume;
                    }
                    continue;
                }

                if (!volume.IsPush)
                    continue;

                Vector3 pushVelocity = volume.WorldPushVelocity;
                if (volume.PushOnce)
                {
                    if (_consumedSourcePushVolumes.Contains(volume))
                        continue;

                    _consumedSourcePushVolumes.Add(volume);
                    _surfer.MoveData.Velocity += pushVelocity;
                    DetachFromGroundForPush(
                        pushVelocity,
                        groundAtTickStart);
                }
                else
                {
                    // CTriggerPush adds its velocity to the player's base
                    // velocity every touch. Summing overlapping pushes keeps
                    // that Source behavior and leaves the result to the
                    // normal StartGravity/Move/Finish consumption path.
                    _sourceTriggerBaseVelocity += pushVelocity;
                    DetachFromGroundForPush(
                        pushVelocity,
                        groundAtTickStart);
                }
            }

            if (selectedGravity != null)
                _sourceGravityScale = selectedGravity.GravityScale;
        }

        private void DetachFromGroundForPush(
            Vector3 pushVelocity,
            bool groundAtTickStart)
        {
            if (Vector3.Dot(pushVelocity, _movementUp) <= 0f)
                return;

            _sourcePushDetachedThisTick = true;
            if (!groundAtTickStart)
                return;

            Grounded = false;
            _surfer.MoveData.grounded = false;
            if (_surfer.MoveData.playerTransform != null)
            {
                Vector3 origin = _surfer.MoveData.playerTransform.position +
                    _movementUp * UnitScale;
                _surfer.MoveData.playerTransform.position = origin;
                _surfer.MoveData.Origin = origin;
                Physics.SyncTransforms();
            }
        }

        private Transform GetGroundEntityTransform(RaycastHit groundHit)
        {
            if (groundHit.collider == null)
                return null;

            Rigidbody groundBody = groundHit.collider.attachedRigidbody;
            return groundBody != null
                ? groundBody.transform
                : groundHit.transform;
        }

        private Vector3 GetGroundEntityVelocity(RaycastHit groundHit)
        {
            if (groundHit.collider == null)
                return Vector3.zero;

            Rigidbody groundBody = groundHit.collider.attachedRigidbody;
            if (groundBody != null && _surfer != null &&
                _surfer.MoveData.playerTransform != null)
                return groundBody.GetPointVelocity(
                    _surfer.MoveData.playerTransform.position);

            Transform groundTransform = GetGroundEntityTransform(groundHit);
            return _hasGroundTransformSample &&
                groundTransform == _sampledGroundTransform
                ? _sampledGroundVelocity
                : Vector3.zero;
        }

        private void UpdateGroundEntityVelocitySample()
        {
            Transform groundTransform =
                GetGroundEntityTransform(_groundHit);
            if (groundTransform == null)
                return;

            Vector3 currentPosition = groundTransform.position;
            if (!_hasGroundTransformSample ||
                _sampledGroundTransform != groundTransform)
            {
                _sampledGroundTransform = groundTransform;
                _sampledGroundPosition = currentPosition;
                _sampledGroundVelocity = Vector3.zero;
                _hasGroundTransformSample = true;
                return;
            }

            _sampledGroundVelocity =
                (currentPosition - _sampledGroundPosition) / TickInterval;
            _sampledGroundPosition = currentPosition;
        }

        private static float GetSurfaceFriction(RaycastHit hit)
        {
            return Mathf.Max(0f,
                GokzSurfaceProperties.Resolve(hit.collider).sourceFriction);
        }

        private static float GetSurfaceMaxSpeedFactor(RaycastHit hit)
        {
            return Mathf.Max(0f,
                GokzSurfaceProperties.Resolve(hit.collider).sourceMaxSpeedFactor);
        }

        private static float GetSurfaceJumpFactor(RaycastHit hit)
        {
            return Mathf.Max(0f,
                GokzSurfaceProperties.Resolve(hit.collider).sourceJumpFactor);
        }

        private void ConfigureSourceHullTransform(Transform playerTransform)
        {
            if (_sourceHullTransform == null || playerTransform == null)
                return;

            // Source's player bbox is world-axis aligned. The visible player
            // may yaw freely, but that yaw must never rotate the movement hull.
            _sourceHullTransform.localPosition = Vector3.zero;
            _sourceHullTransform.localRotation = Quaternion.Inverse(playerTransform.rotation);

            Vector3 scale = playerTransform.localScale;
            _sourceHullTransform.localScale = new Vector3(
                1f / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                1f / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                1f / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
        }

        private Vector3 GetQueryHalfExtents(Vector3 halfExtents)
        {
            // Leave the Source collision skin inside the hull. Without this
            // inset, a BoxCast that starts exactly on a Unity floor reports a
            // zero-distance self-contact and the slide loop cannot advance.
            return new Vector3(
                Mathf.Max(0.0001f, halfExtents.x - CollisionSkin),
                Mathf.Max(0.0001f, halfExtents.y - CollisionSkin),
                Mathf.Max(0.0001f, halfExtents.z - CollisionSkin));
        }

        private int GetCollisionMask()
        {
            if (_surfer == null || _surfer.MoveData.playerTransform == null)
                return Physics.DefaultRaycastLayers;

            int playerLayer = _surfer.MoveData.playerTransform.gameObject.layer;
            int mask = Physics.DefaultRaycastLayers;
            for (int layer = 0; layer < 32; layer++)
            {
                if (Physics.GetIgnoreLayerCollision(playerLayer, layer))
                    mask &= ~(1 << layer);
            }
            return mask;
        }

        private void ResolveInitialPenetration(Transform playerTransform)
        {
            if (_sourceHullCollider == null || playerTransform == null)
                return;

            Vector3 halfExtents = GetQueryHalfExtents(GetBoxHalfExtents(_isCrouched));
            for (int iteration = 0; iteration < 4; iteration++)
            {
                Vector3 center = playerTransform.position + _movementUp * GetBoxHalfExtents(_isCrouched).y;
                int count = Physics.OverlapBoxNonAlloc(
                    center,
                    halfExtents,
                    _hullOverlaps,
                    Quaternion.identity,
                    GetCollisionMask(),
                    QueryTriggerInteraction.Ignore);

                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = _hullOverlaps[i];
                    if (other == null || other == _sourceHullCollider ||
                        other.transform.IsChildOf(playerTransform))
                        continue;

                    if (!Physics.ComputePenetration(
                        _sourceHullCollider,
                        playerTransform.position,
                        _sourceHullTransform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out Vector3 direction,
                        out float distance))
                        continue;

                    if (distance <= CollisionSkin)
                        continue;

                    playerTransform.position += direction * (distance + CollisionSkin);
                    moved = true;
                    Physics.SyncTransforms();
                }

                if (!moved)
                    break;
            }
        }

        private bool TrySourceStuckRecovery()
        {
            if (!_collisionReady || _sourceHullCollider == null ||
                _surfer == null || _surfer.MoveData.playerTransform == null ||
                _elapsed < _nextStuckCheckTime)
                return false;

            _nextStuckCheckTime = _elapsed + SourceCheckStuckInterval;
            Transform playerTransform = _surfer.MoveData.playerTransform;
            Vector3 halfExtents = GetBoxHalfExtents(_isCrouched);
            if (!IsHullStuckAt(playerTransform.position, halfExtents))
            {
                _stuckOffsetIndex = 0;
                return false;
            }

            // CGameMovement::CheckStuck probes the fixed Source offset table.
            // ResolveInitialPenetration already handled ordinary overlap; this
            // is the deterministic last-resort path for a hull that still
            // cannot be placed after a scene/physics transform mismatch.
            for (int attempt = 0; attempt < SourceStuckOffsetsHu.Length; attempt++)
            {
                int index = (_stuckOffsetIndex + attempt) %
                    SourceStuckOffsetsHu.Length;
                Vector3 offsetHu = SourceStuckOffsetsHu[index];
                Vector3 candidate = playerTransform.position +
                    Vector3.right * (offsetHu.x * UnitScale) +
                    Vector3.forward * (offsetHu.y * UnitScale) +
                    _movementUp * (offsetHu.z * UnitScale);

                if (IsHullStuckAt(candidate, halfExtents))
                    continue;

                playerTransform.position = candidate;
                _surfer.MoveData.Origin = candidate;
                Physics.SyncTransforms();
                _stuckOffsetIndex = 0;
                return false;
            }

            _stuckOffsetIndex = (_stuckOffsetIndex + 1) %
                SourceStuckOffsetsHu.Length;
            return true;
        }

        private static readonly Vector3[] SourceStuckOffsetsHu =
            CreateSourceStuckOffsets();

        private static Vector3[] CreateSourceStuckOffsets()
        {
            Vector3[] offsets = new Vector3[54];
            float[] small = { -0.125f, 0f, 0.125f };
            float[] bigVertical = { 0f, 1f, 6f };
            float[] bigHorizontal = { -2f, 0f, 2f };
            int index = 0;

            // Source's small probes: vertical, then lateral, then each
            // small corner of the player box.
            for (int i = 0; i < small.Length; i++)
                offsets[index++] = new Vector3(0f, 0f, small[i]);
            for (int i = 0; i < small.Length; i++)
                offsets[index++] = new Vector3(0f, small[i], 0f);
            for (int i = 0; i < small.Length; i++)
                offsets[index++] = new Vector3(small[i], 0f, 0f);
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
                offsets[index++] = new Vector3(
                    small[x == 0 ? 0 : 2],
                    small[y == 0 ? 0 : 2],
                    small[z == 0 ? 0 : 2]);

            // Source's larger recovery probes: up/down first, then lateral,
            // then the full 3x3x3 combinations.
            for (int i = 0; i < bigVertical.Length; i++)
                offsets[index++] = new Vector3(0f, 0f, bigVertical[i]);
            for (int i = 0; i < bigHorizontal.Length; i++)
                offsets[index++] = new Vector3(0f, bigHorizontal[i], 0f);
            for (int i = 0; i < bigHorizontal.Length; i++)
                offsets[index++] = new Vector3(bigHorizontal[i], 0f, 0f);
            for (int x = 0; x < bigHorizontal.Length; x++)
            for (int y = 0; y < bigHorizontal.Length; y++)
            for (int z = 0; z < bigVertical.Length; z++)
                offsets[index++] = new Vector3(
                    bigHorizontal[x],
                    bigHorizontal[y],
                    bigVertical[z]);

            // The source table has a final zero-initialized slot. Keep it so
            // the probe count and ordering remain directly auditable.
            return offsets;
        }

        private Vector3 GetBoxHalfExtents(bool crouched)
        {
            float height = crouched ? DuckHullHeight : PlayerHullHeight;
            return new Vector3(HullRadius, height * 0.5f, HullRadius);
        }

        private void GroundMove(float realPreVelMod)
        {
            _walkMovedThisTick = true;
            _hasWalkMovedSinceLastJump = true;
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                _surfer.MoveData.Velocity,
                _movementUp);
            float speedBeforeFrictionHu = horizontalVelocity.magnitude *
                HammerUnitsPerUnityUnit;

            ApplyFriction(
                ref horizontalVelocity,
                Friction,
                _surfer.MoveData.SurfaceFriction);

            // Source's HandleDuckingSpeedCrop applies the fractional duck
            // modifier to the command and max speed. CS:GO's crouched speed
            // is 0.34 * 250 = 85 HU/s, not an invented one-third cap.
            // This is the value returned by GOKZ's
            // DHooks_OnGetPlayerMaxSpeed. It is a wish-speed input to
            // WalkMove, not a post-acceleration velocity cap.
            float maxSpeed = GetPlayerMaxSpeed(realPreVelMod) *
                _surfer.MoveData.SurfaceMaxSpeedFactor *
                GetDuckSpeedModifier();
            bool duckButtonDown = _surfer.MoveData.crouching || _ducking ||
                _isCrouched;
            bool walkButtonDown = _surfer.MoveData.sprinting && !duckButtonDown;
            float maxSpeedBeforeWalkHu = GetPlayerMaxSpeed(realPreVelMod) *
                HammerUnitsPerUnityUnit;
            // CCSGameMovement::CheckParameters only applies the walk cap
            // while the player is near walk speed; otherwise it lets the
            // player decelerate naturally. Accelerate still receives the raw
            // IN_SPEED state below, exactly as CCSGameMovement does.
            if (walkButtonDown && speedBeforeFrictionHu <
                maxSpeedBeforeWalkHu * WalkSpeedModifier + 25f)
            {
                maxSpeed *= WalkSpeedModifier;
            }
            GetWishValues(
                maxSpeed,
                duckButtonDown ? GetDuckSpeedModifier() : 1f,
                out Vector3 wishDirection,
                out float wishSpeed);
            float movementScale = duckButtonDown
                // CCSGameMovement::Accelerate uses the fixed
                // CS_PLAYER_SPEED_DUCK_MODIFIER for its acceleration and
                // goal-speed branch. The continuous m_flDuckAmount modifier
                // has already been applied to m_flForwardMove/m_flMaxSpeed
                // by HandleDuckingSpeedCrop above; multiplying acceleration
                // by that fraction would be a second, non-Source rule.
                ? DuckSpeedModifier
                : (walkButtonDown ? WalkSpeedModifier : 1f);
            horizontalVelocity = GokzMovementRules.AccelerateCsGo(
                horizontalVelocity,
                wishDirection,
                wishSpeed,
                Accelerate,
                _surfer.MoveData.SurfaceFriction,
                movementScale,
                TickInterval,
                walkButtonDown,
                duckButtonDown,
                _settings.useWeaponSpeed,
                _settings.weaponMaxSpeed);

            // CGameMovement::WalkMove performs a final horizontal maxspeed
            // clamp after acceleration. Leaving this out is the direct cause
            // of ground speed drifting above 250/276 while holding a key.
            float speed = horizontalVelocity.magnitude;
            if (speed > maxSpeed && speed > 0.000001f)
                horizontalVelocity *= maxSpeed / speed;

            _movementVelocity = horizontalVelocity;
            _surfer.MoveData.Velocity = horizontalVelocity;
        }

        private void ApplySlopeFix(
            ref Vector3 horizontalVelocity,
            Vector3 landingOrigin,
            bool landed)
        {
            if (!landed)
                return;

            // GOKZ's SlopeFix is a post-CategorizePosition hook. It does not
            // blindly use the short ground probe; it traces the duck hull
            // down to sv_maxvelocity and uses the first slope in the same
            // way as the SourceMod plugin.
            Vector3 duckHalfExtents = GetBoxHalfExtents(true);
            Vector3 center = landingOrigin + _movementUp * duckHalfExtents.y;
            int hitCount = Physics.BoxCastNonAlloc(
                center,
                GetQueryHalfExtents(duckHalfExtents),
                -_movementUp,
                _moveHits,
                Quaternion.identity,
                MaxVelocity,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);
            if (!TryGetNearestHit(hitCount, out RaycastHit slopeHit))
                return;

            float groundUpDot = Vector3.Dot(slopeHit.normal, _movementUp);
            if (groundUpDot < WalkableNormal || groundUpDot >= 0.999999f)
                return;

            Vector3 clipped = ClipLandingVelocity(
                horizontalVelocity,
                slopeHit.normal,
                _movementUp);
            if (clipped.magnitude > horizontalVelocity.magnitude)
                horizontalVelocity = clipped;
        }

        private static Vector3 ClipLandingVelocity(Vector3 velocity, Vector3 normal, Vector3 up)
        {
            float backoff = Vector3.Dot(velocity, normal);
            Vector3 clipped = velocity - normal * backoff;
            float correction = Vector3.Dot(clipped, normal);
            if (correction < 0f)
                clipped -= normal * correction;
            return Vector3.ProjectOnPlane(clipped, up);
        }

        private void AirMove(float realPreVelMod)
        {
            Vector3 velocity = _surfer.MoveData.Velocity;

            // The same Source duck crop is applied before AirMove. GOKZ's
            // AirAccelerate detour removes only the prestrafe modifier; it
            // does not remove the 85 HU duck wish-speed.
            // AirAccelerate receives the same Source max-wish-speed first;
            // the KZTimer detour removes _preVelMod from that parameter
            // immediately before the 30 HU air-speed test.
            float maxSpeed = GetPlayerMaxSpeed(realPreVelMod) *
                _surfer.MoveData.SurfaceMaxSpeedFactor *
                GetDuckSpeedModifier();
            bool duckButtonDown = _surfer.MoveData.crouching || _ducking ||
                _isCrouched;
            GetWishValues(
                maxSpeed,
                duckButtonDown ? GetDuckSpeedModifier() : 1f,
                out Vector3 wishDirection,
                out float wishSpeed);

            // KZTimer detours AirAccelerate and removes the prestrafe modifier
            // from wishspeed. The 30 HU cap stays in place.
            float airWishSpeed = wishSpeed / Mathf.Max(1f, _preVelMod);
            velocity = GokzMovementRules.AirAccelerate(
                velocity,
                wishDirection,
                airWishSpeed,
                AirAccelerate,
                AirMaxWishSpeed,
                _surfer.MoveData.SurfaceFriction,
                TickInterval);

            _movementVelocity = velocity;
            _surfer.MoveData.Velocity = velocity;
        }

        private void WaterMove(float realPreVelMod)
        {
            Vector3 velocity = _surfer.MoveData.Velocity;

            // Source WaterMove uses the full view vector, a 0.8 wish-speed
            // multiplier, and speed-proportional water friction. Ground
            // Friction's stop-speed term must not be reused here.
            velocity = GokzMovementRules.ApplyWaterFriction(
                velocity,
                Friction,
                _surfer.MoveData.SurfaceFriction,
                TickInterval);

            // WaterMove receives mv->m_flMaxSpeed after CheckParameters and
            // HandleDuckingSpeedCrop, not the unmodified client speed.
            float waterMaxSpeed = GetPlayerMaxSpeed(realPreVelMod) *
                _surfer.MoveData.SurfaceMaxSpeedFactor;
            bool duckButtonDown = _surfer.MoveData.crouching || _ducking ||
                _isCrouched;
            if (duckButtonDown)
            {
                waterMaxSpeed *= GetDuckSpeedModifier();
            }
            else if (_surfer.MoveData.sprinting)
            {
                float horizontalSpeedHu = Vector3.ProjectOnPlane(
                    velocity,
                    _movementUp).magnitude * HammerUnitsPerUnityUnit;
                float walkThresholdHu = GetPlayerMaxSpeed(realPreVelMod) *
                    HammerUnitsPerUnityUnit * WalkSpeedModifier + 25f;
                if (horizontalSpeedHu < walkThresholdHu)
                    waterMaxSpeed *= WalkSpeedModifier;
            }

            GetWaterWishValues(
                waterMaxSpeed,
                duckButtonDown ? GetDuckSpeedModifier() : 1f,
                out Vector3 wishDirection,
                out float wishSpeed);
            if (wishSpeed > waterMaxSpeed)
                wishSpeed = waterMaxSpeed;
            wishSpeed *= WaterMoveSpeedMultiplier;
            velocity = GokzMovementRules.AccelerateWater(
                velocity,
                wishDirection,
                wishSpeed,
                WaterAccelerate,
                _surfer.MoveData.SurfaceFriction,
                TickInterval);

            _movementVelocity = velocity;
            _surfer.MoveData.Velocity = velocity;
        }

        private void TryWaterJump()
        {
            // CGameMovement::CheckWaterJump is only attempted at waist level.
            // A Unity trigger supplies the water level; the two hull traces
            // below reproduce Source's 24-HU waist obstruction and its open
            // eye-height/standable landing checks without treating a water
            // trigger itself as solid.
            if (_surfer.MoveData.waterLevel != GokzWaterLevel.Waist ||
                _waterJumpTimeMilliseconds > 0f)
                return;

            float verticalSpeed = Vector3.Dot(
                _surfer.MoveData.Velocity,
                _movementUp) * HammerUnitsPerUnityUnit;
            if (verticalSpeed < -180f)
                return;

            Vector3 flatVelocity = Vector3.ProjectOnPlane(
                _surfer.MoveData.Velocity,
                _movementUp);
            if (flatVelocity.sqrMagnitude < 0.000001f)
                return;
            flatVelocity.Normalize();

            Vector3 flatForward = Vector3.ProjectOnPlane(
                _surfer.Forward,
                _movementUp);
            if (flatForward.sqrMagnitude < 0.000001f)
                return;
            flatForward.Normalize();
            if (Vector3.Dot(flatVelocity, flatForward) < 0f)
                return;

            Vector3 origin = _surfer.MoveData.playerTransform.position;
            float waistHeight = (_isCrouched ? 27f : 36f) * UnitScale;
            Vector3 waist = origin + _movementUp * waistHeight;
            Vector3 waistEnd = waist + flatForward * WaterJumpForwardDistance;
            if (!TraceBox(waist, waistEnd, GetBoxHalfExtents(_isCrouched), out RaycastHit waistHit) ||
                waistHit.collider == null)
                return;

            float eyeHeight = Mathf.Lerp(
                DuckEyeStanding,
                DuckEyeCrouched,
                Mathf.Clamp01(_eyeDuckAmount));
            Vector3 eyeStart = origin + _movementUp *
                ((eyeHeight + WaterJumpHeight) * UnitScale);
            Vector3 eyeEnd = eyeStart + flatForward * WaterJumpForwardDistance;
            if (TraceBox(eyeStart, eyeEnd, GetBoxHalfExtents(_isCrouched), out RaycastHit eyeHit) &&
                eyeHit.collider != null)
                return;

            Vector3 landingStart = eyeEnd;
            Vector3 landingEnd = landingStart - _movementUp *
                WaterJumpTraceDistance;
            if (!TraceBox(landingStart, landingEnd, GetBoxHalfExtents(_isCrouched),
                    out RaycastHit landingHit) ||
                landingHit.collider == null ||
                Vector3.Dot(landingHit.normal, _movementUp) < WalkableNormal)
                return;

            _waterJumpTimeMilliseconds = WaterJumpDurationMilliseconds;
            _waterJumpVelocity = -50f * UnitScale * waistHit.normal;
            _surfer.MoveData.Velocity = _waterJumpVelocity +
                _movementUp * WaterJumpVelocity;
            Grounded = false;
            _surfer.MoveData.grounded = false;
        }

        private void WaterJump()
        {
            if (_waterJumpTimeMilliseconds <= 0f)
                return;

            _waterJumpTimeMilliseconds = Mathf.Max(
                0f,
                _waterJumpTimeMilliseconds - TickInterval * 1000f);
            Vector3 velocity = _surfer.MoveData.Velocity;
            float vertical = Vector3.Dot(velocity, _movementUp);
            // Source writes m_vecWaterJumpVel back every WaterJump call,
            // including the tick on which the timer expires or the player
            // leaves the water. Do not clear the horizontal component before
            // that write; otherwise the last water-jump tick silently regains
            // the old air velocity.
            velocity = _waterJumpVelocity + _movementUp * vertical;
            _surfer.MoveData.Velocity = velocity;

            // Source keeps the timer while the player is still in any water
            // level (feet, waist, or eyes); it only clears it after leaving
            // the water volume completely.
            if (_waterJumpTimeMilliseconds <= 0f ||
                _surfer.MoveData.waterLevel == GokzWaterLevel.NotInWater)
            {
                _waterJumpTimeMilliseconds = 0f;
                _waterJumpVelocity = Vector3.zero;
            }
        }

        private bool TraceBox(
            Vector3 startCenter,
            Vector3 endCenter,
            Vector3 halfExtents,
            out RaycastHit hit)
        {
            hit = default;
            Vector3 delta = endCenter - startCenter;
            float distance = delta.magnitude;
            if (distance <= CollisionSkin)
                return false;

            int hitCount = Physics.BoxCastNonAlloc(
                startCenter,
                GetQueryHalfExtents(halfExtents),
                delta / distance,
                _moveHits,
                Quaternion.identity,
                distance,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);
            return TryGetNearestHit(hitCount, out hit);
        }

        private void ApplyJump(bool perfect)
        {
            Vector3 velocity = _surfer.MoveData.Velocity;
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, _movementUp);
            LastJumpWasDuck = _isCrouched || _ducking || _duckAmount > 0f;
            JumpCount++;
            _hasWalkMovedSinceLastJump = false;

            bool appliedPerfRules = perfect &&
                (_settings.tweakPerfJump || _mode == GokzMode.SimpleKz);
            if (appliedPerfRules)
            {
                float horizontalSpeed = horizontalVelocity.magnitude;
                if (_mode == GokzMode.SimpleKz)
                {
                    int cmdsSinceLanding = _landingTick < 0
                        ? int.MaxValue
                        : _movementTick - _landingTick;
                    float landingSpeedHu = _landingVelocity.magnitude * HammerUnitsPerUnityUnit;
                    float takeoffSpeedHu = landingSpeedHu;
                    if (landingSpeedHu > 250f)
                    {
                        takeoffSpeedHu = Mathf.Min(
                            landingSpeedHu,
                            (0.2f * landingSpeedHu + 200f) * _landingPreVelMod);
                    }

                    Vector3 landingDirection = _landingVelocity.sqrMagnitude >
                        0.000001f
                        ? _landingVelocity.normalized
                        : horizontalVelocity.normalized;
                    if (landingDirection.sqrMagnitude > 0.000001f)
                        horizontalVelocity = landingDirection *
                            (takeoffSpeedHu * UnitScale);

                    if (cmdsSinceLanding > 1 || takeoffSpeedHu > 250f)
                        _preVelMod = _landingPreVelMod;
                }
                else if (horizontalSpeed > PerfSpeedCap)
                    horizontalVelocity = GokzMovementRules.ApplyPerfJumpSpeedCap(
                        horizontalVelocity,
                        _movementUp,
                        PerfSpeedCap);
                LastJumpWasPerfect = true;
            }
            // MovementAPI records takeoff speed after GOKZ's Movement_OnJumpPre
            // hook has changed the velocity. Keep the HUD/stat value aligned
            // with the velocity that actually leaves the ground.
            LastTakeoffSpeed = horizontalVelocity.magnitude * HammerUnitsPerUnityUnit;

            float currentVerticalSpeed = Vector3.Dot(velocity, _movementUp);
            bool crouchJump = LastJumpWasDuck;
            float jumpVerticalSpeed = crouchJump
                ? JumpImpulse
                : currentVerticalSpeed + JumpImpulse;
            float jumpImpulseHu = Mathf.Abs(
                (jumpVerticalSpeed - currentVerticalSpeed) *
                    HammerUnitsPerUnityUnit);
            jumpVerticalSpeed *= GetStaminaJumpScale();
            jumpVerticalSpeed *= Mathf.Max(
                0f, _surfer.MoveData.SurfaceJumpFactor);
            velocity = horizontalVelocity + _movementUp * jumpVerticalSpeed;
            _surfer.MoveData.Velocity = velocity;

            if (_mode == GokzMode.SimpleKz && perfect &&
                _landingTick >= 0 && _movementTick - _landingTick <= 1)
                NerfRealPerfOrigin(velocity);

            _movementVelocity = velocity;

            // CGameMovement::CheckJumpButton calls FinishGravity immediately
            // after applying the jump impulse. FullWalkMove then calls its
            // normal end-of-tick FinishGravity as well. Keep both halves so
            // this port has the same StartGravity -> jump -> FinishGravity
            // ordering as Source instead of silently giving the jump one
            // extra half-tick of vertical velocity.
            FinishGravity();
            AddJumpStamina(jumpImpulseHu);

            _surfer.MoveData.hasJump = true;
            _surfer.MoveData.grounded = false;
            Grounded = false;
        }

        private void NerfRealPerfOrigin(Vector3 processingVelocity)
        {
            // SimpleKZ's NerfRealPerf traces the active player hull down two
            // Source units and snaps the processing origin to the trace end
            // when the processing velocity is still rising.
            if (Vector3.Dot(processingVelocity, _movementUp) <
                0.000001f || _surfer.MoveData.playerTransform == null)
                return;

            Vector3 start = _surfer.MoveData.playerTransform.position;
            Vector3 center = start + _movementUp *
                GetBoxHalfExtents(_isCrouched).y;
            int hitCount = Physics.BoxCastNonAlloc(
                center,
                GetQueryHalfExtents(GetBoxHalfExtents(_isCrouched)),
                -_movementUp,
                _moveHits,
                Quaternion.identity,
                2f * UnitScale,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);
            if (!TryGetNearestHit(hitCount, out RaycastHit hit))
                return;

            float distance = Mathf.Max(0f, hit.distance - CollisionSkin);
            _surfer.MoveData.playerTransform.position -=
                _movementUp * distance;
            _surfer.MoveData.Origin =
                _surfer.MoveData.playerTransform.position;
            Physics.SyncTransforms();
        }

        private void GetWishValues(
            float maxSpeed,
            float commandCrop,
            out Vector3 wishDirection,
            out float wishSpeed)
        {
            Vector3 forward = Vector3.ProjectOnPlane(_surfer.Forward, _movementUp);
            Vector3 right = Vector3.ProjectOnPlane(_surfer.Right, _movementUp);
            if (forward.sqrMagnitude < 0.000001f)
                forward = _surfer.MoveData.playerTransform.forward;
            if (right.sqrMagnitude < 0.000001f)
                right = _surfer.MoveData.playerTransform.right;

            forward.Normalize();
            right.Normalize();

            // CGameMovement::CheckParameters receives the raw client command
            // at the Source client command speeds (450 HU/s for forward and
            // side), then crops that command to mv->m_flMaxSpeed before
            // WalkMove/AirMove. Using the command fields preserves the
            // distinction for analog input as well as keyboard input.
            GetSourceCommandMoves(
                out float forwardMove,
                out float sideMove,
                out _);
            Vector3 wishVelocity = forward * (forwardMove * UnitScale) +
                right * (sideMove * UnitScale);
            float uncroppedWishSpeed = wishVelocity.magnitude;
            float uncroppedMaxSpeed = Mathf.Max(0f, maxSpeed) /
                Mathf.Max(0.000001f, commandCrop);
            wishSpeed = Mathf.Min(uncroppedWishSpeed, uncroppedMaxSpeed) *
                commandCrop;
            wishDirection = wishVelocity.sqrMagnitude > 0.000001f
                ? wishVelocity.normalized
                : Vector3.zero;
        }

        private float GetDuckSpeedModifier()
        {
            // Source's GetDuckSpeedModifier(duckFraction):
            // CS_PLAYER_SPEED_DUCK_MODIFIER * fraction + 1 - fraction.
            return Mathf.Lerp(1f, DuckSpeedModifier, Mathf.Clamp01(_duckAmount));
        }

        private void GetWaterWishValues(
            float commandMaxSpeed,
            float commandCrop,
            out Vector3 wishDirection,
            out float wishSpeed)
        {
            Vector3 forward = _surfer.Forward.normalized;
            Vector3 right = _surfer.Right.normalized;
            // CGameMovement::CheckParameters first crops the scalar command
            // (forward/side/up) to mv->m_flMaxSpeed. WaterMove then builds a
            // full 3D wish vector from that cropped command. Keep every value
            // in Unity-space here.
            GetSourceCommandMoves(
                out float sourceForwardMove,
                out float sourceSideMove,
                out float sourceUpMove);
            // CCSGameMovement::HandleDuckingSpeedCrop scales the command
            // fields themselves, not only m_flMaxSpeed. This is observable
            // for partial analog input in water, where the uncropped command
            // can remain below the ducked max speed.
            float rawForwardMove = sourceForwardMove * commandCrop * UnitScale;
            float rawSideMove = sourceSideMove * commandCrop * UnitScale;
            float rawUpMove = sourceUpMove * commandCrop * UnitScale;

            float commandMagnitude = new Vector3(
                rawForwardMove,
                rawSideMove,
                rawUpMove).magnitude;
            if (commandMagnitude > commandMaxSpeed && commandMagnitude > 0.000001f)
            {
                float commandScale = commandMaxSpeed / commandMagnitude;
                rawForwardMove *= commandScale;
                rawSideMove *= commandScale;
                rawUpMove *= commandScale;
            }

            Vector3 wishVelocity = forward * rawForwardMove +
                right * rawSideMove;

            if (_surfer.MoveData.wishJump || _surfer.MoveData.jumpHeld)
            {
                // Source uses mv->m_flClientMaxSpeed, not mv->m_flMaxSpeed,
                // for the swimming jump impulse.
                wishVelocity += _movementUp * ClientMaxSpeed;
            }
            else if (Mathf.Abs(rawForwardMove) < 0.0001f &&
                     Mathf.Abs(rawSideMove) < 0.0001f &&
                     Mathf.Abs(rawUpMove) < 0.0001f)
            {
                // CGameMovement sinks an idle swimmer at 60 HU/s.
                wishVelocity -= _movementUp * (60f * UnitScale);
            }
            else
            {
                // Source exaggerates upward movement caused by looking up
                // while moving forward. The clamp is in client-speed units.
                float forwardUpMovement = rawForwardMove *
                    Vector3.Dot(forward, _movementUp) * 2f;
                forwardUpMovement = Mathf.Clamp(
                    forwardUpMovement,
                    0f,
                    ClientMaxSpeed);
                wishVelocity += _movementUp *
                    (forwardUpMovement + rawUpMove);
            }

            wishSpeed = wishVelocity.magnitude;
            wishDirection = wishSpeed > 0.000001f
                ? wishVelocity / wishSpeed
                : Vector3.zero;
        }

        private void GetSourceCommandMoves(
            out float forwardMove,
            out float sideMove,
            out float upMove)
        {
            forwardMove = _surfer.MoveData.ForwardMove;
            sideMove = _surfer.MoveData.SideMove;
            upMove = _surfer.MoveData.UpMove;

            // Editor tests and older callers may provide only normalized
            // axes. Keep that compatibility fallback, while the active GOKZ
            // input path supplies the actual Source command fields.
            if (Mathf.Abs(forwardMove) < 0.0001f &&
                Mathf.Abs(_surfer.MoveData.verticalAxis) > 0.0001f)
            {
                forwardMove = Mathf.Clamp(
                    _surfer.MoveData.verticalAxis,
                    -1f,
                    1f) * SourceClientMoveSpeed;
            }

            if (Mathf.Abs(sideMove) < 0.0001f &&
                Mathf.Abs(_surfer.MoveData.horizontalAxis) > 0.0001f)
            {
                sideMove = Mathf.Clamp(
                    _surfer.MoveData.horizontalAxis,
                    -1f,
                    1f) * SourceClientMoveSpeed;
            }
        }

        private void ApplyFriction(
            ref Vector3 velocity,
            float friction,
            float surfaceFriction = 1f)
        {
            velocity = GokzMovementRules.ApplyFriction(
                velocity,
                StopSpeed,
                friction,
                surfaceFriction,
                TickInterval);
        }

        private static void AccelerateVelocity(
            ref Vector3 velocity,
            Vector3 wishDirection,
            float wishSpeed,
            float acceleration,
            float surfaceFriction)
        {
            velocity = GokzMovementRules.Accelerate(
                velocity,
                wishDirection,
                wishSpeed,
                acceleration,
                surfaceFriction,
                TickInterval);
        }

        private static Vector3 ClipVelocity(Vector3 velocity, Vector3 normal, float overbounce)
        {
            // Source's ClipVelocity: project out of the blocking plane and
            // make one correction pass so floating point error cannot leave
            // the hull moving back into that plane.
            float backoff = Vector3.Dot(velocity, normal) * overbounce;
            Vector3 clipped = velocity - normal * backoff;
            float adjust = Vector3.Dot(clipped, normal);
            if (adjust < 0f)
                clipped -= normal * adjust;
            return clipped;
        }

        private void CheckVelocity(ref Vector3 velocity)
        {
            if (float.IsNaN(velocity.x) || float.IsInfinity(velocity.x))
                velocity.x = 0f;
            if (float.IsNaN(velocity.y) || float.IsInfinity(velocity.y))
                velocity.y = 0f;
            if (float.IsNaN(velocity.z) || float.IsInfinity(velocity.z))
                velocity.z = 0f;

            // Source bounds each component, not the vector magnitude.
            velocity.x = Mathf.Clamp(velocity.x, -MaxVelocity, MaxVelocity);
            velocity.y = Mathf.Clamp(velocity.y, -MaxVelocity, MaxVelocity);
            velocity.z = Mathf.Clamp(velocity.z, -MaxVelocity, MaxVelocity);
        }

        private void CheckOrigin()
        {
            if (_surfer == null || _surfer.MoveData.playerTransform == null)
                return;

            Vector3 origin = _surfer.MoveData.playerTransform.position;
            bool invalid = float.IsNaN(origin.x) || float.IsInfinity(origin.x) ||
                float.IsNaN(origin.y) || float.IsInfinity(origin.y) ||
                float.IsNaN(origin.z) || float.IsInfinity(origin.z);
            if (!invalid)
                return;

            // CGameMovement::CheckVelocity resets a bogus origin component to
            // zero instead of allowing a NaN to poison every later trace.
            if (float.IsNaN(origin.x) || float.IsInfinity(origin.x))
                origin.x = 0f;
            if (float.IsNaN(origin.y) || float.IsInfinity(origin.y))
                origin.y = 0f;
            if (float.IsNaN(origin.z) || float.IsInfinity(origin.z))
                origin.z = 0f;
            _surfer.MoveData.playerTransform.position = origin;
            _surfer.MoveData.Origin = origin;
            Physics.SyncTransforms();
        }

        private float GetGravityAcceleration()
        {
            if (_surfer == null || !_surfer.MoveData.isGravity || _surfer.MoveData.LoseWeight)
                return 0f;

            return Gravity * Mathf.Max(0f, _surfer.MoveData.GravityFactor) *
                _sourceGravityScale;
        }

        private float GetStaminaSpeedScale()
        {
            if (_settings.staminaMax <= 0f || _stamina <= 0f)
                return 1f;

            // CCSGameMovement uses the fixed STAMINA_RANGE (100), while the
            // mode cvar limits the accumulated value to sv_staminamax.
            float scale = Mathf.Clamp01(
                1f - _stamina / SourceStaminaRange);
            return scale * scale;
        }

        private float GetStaminaJumpScale()
        {
            if (_settings.staminaMax <= 0f || _stamina <= 0f)
                return 1f;

            return Mathf.Clamp01(1f - _stamina / SourceStaminaRange);
        }

        private void AddJumpStamina(float impulseHu)
        {
            if (_settings.staminaMax <= 0f ||
                _settings.staminaJumpCost <= 0f)
                return;

            _stamina = Mathf.Clamp(
                _stamina + _settings.staminaJumpCost * impulseHu,
                0f,
                _settings.staminaMax);
        }

        private void AddLandingStamina(float fallVelocityHu)
        {
            if (_settings.staminaMax <= 0f ||
                _settings.staminaLandCost <= 0f ||
                fallVelocityHu <= 0f)
                return;

            _stamina = Mathf.Clamp(
                _stamina + _settings.staminaLandCost * fallVelocityHu,
                0f,
                _settings.staminaMax);
        }

        private float UpdatePrestrafeModifier(bool grounded)
        {
            if (!_settings.prestrafe)
            {
                _preVelMod = 1f;
                return 1f;
            }

            if (_mode == GokzMode.SimpleKz)
                return UpdateSimplePrestrafeModifier(grounded);

            Vector3 currentForward = Vector3.ProjectOnPlane(_surfer.Forward, _movementUp);
            if (currentForward.sqrMagnitude < 0.000001f)
                currentForward = Vector3.forward;
            currentForward.Normalize();

            if (!_hasPreviousView)
            {
                _previousViewForward = currentForward;
                _hasPreviousView = true;
            }

            float yawDelta = _surfer.MoveData.ViewYawDelta;
            if (Mathf.Abs(yawDelta) <= 0.0001f)
            {
                float previousYaw = Mathf.Atan2(_previousViewForward.x, _previousViewForward.z) * Mathf.Rad2Deg;
                float currentYaw = Mathf.Atan2(currentForward.x, currentForward.z) * Mathf.Rad2Deg;
                yawDelta = Mathf.DeltaAngle(previousYaw, currentYaw);
            }
            bool turning = Mathf.Abs(yawDelta) > 0.0001f;
            bool turningLeft = yawDelta < 0f;
            _previousViewForward = currentForward;

            if (!grounded)
                return _preVelMod;

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                _surfer.MoveData.Velocity,
                _movementUp);
            float speed = horizontalVelocity.magnitude;
            bool strafing = _surfer.MoveData.moveLeft || _surfer.MoveData.moveRight;

            if (!turning)
            {
                if (_elapsed - _preVelModLastChange > 0.2f)
                {
                    _preVelMod = 1f;
                    _preVelModLastChange = _elapsed;
                }
                else if (_preVelMod > PreVelModMax + 0.007f)
                {
                    return PreVelModMax - 0.001f;
                }
            }
            else if (strafing && speed > PreSpeedThreshold)
            {
                float increment = _preVelMod > 1.04f
                    ? PreTickIncrementFast
                    : PreTickIncrement;
                bool movingForward = GetClientMovingDirection() > 0f;
                bool movingRight = _surfer.MoveData.moveRight;
                bool movingLeft = _surfer.MoveData.moveLeft;
                bool turningRight = !turningLeft;

                bool validTurn =
                    (movingRight && turningRight || turningLeft && !movingForward) ||
                    (movingLeft && turningLeft || turningRight && !movingForward);

                if (validTurn)
                {
                    _preTickCounter++;
                    if (_preTickCounter < PreTickTurnWindow)
                    {
                        _preVelMod += increment;
                        if (_preVelMod > PreVelModMax)
                        {
                            if (_preVelMod > PreVelModMax + 0.007f)
                                _preVelMod = PreVelModMax - 0.001f;
                            else
                                _preVelMod -= 0.007f;
                        }
                        _preVelMod += increment;
                    }
                    else
                    {
                        _preVelMod -= PreTickTurnDecay;
                        _preTickCounter -= 2;
                        if (_preVelMod < 1f)
                        {
                            _preVelMod = 1f;
                            _preTickCounter = 0;
                        }
                    }
                }
                else
                {
                    _preVelMod -= PreTickDecrement;
                    if (_preVelMod < 1f)
                        _preVelMod = 1f;
                }

                _preVelModLastChange = _elapsed;
            }
            else
            {
                _preTickCounter = 0;
                return 1f;
            }

            return _preVelMod;
        }

        private float GetClientMovingDirection()
        {
            Vector3 velocity = _surfer.MoveData.Velocity;
            if (velocity.sqrMagnitude < 0.000001f)
                return 0f;
            velocity.Normalize();

            // GOKZ clamps eye pitch to [-70, 70] before GetAngleVectors.
            // Reconstruct that same forward vector instead of using a flat
            // yaw-only comparison; looking up/down changes the dot product.
            Vector3 view = _surfer.Forward.normalized;
            float pitch = Mathf.Asin(Mathf.Clamp(view.y, -1f, 1f)) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(view.x, view.z) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -70f, 70f);
            Vector3 viewDirection = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
            return Vector3.Dot(velocity, viewDirection.normalized);
        }

        private float UpdateSimplePrestrafeModifier(bool grounded)
        {
            _simpleTicksSinceIncrement++;

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                _surfer.MoveData.Velocity,
                _movementUp);
            float speed = horizontalVelocity.magnitude;
            if (speed < 0.000001f)
            {
                _simpleBonusSpeed = 0f;
                _simpleTurnRate = 0f;
                _preVelMod = 1f;
                return 1f;
            }

            float baseSpeed = Mathf.Min(SpeedNormal, speed / Mathf.Max(1f, _preVelMod));
            float newBonusSpeed = _simpleBonusSpeed;
            if (!grounded)
            {
                newBonusSpeed -= SimpleSpeedDecrementMidair * UnitScale;
            }
            else
            {
                bool validButtons =
                    (_surfer.MoveData.moveForward ^ _surfer.MoveData.moveBack) ||
                    (_surfer.MoveData.moveLeft ^ _surfer.MoveData.moveRight);
                float yawDelta = _surfer.MoveData.ViewYawDelta;
                bool turning = Mathf.Abs(yawDelta) > 0.0001f;

                if (turning && validButtons)
                {
                    bool turningLeft = yawDelta < 0f;
                    if (turningLeft != _simpleTurningLeft)
                    {
                        _simpleBonusSpeed = 0f;
                        _simpleTurnRate = 0f;
                        _preVelMod = 1f;
                        newBonusSpeed = 0f;
                    }

                    _simpleTurningLeft = turningLeft;
                    float newTurningRate = Mathf.Min(
                        SimpleMaxRewardTurnRate,
                        Mathf.Abs(yawDelta));
                    if (_simpleTicksSinceIncrement <= SimpleGraceTicks)
                    {
                        newTurningRate = Mathf.Min(
                            SimpleMaxRewardTurnRate,
                            newTurningRate / Mathf.Max(1, _simpleTicksSinceIncrement));
                        _simpleTurnRate = Mathf.Max(
                            newTurningRate,
                            _simpleTurnRate -
                            SimpleMaxTurnRateDecrement * _simpleTicksSinceIncrement);
                        newBonusSpeed += CalculateSimplePrestrafeReward(
                            _simpleTurnRate,
                            baseSpeed) * _simpleTicksSinceIncrement;
                    }
                    else
                    {
                        _simpleTurnRate = Mathf.Max(
                            newTurningRate,
                            _simpleTurnRate - SimpleMaxTurnRateDecrement);
                        newBonusSpeed += CalculateSimplePrestrafeReward(
                            _simpleTurnRate,
                            baseSpeed);
                    }

                    _simpleTicksSinceIncrement = 0;
                }
                else if (_simpleTicksSinceIncrement > SimpleGraceTicks)
                {
                    _simpleTurnRate = Mathf.Max(
                        0f,
                        _simpleTurnRate - SimpleMaxTurnRateDecrement);
                }
            }

            if (newBonusSpeed < 0f)
            {
                newBonusSpeed = 0f;
            }
            else
            {
                float baseSpeedScale = Mathf.Clamp01(baseSpeed / SpeedNormal);
                float turnRateScale = Mathf.Min(
                    1f,
                    _simpleTurnRate / SimpleMaxRewardTurnRate);
                float scaledMaxBonus = SimpleSpeedMax * UnitScale *
                    baseSpeedScale * turnRateScale;
                newBonusSpeed = Mathf.Min(newBonusSpeed, scaledMaxBonus);
            }

            _simpleBonusSpeed = newBonusSpeed;
            _preVelMod = 1f + newBonusSpeed / Mathf.Max(baseSpeed, 0.000001f);
            return _preVelMod;
        }

        private static float CalculateSimplePrestrafeReward(
            float yawDifference,
            float baseSpeed)
        {
            float rewardScale = Mathf.Clamp01(
                yawDifference / SimpleMaxRewardTurnRate);
            return SimpleSpeedIncrement * UnitScale *
                rewardScale * baseSpeed / (250f * UnitScale);
        }
    }
}
