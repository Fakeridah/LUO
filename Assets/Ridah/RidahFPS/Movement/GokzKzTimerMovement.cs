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
        private const float CollisionSkin = 0.001f;
        private const float SourceDistanceEpsilon = 0.03125f * UnitScale;
        // A BoxCast that begins overlapping reports distance 0. Only that is
        // "startsolid" in Source's CTrace_PlayerAABB_vs_Portals sense, so this
        // must stay tiny: a proximity tolerance here would also excuse real
        // geometry standing a few hundredths away.
        private const float PortalSpaceStartSolidEpsilon = 0.0001f;
        // How deep a hull has to be buried in a carved portal host before the
        // periodic stuck recovery calls it stuck. Leaning on the aperture rim
        // is a normal resting place, and a false positive here aborts the whole
        // movement command, so this stays well clear of contact noise while
        // still catching a hull that landed straddling the rim.
        private const float CarvedHostStuckPenetration = 0.05f;
        // Largest single depenetration step used to free a hull that Source's
        // 54-offset probe table cannot reach. Source's table tops out at 6 HU,
        // which is far too short for a hull buried in a mesh; one full hull
        // width per step keeps the repair local while still clearing it.
        private const float MaxStuckDepenetration = 32f * UnitScale;
        private const float SourcePlaneEpsilon = 1.1920929e-7f;
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
        private Vector3 _lastShellProbeOrigin;
        private float _lastShellProbeLogTime = -1f;
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
        private readonly Collider[] _sourceVolumeOverlaps = new Collider[64];
        private readonly HashSet<GokzMovementVolume> _sourceVolumesThisTick =
            new HashSet<GokzMovementVolume>();
        private readonly HashSet<GokzMovementVolume> _consumedSourcePushVolumes =
            new HashSet<GokzMovementVolume>();
        private RaycastHit[] _moveHits = new RaycastHit[16];
        private Vector3 _lastBoxQueryCenter;
        private Vector3 _lastBoxQueryHalfExtents;
        private Quaternion _lastBoxQueryRotation = Quaternion.identity;
        private Vector3 _lastBoxQuerySweepDelta;
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

                float distance = velocity.magnitude * timeLeft;
                Vector3 direction = velocity / velocity.magnitude;
                int hitCount = SweepBox(position, halfExtents, direction, distance);
                RaycastHit nearest;
                bool hit = TryGetNearestHit(
                    hitCount, out nearest, out bool contactedPortalRamp);
                Vector3 endPosition = position + velocity * timeLeft;
                // Unity casts use a slightly inset hull. Their contact point
                // is therefore one skin farther along the path than the
                // equivalent full Source hull trace. Convert back to Source's
                // full-hull travel distance before advancing or clipping.
                float safeDistance = hit
                    ? Mathf.Clamp(
                        nearest.distance - CollisionSkin,
                        0f,
                        distance)
                    : distance;
                float fraction = distance > 0f
                    ? safeDistance / distance
                    : 0f;
                Vector3 traceEndPosition = hit
                    ? position + direction * safeDistance
                    : endPosition;

                // gokz-momsurffix treats a later invalid airborne trace as a
                // ramp precision failure. It retries from a nudged origin and
                // may reconstruct a valid plane from 27 nearby hull traces.
                if (bump > 0 && !Grounded && hit &&
                    !IsValidMovementTrace(
                        position,
                        halfExtents,
                        nearest,
                        traceEndPosition,
                        fraction,
                        contactedPortalRamp))
                {
                    lastTracePlane = nearest.normal.normalized;
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
                Vector3 planeNormal = nearest.normal.normalized;
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
            Vector3 startPosition,
            Vector3 halfExtents,
            RaycastHit hit,
            Vector3 traceEndPosition,
            float fraction,
            bool contactedPortalRamp)
        {
            if (IsHullStuckAt(startPosition, halfExtents))
                return false;

            Vector3 normal = hit.normal.normalized;
            float vertical = Vector3.Dot(normal, _movementUp);
            bool axisAlignedSidePlane =
                Mathf.Abs(vertical) <= SourcePlaneEpsilon &&
                (Mathf.Abs(Mathf.Abs(normal.x) - 1f) <= SourcePlaneEpsilon ||
                 Mathf.Abs(Mathf.Abs(normal.z) - 1f) <= SourcePlaneEpsilon);
            bool sourcePlaneTypeAtLeastZ = !axisAlignedSidePlane;
            if (fraction <= SourcePlaneEpsilon &&
                sourcePlaneTypeAtLeastZ &&
                vertical < WalkableNormal &&
                !(contactedPortalRamp && vertical > 0f))
                return false;

            // gokz-momsurffix::IsValidMovementTrace also performs a stationary
            // TracePlayerBBox at tr.endpos and rejects startsolid or any
            // incomplete stationary trace. This catches an endpoint that a
            // swept trace reports as valid even though it leaves the hull
            // embedded in a ramp triangle.
            return !IsHullStuckAt(traceEndPosition, halfExtents);
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
                Vector3 delta = traceEnd - traceStart;
                float distance = delta.magnitude;
                if (distance <= CollisionSkin)
                    continue;

                int hitCount = BoxCastNonAlloc(
                    traceStart,
                    queryHalfExtents,
                    delta / distance,
                    Quaternion.identity,
                    distance,
                    GetCollisionMask(),
                    QueryTriggerInteraction.Ignore);
                if (!TryGetNearestHit(hitCount, out RaycastHit hit) ||
                    hit.distance <= 0f || hit.distance >= distance ||
                    IsHullStuckAt(startPosition, halfExtents))
                    continue;

                normalSum += hit.normal.normalized;
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
                if (Portal.IsPairedPortalWallContact(
                    upHit,
                    _lastBoxQueryCenter,
                    _lastBoxQueryHalfExtents +
                        Vector3.one * CollisionSkin,
                    _lastBoxQueryRotation,
                    _lastBoxQuerySweepDelta))
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
            bool downHitFound = TryGetNearestHit(
                downHitCount, out downHit, out bool downHitPortalRamp);
            float downNormal = downHitFound
                ? Vector3.Dot(downHit.normal, _movementUp)
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
                 !(downHitPortalRamp && downNormal > 0f)))
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
            return BoxCastNonAlloc(
                center,
                queryHalfExtents,
                direction,
                Quaternion.identity,
                distance,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);
        }

        private int BoxCastNonAlloc(
            Vector3 center,
            Vector3 halfExtents,
            Vector3 direction,
            Quaternion orientation,
            float distance,
            int layerMask,
            QueryTriggerInteraction queryTriggerInteraction)
        {
            _lastBoxQueryCenter = center;
            _lastBoxQueryHalfExtents = halfExtents;
            _lastBoxQueryRotation = orientation;
            _lastBoxQuerySweepDelta = direction.sqrMagnitude > 0.000001f
                ? direction.normalized * distance
                : Vector3.zero;

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
                    return hitCount;

                // A Source hull trace returns the closest blocking trace. A
                // full Unity NonAlloc buffer can omit the nearest collider, so
                // grow and retry instead of letting buffer capacity change the
                // movement result in dense corners.
                System.Array.Resize(ref _moveHits, _moveHits.Length * 2);
            }
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
                if (candidate.collider == null ||
                    candidate.collider == _sourceHullCollider)
                    continue;

                bool ignoredByPortal = Portal.ShouldIgnoreMovementCollision(
                    candidate,
                    _lastBoxQueryCenter,
                    _lastBoxQueryHalfExtents +
                        Vector3.one * CollisionSkin,
                    _lastBoxQueryRotation,
                    _lastBoxQuerySweepDelta,
                    GetQueryHalfExtents(GetBoxHalfExtents(true)),
                    _movementUp);
                Portal.LogPortalHostContact(
                    candidate, _lastBoxQueryCenter, ignoredByPortal);
                if (ignoredByPortal ||
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

            // Source runs CTrace_PlayerAABB_vs_Portals with two gates.
            //
            // Gate 1 (1711) - enter the portal branch only when the plain
            // real-space trace is obstructed:
            //     RealTrace.startsolid || (ray_local.m_IsSwept && RealTrace.fraction < 1.0f)
            //
            // Gate 2 (1850) - the portal trace may never be stricter than that
            // plain trace:
            //     RealTrace.startsolid ||
            //         ( !PortalTrace.startsolid && PortalTrace.fraction >= RealTrace.fraction )
            // Because the aperture filter can only ever remove hits, the
            // filtered result is always >= the plain one, so in practice the
            // plain trace is the floor under the result: a remote hit nearer
            // than it is discarded.
            //
            // Source's trace mask excludes the portal prop, so the plain trace
            // here has to exclude portal-owned colliders too. Leaving them in
            // put the portal's own 0.08 UU thick collider - sitting exactly on
            // the plane - at ~0 distance, which let every remote hit through
            // gate 2 and is why the previous attempt still pinned the player.
            float sweepLength = _lastBoxQuerySweepDelta.magnitude;
            float plainLocalDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit raw = _moveHits[i];
                if (raw.collider == null || raw.collider == _sourceHullCollider)
                    continue;
                if (Portal.IsPortalOwnedCollider(raw.collider))
                    continue;
                if (_surfer != null && _surfer.MoveData.playerTransform != null &&
                    raw.collider.transform.IsChildOf(_surfer.MoveData.playerTransform))
                    continue;
                if (raw.distance < plainLocalDistance)
                    plainLocalDistance = raw.distance;
            }

            bool plainStartSolid =
                plainLocalDistance <= PortalSpaceStartSolidEpsilon;
            bool localTraceObstructed = plainStartSolid ||
                plainLocalDistance < sweepLength;

            Transform movingRoot = _surfer != null
                ? _surfer.MoveData.playerTransform
                : null;
            if (localTraceObstructed &&
                Portal.TryGetPortalSpaceCollision(
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
                portalHit.distance < nearestDistance &&
                (plainStartSolid ||
                 portalHit.distance >= plainLocalDistance))
            {
                nearest = portalHit;
                contactedPortalTransitionRamp = portalRampContact;
                return true;
            }

            // Same zero-thickness mesh correction as the standability test:
            // ClipVelocity needs the real ramp normal, otherwise a
            // single-sided mesh ramp clips movement along a phantom
            // straight-up plane.
            if (found &&
                TryGetPlanarMeshBlockingNormal(
                    nearest.collider,
                    _lastBoxQueryCenter,
                    _lastBoxQuerySweepDelta,
                    out Vector3 planarBlockingNormal))
                nearest.normal = planarBlockingNormal;

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
            int hitCount = BoxCastNonAlloc(
                center,
                queryHalfExtents,
                -_movementUp,
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
                    int quadrantHitCount = BoxCastNonAlloc(
                        center + quadrantOffsets[quadrant],
                        GetQueryHalfExtents(quadrantHalfExtents),
                        -_movementUp,
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
                    candidate.collider == _sourceHullCollider)
                    continue;

                bool ignoredByPortal = Portal.ShouldIgnoreMovementCollision(
                    candidate,
                    _lastBoxQueryCenter,
                    _lastBoxQueryHalfExtents +
                        Vector3.one * CollisionSkin,
                    _lastBoxQueryRotation,
                    _lastBoxQuerySweepDelta,
                    GetQueryHalfExtents(GetBoxHalfExtents(true)),
                    _movementUp);
                Portal.LogPortalHostContact(
                    candidate, _lastBoxQueryCenter, ignoredByPortal);
                if (ignoredByPortal ||
                    (_surfer != null &&
                     _surfer.MoveData.playerTransform != null &&
                     candidate.collider.transform.IsChildOf(
                         _surfer.MoveData.playerTransform)))
                    continue;

                // A zero-thickness non-convex MeshCollider (a single-quad
                // ramp) has no volume, so PhysX reports an unreliable normal
                // for it - typically straight up even on a steep ramp. That
                // makes a steep single-sided ramp look standable, which is
                // why the player could not slide on it. Rebuild the normal
                // from the mesh's own plane so the standability test below
                // sees the real slope angle.
                if (TryGetPlanarMeshBlockingNormal(
                        candidate.collider,
                        _lastBoxQueryCenter,
                        _lastBoxQuerySweepDelta,
                        out Vector3 candidatePlanarNormal))
                    candidate.normal = candidatePlanarNormal;

                if (Vector3.Dot(candidate.normal, _movementUp) < WalkableNormal ||
                    candidate.distance >= nearestDistance)
                    continue;

                nearest = candidate;
                nearestDistance = candidate.distance;
                found = true;
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
                (Vector3.Dot(portalHit.normal, _movementUp) >= WalkableNormal ||
                 (portalRampContact &&
                  Vector3.Dot(portalHit.normal, _movementUp) > 0f)) &&
                portalHit.distance < nearestDistance)
            {
                nearest = portalHit;
                return true;
            }

            return found;
        }

        // A zero-thickness non-convex MeshCollider - a single-quad ramp or a
        // Unity Plane primitive - has no volume, so PhysX has no reliable
        // plane for it and can report a straight-up normal on a steep ramp.
        // That makes the ramp look standable, which kills slide and makes the
        // slope behave like flat ground. Source never has this problem
        // because SolidBSP hands CGameMovement the real brush plane. Rebuild
        // the plane from the mesh geometry so both the standability test and
        // ClipVelocity see the true slope angle.
        private struct PlanarMeshInfo
        {
            public bool valid;
            public Vector3 localPoint;
            public Vector3 localNormal;
        }

        private static readonly Dictionary<Mesh, PlanarMeshInfo>
            _planarMeshCache = new Dictionary<Mesh, PlanarMeshInfo>();

        private static bool IsZeroThicknessMesh(MeshCollider meshCollider)
        {
            if (meshCollider == null || meshCollider.convex)
                return false;

            Mesh mesh = meshCollider.sharedMesh;
            if (mesh == null)
                return false;

            // Bounds are always available, even for a mesh imported without
            // Read/Write, so this gate never depends on vertex access.
            Vector3 size = mesh.bounds.size;
            float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            return largest > 0f &&
                smallest <= Mathf.Max(0.00001f, largest * 0.00001f);
        }

        private static bool TryGetPlanarMeshInfo(
            Mesh mesh,
            out PlanarMeshInfo info)
        {
            info = default;
            if (mesh == null)
                return false;

            if (_planarMeshCache.TryGetValue(mesh, out info))
                return info.valid;

            // Meshes are shared, so this analysis runs once per mesh rather
            // than once per movement tick.
            info = BuildPlanarMeshInfo(mesh);
            if (_planarMeshCache.Count > 512)
                _planarMeshCache.Clear();
            _planarMeshCache[mesh] = info;
            return info.valid;
        }

        private static PlanarMeshInfo BuildPlanarMeshInfo(Mesh mesh)
        {
            PlanarMeshInfo info = default;
            if (mesh == null)
                return info;

            Vector3 size = mesh.bounds.size;
            float smallest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (largest <= 0f ||
                smallest > Mathf.Max(0.00001f, largest * 0.00001f))
                return info;

            // Preferred path: derive the plane from the real triangles. This
            // is exact for any sheet orientation, not just an axis-aligned
            // one, and the planarity test keeps a thin-but-solid mesh on
            // PhysX's own normals.
            if (mesh.isReadable &&
                TryBuildTrianglePlane(
                    mesh,
                    out Vector3 trianglePoint,
                    out Vector3 triangleNormal))
            {
                info.valid = true;
                info.localPoint = trianglePoint;
                info.localNormal = triangleNormal;
                return info;
            }

            // Fallback for a mesh imported without Read/Write: a collapsed
            // bounds axis still identifies the sheet normal when the quad was
            // authored in one of the local planes, which is the case for
            // Unity's Plane primitive and most single-face ramps.
            if (size.x <= size.y && size.x <= size.z)
                info.localNormal = Vector3.right;
            else if (size.y <= size.z)
                info.localNormal = Vector3.up;
            else
                info.localNormal = Vector3.forward;

            info.valid = true;
            info.localPoint = mesh.bounds.center;
            return info;
        }

        private static bool TryBuildTrianglePlane(
            Mesh mesh,
            out Vector3 planePoint,
            out Vector3 planeNormal)
        {
            planePoint = Vector3.zero;
            planeNormal = Vector3.zero;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices.Length < 3 || triangles.Length < 3)
                return false;

            // The largest triangle gives the most numerically stable plane.
            float bestArea = 0f;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                float area = cross.magnitude;
                if (area <= bestArea)
                    continue;

                bestArea = area;
                planeNormal = cross / area;
                planePoint = (a + b + c) / 3f;
            }

            if (bestArea <= 0f)
                return false;

            // Reject anything with real thickness. This keeps ordinary level
            // meshes - a floor with walls, terrain, a curved shell - on
            // PhysX's own normals instead of flattening them.
            float tolerance = Mathf.Max(
                0.00001f,
                mesh.bounds.size.magnitude * 0.00001f);
            for (int i = 0; i < vertices.Length; i++)
            {
                float deviation = Mathf.Abs(
                    Vector3.Dot(vertices[i] - planePoint, planeNormal));
                if (deviation > tolerance)
                    return false;
            }

            return true;
        }

        private static bool TryGetZeroThicknessMeshPlane(
            MeshCollider meshCollider,
            out Vector3 worldPoint,
            out Vector3 worldNormal)
        {
            worldPoint = Vector3.zero;
            worldNormal = Vector3.zero;
            if (!IsZeroThicknessMesh(meshCollider) ||
                !TryGetPlanarMeshInfo(
                    meshCollider.sharedMesh,
                    out PlanarMeshInfo info))
                return false;

            Transform meshTransform = meshCollider.transform;
            worldPoint = meshTransform.TransformPoint(info.localPoint);
            // Normals transform by the inverse transpose, which is the
            // transpose of worldToLocal; using the forward matrix would skew
            // the slope angle under non-uniform scale.
            worldNormal = meshTransform.worldToLocalMatrix
                .transpose.MultiplyVector(info.localNormal);
            float length = worldNormal.magnitude;
            if (length <= 0.000001f)
                return false;

            worldNormal /= length;
            return true;
        }

        // The contact normal for a zero-thickness sheet, oriented toward the
        // side the hull is on. Returns false only when the hull sits exactly
        // on the sheet and the sweep gives no direction to orient against.
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
                    out Vector3 planeNormal))
                return false;

            // Do not require the sweep to cross the sheet. Sliding along a
            // ramp never crosses its own plane, yet that is exactly the case
            // that needs the real slope normal for ClipVelocity.
            float side = Vector3.Dot(sweepStart - planePoint, planeNormal);
            if (Mathf.Abs(side) > SourceDistanceEpsilon)
            {
                blockingNormal = side > 0f ? planeNormal : -planeNormal;
                return true;
            }

            // The hull centre sits on the sheet, so orient the normal
            // against the crossing direction; either side then works.
            if (sweepDelta.sqrMagnitude <= 0.000000000001f)
                return false;

            float directionAcrossPlane =
                Vector3.Dot(sweepDelta.normalized, planeNormal);
            if (Mathf.Abs(directionAcrossPlane) <= 0.00001f)
                return false;

            blockingNormal = directionAcrossPlane > 0f
                ? -planeNormal
                : planeNormal;
            return true;
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

            Vector3 delta = end - start;
            Vector3 halfExtents = GetBoxHalfExtents(false);
            Vector3 center = start + _movementUp * halfExtents.y;
            if (delta.sqrMagnitude > CollisionSkin * CollisionSkin)
            {
                int hitCount = BoxCastNonAlloc(
                    center,
                    GetQueryHalfExtents(halfExtents),
                    delta.normalized,
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
            Vector3 halfExtents = GetQueryHalfExtents(
                hullHalfExtents);
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
                    Portal.ShouldIgnoreMovementCollision(
                        collider, center, hullHalfExtents, Quaternion.identity) ||
                    collider.transform.IsChildOf(_surfer.MoveData.playerTransform))
                    continue;
                return false;
            }

            return true;
        }

        private bool IsHullStuckAt(Vector3 basePosition, Vector3 halfExtents)
        {
            return IsHullStuckAt(basePosition, halfExtents, out _);
        }

        /// <summary>
        /// Same test, but reports which collider held the hull. Diagnostics
        /// only: a command aborted by the stuck check never reaches
        /// TryHandlePlayerMovement, so a stuck hull inside a portal aperture
        /// produces no portal log at all and reads as "the portal silently did
        /// nothing". Naming the blocker is the only way to tell that apart
        /// from the crossing being rejected.
        /// </summary>
        private bool IsHullStuckAt(
            Vector3 basePosition,
            Vector3 halfExtents,
            out Collider blocker)
        {
            return IsHullStuckAt(
                basePosition,
                halfExtents,
                out blocker,
                false,
                GetCollisionMask());
        }

        private bool IsHullStuckAt(
            Vector3 basePosition,
            Vector3 halfExtents,
            out Collider blocker,
            bool strictPenetrationCheck)
        {
            return IsHullStuckAt(
                basePosition,
                halfExtents,
                out blocker,
                strictPenetrationCheck,
                GetCollisionMask());
        }

        private bool IsHullStuckAt(
            Vector3 basePosition,
            Vector3 halfExtents,
            out Collider blocker,
            bool strictPenetrationCheck,
            int collisionMask)
        {
            blocker = null;
            if (_surfer == null || _surfer.MoveData.playerTransform == null)
                return false;

            Vector3 center = basePosition + _movementUp * halfExtents.y;
            Vector3 queryHalfExtents = strictPenetrationCheck
                ? halfExtents
                : GetQueryHalfExtents(halfExtents);
            int count;
            while (true)
            {
                count = Physics.OverlapBoxNonAlloc(
                    center,
                    queryHalfExtents,
                    _hullOverlaps,
                    Quaternion.identity,
                    collisionMask,
                    QueryTriggerInteraction.Ignore);
                if (count < _hullOverlaps.Length)
                    break;

                // A full NonAlloc buffer may omit the blocking collider. Grow
                // only in crowded overlaps, then repeat the same query.
                System.Array.Resize(
                    ref _hullOverlaps, _hullOverlaps.Length * 2);
            }
            for (int i = 0; i < count; i++)
            {
                Collider collider = _hullOverlaps[i];
                if (collider == null || collider == _sourceHullCollider ||
                    collider.transform.IsChildOf(
                        _surfer.MoveData.playerTransform))
                    continue;

                if (Portal.ShouldIgnoreMovementCollision(
                        collider,
                        center,
                        halfExtents,
                        Quaternion.identity))
                    continue;

                // Stuck recovery normally ignores portal hosts: a hull resting
                // on the aperture rim is a normal resting place, and treating
                // that contact as stuck zeroes the velocity every command.
                // A carved host is the exception. Its hole is real geometry, so
                // a hull that actually penetrates it is straddling the rim, and
                // leaving that invisible meant a hull which drifted into the
                // rim after a teleport stayed frozen forever.
                bool carvedHost = Portal.IsCarvedPortalHostCollider(collider);
                if (!carvedHost &&
                    Portal.ShouldIgnoreStuckRecoveryCollision(
                        collider,
                        center,
                        halfExtents,
                        Quaternion.identity))
                    continue;

                // Physics.OverlapBox also reports a collider that merely
                // touches the query box (including Unity's contact offset).
                // Source's allsolid/CheckStuck path is only for a hull that
                // is actually penetrating solid geometry. Treating ordinary
                // wall contact as stuck zeroes the clipped velocity every
                // command, so the player can only escape by changing hull
                // size (duck) or teleporting (reset).
                //
                // A carved host needs a wider margin than the plain contact
                // tolerance: leaning on the aperture rim is still a normal
                // resting place, and only a hull clearly buried in the rim
                // should be treated as the bad-landing case worth repairing.
                float stuckTolerance = strictPenetrationCheck
                    ? CollisionSkin
                    : GetContactPenetrationTolerance(collider);
                if (carvedHost)
                    stuckTolerance = Mathf.Max(
                        stuckTolerance, CarvedHostStuckPenetration);

                if (Physics.ComputePenetration(
                    _sourceHullCollider,
                    basePosition,
                    Quaternion.identity,
                    collider,
                    collider.transform.position,
                    collider.transform.rotation,
                    out _,
                    out float penetration) &&
                     penetration > stuckTolerance)
                {
                    blocker = collider;
                    return true;
                }
            }

            return false;
        }

        private float GetContactPenetrationTolerance(Collider other)
        {
            if (other == null)
                return CollisionSkin;

            float hullContactOffset = _sourceHullCollider != null
                ? _sourceHullCollider.contactOffset
                : 0f;
            return Mathf.Max(
                CollisionSkin,
                Mathf.Max(hullContactOffset, other.contactOffset)) +
                CollisionSkin;
        }

        // DIAGNOSTIC ONLY. Rate limited so a hull held for a second logs once
        // instead of once per command.
        private float _lastStuckBlockLogTime = -1f;

        private void LogStuckBlock(
            Vector3 basePosition,
            Vector3 halfExtents,
            Collider blocker)
        {
            float now = Time.unscaledTime;
            if (now - _lastStuckBlockLogTime < 0.5f)
                return;
            _lastStuckBlockLogTime = now;

            Vector3 center = basePosition + _movementUp * halfExtents.y;
            string extra = string.Empty;
            Portal hostPortal = Portal.FindPortalHostingCollider(blocker);
            if (hostPortal != null)
            {
                Vector3 normal = Portal.GetPortalPlaneNormal(hostPortal);
                Vector3 offset = center - hostPortal.transform.position;
                float distance = Vector3.Dot(offset, normal);
                extra = " portal=" + hostPortal.name +
                    " dist=" + distance.ToString("F3") +
                    " rightOff=" +
                        Vector3.Dot(offset, hostPortal.transform.right)
                            .ToString("F3") +
                    " upOff=" +
                        Vector3.Dot(offset, hostPortal.transform.up)
                            .ToString("F3") +
                    " fitsOpening=" + Portal.IsHullInsidePortalOpening(
                        hostPortal, center, halfExtents);
            }

            Debug.Log("[GokzStuck] command blocked at " +
                basePosition.ToString("F3") +
                " by " + (blocker != null ? blocker.name : "unknown") +
                " carvedHost=" +
                (blocker != null &&
                 Portal.IsCarvedPortalHostCollider(blocker)) +
                extra);
        }

        /// <summary>
        /// Penetration test against one specific collider, without the
        /// portal-host exclusion that IsHullStuckAt applies. The post-teleport
        /// rescue needs it to confirm that sliding the hull into the aperture
        /// really freed it instead of just moving it sideways.
        /// </summary>
        private bool IsHullPenetratingCollider(
            Collider collider,
            Vector3 basePosition)
        {
            if (collider == null || _sourceHullCollider == null)
                return false;

            if (!Physics.ComputePenetration(
                _sourceHullCollider,
                basePosition,
                Quaternion.identity,
                collider,
                collider.transform.position,
                collider.transform.rotation,
                out _,
                out float penetration))
                return false;

            return penetration > GetContactPenetrationTolerance(collider);
        }

        /// <summary>
        /// Frees a hull that is embedded in a non-convex mesh shell, which is
        /// the one trap the Source stuck path cannot see.
        /// </summary>
        /// <remarks>
        /// An open mesh - a pipe whose mouth is not capped, a single-sided
        /// sheet - is a shell of triangles with no volume. Every ordinary query
        /// ignores its back faces: shape sweeps do by default
        /// (Physics.queriesHitBackfaces), and Physics.ComputePenetration says so
        /// explicitly in its own docs. So once the hull straddles the shell, the
        /// sweeps start overlapped and report distance 0 in every direction,
        /// while ComputePenetration only measures the front-facing half and
        /// reports nothing worth repairing. The hull cannot move, never trips
        /// IsHullStuckAt, and Source's offset table never runs - the player is
        /// simply frozen.
        ///
        /// Measure the embedding with the one query that does see a wall from
        /// its far side: a ray probe with back-face hits enabled. Six axes are
        /// enough because a shell that freezes the hull has to cross near its
        /// centre, and the shortest crossing is the separation to apply.
        /// </remarks>
        private bool TryEscapeEmbeddedMeshShell(
            Vector3 basePosition,
            Vector3 halfExtents)
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

            // This probe runs once per second during normal play, so a report
            // is only worth a line when the hull has not moved since the last
            // one: that is the frozen state we are hunting.
            bool frozen =
                (basePosition - _lastShellProbeOrigin).sqrMagnitude < 0.000001f;
            _lastShellProbeOrigin = basePosition;
            float deepest = 0f;
            int shells = 0;
            string firstName = null;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _hullOverlaps[i];
                if (collider == null || collider == _sourceHullCollider)
                    continue;
                if (collider.transform.IsChildOf(
                        _surfer.MoveData.playerTransform))
                    continue;
                if (firstName == null)
                    firstName = collider.name + "(" +
                        collider.GetType().Name + ")";

                // A convex mesh is measured correctly by ComputePenetration,
                // so only the non-convex ones have this blind spot.
                MeshCollider shell = collider as MeshCollider;
                if (shell == null || shell.convex)
                    continue;
                if (Portal.ShouldIgnoreMovementCollision(
                        collider,
                        center,
                        halfExtents,
                        Quaternion.identity))
                    continue;

                shells++;
                Vector3 candidate = basePosition;
                bool embedded = false;
                for (int iteration = 0; iteration < 4; iteration++)
                {
                    float step = GetShellPenetration(
                        shell, candidate, halfExtents, out Vector3 push);
                    if (step <= 0f)
                        break;
                    if (step > deepest)
                        deepest = step;
                    embedded = true;
                    candidate += push;
                }

                if (!embedded)
                    continue;
                if ((candidate - basePosition).sqrMagnitude < 0.0000001f)
                    continue;
                if (GetShellPenetration(
                        shell, candidate, halfExtents, out _) > 0f)
                    continue;
                if (IsHullStuckAt(candidate, halfExtents))
                    continue;

                Debug.Log("[GokzMove] freed the hull from the open mesh " +
                    collider.name + " (" + collider.GetType().Name + ") by " +
                    (candidate - basePosition).magnitude.ToString("F3") + " UU");

                Transform playerTransform = _surfer.MoveData.playerTransform;
                playerTransform.position = candidate;
                _surfer.MoveData.Origin = candidate;
                Physics.SyncTransforms();
                _stuckOffsetIndex = 0;
                return true;
            }

            // Nothing could be measured, so record what the hull is sitting in.
            // Without this the frozen case is completely silent: the sweeps
            // report start-solid, IsHullStuckAt sees no penetration, and no
            // recovery path ever logs a reason.
            if (frozen && count > 0 &&
                _elapsed - _lastShellProbeLogTime > 1f)
            {
                _lastShellProbeLogTime = _elapsed;
                Debug.Log("[GokzMove] hull frozen at " +
                    basePosition.ToString("F3") + " with " + count +
                    " overlaps, " + shells + " non-convex mesh(es), deepest " +
                    deepest.ToString("F3") + " UU; first=" +
                    (firstName != null ? firstName : "none"));
            }

            return false;
        }

        /// <summary>
        /// How far the mesh surface reaches inside the hull at the given pose,
        /// plus the translation that would push it back out.
        /// </summary>
        /// <remarks>
        /// Probed by shooting the six hull axes with back-face hits enabled.
        /// That is the only query that can see a wall from its far side:
        /// shape sweeps ignore back faces by default, ComputePenetration says
        /// so in its own docs, and for a non-convex mesh ClosestPoint answers
        /// with the query point itself, so none of the three can measure how
        /// deep a shell reaches inside the hull.
        /// </remarks>
        private float GetShellPenetration(
            MeshCollider shell,
            Vector3 basePosition,
            Vector3 halfExtents,
            out Vector3 push)
        {
            push = Vector3.zero;

            Vector3 center = basePosition + _movementUp * halfExtents.y;
            float tolerance =
                GetContactPenetrationTolerance(shell) + CollisionSkin;
            float smallest = float.PositiveInfinity;
            Vector3 smallestAxis = Vector3.zero;

            bool previousBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                for (int axis = 0; axis < ShellProbeAxes.Length; axis++)
                {
                    Vector3 direction = ShellProbeAxes[axis];
                    float extent =
                        Mathf.Abs(direction.x) * halfExtents.x +
                        Mathf.Abs(direction.y) * halfExtents.y +
                        Mathf.Abs(direction.z) * halfExtents.z;
                    Ray ray = new Ray(center, direction);
                    if (!shell.Raycast(ray, out RaycastHit hit,
                            extent + tolerance))
                        continue;

                    float depth = extent - hit.distance;
                    if (depth <= tolerance || depth >= smallest)
                        continue;

                    smallest = depth;
                    smallestAxis = direction;
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = previousBackfaces;
            }

            if (smallestAxis == Vector3.zero)
                return 0f;

            push = smallestAxis * (smallest + CollisionSkin);
            return smallest;
        }

        private static readonly Vector3[] ShellProbeAxes =
        {
            Vector3.right, Vector3.left,
            Vector3.up, Vector3.down,
            Vector3.forward, Vector3.back
        };

        /// <summary>
        /// Frees a hull buried in geometry deeper than Source's probe table can
        /// reach, using the physics engine's own separation vector.
        /// </summary>
        /// <remarks>
        /// CGameMovement::CheckStuck probes 54 offsets whose largest step is
        /// 6 HU (0.114 UU). A hull that ends up intersecting a MeshCollider -
        /// an open pipe mouth, a thin ramp triangle, a prop whose collision is
        /// a shell - is usually buried much deeper than that, so every probe
        /// fails, CheckStuck reports "still stuck" forever, and the movement
        /// command is aborted on every tick: the player is frozen until a
        /// teleport. ComputePenetration already returns the minimal
        /// translation that separates the pair, which is exactly what the
        /// table is approximating with fixed steps.
        /// </remarks>
        private bool TryDepenetrateHull(
            Vector3 basePosition,
            Vector3 halfExtents,
            out Vector3 freed,
            out Collider blocker)
        {
            freed = basePosition;
            blocker = null;
            if (_sourceHullCollider == null || _surfer == null ||
                _surfer.MoveData.playerTransform == null)
                return false;

            Transform playerTransform = _surfer.MoveData.playerTransform;
            Vector3 position = basePosition;
            bool moved = false;
            for (int iteration = 0; iteration < 4; iteration++)
            {
                Vector3 center = position + _movementUp * halfExtents.y;
                int count = Physics.OverlapBoxNonAlloc(
                    center,
                    GetQueryHalfExtents(halfExtents),
                    _hullOverlaps,
                    Quaternion.identity,
                    GetCollisionMask(),
                    QueryTriggerInteraction.Ignore);

                Vector3 push = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    Collider collider = _hullOverlaps[i];
                    if (collider == null || collider == _sourceHullCollider)
                        continue;
                    if (collider.transform.IsChildOf(playerTransform))
                        continue;
                    if (Portal.ShouldIgnoreMovementCollision(
                            collider,
                            center,
                            halfExtents,
                            Quaternion.identity))
                        continue;
                    if (!Physics.ComputePenetration(
                            _sourceHullCollider,
                            position,
                            Quaternion.identity,
                            collider,
                            collider.transform.position,
                            collider.transform.rotation,
                            out Vector3 direction,
                            out float distance))
                        continue;

                    float tolerance = GetContactPenetrationTolerance(collider);
                    if (distance <= tolerance)
                        continue;

                    push += direction * (distance - tolerance + CollisionSkin);
                    blocker = collider;
                }

                if (push.sqrMagnitude < 0.0000001f)
                    break;
                // Cap one step so a degenerate separation vector cannot fling
                // the hull across the map; four steps still clear a deep embed.
                if (push.magnitude > MaxStuckDepenetration)
                    push = push.normalized * MaxStuckDepenetration;
                position += push;
                moved = true;
            }

            if (!moved)
                return false;
            if (IsHullStuckAt(position, halfExtents))
                return false;

            freed = position;
            return true;
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
            Vector3 center = _surfer.MoveData.playerTransform.position +
                up * GetBoxHalfExtents(_isCrouched).y;
            int hitCount = BoxCastNonAlloc(
                center,
                GetQueryHalfExtents(GetBoxHalfExtents(_isCrouched)),
                probeDirection,
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
                    Portal.ShouldIgnoreMovementCollision(
                        candidate,
                        _lastBoxQueryCenter,
                        _lastBoxQueryHalfExtents +
                            Vector3.one * CollisionSkin,
                        _lastBoxQueryRotation,
                        _lastBoxQuerySweepDelta,
                        GetQueryHalfExtents(GetBoxHalfExtents(true)),
                        _movementUp) ||
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
            int upHitCount = BoxCastNonAlloc(
                originalCenter,
                GetQueryHalfExtents(halfExtents),
                _movementUp,
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

            int hitCount = BoxCastNonAlloc(
                startCenter,
                GetQueryHalfExtents(halfExtents),
                downDelta / traceDistance,
                Quaternion.identity,
                traceDistance,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);

            if (!TryGetNearestHit(
                    hitCount,
                    out RaycastHit groundHit,
                    out bool contactedPortalRamp))
                return;

            float groundNormal = Vector3.Dot(
                groundHit.normal, _movementUp);
            if (groundNormal < WalkableNormal &&
                !(contactedPortalRamp && groundNormal > 0f))
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
            int count = Physics.OverlapBoxNonAlloc(
                center,
                GetQueryHalfExtents(halfExtents),
                _sourceVolumeOverlaps,
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
            int count = Physics.OverlapBoxNonAlloc(
                center,
                GetQueryHalfExtents(halfExtents),
                _sourceVolumeOverlaps,
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
            int startCount = Physics.OverlapBoxNonAlloc(
                startCenter,
                GetQueryHalfExtents(halfExtents),
                _sourceVolumeOverlaps,
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
                    GetQueryHalfExtents(halfExtents),
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

        private bool TryRecoverPortalExit(
            Portal exitPortal,
            ref Vector3 rootPosition,
            Vector3 halfExtents)
        {
            if (exitPortal == null)
                return false;

            // The layer collision matrix is constant during this synchronous
            // recovery, so cache its filtered mask across the up-to-100 probes.
            int exitCollisionMask = GetCollisionMask();

            // The landing clamp fits the hull to the exit aperture, but the
            // position that ends up placed can still straddle the rim: the
            // exit gate validates the crossing point, while the landing is the
            // end of the frame, and the two differ by the tangential travel
            // left in it. A slope exit is almost pure tangential travel, and a
            // wall exit pressed to one side has the same problem.
            //
            // The physics test below cannot see this - stuck recovery
            // deliberately ignores portal hosts, so a hull resting on the rim
            // is not zeroed every command - which means a straddling landing
            // is invisible to it and can never be repaired. Fit the geometry
            // first, then confirm against the host that it actually freed the
            // hull before claiming success.
            Vector3 landingCenter = rootPosition + _movementUp * halfExtents.y;
            if (Portal.TryFitHullIntoPortalOpening(
                    exitPortal, landingCenter, halfExtents,
                    out Vector3 fittedCenter))
            {
                Vector3 fittedRoot =
                    rootPosition + (fittedCenter - landingCenter);
                if (!IsHullPenetratingCollider(
                        exitPortal.WallCollider, fittedRoot) &&
                    !IsHullStuckAt(
                        fittedRoot, halfExtents, out _, true,
                        exitCollisionMask))
                {
                    rootPosition = fittedRoot;
                    return true;
                }
            }

            // Normal movement recovery tolerates Unity's contact-offset shell
            // so resting against a wall does not freeze the player. A portal
            // exit needs a stricter check: the mapped pose can begin a few
            // millimetres inside a cube at an edge, and the inset movement
            // hull then starts inside the collider without producing a useful
            // sweep hit. Keep the same tolerance for ordinary movement.
            if (!IsHullStuckAt(
                    rootPosition, halfExtents, out _, true,
                    exitCollisionMask))
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
            if (!IsHullStuckAt(
                    centeredRoot, halfExtents, out _, true,
                    exitCollisionMask))
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

                if (!IsHullStuckAt(
                        candidate, halfExtents, out _, true,
                        exitCollisionMask))
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
            if (!IsHullStuckAt(
                    playerTransform.position, halfExtents, out Collider blocker))
            {
                // An open mesh traps the hull without ever tripping the
                // penetration test above; see TryEscapeEmbeddedMeshShell.
                if (TryEscapeEmbeddedMeshShell(
                        playerTransform.position, halfExtents))
                    return false;

                _stuckOffsetIndex = 0;
                return false;
            }

            // DIAGNOSTIC ONLY, no behaviour change. Every command that gets
            // here is aborted before TryHandlePlayerMovement runs, so the
            // portal is never even asked to fire. Until this line existed that
            // case produced no log output whatsoever, which is why a hull
            // stuck inside a portal aperture looked exactly like "the portal
            // silently did nothing".
            LogStuckBlock(playerTransform.position, halfExtents, blocker);

            // CGameMovement::CheckStuck rate-limits retries to 0.05 seconds,
            // tests exactly one entry from the 54-offset Source table per
            // accepted attempt, and leaves velocity untouched when it fails.
            if (_elapsed - _lastStuckCheckTime < 0.05f)
                return true;

            _lastStuckCheckTime = _elapsed;

            // A carved portal host is the one stuck case the offset table is
            // bad at: the hull has to slide into the aperture, which can be a
            // long way laterally, and Source's few-Hammer-unit probes would
            // need dozens of attempts to get there. The opening is known, so
            // fit it directly instead.
            Vector3 hullCenter =
                playerTransform.position + _movementUp * halfExtents.y;
            if (Portal.TryFitHullOutOfCarvedPortalHost(
                    _sourceHullCollider,
                    playerTransform.position,
                    hullCenter,
                    halfExtents,
                    out Vector3 fittedCenter))
            {
                Vector3 fittedRoot =
                    playerTransform.position + (fittedCenter - hullCenter);
                if (!IsHullStuckAt(fittedRoot, halfExtents))
                {
                    playerTransform.position = fittedRoot;
                    _surfer.MoveData.Origin = fittedRoot;
                    Physics.SyncTransforms();
                    _stuckOffsetIndex = 0;
                    return false;
                }
            }

            // Source's probe table cannot clear a deep embed: its largest step
            // is 6 HU, so a hull intersecting a MeshCollider retries every
            // offset forever and the player stays frozen. Ask the engine for
            // the separation vector instead, before falling back to the table.
            if (TryDepenetrateHull(
                    playerTransform.position,
                    halfExtents,
                    out Vector3 freedRoot,
                    out Collider freedBlocker))
            {
                Debug.Log("[GokzStuck] depenetrated the hull by " +
                    (freedRoot - playerTransform.position).magnitude
                        .ToString("F3") + " UU out of " +
                    (freedBlocker != null ? freedBlocker.name : "unknown"));

                playerTransform.position = freedRoot;
                _surfer.MoveData.Origin = freedRoot;
                Physics.SyncTransforms();
                _stuckOffsetIndex = 0;
                return false;
            }

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
            int hitCount = BoxCastNonAlloc(
                center,
                GetQueryHalfExtents(duckHalfExtents),
                -_movementUp,
                Quaternion.identity,
                MaxVelocity,
                GetCollisionMask(),
                QueryTriggerInteraction.Ignore);
            // gokz-mode-kztimer.sp uses MASK_PLAYERSOLID_BRUSHONLY and its
            // trace filter excludes every client. Unity has no Source CONTENTS
            // mask, so map those exclusions to this project's Player/Enemy
            // tags while retaining movable world colliders in the query.
            if (!TryGetNearestBrushOnlyHit(hitCount, out RaycastHit slopeHit))
                return;

            float groundUpDot = Vector3.Dot(slopeHit.normal, _movementUp);
            if (groundUpDot < WalkableNormal || groundUpDot >= 0.999999f)
                return;

            Vector3 originalHorizontalVelocity = Vector3.ProjectOnPlane(
                landingVelocity,
                _movementUp);
            Vector3 clipped = ClipLandingVelocity(
                landingVelocity,
                slopeHit.normal,
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

        private bool TryGetNearestBrushOnlyHit(
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
                    Portal.ShouldIgnoreMovementCollision(
                        candidate,
                        _lastBoxQueryCenter,
                        _lastBoxQueryHalfExtents +
                            Vector3.one * CollisionSkin,
                        _lastBoxQueryRotation,
                        _lastBoxQuerySweepDelta) ||
                    (_surfer != null &&
                     _surfer.MoveData.playerTransform != null &&
                     candidate.collider.transform.IsChildOf(
                         _surfer.MoveData.playerTransform)) ||
                    IsSourceMonsterCollider(candidate.collider) ||
                    candidate.distance >= nearestDistance)
                    continue;

                nearest = candidate;
                nearestDistance = candidate.distance;
                found = true;
            }

            return found;
        }

        private static bool IsSourceMonsterCollider(Collider collider)
        {
            for (Transform current = collider != null
                     ? collider.transform
                     : null;
                 current != null;
                 current = current.parent)
            {
                // These tags exist in this LUO project. Source's brush-only
                // movement mask excludes CONTENTS_MONSTER, which covers
                // players and NPCs; the tags are the closest project mapping.
                if (current.CompareTag("Player") ||
                    current.CompareTag("Enemy"))
                    return true;
            }

            return false;
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
            out RaycastHit hit)
        {
            hit = default;
            Vector3 delta = endCenter - startCenter;
            float distance = delta.magnitude;
            if (distance <= CollisionSkin)
                return false;

            int hitCount = BoxCastNonAlloc(
                startCenter,
                GetQueryHalfExtents(halfExtents),
                delta / distance,
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
            int hitCount = BoxCastNonAlloc(
                center,
                GetQueryHalfExtents(GetBoxHalfExtents(_isCrouched)),
                -_movementUp,
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
