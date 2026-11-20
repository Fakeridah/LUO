using System.Collections.Generic;
using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// KZTimer movement port.
    ///
    /// The values and order of operations come from GOKZ KZTimer and the Source
    /// CGameMovement implementation. The active hull is an axis-aligned
    /// Source player box and is swept manually; CharacterController is not
    /// used by this path.
    /// </summary>
    public sealed class SourceMovementCore
    {
        private enum SourcePlaneType
        {
            X = 0,
            Y = 1,
            Z = 2,
            AnyX = 3,
            AnyY = 4,
            AnyZ = 5
        }

        private struct SourceTraceResult
        {
            public bool hit;
            public bool startSolid;
            public bool allSolid;
            public float fraction;
            public float fractionLeftSolid;
            public Vector3 endPosition;
            public Vector3 planeNormal;
            public SourcePlaneType planeType;
            public Collider hitCollider;
            public GokzSourceContents contents;
            public GokzSourceSurfaceData surface;
            public bool contactedPortalRamp;
            public RaycastHit unityHit;
        }

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
        private float Bounce => _settings.bounce;
        private float WaterAccelerate => _settings.waterAccelerate;
        private float WaterMoveSpeedMultiplier => _settings.waterMoveSpeedMultiplier;

        // Source player hull: {-16, -16, 0} to {16, 16, 72}; duck max Z 54.
        private const float PlayerHullHeight = 72f * UnitScale;
        private const float DuckHullHeight = 54f * UnitScale;
        private const float HullRadius = 16f * UnitScale;
        private const float StepHeight = 18f * UnitScale;
        private const float SourceStepEpsilon = 0.03125f * UnitScale;
        private const float SourceDistanceEpsilon = 0.03125f * UnitScale;
        private const float CollisionSkin = SourceDistanceEpsilon;
        private const float SourcePlaneEpsilon = 1.1920929e-7f;
        private const float SourceStaminaRange = 100f;
        private const GokzSourceContents PlayerSolidContents =
            GokzSourceContents.Solid |
            GokzSourceContents.Moveable |
            GokzSourceContents.PlayerClip |
            GokzSourceContents.Window |
            GokzSourceContents.Monster |
            GokzSourceContents.Grate;
        private const GokzSourceContents PlayerSolidBrushContents =
            GokzSourceContents.Solid |
            GokzSourceContents.Moveable |
            GokzSourceContents.PlayerClip |
            GokzSourceContents.Window |
            GokzSourceContents.Grate;
        private const GokzSourceContents LadderTraceContents =
            PlayerSolidContents & ~GokzSourceContents.PlayerClip;

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

        private IGokzMovementPlayer _surfer;
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
        private bool _startGrounded;
        private bool _tickStarted;
        private bool _isCrouched;
        private bool _ducking;
        private float _duckAmount;
        // Track CS:GO's eye offset separately from the collision hull. The
        // Source FinishDuck/FinishUnDuck transitions update both, but their
        // timing differs while the player is airborne.
        private float _eyeDuckAmount;
        private float _duckSpeed = DuckSpeedNormal;
        private Vector3 _lastFullDuckSpeedPosition;
        private bool _rawDuckHeld;
        private bool _duckInputInitialized;
        private bool _effectiveDuckButtonHeld;
        private float _lastDuckTime = float.NegativeInfinity;
        private bool _wasPhysicallyCrouched;
        private bool _duckedAtProcessStart;
        // GOKZ's FixDisplacementStuck restores the velocity sampled by the
        // previous OnPlayerRunCmd, before that command entered Source movement.
        private Vector3 _gokzOldVelocity;
        private bool _gokzCommandInitialized;
        private int _movementTick;
        private int _landingTick = -1;
        private int _lastJumpRequestTick = int.MinValue;
        private bool _hullInitialized;
        private bool _groundStateInitialized;
        private bool _wasCrouching;
        private bool _wasJumping;
        private bool _wasJumpHeld;
        // Source FinishMove button history and GOKZ OnPlayerRunCmd history
        // are separate: CheckStuck exits still update the former, while the
        // GOKZ command snapshot is captured before CCSGameMovement mutates
        // the effective duck bit.
        private int _gokzOldButtons;
        private bool _previousWalkMoved;
        private bool _walkMovedThisTick;
        private bool _hasWalkMovedSinceLastJump;
        private bool _onLadderThisTick;
        private bool _wasOnLadder;
        private bool _waterMoveBranchThisTick;
        private float _ladderJumpBlockedUntil;
        private float _waterJumpTimeMilliseconds;
        private Vector3 _waterJumpVelocity;
        private bool _waterJumpProcessedThisTick;
        private float _lastGroundSurfaceMaxSpeedFactor = 1f;
        private int _stuckOffsetIndex;
        private float _lastStuckCheckTime = -0.05f;
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
        private Collider[] _hullOverlaps = new Collider[32];
        private Collider[] _sourceVolumeOverlaps = new Collider[64];
        private readonly HashSet<GokzMovementVolume> _sourceVolumesThisTick =
            new HashSet<GokzMovementVolume>();
        private readonly HashSet<GokzMovementVolume> _consumedSourcePushVolumes =
            new HashSet<GokzMovementVolume>();
        private RaycastHit[] _moveHits = new RaycastHit[16];
        private Vector3 _lastBoxQueryCenter;
        private Vector3 _lastBoxQueryHalfExtents;
        private Quaternion _lastBoxQueryRotation = Quaternion.identity;
        private Vector3 _lastBoxQuerySweepDelta;
        private GokzSourceContents _lastSourceContentsMask =
            PlayerSolidContents;
        private BoxCollider _sourceHullCollider;
        private Transform _sourceHullTransform;
        private Rigidbody _sourceRigidbody;
        private bool _collisionReady;
        private float _sourceGravityScale = 1f;
        private Vector3 _sourceTriggerBaseVelocity;
        private Vector3 _processStartPosition;
        private bool _sourceTriggerBaseVelocityFlag;
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

        public void EnableSourceCollision(IGokzMovementPlayer surfer)
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

        public void DisableSourceCollision(IGokzMovementPlayer surfer)
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

        public void Move(IGokzMovementPlayer surfer)
        {
            if (!_collisionReady || _movementBlockedThisTick ||
                surfer.MoveData.playerTransform == null)
                return;

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
            bool hitCollision;
            bool hitPortalWall;
            TrySlideMove(
                startPosition,
                startVelocity,
                startFinalVelocity,
                TickInterval,
                halfExtents,
                out slidePosition,
                out slideVelocity,
                out slideFinalVelocity,
                out hitCollision,
                out hitPortalWall);

            Vector3 bestPosition = slidePosition;
            Vector3 bestVelocity = slideVelocity;
            Vector3 bestFinalVelocity = slideFinalVelocity;

            if (Grounded && _waterJumpTimeMilliseconds <= 0f &&
                hitCollision && !hitPortalWall)
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

            // CGameMovement::WalkMove calls StayOnGround after either a clear
            // direct trace or the StepMove path. WaterMove/LadderMove and its
            // sub-1 HU early return do not run this snap.
            if (_walkMovedThisTick && startVelocity.magnitude >= UnitScale)
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
            out bool hitCollision,
            out bool hitPortalWall)
        {
            position = startPosition;
            velocity = startVelocity;
            finalVelocity = startFinalVelocity;
            hitCollision = false;
            hitPortalWall = false;

            // GOKZ gokz-momsurffix replaces CGameMovement::TryPlayerMove. Keep
            // its eight bump attempts, five clip planes, grounded first-plane
            // clipping, invalid ramp trace retries, and duplicate-plane ramp
            // correction in this one solver.
            Vector3 primalVelocity = startVelocity;
            Vector3 originalVelocity = startVelocity;
            Vector3[] planes = new Vector3[5];
            int numPlanes = 0;
            float timeLeft = duration;
            float allFraction = 0f;
            bool stuckOnRamp = false;
            bool hasValidPlane = false;
            Vector3 validPlane = Vector3.zero;
            Vector3 lastTracePlane = Vector3.zero;

            // GOKZ's gokz-momsurffix detours CGameMovement::TryPlayerMove and
            // raises its bump count from the CS:GO base value of four to eight.
            for (int bump = 0; bump < 8 && timeLeft > 0.000001f; bump++)
            {
                if (velocity.sqrMagnitude <= 0.00000001f)
                    break;

                if (stuckOnRamp)
                {
                    if (!hasValidPlane)
                    {
                        if (!IsNearlyZeroPlane(lastTracePlane) &&
                            !ArePlanesClose(lastTracePlane, validPlane))
                        {
                            validPlane = lastTracePlane;
                            hasValidPlane = true;
                        }
                        else
                        {
                            for (int plane = numPlanes - 1; plane >= 0; plane--)
                            {
                                if (IsNearlyZeroPlane(planes[plane]) ||
                                    !IsUnitPlane(planes[plane]) ||
                                    ArePlanesClose(planes[plane], validPlane))
                                    continue;

                                validPlane = planes[plane];
                                hasValidPlane = true;
                                break;
                            }
                        }
                    }

                    if (hasValidPlane)
                    {
                        float overbounce = Vector3.Dot(validPlane, _movementUp) >=
                            WalkableNormal
                            ? 1f
                            : 1f + Bounce * (1f - Mathf.Clamp01(
                                _surfer.MoveData.SurfaceFriction));
                        velocity = ClipVelocity(velocity, validPlane, overbounce);
                        originalVelocity = velocity;
                    }
                    else
                    {
                        float verticalSpeed = Vector3.Dot(velocity, _movementUp) *
                            HammerUnitsPerUnityUnit;
                        if ((verticalSpeed < -6.25f || verticalSpeed > 0f) &&
                            TryFindValidRampPlane(
                                position,
                                velocity,
                                timeLeft,
                                halfExtents,
                                bump,
                                out validPlane))
                        {
                            hasValidPlane = true;
                            continue;
                        }
                    }

                    if (hasValidPlane)
                    {
                        position += validPlane * (0.03125f * UnitScale);
                    }
                    else
                    {
                        stuckOnRamp = false;
                        continue;
                    }
                }

                Vector3 endPosition = position + velocity * timeLeft;
                SourceTraceResult trace = TracePlayerBBox(
                    position,
                    endPosition,
                    halfExtents);
                bool hit = trace.hit;
                RaycastHit nearest = trace.unityHit;
                bool contactedPortalRamp = trace.contactedPortalRamp;
                // Source applies the recovered plane to the retrace before
                // validating the trace and before it advances/reset flags.
                bool useRecoveredRampPlane = stuckOnRamp && hasValidPlane;
                if (useRecoveredRampPlane)
                {
                    trace.planeNormal = validPlane.normalized;
                    trace.planeType = GetSourcePlaneType(trace.planeNormal);
                }
                Vector3 tracePlaneNormal = trace.planeNormal;
                float fraction = trace.fraction;
                Vector3 traceEndPosition = trace.endPosition;

                if (trace.allSolid)
                {
                    // CGameMovement::TryPlayerMove stops immediately when
                    // TracePlayerBBox reports allsolid.
                    velocity = Vector3.zero;
                    finalVelocity = velocity;
                    return;
                }

                // gokz-momsurffix treats a later invalid airborne trace as a
                // ramp precision failure. It retries from a nudged origin and
                // may reconstruct a valid plane from 27 nearby hull traces.
                if (bump > 0 && !Grounded &&
                    !IsValidMovementTrace(
                        halfExtents,
                        trace))
                {
                    lastTracePlane = tracePlaneNormal.normalized;
                    hasValidPlane = false;
                    stuckOnRamp = true;
                    continue;
                }

                if (!hit || fraction >= 1f - SourcePlaneEpsilon)
                {
                    if ((bump == 0 || Grounded) &&
                        IsHullStuckAt(traceEndPosition, halfExtents))
                    {
                        hasValidPlane = false;
                        if (bump == 0)
                        {
                            stuckOnRamp = true;
                            continue;
                        }

                        // gokz-momsurffix retries the first full-fraction
                        // endpoint as a ramp precision failure. If a later
                        // grounded bump still ends inside solid geometry, it
                        // zeros velocity and exits this move.
                        velocity = Vector3.zero;
                        break;
                    }

                    allFraction += fraction;
                    position = traceEndPosition;
                    break;
                }

                hitCollision = true;
                if (Portal.IsPairedPortalWallContact(
                    nearest,
                    _lastBoxQueryCenter,
                    _lastBoxQueryHalfExtents +
                        Vector3.one * CollisionSkin,
                    _lastBoxQueryRotation,
                    _lastBoxQuerySweepDelta))
                    hitPortalWall = true;
                if (fraction > 0f)
                {
                    position += velocity * (timeLeft * fraction);
                    originalVelocity = velocity;
                    numPlanes = 0;
                    hasValidPlane = false;
                    stuckOnRamp = false;
                }

                allFraction += fraction;
                // gokz-momsurffix replaces the retrace plane with the
                // reconstructed ramp plane after nudging the origin. Keep
                // PhysX's hit distance/end position, but clip against that
                // same recovered plane instead of the raw triangle normal.
                Vector3 planeNormal = tracePlaneNormal;
                if (Mathf.Abs(Vector3.Dot(planeNormal, _movementUp)) <
                    0.0001f)
                {
                    planeNormal = Vector3.ProjectOnPlane(
                        planeNormal,
                        _movementUp).normalized;
                }

                // A hit at the end of the trace means the whole displacement
                // was covered. There is no remaining time to clip.
                if (fraction >= 1f - SourcePlaneEpsilon)
                    break;

                timeLeft -= timeLeft * fraction;
                if (numPlanes >= planes.Length)
                {
                    velocity = Vector3.zero;
                    break;
                }

                planes[numPlanes++] = planeNormal;

                if (numPlanes == 1 && Grounded)
                {
                    float overbounce = Vector3.Dot(
                        planes[0], _movementUp) >= WalkableNormal
                        ? 1f
                        : 1f + Bounce * (1f - Mathf.Clamp01(
                            _surfer.MoveData.SurfaceFriction));
                    velocity = ClipVelocity(
                        originalVelocity,
                        planes[0],
                        overbounce);
                    originalVelocity = velocity;
                }
                else
                {
                    bool foundValidPlane = false;
                    for (int i = 0; i < numPlanes; i++)
                    {
                        velocity = ClipVelocity(
                            originalVelocity,
                            planes[i],
                            1f);
                        bool valid = true;
                        for (int j = 0; j < numPlanes; j++)
                        {
                            if (j != i &&
                                Vector3.Dot(velocity, planes[j]) < 0f)
                            {
                                valid = false;
                                break;
                            }
                        }

                        if (valid)
                        {
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

                        // gokz-momsurffix's vertical-ramp fix pushes away
                        // from two duplicate planes while leaving gravity's
                        // current vertical component untouched.
                        if (ArePlanesClose(planes[0], planes[1]))
                        {
                            Vector3 pushedVelocity = originalVelocity +
                                2f * planes[0];
                            velocity = Vector3.ProjectOnPlane(
                                pushedVelocity,
                                _movementUp) +
                                _movementUp * Vector3.Dot(velocity, _movementUp);
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

                        if (Vector3.Dot(velocity, primalVelocity) <= 0f)
                        {
                            velocity = Vector3.zero;
                            break;
                        }
                    }
                }
            }

            if (allFraction <= 0f)
                velocity = Vector3.zero;

            finalVelocity = velocity;
        }

        private bool IsValidMovementTrace(
            Vector3 halfExtents,
            SourceTraceResult trace)
        {
            if (trace.allSolid || trace.startSolid)
                return false;

            Vector3 normal = trace.planeNormal.normalized;
            float vertical = Vector3.Dot(normal, _movementUp);
            if (trace.fraction <= SourcePlaneEpsilon &&
                trace.planeType >= SourcePlaneType.Z &&
                vertical < WalkableNormal &&
                !(trace.contactedPortalRamp && vertical > 0f))
                return false;

            // gokz-momsurffix::IsValidMovementTrace also performs a stationary
            // TracePlayerBBox at tr.endpos and rejects startsolid or any
            // incomplete stationary trace. This catches an endpoint that a
            // swept trace reports as valid even though it leaves the hull
            // embedded in a ramp triangle.
            return !IsHullStuckAt(trace.endPosition, halfExtents);
        }

        private bool TryFindValidRampPlane(
            Vector3 startPosition,
            Vector3 velocity,
            float timeLeft,
            Vector3 halfExtents,
            int bump,
            out Vector3 validPlane)
        {
            validPlane = Vector3.zero;
            float offsetRangeHu = bump * 2f * 0.03125f;
            float[] offsetsHu = { -offsetRangeHu, 0f, offsetRangeHu };
            Vector3 normalSum = Vector3.zero;
            int validPlanes = 0;
            Vector3 originalCenter = startPosition +
                _movementUp * halfExtents.y;
            Vector3 originalEndCenter = originalCenter + velocity * timeLeft;

            for (int x = 0; x < offsetsHu.Length; x++)
            for (int y = 0; y < offsetsHu.Length; y++)
            for (int z = 0; z < offsetsHu.Length; z++)
            {
                Vector3 offset = Vector3.right * (offsetsHu[x] * UnitScale) +
                    Vector3.forward * (offsetsHu[y] * UnitScale) +
                    _movementUp * (offsetsHu[z] * UnitScale);

                // gokz-momsurffix builds an asymmetric swept hull for each
                // offset. Convert its modified mins/maxs into a Unity center
                // shift and half-extents change.
                Vector3 offsetMins = offset * 0.5f;
                Vector3 offsetMaxs = offset * 0.5f;
                AdjustRampOffset(ref offsetMins, ref offsetMaxs, offset.x,
                    Vector3.right);
                AdjustRampOffset(ref offsetMins, ref offsetMaxs, offset.z,
                    Vector3.forward);
                AdjustRampOffset(ref offsetMins, ref offsetMaxs,
                    Vector3.Dot(offset, _movementUp), _movementUp);

                Vector3 centerShift = (offsetMaxs - offsetMins) * 0.5f;
                Vector3 halfDelta = (offsetMaxs + offsetMins) * 0.5f;
                Vector3 queryHalfExtents = new Vector3(
                    Mathf.Max(0.0001f, halfExtents.x + halfDelta.x),
                    Mathf.Max(0.0001f, halfExtents.y + halfDelta.y),
                    Mathf.Max(0.0001f, halfExtents.z + halfDelta.z));
                Vector3 traceStart = originalCenter + offset + centerShift;
                Vector3 traceEnd = originalEndCenter - offset + centerShift;
                SourceTraceResult rampTrace = TraceBoxByCenter(
                    traceStart,
                    traceEnd,
                    queryHalfExtents);
                if (!rampTrace.hit || rampTrace.startSolid ||
                    rampTrace.fraction <= 0f || rampTrace.fraction >= 1f)
                    continue;

                normalSum += rampTrace.planeNormal;
                validPlanes++;
            }

            if (validPlanes == 0 || IsNearlyZeroPlane(normalSum))
                return false;

            validPlane = normalSum.normalized;
            return true;
        }

        private static void AdjustRampOffset(
            ref Vector3 offsetMins,
            ref Vector3 offsetMaxs,
            float sourceAxisOffset,
            Vector3 axis)
        {
            if (sourceAxisOffset > 0f)
                offsetMins -= axis * (sourceAxisOffset * 0.25f);
            else if (sourceAxisOffset < 0f)
                offsetMaxs -= axis * (sourceAxisOffset * 0.25f);
        }

        private static bool IsUnitPlane(Vector3 plane)
        {
            return Mathf.Abs(plane.x) <= 1f &&
                Mathf.Abs(plane.y) <= 1f &&
                Mathf.Abs(plane.z) <= 1f;
        }

        private static bool IsNearlyZeroPlane(Vector3 plane)
        {
            return Mathf.Abs(plane.x) <= SourcePlaneEpsilon &&
                Mathf.Abs(plane.y) <= SourcePlaneEpsilon &&
                Mathf.Abs(plane.z) <= SourcePlaneEpsilon;
        }

        private static bool ArePlanesClose(Vector3 first, Vector3 second)
        {
            return Mathf.Abs(first.x - second.x) <= SourcePlaneEpsilon &&
                Mathf.Abs(first.y - second.y) <= SourcePlaneEpsilon &&
                Mathf.Abs(first.z - second.z) <= SourcePlaneEpsilon;
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
            bool downHitPortalWall;
            TrySlideMove(
                startPosition,
                startVelocity,
                startFinalVelocity,
                TickInterval,
                halfExtents,
                out downPosition,
                out _,
                out downFinalVelocity,
                out _,
                out downHitPortalWall);
            if (downHitPortalWall)
                return false;

            // Raise the hull. If the full step is blocked, this is not a
            // stair and the ordinary clipped path remains authoritative. A
            // partial trace is still a valid Source headroom result.
            float stepTraceDistance = StepHeight + SourceStepEpsilon;
            SourceTraceResult upTrace = TracePlayerBBox(
                startPosition,
                startPosition + _movementUp * stepTraceDistance,
                halfExtents);
            Vector3 raisedPosition;
            if (upTrace.allSolid)
            {
                return false;
            }
            if (upTrace.startSolid)
            {
                // Source StepMove only adopts the upward trace endpoint when
                // the trace begins outside solid geometry.
                raisedPosition = startPosition;
            }
            else if (upTrace.hit)
            {
                if (Portal.IsPairedPortalWallContact(
                    upTrace.unityHit,
                    _lastBoxQueryCenter,
                    _lastBoxQueryHalfExtents +
                        Vector3.one * CollisionSkin,
                    _lastBoxQueryRotation,
                    _lastBoxQuerySweepDelta))
                    return false;

                float availableRise = Mathf.Max(
                    0f,
                    Mathf.Min(stepTraceDistance,
                        Vector3.Dot(
                            upTrace.endPosition - startPosition,
                            _movementUp)));
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
            bool upHitPortalWall;
            TrySlideMove(
                raisedPosition,
                startVelocity,
                startFinalVelocity,
                TickInterval,
                halfExtents,
                out upPosition,
                out _,
                out upFinalVelocity,
                out _,
                out upHitPortalWall);
            if (upHitPortalWall)
                return false;

            // StepMove lowers the raised path back onto the walkable surface.
            // A missing/steep hit means Source restores the ordinary path.
            SourceTraceResult downTrace = TracePlayerBBox(
                upPosition,
                upPosition - _movementUp * stepTraceDistance,
                halfExtents);
            // Source checks trace.plane.normal[2] before accepting the
            // stepped path.  An empty trace has a zero/default plane normal,
            // so a missing lower hit must restore the ordinary path instead
            // of leaving the player in the air at the full step distance.
            bool downHitFound = downTrace.hit;
            RaycastHit downHit = downTrace.unityHit;
            float downNormal = downHitFound
                ? Vector3.Dot(downTrace.planeNormal, _movementUp)
                : 0f;
            if (!downHitFound ||
                Portal.IsPairedPortalWallContact(
                    downHit,
                    _lastBoxQueryCenter,
                    _lastBoxQueryHalfExtents +
                        Vector3.one * CollisionSkin,
                    _lastBoxQueryRotation,
                    _lastBoxQuerySweepDelta) ||
                (downNormal < WalkableNormal &&
                 !(downTrace.contactedPortalRamp && downNormal > 0f)))
                return false;

            Vector3 steppedPosition = downTrace.endPosition;

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

        private int OverlapBoxNonAlloc(
            Vector3 center,
            Vector3 halfExtents,
            ref Collider[] results,
            Quaternion orientation,
            int layerMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            while (true)
            {
                int overlapCount = Physics.OverlapBoxNonAlloc(
                    center,
                    halfExtents,
                    results,
                    orientation,
                    layerMask,
                    queryTriggerInteraction);
                if (overlapCount < results.Length)
                    return overlapCount;

                // A truncated overlap list can make a Source startsolid or
                // CanUnduck trace miss the actual blocking entity. Grow and
                // repeat so the result does not depend on nearby collider
                // count or PhysX's unspecified overlap ordering.
                System.Array.Resize(ref results, results.Length * 2);
            }
        }

        private int BoxCastNonAlloc(
            Vector3 center,
            Vector3 halfExtents,
            Vector3 direction,
            Quaternion orientation,
            float distance,
            int layerMask,
            QueryTriggerInteraction queryTriggerInteraction,
            GokzSourceContents sourceContentsMask = PlayerSolidContents)
        {
            _lastBoxQueryCenter = center;
            _lastBoxQueryHalfExtents = halfExtents;
            _lastBoxQueryRotation = orientation;
            _lastBoxQuerySweepDelta = direction.sqrMagnitude > 0.000001f
                ? direction.normalized * distance
                : Vector3.zero;
            _lastSourceContentsMask = sourceContentsMask;

            while (true)
            {
                int hitCount = Physics.BoxCastNonAlloc(
                    center,
                    halfExtents,
                    direction,
                    _moveHits,
                    orientation,
                    distance,
                    layerMask,
                    queryTriggerInteraction);
                if (hitCount < _moveHits.Length)
                {
                    if (queryTriggerInteraction == QueryTriggerInteraction.Ignore)
                        return FilterSourceMovementHits(
                            hitCount,
                            sourceContentsMask);
                    return hitCount;
                }

                // A Source hull trace returns the closest blocking trace. A
                // full Unity NonAlloc buffer can omit the nearest collider, so
                // grow and retry instead of letting buffer capacity change the
                // movement result in dense corners.
                System.Array.Resize(ref _moveHits, _moveHits.Length * 2);
            }
        }

        private SourceTraceResult TracePlayerBBox(
            Vector3 startPosition,
            Vector3 endPosition,
            Vector3 halfExtents,
            GokzSourceContents sourceContentsMask = PlayerSolidContents)
        {
            Vector3 centerOffset = _movementUp * halfExtents.y;
            SourceTraceResult trace = TraceBoxByCenter(
                startPosition + centerOffset,
                endPosition + centerOffset,
                halfExtents,
                sourceContentsMask);
            trace.endPosition -= centerOffset;
            return trace;
        }

        private SourceTraceResult TraceBoxByCenter(
            Vector3 startCenter,
            Vector3 endCenter,
            Vector3 halfExtents,
            GokzSourceContents sourceContentsMask = PlayerSolidContents)
        {
            Vector3 displacement = endCenter - startCenter;
            float distance = displacement.magnitude;
            SourceTraceResult trace = new SourceTraceResult
            {
                fraction = 1f,
                endPosition = endCenter,
                planeType = SourcePlaneType.Z
            };

            trace.startSolid = IsBoxStuckAtCenter(
                startCenter,
                halfExtents,
                sourceContentsMask);
            float castStartFraction = 0f;
            if (trace.startSolid)
            {
                if (!TryFindTraceExitFraction(
                        startCenter,
                        displacement,
                        halfExtents,
                        sourceContentsMask,
                        out trace.fractionLeftSolid))
                {
                    trace.allSolid = true;
                    trace.fraction = 0f;
                    trace.endPosition = startCenter;
                    return trace;
                }

                castStartFraction = Mathf.Min(
                    1f,
                    trace.fractionLeftSolid +
                        SourceDistanceEpsilon / Mathf.Max(distance, SourceDistanceEpsilon));
            }

            // DIST_EPSILON is a trace separation tolerance, not a minimum
            // movement distance. Skipping short sweeps lets repeated low-speed
            // commands advance through a thin BoxCollider until the next trace
            // begins solid and reports allsolid. Only omit a true zero-length
            // sweep (or one below float precision).
            if (distance <= 0.000001f || castStartFraction >= 1f)
            {
                trace.fraction = 1f;
                trace.endPosition = endCenter;
                return trace;
            }

            Vector3 direction = displacement / distance;
            Vector3 castStart = startCenter +
                displacement * castStartFraction;
            float castDistance = distance * (1f - castStartFraction);
            int hitCount = BoxCastNonAlloc(
                castStart,
                GetQueryHalfExtents(halfExtents),
                direction,
                Quaternion.identity,
                castDistance,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore,
                sourceContentsMask);

            if (!TryGetNearestHit(
                    hitCount,
                    out RaycastHit hit,
                    out trace.contactedPortalRamp))
                return trace;

            trace.hit = true;
            trace.unityHit = hit;
            trace.hitCollider = hit.collider;
            trace.planeNormal = TryGetPlanarMeshBlockingNormal(
                    hit.collider,
                    castStart,
                    direction,
                    out Vector3 planarMeshNormal)
                ? planarMeshNormal
                : hit.normal.normalized;
            trace.planeType = GetSourcePlaneType(trace.planeNormal);
            GokzSourceCollisionData collisionData =
                GokzSurfaceProperties.ResolveCollisionData(hit.collider);
            trace.contents = collisionData.contents;
            trace.surface = GokzSurfaceProperties.Resolve(hit.collider);

            // Unity's BoxCast query hull is inset by CollisionSkin on each
            // axis. Convert its hit distance back to the full Source hull.
            // The center travels farther than one skin on oblique impacts:
            // the inset support distance is skin * |normal|_1, projected
            // onto the sweep direction. Subtracting a fixed skin lets the
            // full hull advance into ordinary BoxColliders at shallow angles;
            // the next trace then misclassifies that overlap as allsolid.
            Vector3 hitNormal = trace.planeNormal;
            float normalTravel = Mathf.Abs(Vector3.Dot(hitNormal, direction));
            float skinCorrection = normalTravel > 0.000001f
                ? CollisionSkin * (Mathf.Abs(hitNormal.x) +
                    Mathf.Abs(hitNormal.y) + Mathf.Abs(hitNormal.z)) /
                    normalTravel
                : castDistance;
            float safeDistance = Mathf.Clamp(
                hit.distance - skinCorrection,
                0f,
                castDistance);
            trace.fraction = castStartFraction +
                (1f - castStartFraction) * safeDistance / castDistance;
            trace.endPosition = startCenter +
                displacement * trace.fraction;
            return trace;
        }

        private bool TryFindTraceExitFraction(
            Vector3 startCenter,
            Vector3 displacement,
            Vector3 halfExtents,
            GokzSourceContents sourceContentsMask,
            out float fractionLeftSolid)
        {
            fractionLeftSolid = 0f;
            float previousSolidFraction = 0f;
            const int exitSamples = 32;

            for (int sample = 1; sample <= exitSamples; sample++)
            {
                float fraction = sample / (float)exitSamples;
                Vector3 samplePosition = startCenter +
                    displacement * fraction;
                if (IsBoxStuckAtCenter(
                        samplePosition,
                        halfExtents,
                        sourceContentsMask))
                {
                    previousSolidFraction = fraction;
                    continue;
                }

                float low = previousSolidFraction;
                float high = fraction;
                for (int iteration = 0; iteration < 10; iteration++)
                {
                    float middle = (low + high) * 0.5f;
                    Vector3 middlePosition = startCenter +
                        displacement * middle;
                    if (IsBoxStuckAtCenter(
                            middlePosition,
                            halfExtents,
                            sourceContentsMask))
                        low = middle;
                    else
                        high = middle;
                }

                fractionLeftSolid = high;
                return true;
            }

            return false;
        }

        private SourcePlaneType GetSourcePlaneType(Vector3 unityNormal)
        {
            Vector3 normal = unityNormal.normalized;
            float sourceX = Mathf.Abs(normal.x);
            float sourceY = Mathf.Abs(normal.z);
            float sourceZ = Mathf.Abs(Vector3.Dot(normal, _movementUp));

            // PlaneTypeForNormal in the local Source tree recognizes an axial
            // plane only when its normal component is exactly +/-1. Near-axis
            // mesh normals stay in the PLANE_ANY* range.
            if (sourceX == 1f)
                return SourcePlaneType.X;
            if (sourceY == 1f)
                return SourcePlaneType.Y;
            if (sourceZ == 1f)
                return SourcePlaneType.Z;

            if (sourceZ >= sourceX && sourceZ >= sourceY)
                return SourcePlaneType.AnyZ;
            if (sourceX >= sourceY)
                return SourcePlaneType.AnyX;
            return SourcePlaneType.AnyY;
        }

        private int FilterSourceMovementHits(
            int hitCount,
            GokzSourceContents sourceContentsMask)
        {
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < hitCount; readIndex++)
            {
                RaycastHit hit = _moveHits[readIndex];
                if (!ShouldHitSourceMovementCollider(
                        hit.collider,
                        sourceContentsMask))
                    continue;

                _moveHits[writeIndex++] = hit;
            }

            return writeIndex;
        }

        private bool ShouldHitSourceMovementCollider(
            Collider collider,
            GokzSourceContents sourceContentsMask)
        {
            if (collider == null || !collider.enabled ||
                !collider.gameObject.activeInHierarchy || collider.isTrigger)
                return false;

            Transform playerTransform = _surfer != null
                ? _surfer.MoveData.playerTransform
                : null;
            if (playerTransform != null &&
                (collider.transform == playerTransform ||
                 collider.transform.IsChildOf(playerTransform)))
                return false;

            int playerLayer = _sourceHullCollider != null
                ? _sourceHullCollider.gameObject.layer
                : playerTransform != null
                    ? playerTransform.gameObject.layer
                    : 0;
            if (Physics.GetIgnoreLayerCollision(
                    playerLayer,
                    collider.gameObject.layer))
                return false;

            GokzSourceCollisionData collisionData =
                GokzSurfaceProperties.ResolveCollisionData(collider);
            // util_shared.cpp::StandardFilterRules permits world/static
            // geometry and brush entities directly. Other Source entities
            // are only considered when MONSTER is in the trace mask, even
            // when their contents also intersect MOVEABLE or SOLID.
            if ((sourceContentsMask & GokzSourceContents.Monster) == 0 &&
                collisionData.entityKind ==
                    GokzSourceEntityKind.NonBrushEntity)
                return false;

            GokzSourceCollisionData playerCollisionData =
                _sourceHullCollider != null
                    ? GokzSurfaceProperties.ResolveCollisionData(
                        _sourceHullCollider)
                    : default;
            if (playerTransform != null &&
                (IsSameSourceEntity(
                     collisionData.owner,
                     playerTransform) ||
                 IsSameSourceEntity(
                     playerCollisionData.owner,
                     GetSourceEntityTransform(collider))))
                return false;

            GokzSourceContents teamContents =
                GokzSourceContents.Team1 | GokzSourceContents.Team2;
            GokzSourceContents playerTeamContents =
                playerCollisionData.contents & teamContents;
            GokzSourceContents effectiveContentsMask =
                sourceContentsMask | playerTeamContents;
            // CCSGameMovement::PlayerSolidMask(brushOnly: true) returns
            // MASK_PLAYERSOLID_BRUSHONLY without the querying player's team
            // bits. Normal player and ladder traces use the entity mask.
            if (sourceContentsMask == PlayerSolidBrushContents)
                effectiveContentsMask &= ~teamContents;
            if ((collisionData.contents & effectiveContentsMask) == 0)
                return false;

            GokzSourceContents otherTeamContents =
                collisionData.contents & teamContents;
            if (collisionData.collisionGroup ==
                    GokzSourceCollisionGroup.Player &&
                playerTeamContents != GokzSourceContents.Empty &&
                playerTeamContents == otherTeamContents)
                return false;

            return SourceGameRulesShouldCollide(
                GokzSourceCollisionGroup.PlayerMovement,
                collisionData.collisionGroup);
        }

        private static Transform GetSourceEntityTransform(Collider collider)
        {
            if (collider == null)
                return null;

            GokzSurfaceProperties properties =
                collider.GetComponentInParent<GokzSurfaceProperties>();
            if (properties != null)
                return properties.transform;

            Rigidbody body = collider.attachedRigidbody;
            return body != null ? body.transform : collider.transform;
        }

        private static bool IsSameSourceEntity(
            Transform first,
            Transform second)
        {
            return first != null && second != null &&
                (first == second || first.IsChildOf(second) ||
                 second.IsChildOf(first));
        }

        private static bool SourceGameRulesShouldCollide(
            GokzSourceCollisionGroup first,
            GokzSourceCollisionGroup second)
        {
            // CGameRules sorts groups before applying its pair rules. These
            // are every CS:GO/base-game exclusion that applies when one side
            // is COLLISION_GROUP_PLAYER_MOVEMENT, including CS:GO's weapon
            // and push-away overrides.
            int group0 = Mathf.Min((int)first, (int)second);
            int group1 = Mathf.Max((int)first, (int)second);

            if (group0 == (int)GokzSourceCollisionGroup.PlayerMovement &&
                group1 == (int)GokzSourceCollisionGroup.Weapon)
                return false;

            if ((group0 == (int)GokzSourceCollisionGroup.Player ||
                 group0 == (int)GokzSourceCollisionGroup.PlayerMovement) &&
                group1 == (int)GokzSourceCollisionGroup.PushAway)
                return false;

            if (group0 == (int)GokzSourceCollisionGroup.Projectile &&
                group1 == (int)GokzSourceCollisionGroup.DebrisBlockProjectile)
                return true;

            if (group0 == (int)GokzSourceCollisionGroup.DebrisBlockProjectile ||
                group1 == (int)GokzSourceCollisionGroup.DebrisBlockProjectile)
                return false;

            if (group0 == (int)GokzSourceCollisionGroup.InVehicle ||
                group1 == (int)GokzSourceCollisionGroup.InVehicle)
                return false;

            if (group1 == (int)GokzSourceCollisionGroup.DoorBlocker &&
                group0 != (int)GokzSourceCollisionGroup.Npc)
                return false;

            if (group0 == (int)GokzSourceCollisionGroup.Debris ||
                group0 == (int)GokzSourceCollisionGroup.DebrisTrigger)
                return false;

            if (group0 == (int)GokzSourceCollisionGroup.Dissolving ||
                group1 == (int)GokzSourceCollisionGroup.Dissolving)
                return group0 == (int)GokzSourceCollisionGroup.None;

            if (group0 == (int)GokzSourceCollisionGroup.InteractiveDebris &&
                (group1 == (int)GokzSourceCollisionGroup.PlayerMovement ||
                 group1 == (int)GokzSourceCollisionGroup.Player))
                return false;

            if (group0 == (int)GokzSourceCollisionGroup.BreakableGlass &&
                group1 == (int)GokzSourceCollisionGroup.BreakableGlass)
                return false;

            if (group1 == (int)GokzSourceCollisionGroup.Interactive &&
                group0 != (int)GokzSourceCollisionGroup.None &&
                group0 != (int)GokzSourceCollisionGroup.Debris &&
                group0 != (int)GokzSourceCollisionGroup.InteractiveDebris)
                return false;

            if (group1 == (int)GokzSourceCollisionGroup.Projectile &&
                (group0 == (int)GokzSourceCollisionGroup.Debris ||
                 group0 == (int)GokzSourceCollisionGroup.Weapon ||
                 group0 == (int)GokzSourceCollisionGroup.Projectile))
                return false;

            if (group1 == (int)GokzSourceCollisionGroup.Weapon &&
                (group0 == (int)GokzSourceCollisionGroup.Vehicle ||
                 group0 == (int)GokzSourceCollisionGroup.Player ||
                 group0 == (int)GokzSourceCollisionGroup.Npc ||
                 group0 == (int)GokzSourceCollisionGroup.Weapon))
                return false;

            if (group0 == (int)GokzSourceCollisionGroup.VehicleClip ||
                group1 == (int)GokzSourceCollisionGroup.VehicleClip)
                return group0 == (int)GokzSourceCollisionGroup.Vehicle;

            return true;
        }

        private bool TryGetNearestHit(int hitCount, out RaycastHit nearest)
        {
            return TryGetNearestHit(hitCount, out nearest, out _);
        }

        private bool TryGetNearestHit(
            int hitCount,
            out RaycastHit nearest,
            out bool contactedPortalTransitionRamp)
        {
            nearest = default;
            contactedPortalTransitionRamp = false;
            float nearestDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = _moveHits[i];
                if (candidate.collider == null || candidate.collider == _sourceHullCollider ||
                    !ShouldHitSourceMovementCollider(
                        candidate.collider,
                        _lastSourceContentsMask) ||
                    ShouldIgnorePlanarMeshSweepHit(
                        candidate.collider,
                        _lastBoxQueryCenter,
                        _lastBoxQuerySweepDelta) ||
                    Portal.ShouldIgnoreMovementCollision(
                        candidate,
                        _lastBoxQueryCenter,
                        _lastBoxQueryHalfExtents +
                            Vector3.one * CollisionSkin,
                        _lastBoxQueryRotation,
                        _lastBoxQuerySweepDelta,
                        GetQueryHalfExtents(GetBoxHalfExtents(true)),
                        _movementUp) ||
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

            Transform movingRoot = _surfer != null
                ? _surfer.MoveData.playerTransform
                : null;
            if (Portal.TryGetPortalSpaceCollision(
                _lastBoxQueryCenter,
                _lastBoxQueryHalfExtents,
                GetQueryHalfExtents(GetBoxHalfExtents(true)),
                _lastBoxQueryRotation,
                _movementUp,
                _lastBoxQuerySweepDelta,
                GetCollisionMask(),
                movingRoot,
                out RaycastHit portalHit,
                out bool portalRampContact) &&
                (portalHit.collider == null ||
                 ShouldHitSourceMovementCollider(
                     portalHit.collider,
                     _lastSourceContentsMask)) &&
                portalHit.distance < nearestDistance)
            {
                nearest = portalHit;
                contactedPortalTransitionRamp = portalRampContact;
                return true;
            }

            return found;
        }

        public void Process(IGokzMovementPlayer surfer)
        {
            _surfer = surfer;
            _processStartPosition = surfer.MoveData.playerTransform != null
                ? surfer.MoveData.playerTransform.position
                : Vector3.zero;
            _tickStarted = false;
            _movementBlockedThisTick = false;
            _elapsed += TickInterval;
            _movementTick++;
            // GOKZ's CanUnduck detour compares the landing command to the
            // immediately previous user command. Derive the same one-command
            // window from the recorded landing tick so a blocked movement
            // command cannot leave a stale landing flag set indefinitely.
            _landedThisTick = _landingTick == _movementTick - 1;
            // GOKZ SimpleKZ records the last command containing IN_JUMP in
            // OnPlayerRunCmdPost, not only a newly pressed jump pulse. This
            // matters when a held space key is used to satisfy the three
            // command SimpleKZ perf window after landing.
            if (_surfer.MoveData.wishJump || _surfer.MoveData.jumpHeld)
                _lastJumpRequestTick = _movementTick;
            LastJumpWasPerfect = false;

            // The project can temporarily reverse gravity or disable it in a
            // trigger. The normal state remains the exact GOKZ +Z-up path.
            _movementUp = _surfer.MoveData.reverseGravity < 0f ? Vector3.down : Vector3.up;

            // CPlayerMove::CheckMovingGround runs before ProcessMovement. A
            // trigger_push leaves FL_BASEVELOCITY set for the next command;
            // when the flag is no longer set, Source transfers the remaining
            // base velocity into the player's real velocity and clears it.
            // Keeping this state across ticks is what makes a horizontal push
            // carry out of the trigger instead of existing only inside it.
            ConsumeSourceTriggerBaseVelocity();

            // Sample the ground entity before recategorizing. This is the
            // Unity equivalent of Source reading GetAbsVelocity() from the
            // entity attached during the previous command.
            bool groundAtTickStart = Grounded;
            Vector3 groundVelocityAtTickStart = groundAtTickStart
                ? GetGroundEntityVelocity(_groundHit)
                : Vector3.zero;
            UpdateGroundEntityVelocitySample();

            // The active controller moves the root Transform directly. Keep
            // the BoxCollider used by the sweep queries synchronized before
            // categorizing the next tick's ground state.
            ConfigureSourceHullTransform(_surfer.MoveData.playerTransform);
            Physics.SyncTransforms();
            RefreshSourceWaterLevel();

            // CS:GO's default sv_optimizedmovement path keeps the ground
            // entity from the previous command. It only categorizes here
            // when game code moved the player (or when initializing this
            // Unity adapter); otherwise it merely detaches a player moving
            // upward faster than 250 HU/s.
            if (!_groundStateInitialized && _collisionReady)
            {
                Grounded = CategorizePosition(out _groundHit);
                _groundStateInitialized = true;
                groundAtTickStart = Grounded;
                groundVelocityAtTickStart = Grounded
                    ? GetGroundEntityVelocity(_groundHit)
                    : Vector3.zero;
                UpdateGroundEntityVelocitySample();
            }
            // This is Source's old ground entity state, not the result of the
            // new CategorizePosition trace. Finish() uses it to distinguish a
            // real landing from a player who was already grounded.
            _startGrounded = groundAtTickStart;
            _onLadderThisTick = false;
            _waterJumpProcessedThisTick = false;
            _waterMoveBranchThisTick = false;
            _wasOnLadder = _surfer.MoveData.climbingLadder;
            _surfer.MoveData.climbingLadder = false;
            _walkMovedThisTick = false;

            int buttons = _surfer.MoveData.Buttons;
            bool jumpButtonDown = HasSourceButton(buttons, InputButtons.Jump);
            bool duckButtonDown = HasSourceButton(buttons, InputButtons.Duck);

            // GOKZ removes a newly pressed duck bind when jump is pressed on
            // the ground. Holding duck before the jump remains a real crouch
            // jump; only the tick-perfect jump+duck bind is removed.
            if (_mode != GokzMode.Vanilla && Grounded &&
                jumpButtonDown &&
                !HasSourceButton(_gokzOldButtons, InputButtons.Jump) &&
                duckButtonDown &&
                !HasSourceButton(_gokzOldButtons, InputButtons.Duck))
            {
                buttons &= ~(int)InputButtons.Duck;
                duckButtonDown = false;
            }
            _surfer.MoveData.Buttons = buttons;
            _surfer.MoveData.crouching = duckButtonDown;

            if (!_gokzCommandInitialized)
            {
                _gokzOldVelocity = _surfer.MoveData.Velocity;
                _gokzCommandInitialized = true;
            }
            // GOKZ's FixDisplacementStuck compares the flags observed at the
            // start of the previous command with the flags at this command's
            // start (gI_OldFlags versus GetEntityFlags). Preserve that prior
            // state until the hook has run.
            _duckedAtProcessStart = _isCrouched;
            ReduceMovementTimers();

            // GOKZ's OnPlayerRunCmd hooks run before CCSGameMovement::Duck.
            // CalcPrestrafeVelMod is the first hook, followed by the
            // KZTimer/SimpleKZ water and displacement fixes. Vanilla mode has
            // none of these SourceMod hooks.
            float realPreVelMod = UpdatePrestrafeModifier(Grounded);
            if (_mode != GokzMode.Vanilla &&
                _surfer.MoveData.waterLevel >= GokzWaterLevel.Waist &&
                duckButtonDown &&
                !HasSourceButton(_gokzOldButtons, InputButtons.Duck))
            {
                TryWaterCrouchBoost();
            }

            // A zero-thickness non-convex MeshCollider (Unity Plane) has no
            // interior volume for the Source startsolid/exit trace to model.
            // If PhysX reports the hull straddling that sheet, move it back
            // to a side using the pair's MTD before stuck recovery or movement.
            if (_collisionReady)
                RecoverPlanarMeshOverlaps();

            // Fix a standing hull that was left embedded by the previous
            // unduck before evaluating this command's duck state.
            if (_mode != GokzMode.Vanilla)
                FixDisplacementStuck();

            // gokz-mode-kztimer/simplekz.sp snapshots the post-plugin button
            // command here, before CCSGameMovement::CheckParameters adds
            // IN_RAWDUCK or suppresses the effective IN_DUCK bit.
            _gokzOldButtons = buttons;

            // CCSGameMovement::CheckParameters runs before CheckStuck. It
            // saves the raw duck bit, applies the CS duck-spam gate, then
            // leaves Duck() to update the continuous hull/eye state only
            // after the stuck check and ground categorization.
            PrepareDuckInput();

            // These are the values GOKZ records at the end of
            // OnPlayerRunCmd, before Source begins this movement command.
            _gokzOldVelocity = _surfer.MoveData.Velocity;
            _wasCrouching = _rawDuckHeld;
            _wasPhysicallyCrouched = _duckedAtProcessStart;

            // CGameMovement::CheckStuck runs after the GOKZ OnPlayerRunCmd
            // hooks and before CategorizePosition/Duck. It does not zero the
            // player's velocity when it cannot free the hull; it simply exits
            // this movement command. Preserve that state and cadence here.
            if (TrySourceStuckRecovery())
            {
                _movementBlockedThisTick = true;
                _tickStarted = true;
                return;
            }

            if (_collisionReady &&
                (_wasOnLadder || _surfer.MoveData.gameCodeMovedPlayer))
            {
                Grounded = CategorizePosition(out _groundHit);
                _surfer.MoveData.gameCodeMovedPlayer = false;
            }
            else if (_collisionReady && Grounded &&
                Vector3.Dot(_surfer.MoveData.Velocity, _movementUp) >
                    250f * UnitScale)
            {
                // CGameMovement::PlayerMove's optimized path clears the old
                // ground entity when mv->m_vecVelocity.z exceeds 250 HU/s.
                // The separate 140 HU/s test belongs to CategorizePosition.
                Grounded = false;
                _groundHit = default;
            }
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

            // Source updates the CS:GO continuous duck amount before movement.
            // The physical hull is changed by FinishDuck/FinishUnDuck, while
            // the fractional amount is used for the eye and speed crop.
            UpdateDuckState();

            // CGameMovement::PlayerMove attempts LadderMove before it dispatches
            // to FullWalkMove. Water and water-jump handling are inside
            // FullWalkMove, so they cannot take priority over an active ladder.
            if (TryLadderMove())
            {
                // LadderMove is a complete movement branch in Source.
            }
            else
            {
                // FullWalkMove applies its first gravity half-step unless
                // CheckWater reports water above the feet, then processes an
                // active water-jump before ordinary swimming or air movement.
                if (_surfer.MoveData.waterLevel <= GokzWaterLevel.Feet)
                    StartGravity();

                if (_waterJumpTimeMilliseconds > 0f)
                {
                    _waterJumpProcessedThisTick = true;
                    WaterJump();
                }
                else if (_surfer.MoveData.waterLevel >= GokzWaterLevel.Waist)
                {
                    _waterMoveBranchThisTick = true;
                    TryWaterJump();
                    // Source checks the active water-jump timer before
                    // CheckWaterJump. A water-jump started by this call still
                    // goes through CheckJumpButton and WaterMove this command;
                    // WaterJump() takes over on the next command.
                    CheckJumpButton();
                    WaterMove(realPreVelMod);
                }
                else
                {
                    CheckJumpButton();
                    if (Grounded)
                        GroundMove(realPreVelMod);
                    else
                        AirMove(realPreVelMod);
                }
            }

            CheckOrigin();
            _tickStarted = true;
        }

        public void Finish(IGokzMovementPlayer surfer)
        {
            if (!_tickStarted || _surfer != surfer)
                return;

            if (_movementBlockedThisTick)
            {
                // CGameMovement::ProcessMovement still calls FinishMove after
                // PlayerMove returns from CheckStuck, so m_nOldButtons takes
                // this command's jump bit even when no displacement occurs.
                _surfer.MoveData.OldButtons = _surfer.MoveData.Buttons;
                _surfer.MoveData.ViewYawDelta = 0f;
                _movementBlockedThisTick = false;
                _tickStarted = false;
                _surfer = null;
                return;
            }

            Physics.SyncTransforms();
            RefreshSourceWaterLevel();
            RaycastHit finalGroundHit = _groundHit;
            // FullLadderMove and FullWalkMove's active WaterJump early return
            // do not call CategorizePosition at their exit. Ordinary walk,
            // air, and swimming branches do.
            bool categorizeAtEnd = !_onLadderThisTick &&
                !_waterJumpProcessedThisTick;
            bool finalGrounded = categorizeAtEnd
                ? CategorizePosition(out finalGroundHit)
                : Grounded;
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
                // GOKZ's SlopeFix passes the complete landing velocity to
                // ClipVelocity. The downward component is what is converted
                // into downhill horizontal speed; projecting it out first
                // silently removes the actual slope boost.
                Vector3 landingVelocity = incomingVelocity;
                ApplySlopeFix(
                    ref landingVelocity,
                    _surfer.MoveData.playerTransform.position,
                    landed);
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(
                    landingVelocity,
                    _movementUp);
                _surfer.MoveData.Velocity = horizontalVelocity +
                    _movementUp * Vector3.Dot(landingVelocity, _movementUp);
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
            if (categorizeAtEnd && !_waterMoveBranchThisTick)
            {
                // FullWalkMove's non-swimming branch checks velocity after
                // movement and CategorizePosition, before FinishGravity.
                CheckVelocity(ref _surfer.MoveData.Velocity);
            }
            if (!_movementBlockedThisTick &&
                !_waterMoveBranchThisTick &&
                !_onLadderThisTick &&
                !_waterJumpProcessedThisTick)
                FinishGravity();

            // Source explicitly clears the gravity component after the final
            // ground categorization. This is why a player that walks off an
            // edge starts falling, while a player that remains on a floor has
            // no accumulating downward velocity.
            if (Grounded && !_onLadderThisTick &&
                !_waterJumpProcessedThisTick)
                RemoveVerticalVelocity(ref _surfer.MoveData.Velocity);

            Vector3 baseVelocityBeforePortal = _baseVelocity;
            Vector3 portalVelocity = _surfer.MoveData.Velocity;
            Vector3 portalPosition =
                _surfer.MoveData.playerTransform.position;
            PortablePlayer portablePlayer =
                _surfer.MoveData.playerTransform.GetComponent<PortablePlayer>();
            if (portablePlayer != null)
            {
                Vector3 movementHullHalfExtents =
                    GetBoxHalfExtents(_isCrouched);
                portablePlayer.SetPortalEnvironment(
                    Portal.FindPlayerPortalEnvironment(
                        _processStartPosition,
                        portalPosition,
                        movementHullHalfExtents,
                        _movementUp,
                        portablePlayer.PortalEnvironment));
            }
            bool wasGroundedBeforePortal = Grounded;
            if (Portal.TryHandlePlayerMovement(
                portablePlayer,
                _processStartPosition,
                ref portalPosition,
                GetBoxHalfExtents(_isCrouched),
                GetBoxHalfExtents(true),
                _movementUp,
                ref portalVelocity,
                wasGroundedBeforePortal,
                GetGravityAcceleration(),
                TickInterval,
                out Quaternion portalRotation,
                out bool forcePortalDuck,
                out Portal exitPortal,
                out Vector3 transformedOldEyePosition))
            {
                if (forcePortalDuck)
                    ForcePortalCrouch();

                _baseVelocity = portalRotation * baseVelocityBeforePortal;
                _sourceTriggerBaseVelocity =
                    portalRotation * _sourceTriggerBaseVelocity;
                _surfer.MoveData.Velocity = portalVelocity;
                _surfer.MoveData.Velocity.x = Mathf.Clamp(
                    _surfer.MoveData.Velocity.x, -MaxVelocity, MaxVelocity);
                _surfer.MoveData.Velocity.y = Mathf.Clamp(
                    _surfer.MoveData.Velocity.y, -MaxVelocity, MaxVelocity);
                _surfer.MoveData.Velocity.z = Mathf.Clamp(
                    _surfer.MoveData.Velocity.z, -MaxVelocity, MaxVelocity);
                _movementVelocity = _surfer.MoveData.Velocity;
                _surfer.MoveData.playerTransform.position = portalPosition;
                _surfer.MoveData.Origin = portalPosition;

                // CPortal_Player::ApplyPortalTeleportation drops the entry
                // ground entity after movement and carries the transformed
                // base velocity into the next command.
                Grounded = false;
                _groundHit = default;
                _groundNormal = _movementUp;
                _surfer.MoveData.grounded = false;
                _surfer.MoveData.groundTransform = null;
                _surfer.MoveData.SurfaceFriction = 1f;
                _hasGroundTransformSample = false;
                _sampledGroundTransform = null;
                _sampledGroundPosition = Vector3.zero;
                _sampledGroundVelocity = Vector3.zero;
                Physics.SyncTransforms();

                // Source checks the new portal collision environment in the
                // same command and moves a start-solid player to the nearest
                // passable point. This is especially important for a CS:GO
                // axis-aligned hull exiting near a portal edge.
                if (TryRecoverPortalExit(
                    exitPortal,
                    ref portalPosition,
                    GetBoxHalfExtents(_isCrouched)))
                {
                    _surfer.MoveData.playerTransform.position = portalPosition;
                    _surfer.MoveData.Origin = portalPosition;
                    Physics.SyncTransforms();
                }

                portablePlayer.RecordPortalTransition(exitPortal);
                portablePlayer.QueuePortalEyeOffset(
                    transformedOldEyePosition);
            }

            // Source calls CMoveHelperServer::ProcessImpacts after
            // ProcessMovement has finished. trigger_push therefore writes
            // BaseVelocity (or applies its one-shot impulse) for the next
            // movement command; it is not a pre-movement water/gravity
            // modifier. Keep this post-move ordering so an upward push can
            // detach a grounded player and the next tick can jump normally.
            UpdateSourceMovementVolumes(Grounded);

            _surfer.MoveData.grounded = Grounded;
            _surfer.MoveData.groundTransform = Grounded ? _groundHit.transform : null;
            // GOKZ FixWaterBoost compares the raw previous IN_DUCK bit from
            // OnPlayerRunCmd, not CCSGameMovement's possibly anti-spam-
            // suppressed effective duck flag.
            _wasCrouching = _rawDuckHeld;
            _wasJumping = _surfer.MoveData.wishJump;
            _wasJumpHeld = _surfer.MoveData.jumpHeld;
            _surfer.MoveData.OldButtons = _surfer.MoveData.Buttons;
            // Keep the previous command's starting flag for the next
            // OnPlayerRunCmd-equivalent pass. This must not be the final flag
            // after Duck(), otherwise an unduck transition can never be seen
            // by FixDisplacementStuck.
            _wasPhysicallyCrouched = _duckedAtProcessStart;
            _previousWalkMoved = _walkMovedThisTick;

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
            _startGrounded = false;
            _tickStarted = false;
            _wasCrouching = false;
            _wasJumping = false;
            _wasJumpHeld = false;
            _gokzOldButtons = 0;
            _previousWalkMoved = false;
            _walkMovedThisTick = false;
            _hasWalkMovedSinceLastJump = false;
            _onLadderThisTick = false;
            _wasOnLadder = false;
            _ladderJumpBlockedUntil = 0f;
            _waterJumpTimeMilliseconds = 0f;
            _waterJumpVelocity = Vector3.zero;
            _waterJumpProcessedThisTick = false;
            _waterMoveBranchThisTick = false;
            _lastGroundSurfaceMaxSpeedFactor = 1f;
            _stuckOffsetIndex = 0;
            _lastStuckCheckTime = -0.05f;
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
            _rawDuckHeld = false;
            _duckInputInitialized = false;
            _effectiveDuckButtonHeld = false;
            _lastDuckTime = float.NegativeInfinity;
            _wasPhysicallyCrouched = false;
            _duckedAtProcessStart = false;
            _gokzOldVelocity = Vector3.zero;
            _gokzCommandInitialized = false;
            _movementTick = 0;
            _landingTick = -1;
            _lastJumpRequestTick = int.MinValue;
            _hullInitialized = false;
            _groundStateInitialized = false;
            _movementVelocity = Vector3.zero;
            _baseVelocity = Vector3.zero;
            _sourceGravityScale = 1f;
            _sourceTriggerBaseVelocity = Vector3.zero;
            _processStartPosition = Vector3.zero;
            _sourceTriggerBaseVelocityFlag = false;
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

        private void ConsumeSourceTriggerBaseVelocity()
        {
            if (_sourceTriggerBaseVelocityFlag)
            {
                // Source removes FL_BASEVELOCITY at the beginning of the
                // command, but keeps the vector available to movement. A
                // touching trigger will replace it after this command.
                _sourceTriggerBaseVelocityFlag = false;
                return;
            }

            if (_sourceTriggerBaseVelocity.sqrMagnitude <= 0.00000001f)
                return;

            // CPlayerMove::CheckMovingGround applies momentum one command
            // after the base-velocity flag disappears, including Source's
            // small half-frame factor, then clears the base velocity.
            _surfer.MoveData.Velocity += _sourceTriggerBaseVelocity *
                (1f + TickInterval * 0.5f);
            _sourceTriggerBaseVelocity = Vector3.zero;
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
            // Source calls CheckJumpButton whenever IN_JUMP is held. With
            // autobhop disabled, m_nOldButtons prevents a held key from
            // pogoing after landing; it does not suppress the first held
            // command. wishJump also represents LUO's one-command wheel jump.
            bool jumpRequested = _surfer.MoveData.wishJump ||
                _surfer.MoveData.jumpHeld;

            // Source treats IN_JUMP in waist-deep water as swimming, not as a
            // normal ground jump. The button can remain held here; unlike a
            // ground jump this branch is intentionally not edge-triggered.
            if (_surfer.MoveData.waterLevel >= GokzWaterLevel.Waist)
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
            if (!_settings.autoBhop && HasSourceButton(
                _surfer.MoveData.OldButtons,
                InputButtons.Jump))
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

            // CGameMovement::CategorizePosition starts by calling CheckWater.
            // Duck completion can recategorize within PlayerMove, so water
            // must follow the current origin and hull at each such call.
            RefreshSourceWaterLevel();

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
            Vector3 origin = _surfer.MoveData.playerTransform.position;
            bool moveToGroundTraceEnd = Grounded &&
                _surfer.MoveData.waterLevel < GokzWaterLevel.Eyes;
            // CGameMovement extends the ground trace by step size when the
            // player was already walking on ground. This is what keeps a
            // player attached while descending ramps and crossing small
            // displacement seams; a two-HU probe alone makes Unity produce
            // the visible one-tick airborne gap.
            float probeDistance = 2f * UnitScale + CollisionSkin +
                (Grounded && _surfer.MoveData.waterLevel < GokzWaterLevel.Eyes
                    ? StepHeight
                    : 0f);
            SourceTraceResult groundTrace = TracePlayerBBox(
                origin,
                origin - _movementUp * probeDistance,
                halfExtents);
            bool found = IsStandableTrace(groundTrace);
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
                    Vector3 offset = quadrantOffsets[quadrant];
                    SourceTraceResult quadrantTrace = TracePlayerBBox(
                        origin + offset,
                        origin + offset - _movementUp * probeDistance,
                        quadrantHalfExtents);
                    if (!IsStandableTrace(quadrantTrace))
                        continue;

                    // TracePlayerBBoxForGround returns on the first
                    // standable quadrant in Source's fixed order; it does
                    // not choose the numerically nearest quadrant hit.
                    groundTrace = quadrantTrace;
                    groundTrace.endPosition -= offset;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                // Source CategorizePosition moves an already-grounded walker
                // to a partial downward trace endpoint, but not when the
                // trace began in solid or ran the full clear distance.
                hit = groundTrace.unityHit;
                if (moveToGroundTraceEnd &&
                    !groundTrace.startSolid &&
                    groundTrace.fraction > 0f &&
                    groundTrace.fraction < 1f)
                {
                    float snapDistance = Mathf.Abs(Vector3.Dot(
                        origin - groundTrace.endPosition,
                        _movementUp));
                    if (snapDistance > SourceDistanceEpsilon)
                    {
                        Vector3 snappedPosition = groundTrace.endPosition;
                        _surfer.MoveData.playerTransform.position =
                            snappedPosition;
                        _surfer.MoveData.Origin = snappedPosition;
                        Physics.SyncTransforms();
                    }
                }

                return true;
            }

            // Do not carry the previous grounded state forward. Source's
            // CategorizePosition is a real hull trace every tick; retaining
            // the old flag makes a player float when walking off an edge.
            hit = default;
            return false;
        }

        private bool IsStandableTrace(SourceTraceResult trace)
        {
            if (!trace.hit || trace.startSolid || trace.allSolid)
                return false;

            // Source first accepts the nearest blocking trace, then decides
            // whether that plane is standable. Filtering steep hits before
            // selecting the nearest one can incorrectly see through a nearer
            // wall or steep face to a farther floor.
            float groundNormal = Vector3.Dot(
                trace.planeNormal,
                _movementUp);
            return groundNormal >= WalkableNormal ||
                (trace.contactedPortalRamp && groundNormal > 0f);
        }

        private void PrepareDuckInput()
        {
            if (!_collisionReady)
                return;

            int buttons = _surfer.MoveData.Buttons;
            bool rawDuckHeld = HasSourceButton(buttons, InputButtons.Duck);
            bool oldRawDuckHeld = HasSourceButton(
                _surfer.MoveData.OldButtons,
                InputButtons.RawDuck);
            if (!_duckInputInitialized)
            {
                _rawDuckHeld = false;
                _duckInputInitialized = true;
            }

            bool rawDuckChanged = rawDuckHeld != oldRawDuckHeld;
            bool rawDuckReleased = oldRawDuckHeld && !rawDuckHeld;
            if (_mode == GokzMode.SimpleKz)
            {
                if (!_ducking && _duckSpeed < DuckSpeedNormal)
                    _duckSpeed = DuckSpeedNormal;
                else if (_duckSpeed < DuckSpeedMinimum)
                    _duckSpeed = DuckSpeedMinimum;
            }
            else if (_mode == GokzMode.KzTimer && rawDuckReleased)
            {
                // GOKZ ReduceDuckSlowdown runs in OnPlayerRunCmd before the
                // CS:GO CheckParameters edge penalty is applied.
                _duckSpeed = DuckSpeedNormal;
            }

            if (rawDuckChanged)
                _duckSpeed = Mathf.Max(0f, _duckSpeed - DuckSpamPenalty);

            // CheckParameters calls DuckingEnabled before CCSGameMovement::Duck
            // advances the per-tick recovery curve.
            _effectiveDuckButtonHeld = rawDuckHeld && DuckingEnabled();
            if (rawDuckHeld)
                buttons |= (int)InputButtons.RawDuck;
            else
                buttons &= ~(int)InputButtons.RawDuck;
            if (_effectiveDuckButtonHeld)
                buttons |= (int)InputButtons.Duck;
            else
                buttons &= ~(int)InputButtons.Duck;
            _surfer.MoveData.Buttons = buttons;
            _surfer.MoveData.crouching = _effectiveDuckButtonHeld;
            _rawDuckHeld = rawDuckHeld;
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
                _duckInputInitialized = true;
                _lastDuckTime = float.NegativeInfinity;
            }

            // The active gravity direction changes which side of the feet the
            // Source hull occupies. Source itself is Z-up; this is the only
            // adapter needed for the project's reverse-gravity extension.
            if ((_colliderUp - _movementUp).sqrMagnitude > 0.000001f)
                UpdateBoxCollider(_isCrouched);

            _duckSpeed = Mathf.MoveTowards(
                _duckSpeed,
                DuckSpeedNormal,
                3f * TickInterval);

            // This is the CS:GO recovery rule from CCSGameMovement::Duck:
            // only a fully transitioning or fully settled player gets the
            // distance-based 6.0 recovery.
            if (_duckSpeed >= DuckSpeedNormal)
            {
                _lastFullDuckSpeedPosition =
                    _surfer.MoveData.playerTransform.position;
            }
            else if (_duckAmount <= 0f || _duckAmount >= 1f)
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
            }

            bool playerTouchingGround = Grounded;
            bool onLadder = _wasOnLadder;
            bool inAir = !playerTouchingGround && !onLadder;

            // CheckParameters can suppress the effective IN_DUCK while raw
            // input remains held. GroundMove still sees _ducking or the
            // physical hull, just as CCSGameMovement does.
            bool duckButtonHeld = _effectiveDuckButtonHeld;

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

                    if (_duckAmount <= 0f || inAir)
                        FinishUnDuckState();
                    else
                    {
                        SetDuckedEyeOffset(_duckAmount);
                        // CCSGameMovement removes FL_DUCKING only after the
                        // eye fraction reaches 0.75. Until then the collision
                        // hull remains the 54 HU duck hull even though the eye
                        // has started moving upward.
                        if (_duckAmount <= 0.75f && _isCrouched)
                        {
                            _isCrouched = false;
                            UpdateBoxCollider(false);
                        }
                    }
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
            _lastDuckTime = _elapsed;
            SetDuckedEyeOffset(1f);
            UpdateBoxCollider(true);
            FixPlayerCrouchStuck();

            Grounded = CategorizePosition(out RaycastHit duckGroundHit);
            _groundHit = Grounded ? duckGroundHit : default;
            _groundNormal = Grounded ? duckGroundHit.normal : _movementUp;
            _surfer.MoveData.SurfaceFriction = Grounded
                ? GetSurfaceFriction(duckGroundHit)
                : 1f;
            _surfer.MoveData.SurfaceMaxSpeedFactor = Grounded
                ? (_lastGroundSurfaceMaxSpeedFactor =
                    GetSurfaceMaxSpeedFactor(duckGroundHit))
                : _lastGroundSurfaceMaxSpeedFactor;
            _surfer.MoveData.SurfaceJumpFactor = Grounded
                ? GetSurfaceJumpFactor(duckGroundHit)
                : 1f;
            _surfer.MoveData.grounded = Grounded;
            _surfer.MoveData.groundTransform =
                Grounded ? duckGroundHit.transform : null;
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
            SetDuckedEyeOffset(0f);
            UpdateBoxCollider(false);

            Grounded = CategorizePosition(out RaycastHit unduckGroundHit);
            _groundHit = Grounded ? unduckGroundHit : default;
            _groundNormal = Grounded ? unduckGroundHit.normal : _movementUp;
            _surfer.MoveData.SurfaceFriction = Grounded
                ? GetSurfaceFriction(unduckGroundHit)
                : 1f;
            _surfer.MoveData.SurfaceMaxSpeedFactor = Grounded
                ? (_lastGroundSurfaceMaxSpeedFactor =
                    GetSurfaceMaxSpeedFactor(unduckGroundHit))
                : _lastGroundSurfaceMaxSpeedFactor;
            _surfer.MoveData.SurfaceJumpFactor = Grounded
                ? GetSurfaceJumpFactor(unduckGroundHit)
                : 1f;
            _surfer.MoveData.grounded = Grounded;
            _surfer.MoveData.groundTransform =
                Grounded ? unduckGroundHit.transform : null;
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

        private void ForcePortalCrouch()
        {
            if (_isCrouched)
                return;

            _duckAmount = 1f;
            _ducking = false;
            _isCrouched = true;
            _lastDuckTime = _elapsed;
            SetDuckedEyeOffset(1f);
            UpdateBoxCollider(true);
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

            Vector3 halfExtents = GetBoxHalfExtents(false);
            if ((end - start).sqrMagnitude >
                CollisionSkin * CollisionSkin)
            {
                SourceTraceResult trace = TracePlayerBBox(
                    start,
                    end,
                    halfExtents);
                if (trace.hit || trace.startSolid || trace.allSolid)
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
            if (CanFitHullAt(originalPosition, DuckHullHeight))
                return;

            for (int i = 0; i < 36; i++)
            {
                _surfer.MoveData.playerTransform.position +=
                    _movementUp * UnitScale;
                _surfer.MoveData.Origin =
                    _surfer.MoveData.playerTransform.position;
                Physics.SyncTransforms();
                if (CanFitHullAt(
                    _surfer.MoveData.playerTransform.position,
                    DuckHullHeight))
                    return;
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
            // GOKZ FixDisplacementStuck does not move the origin or replace
            // the hull. It restores the previous command velocity and sets
            // m_bDucking; CCSGameMovement::Duck resolves the state through its
            // normal CanUnduck/FinishDuck branches later this command.
            if (!_wasPhysicallyCrouched || _isCrouched ||
                _surfer == null || _surfer.MoveData.playerTransform == null)
                return;

            if (CanFitHullAt(
                _surfer.MoveData.playerTransform.position,
                PlayerHullHeight))
                return;

            _surfer.MoveData.Velocity = _gokzOldVelocity;
            _ducking = true;
        }

        private bool CanFitHullAt(Vector3 basePosition, float height)
        {
            Vector3 center = basePosition + _movementUp * (height * 0.5f);
            Vector3 hullHalfExtents = new Vector3(
                HullRadius, height * 0.5f, HullRadius);
            int count = OverlapBoxNonAlloc(
                center,
                hullHalfExtents,
                ref _hullOverlaps,
                Quaternion.identity,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider collider = _hullOverlaps[i];
                if (collider == null || collider == _sourceHullCollider ||
                    !ShouldHitSourceMovementCollider(
                        collider,
                        PlayerSolidContents) ||
                    Portal.ShouldIgnoreMovementCollision(
                        collider, center, hullHalfExtents, Quaternion.identity) ||
                    collider.transform.IsChildOf(_surfer.MoveData.playerTransform))
                    continue;
                if (ComputeHullPenetration(
                        collider,
                        basePosition,
                        hullHalfExtents,
                        out _,
                        out float penetration) &&
                    penetration > SourceDistanceEpsilon)
                    return false;
            }

            return true;
        }

        private bool IsHullStuckAt(Vector3 basePosition, Vector3 halfExtents)
        {
            if (_surfer == null || _surfer.MoveData.playerTransform == null)
                return false;

            Vector3 center = basePosition + _movementUp * halfExtents.y;
            return IsBoxStuckAtCenter(
                center,
                halfExtents,
                PlayerSolidContents);
        }

        private bool IsBoxStuckAtCenter(
            Vector3 center,
            Vector3 halfExtents,
            GokzSourceContents sourceContentsMask)
        {
            if (_surfer == null || _surfer.MoveData.playerTransform == null)
                return false;

            int count = OverlapBoxNonAlloc(
                center,
                halfExtents,
                ref _hullOverlaps,
                Quaternion.identity,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = _hullOverlaps[i];
                if (collider == null || collider == _sourceHullCollider ||
                    !ShouldHitSourceMovementCollider(
                        collider,
                        sourceContentsMask) ||
                    Portal.ShouldIgnoreMovementCollision(
                        collider,
                        center,
                        halfExtents,
                        Quaternion.identity) ||
                    Portal.ShouldIgnoreStuckRecoveryCollision(
                        collider,
                        center,
                        halfExtents,
                        Quaternion.identity) ||
                    collider.transform.IsChildOf(
                        _surfer.MoveData.playerTransform))
                    continue;

                // Physics.OverlapBox also reports a collider that merely
                // touches the query box (including Unity's contact offset).
                // Source's allsolid/CheckStuck path is only for a hull that
                // is actually penetrating solid geometry. Treating ordinary
                // wall contact as stuck zeroes the clipped velocity every
                // command, so the player can only escape by changing hull
                // size (duck) or teleporting (reset).
                Vector3 basePosition = center -
                    _movementUp * halfExtents.y;
                if (ComputeHullPenetration(
                        collider,
                        basePosition,
                        halfExtents,
                        out _,
                        out float penetration) &&
                    penetration > SourceDistanceEpsilon)
                    return true;
            }

            return false;
        }

        private bool ComputeHullPenetration(
            Collider other,
            Vector3 basePosition,
            Vector3 halfExtents,
            out Vector3 direction,
            out float penetration)
        {
            direction = Vector3.zero;
            penetration = 0f;
            if (_sourceHullCollider == null || other == null ||
                _sourceHullTransform == null)
                return false;

            Vector3 originalSize = _sourceHullCollider.size;
            Vector3 originalCenter = _sourceHullCollider.center;
            Vector3 scale = _sourceHullTransform.lossyScale;
            if (Mathf.Abs(scale.x) < 0.000001f ||
                Mathf.Abs(scale.y) < 0.000001f ||
                Mathf.Abs(scale.z) < 0.000001f)
                return false;

            Vector3 worldCenter = _movementUp * halfExtents.y;
            try
            {
                // ComputePenetration uses the BoxCollider's own dimensions,
                // so temporarily give it the exact queried Source hull. This
                // also makes stand-hull tests correct while the live player
                // collider is crouched.
                _sourceHullCollider.size = new Vector3(
                    2f * halfExtents.x / Mathf.Abs(scale.x),
                    2f * halfExtents.y / Mathf.Abs(scale.y),
                    2f * halfExtents.z / Mathf.Abs(scale.z));
                _sourceHullCollider.center = new Vector3(
                    worldCenter.x / scale.x,
                    worldCenter.y / scale.y,
                    worldCenter.z / scale.z);
                return Physics.ComputePenetration(
                    _sourceHullCollider,
                    basePosition,
                    Quaternion.identity,
                    other,
                    other.transform.position,
                    other.transform.rotation,
                    out direction,
                    out penetration);
            }
            finally
            {
                _sourceHullCollider.size = originalSize;
                _sourceHullCollider.center = originalCenter;
            }
        }

        private bool RecoverPlanarMeshOverlaps()
        {
            if (_surfer == null || _surfer.MoveData.playerTransform == null ||
                _sourceHullCollider == null)
                return false;

            Transform playerTransform = _surfer.MoveData.playerTransform;
            Vector3 halfExtents = GetBoxHalfExtents(_isCrouched);
            bool recoveredAny = false;
            const int maxRecoveryPasses = 8;

            for (int pass = 0; pass < maxRecoveryPasses; pass++)
            {
                Vector3 basePosition = playerTransform.position;
                Vector3 center = basePosition + _movementUp * halfExtents.y;
                int count = OverlapBoxNonAlloc(
                    center,
                    halfExtents,
                    ref _hullOverlaps,
                    Quaternion.identity,
                    GetCollisionMask(),
                    QueryTriggerInteraction.Ignore);
                bool recoveredThisPass = false;

                for (int i = 0; i < count; i++)
                {
                    Collider collider = _hullOverlaps[i];
                    if (!(collider is MeshCollider meshCollider) ||
                        !IsZeroThicknessMesh(meshCollider) ||
                        !ShouldHitSourceMovementCollider(
                            collider,
                            PlayerSolidContents) ||
                        Portal.ShouldIgnoreMovementCollision(
                            collider,
                            center,
                            halfExtents,
                            Quaternion.identity) ||
                        collider.transform.IsChildOf(playerTransform))
                        continue;

                    if (!ComputeHullPenetration(
                            collider,
                            basePosition,
                            halfExtents,
                            out Vector3 separationDirection,
                            out float penetration) ||
                        penetration <= SourceDistanceEpsilon ||
                        separationDirection.sqrMagnitude <=
                            SourcePlaneEpsilon * SourcePlaneEpsilon)
                        continue;

                    basePosition += separationDirection.normalized *
                        (penetration + SourceDistanceEpsilon);
                    playerTransform.position = basePosition;
                    _surfer.MoveData.Origin = basePosition;
                    Physics.SyncTransforms();
                    recoveredAny = true;
                    recoveredThisPass = true;
                    break;
                }

                if (!recoveredThisPass)
                    break;
            }

            return recoveredAny;
        }

        private static bool IsZeroThicknessMesh(MeshCollider meshCollider)
        {
            if (meshCollider == null || meshCollider.convex ||
                meshCollider.sharedMesh == null)
                return false;

            Vector3 size = meshCollider.sharedMesh.bounds.size;
            float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            return largest > 0f && smallest <=
                Mathf.Max(0.00001f, largest * 0.00001f);
        }

        private static bool ShouldIgnorePlanarMeshSweepHit(
            Collider collider,
            Vector3 sweepStart,
            Vector3 sweepDelta)
        {
            if (!(collider is MeshCollider meshCollider) ||
                !TryGetZeroThicknessMeshPlane(
                    meshCollider,
                    out Vector3 planePoint,
                    out Vector3 planeNormal))
                return false;

            if (sweepDelta.sqrMagnitude <= 0.000000000001f)
                return true;

            Vector3 direction = sweepDelta.normalized;
            float side = Vector3.Dot(sweepStart - planePoint, planeNormal);
            float directionAcrossPlane = Vector3.Dot(direction, planeNormal);
            Vector3 blockingNormal;

            if (Mathf.Abs(side) > SourceDistanceEpsilon)
            {
                blockingNormal = side > 0f ? planeNormal : -planeNormal;
            }
            else if (Mathf.Abs(directionAcrossPlane) > 0.00001f)
            {
                // If the start center is on the sheet, orient the blocking
                // normal against the crossing direction so either side works.
                blockingNormal = directionAcrossPlane > 0f
                    ? -planeNormal
                    : planeNormal;
            }
            else
            {
                // A sweep parallel to the sheet is contact, not penetration.
                return true;
            }

            return Vector3.Dot(direction, blockingNormal) >= -0.00001f;
        }

        private static bool TryGetPlanarMeshBlockingNormal(
            Collider collider,
            Vector3 sweepStart,
            Vector3 sweepDelta,
            out Vector3 blockingNormal)
        {
            blockingNormal = Vector3.zero;
            if (!(collider is MeshCollider meshCollider) ||
                !TryGetZeroThicknessMeshPlane(
                    meshCollider,
                    out Vector3 planePoint,
                    out Vector3 planeNormal) ||
                sweepDelta.sqrMagnitude <= 0.000000000001f)
                return false;

            Vector3 direction = sweepDelta.normalized;
            float side = Vector3.Dot(sweepStart - planePoint, planeNormal);
            float directionAcrossPlane = Vector3.Dot(direction, planeNormal);
            if (Mathf.Abs(side) > SourceDistanceEpsilon)
            {
                blockingNormal = side > 0f ? planeNormal : -planeNormal;
            }
            else if (Mathf.Abs(directionAcrossPlane) > 0.00001f)
            {
                blockingNormal = directionAcrossPlane > 0f
                    ? -planeNormal
                    : planeNormal;
            }
            else
            {
                return false;
            }

            return Vector3.Dot(direction, blockingNormal) < -0.00001f;
        }

        private static bool TryGetZeroThicknessMeshPlane(
            MeshCollider meshCollider,
            out Vector3 worldPoint,
            out Vector3 worldNormal)
        {
            worldPoint = Vector3.zero;
            worldNormal = Vector3.zero;
            if (!IsZeroThicknessMesh(meshCollider))
                return false;

            Bounds bounds = meshCollider.sharedMesh.bounds;
            Vector3 size = bounds.size;
            Vector3 localNormal;
            if (size.x <= size.y && size.x <= size.z)
                localNormal = Vector3.right;
            else if (size.y <= size.z)
                localNormal = Vector3.up;
            else
                localNormal = Vector3.forward;

            worldNormal = meshCollider.transform.worldToLocalMatrix
                .transpose.MultiplyVector(localNormal).normalized;
            worldPoint = meshCollider.transform.TransformPoint(bounds.center);
            return worldNormal.sqrMagnitude > 0.999f;
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
            // CGameMovement::LadderDistance is 2 HU both when attaching and
            // while following the previous ladder normal. A wider reattach
            // probe invents ladder contact through nearby walls.
            float probeDistance = 2f * UnitScale;
            Vector3 ladderOrigin =
                _surfer.MoveData.playerTransform.position;
            SourceTraceResult ladderTrace = TracePlayerBBox(
                ladderOrigin,
                ladderOrigin + probeDirection * probeDistance,
                GetBoxHalfExtents(_isCrouched),
                LadderTraceContents);
            RaycastHit ladderHit = ladderTrace.unityHit;
            bool found = ladderTrace.hit &&
                IsLadderTrace(ladderTrace) &&
                Mathf.Abs(Vector3.Dot(ladderTrace.planeNormal, up)) <
                    0.999999f;

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
            if (ladderJumpRequested)
            {
                // CGameMovement::LadderMove enters the jump-button branch
                // before its ordinary forward/side movement branch. During
                // IGNORE_JUMP_TIME it leaves the previous ladder velocity
                // untouched; it does not keep climbing from the current
                // directional buttons.
                velocity = _surfer.MoveData.Velocity;
            }
            else if (desired.sqrMagnitude > 0.000001f)
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
                    out RaycastHit candidate,
                    LadderTraceContents) ||
                candidate.collider == null ||
                !IsLadderCollider(candidate.collider) ||
                Mathf.Abs(Vector3.Dot(candidate.normal, _movementUp) - 1f) <
                    0.000001f)
                return false;

            ladderHit = candidate;
            // Source first finds the ladder with PLAYERCLIP removed, then
            // repeats the exact same swept player-bbox trace with the normal
            // player-solid mask before snapping to its end position. This
            // prevents an edge grab from placing the hull inside a clip brush.
            SourceTraceResult placementTrace = TracePlayerBBox(
                traceStart,
                traceEnd,
                halfExtents);
            _surfer.MoveData.playerTransform.position =
                placementTrace.endPosition;
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

        private static bool IsLadderTrace(SourceTraceResult trace)
        {
            return (trace.contents & GokzSourceContents.Ladder) != 0 ||
                IsLadderCollider(trace.hitCollider);
        }

        private void StayOnGround(ref Vector3 position, Vector3 halfExtents)
        {
            // Source WalkMove performs a second trace from origin + 2 HU to
            // origin - step height. This keeps the player attached to slopes
            // and moving down small steps instead of leaving a tiny airborne
            // gap after the horizontal move.
            float startOffset = 2f * UnitScale;
            SourceTraceResult upTrace = TracePlayerBBox(
                position,
                position + _movementUp * startOffset,
                halfExtents);
            Vector3 safePosition = upTrace.endPosition;

            Vector3 endPosition = position - _movementUp * StepHeight;
            SourceTraceResult groundTrace = TracePlayerBBox(
                safePosition,
                endPosition,
                halfExtents);
            if (!groundTrace.hit || groundTrace.startSolid ||
                groundTrace.allSolid)
                return;

            float groundNormal = Vector3.Dot(
                groundTrace.planeNormal, _movementUp);
            bool standableGround = groundNormal >= WalkableNormal ||
                (groundTrace.contactedPortalRamp && groundNormal > 0f);
            // CGameMovement::StayOnGround only snaps for a partial trace:
            // zero-fraction contacts and a clear trace (fraction == 1) do not
            // move the player.
            if (groundTrace.fraction <= 0f ||
                groundTrace.fraction >= 1f ||
                !standableGround)
                return;

            position = groundTrace.endPosition;
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

        private void RefreshSourceWaterLevel()
        {
            if (_surfer == null || _surfer.MoveData.playerTransform == null)
                return;

            Transform playerTransform = _surfer.MoveData.playerTransform;
            Vector3 feet = playerTransform.position;
            float hullHeight = _isCrouched ? DuckHullHeight : PlayerHullHeight;
            Vector3 waist = feet + _movementUp * (hullHeight * 0.5f);
            float eyeHeight = Mathf.Lerp(
                DuckEyeStanding,
                DuckEyeCrouched,
                Mathf.Clamp01(_eyeDuckAmount)) * UnitScale;
            Vector3 eyes = feet + _movementUp * eyeHeight;

            Vector3 halfExtents = GetBoxHalfExtents(_isCrouched);
            Vector3 center = feet + _movementUp * halfExtents.y;
            int count = OverlapBoxNonAlloc(
                center,
                halfExtents,
                ref _sourceVolumeOverlaps,
                Quaternion.identity,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            GokzWaterLevel waterLevel = GokzWaterLevel.NotInWater;
            GokzWaterType waterType = GokzWaterType.Water;
            for (int i = 0; i < count; i++)
            {
                Collider collider = _sourceVolumeOverlaps[i];
                if (collider == null || collider == _sourceHullCollider ||
                    collider.transform.IsChildOf(playerTransform))
                    continue;

                GokzWaterVolume water =
                    collider.GetComponentInParent<GokzWaterVolume>();
                GokzMovementVolume movementVolume =
                    collider.GetComponentInParent<GokzMovementVolume>();
                bool movementWater = movementVolume != null &&
                    movementVolume.IsWater;
                bool legacyWater = water == null && !movementWater &&
                    collider.CompareTag("Water");
                if (water == null && !movementWater && !legacyWater)
                    continue;

                // Source CheckWater tests point contents at the feet first;
                // waist and eye samples only matter after the feet sample is
                // inside water. ClosestPoint returns the input point itself
                // when it lies inside a Unity collider.
                Vector3 closestFeet = collider.ClosestPoint(feet +
                    _movementUp * UnitScale);
                if ((closestFeet - (feet + _movementUp * UnitScale)).sqrMagnitude >
                    CollisionSkin * CollisionSkin)
                    continue;

                GokzWaterLevel candidateLevel;
                GokzWaterType candidateType;
                if (legacyWater)
                {
                    // Old LUO Water-tag volumes have no surface metadata;
                    // preserve their established fully-submerged meaning.
                    candidateLevel = GokzWaterLevel.Eyes;
                    candidateType = GokzWaterType.Water;
                }
                else
                {
                    candidateLevel = water != null
                        ? water.GetLevel(
                            feet,
                            _isCrouched,
                            _eyeDuckAmount)
                        : movementVolume.GetWaterLevel(
                            feet,
                            _isCrouched,
                            _eyeDuckAmount);
                    candidateType = water != null
                        ? water.waterType
                        : movementVolume.WaterType;

                    // Require the source midpoint/eye sample to remain inside
                    // the same volume before reporting those higher levels.
                    Vector3 closestWaist = collider.ClosestPoint(waist);
                    Vector3 closestEyes = collider.ClosestPoint(eyes);
                    bool containsWaist = (closestWaist - waist).sqrMagnitude <=
                        CollisionSkin * CollisionSkin;
                    bool containsEyes = (closestEyes - eyes).sqrMagnitude <=
                        CollisionSkin * CollisionSkin;
                    if (candidateLevel >= GokzWaterLevel.Eyes && !containsEyes)
                        candidateLevel = containsWaist
                            ? GokzWaterLevel.Waist
                            : GokzWaterLevel.Feet;
                    else if (candidateLevel >= GokzWaterLevel.Waist &&
                             !containsWaist)
                        candidateLevel = GokzWaterLevel.Feet;
                }

                if (candidateLevel > waterLevel)
                {
                    waterLevel = candidateLevel;
                    waterType = candidateType;
                }
            }

            _surfer.MoveData.waterLevel = waterLevel;
            _surfer.MoveData.waterType = waterType;
            _surfer.MoveData.underwater = waterLevel >= GokzWaterLevel.Waist;
        }

        private void UpdateSourceMovementVolumes(bool groundedAtTriggerTouch)
        {
            _sourceGravityScale = 1f;
            _sourceVolumesThisTick.Clear();

            if (_surfer == null || _surfer.MoveData.playerTransform == null)
                return;

            Vector3 halfExtents = GetBoxHalfExtents(_isCrouched);
            Vector3 center = _surfer.MoveData.playerTransform.position +
                _movementUp * halfExtents.y;
            int count = OverlapBoxNonAlloc(
                center,
                halfExtents,
                ref _sourceVolumeOverlaps,
                Quaternion.identity,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            GokzMovementVolume selectedGravity = null;
            bool normalPushTouched = false;

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

                ApplySourcePushVolume(
                    volume,
                    groundedAtTriggerTouch,
                    ref normalPushTouched);
            }

            // Source PhysicsTouchTriggers receives the player's previous
            // origin and finds trigger brushes touched along the movement
            // segment. Endpoint overlap alone misses a fast player who exits
            // a thin trigger in one command. Include volumes at the start and
            // along the sweep, while the per-command set prevents duplicate
            // pushes for volumes already found at the endpoint.
            Vector3 startCenter = _processStartPosition +
                _movementUp * halfExtents.y;
            int startCount = OverlapBoxNonAlloc(
                startCenter,
                halfExtents,
                ref _sourceVolumeOverlaps,
                Quaternion.identity,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < startCount; i++)
            {
                Collider overlap = _sourceVolumeOverlaps[i];
                if (overlap == null || overlap == _sourceHullCollider ||
                    overlap.transform.IsChildOf(
                        _surfer.MoveData.playerTransform))
                    continue;

                GokzMovementVolume volume =
                    overlap.GetComponentInParent<GokzMovementVolume>();
                if (volume == null || !volume.isActiveAndEnabled ||
                    !volume.IsPush || !_sourceVolumesThisTick.Add(volume))
                    continue;

                ApplySourcePushVolume(
                    volume,
                    groundedAtTriggerTouch,
                    ref normalPushTouched);
            }

            Vector3 movementEndCenter =
                _surfer.MoveData.playerTransform.position +
                _movementUp * halfExtents.y;
            Vector3 movementDelta = movementEndCenter - startCenter;
            float movementDistance = movementDelta.magnitude;
            if (movementDistance > CollisionSkin)
            {
                int sweptCount = BoxCastNonAlloc(
                    startCenter,
                    halfExtents,
                    movementDelta / movementDistance,
                    Quaternion.identity,
                    movementDistance,
                    Physics.AllLayers,
                    QueryTriggerInteraction.Collide);
                for (int i = 0; i < sweptCount; i++)
                {
                    Collider overlap = _moveHits[i].collider;
                    if (overlap == null || overlap == _sourceHullCollider ||
                        overlap.transform.IsChildOf(
                            _surfer.MoveData.playerTransform))
                        continue;

                    GokzMovementVolume volume =
                        overlap.GetComponentInParent<GokzMovementVolume>();
                    if (volume == null || !volume.isActiveAndEnabled ||
                        !volume.IsPush || !_sourceVolumesThisTick.Add(volume))
                        continue;

                    ApplySourcePushVolume(
                        volume,
                        groundedAtTriggerTouch,
                        ref normalPushTouched);
                }
            }

            if (selectedGravity != null)
                _sourceGravityScale = selectedGravity.GravityScale;
        }

        private void ApplySourcePushVolume(
            GokzMovementVolume volume,
            bool groundedAtTriggerTouch,
            ref bool normalPushTouched)
        {
            Vector3 pushVelocity = volume.WorldPushVelocity;
            if (volume.PushOnce)
            {
                if (_consumedSourcePushVolumes.Contains(volume))
                    return;

                _consumedSourcePushVolumes.Add(volume);
                _surfer.MoveData.Velocity += pushVelocity;
                DetachFromGroundForPush(
                    pushVelocity,
                    groundedAtTriggerTouch);
                return;
            }

            // CTriggerPush writes BaseVelocity after movement. A second push
            // touched during the same impact pass adds to the first; a push
            // on the next command replaces the previous vector.
            if (!normalPushTouched)
            {
                _sourceTriggerBaseVelocity = pushVelocity;
                normalPushTouched = true;
            }
            else
            {
                _sourceTriggerBaseVelocity += pushVelocity;
            }
            _sourceTriggerBaseVelocityFlag = true;
            DetachFromGroundForPush(
                pushVelocity,
                groundedAtTriggerTouch);
        }

        private void DetachFromGroundForPush(
            Vector3 pushVelocity,
            bool groundedAtTriggerTouch)
        {
            if (Vector3.Dot(pushVelocity, _movementUp) <= 0f)
                return;

            if (!groundedAtTriggerTouch)
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
                return Physics.AllLayers;

            int playerLayer = _sourceHullCollider != null
                ? _sourceHullCollider.gameObject.layer
                : _surfer.MoveData.playerTransform.gameObject.layer;
            int mask = Physics.AllLayers;
            for (int layer = 0; layer < 32; layer++)
            {
                if (Physics.GetIgnoreLayerCollision(playerLayer, layer))
                    mask &= ~(1 << layer);
            }
            return mask;
        }

        private bool TryRecoverPortalExit(
            Portal exitPortal,
            ref Vector3 rootPosition,
            Vector3 halfExtents)
        {
            if (exitPortal == null ||
                !IsHullStuckAt(rootPosition, halfExtents))
                return false;

            Vector3 center = rootPosition + _movementUp * halfExtents.y;
            Vector3 normal = Portal.GetPortalPlaneNormal(exitPortal).normalized;
            Vector3 fromPortal = center - exitPortal.transform.position;
            Vector3 lateralOffset = fromPortal -
                Vector3.Dot(fromPortal, normal) * normal;

            // CPortalGameMovement first traces from the portal centerline to
            // the teleported point. That removes the lateral AABB corner
            // which most often catches on the exit wall.
            Vector3 centeredRoot = rootPosition - lateralOffset;
            if (!IsHullStuckAt(centeredRoot, halfExtents))
            {
                rootPosition = centeredRoot;
                return true;
            }

            // Source's fallback searches for a passable point while keeping
            // the player center on the room side of the exit portal. Search
            // outward in one-Hammer-unit steps, up to its 100-unit limit.
            for (int distanceHu = 1; distanceHu <= 100; ++distanceHu)
            {
                Vector3 candidate = centeredRoot +
                    normal * (distanceHu * UnitScale);
                Vector3 candidateCenter = candidate +
                    _movementUp * halfExtents.y;
                if (Vector3.Dot(
                        candidateCenter - exitPortal.transform.position,
                        normal) < 0f)
                    continue;

                if (!IsHullStuckAt(candidate, halfExtents))
                {
                    rootPosition = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TrySourceStuckRecovery()
        {
            if (!_collisionReady || _sourceHullCollider == null ||
                _surfer == null || _surfer.MoveData.playerTransform == null)
                return false;

            // With Source movement optimizations enabled, CGameMovement only
            // calls CheckStuck once per CHECK_STUCK_INTERVAL while the player
            // has no pending stuck offset. Once an offset attempt has failed,
            // GetCheckInterval(STUCK) switches to every command tick.
            // Source schedules the first phase from command number + entity
            // index; this single-player adapter has no Source entity index,
            // so it preserves the 1-second cadence with a fixed tick phase.
            if (_stuckOffsetIndex == 0 && _movementTick % TickRate != 0)
                return false;

            Transform playerTransform = _surfer.MoveData.playerTransform;
            Vector3 halfExtents = GetBoxHalfExtents(_isCrouched);
            if (!IsHullStuckAt(playerTransform.position, halfExtents))
            {
                _stuckOffsetIndex = 0;
                return false;
            }

            // CGameMovement::CheckStuck rate-limits retries to 0.05 seconds,
            // tests exactly one entry from the 54-offset Source table per
            // accepted attempt, and leaves velocity untouched when it fails.
            if (_elapsed - _lastStuckCheckTime < 0.05f)
                return true;

            _lastStuckCheckTime = _elapsed;
            Vector3 offsetHu = SourceStuckOffsetsHu[
                _stuckOffsetIndex++ % SourceStuckOffsetsHu.Length];
            Vector3 candidate = playerTransform.position +
                Vector3.right * (offsetHu.x * UnitScale) +
                Vector3.forward * (offsetHu.y * UnitScale) +
                _movementUp * (offsetHu.z * UnitScale);

            if (IsHullStuckAt(candidate, halfExtents))
                return true;

            playerTransform.position = candidate;
            _surfer.MoveData.Origin = candidate;
            Physics.SyncTransforms();
            _stuckOffsetIndex = 0;
            return false;
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
            for (int z = 0; z < bigVertical.Length; z++)
            for (int x = 0; x < bigHorizontal.Length; x++)
            for (int y = 0; y < bigHorizontal.Length; y++)
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
            CheckVelocity(ref horizontalVelocity);

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
            ref Vector3 landingVelocity,
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
            SourceTraceResult slopeTrace = TraceBoxByCenter(
                center,
                center - _movementUp * MaxVelocity,
                duckHalfExtents,
                PlayerSolidBrushContents);
            // gokz-mode-kztimer.sp uses MASK_PLAYERSOLID_BRUSHONLY, which
            // retains MOVEABLE but excludes MONSTER contents.
            if (!slopeTrace.hit || slopeTrace.startSolid ||
                slopeTrace.allSolid)
                return;

            float groundUpDot = Vector3.Dot(
                slopeTrace.planeNormal,
                _movementUp);
            if (groundUpDot < WalkableNormal || groundUpDot >= 0.999999f)
                return;

            Vector3 originalHorizontalVelocity = Vector3.ProjectOnPlane(
                landingVelocity,
                _movementUp);
            Vector3 clipped = ClipLandingVelocity(
                landingVelocity,
                slopeTrace.planeNormal,
                _movementUp);
            if (clipped.magnitude > originalHorizontalVelocity.magnitude)
                landingVelocity = clipped;
        }

        private static Vector3 ClipLandingVelocity(Vector3 velocity, Vector3 normal, Vector3 up)
        {
            // Port gokz-mode-kztimer.sp::SlopeFix, which clips only the two
            // horizontal components before clearing vertical velocity. The
            // Source plugin's loop covers X/Y; LUO's floor plane is X/Z.
            float backoff = Vector3.Dot(velocity, normal);
            Vector3 horizontalNormal = Vector3.ProjectOnPlane(normal, up);
            Vector3 clipped = Vector3.ProjectOnPlane(velocity, up) -
                horizontalNormal * backoff;
            float correction = Vector3.Dot(clipped, normal);
            if (correction < 0f)
                clipped -= horizontalNormal * correction;
            return Vector3.ProjectOnPlane(clipped, up);
        }

        private void AirMove(float realPreVelMod)
        {
            Vector3 velocity = _surfer.MoveData.Velocity;
            // FullWalkMove checks the current velocity after CheckJumpButton
            // and before AirMove applies air acceleration.
            CheckVelocity(ref velocity);

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
            out RaycastHit hit,
            GokzSourceContents sourceContentsMask = PlayerSolidContents)
        {
            SourceTraceResult trace = TraceBoxByCenter(
                startCenter,
                endCenter,
                halfExtents,
                sourceContentsMask);
            hit = trace.unityHit;
            return trace.hit;
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
            SourceTraceResult trace = TracePlayerBBox(
                start,
                start - _movementUp * (2f * UnitScale),
                GetBoxHalfExtents(_isCrouched));
            if (!trace.hit || trace.startSolid || trace.allSolid)
                return;

            _surfer.MoveData.playerTransform.position = trace.endPosition;
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

        private static bool HasSourceButton(int buttons, InputButtons button)
        {
            return (buttons & (int)button) != 0;
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
            {
                // CGameMovement::ClipVelocity clamps tiny negative plane
                // errors to -DIST_EPSILON before the correction pass.
                adjust = Mathf.Min(adjust, -SourceDistanceEpsilon);
                clipped -= normal * adjust;
            }
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

            return Gravity * _sourceGravityScale;
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
            float pitchUp = Mathf.Asin(
                Mathf.Clamp(view.y, -1f, 1f)) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(view.x, view.z) * Mathf.Rad2Deg;
            pitchUp = Mathf.Clamp(pitchUp, -70f, 70f);
            // Unity's positive Euler X rotates forward toward negative Y;
            // negate pitchUp to reconstruct the same forward vector before
            // applying GOKZ's Source eye-pitch clamp.
            Vector3 viewDirection = Quaternion.Euler(-pitchUp, yaw, 0f) *
                Vector3.forward;
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
