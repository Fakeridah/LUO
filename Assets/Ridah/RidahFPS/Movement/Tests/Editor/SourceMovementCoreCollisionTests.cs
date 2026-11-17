using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RidahFPS.Controllers.Tests
{
    public sealed class SourceMovementCoreCollisionTests
    {
        private GameObject _playerObject;
        private GameObject _floorObject;
        private CharacterController _characterController;
        private CapsuleCollider _capsuleCollider;
        private TestSurfer _surfer;
        private SourceMovementCore _core;
        private GameObject _extraObject;

        [SetUp]
        public void SetUp()
        {
            _floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floorObject.name = "GokzTestFloor";
            _floorObject.transform.position = new Vector3(0f, -0.05f, 0f);
            _floorObject.transform.localScale = new Vector3(20f, 0.1f, 20f);

            _playerObject = new GameObject("GokzTestPlayer");
            _characterController = _playerObject.AddComponent<CharacterController>();
            _capsuleCollider = _playerObject.AddComponent<CapsuleCollider>();
            _capsuleCollider.enabled = false;

            MoveData moveData = new MoveData
            {
                playerTransform = _playerObject.transform,
                Origin = _playerObject.transform.position,
                isGravity = true,
                reverseGravity = 1f,
                laddersEnabled = false,
                verticalAxis = 1f,
                horizontalAxis = 0f,
                moveForward = true,
                moveBack = false,
                moveLeft = false,
                moveRight = false
            };
            _surfer = new TestSurfer(_characterController, _capsuleCollider, moveData);
            _core = new SourceMovementCore();
            _core.EnableSourceCollision(_surfer);
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null)
                Object.DestroyImmediate(_playerObject);
            if (_floorObject != null)
                Object.DestroyImmediate(_floorObject);
            if (_extraObject != null)
                Object.DestroyImmediate(_extraObject);
        }

        [Test]
        public void GroundMovementDoesNotExceedSourceMaxSpeed()
        {
            for (int tick = 0; tick < 128; tick++)
                Tick();

            float speedHu = HorizontalSpeedHu();
            Assert.That(_core.Grounded, Is.True);
            Assert.That(speedHu, Is.GreaterThan(230f));
            Assert.That(speedHu, Is.LessThanOrEqualTo(250.01f));
        }

        [Test]
        public void AnalogCommandUsesSource450HuClientSpeedBeforeCrop()
        {
            _surfer.MoveData.verticalAxis = 0.5f;
            _surfer.MoveData.moveForward = false;

            for (int tick = 0; tick < 32; tick++)
                Tick();

            // 0.5 * cl_forwardspeed (450) is 225 HU/s. The normalized
            // 320-HU shortcut would incorrectly settle at 160 HU/s.
            Assert.That(HorizontalSpeedHu(), Is.GreaterThan(210f));
            Assert.That(HorizontalSpeedHu(), Is.LessThanOrEqualTo(225.01f));
        }

        [Test]
        public void JumpGetsSourceImpulseAndGravity()
        {
            _surfer.MoveData.wishJump = true;
            Tick();
            _surfer.MoveData.wishJump = false;

            float initialVerticalHu = _surfer.MoveData.Velocity.y *
                GokzMovementRules.HammerUnitsPerUnityUnit;
            Assert.That(initialVerticalHu, Is.EqualTo(292.618f).Within(0.02f));
            Assert.That(_core.Grounded, Is.False);

            for (int tick = 0; tick < 20; tick++)
                Tick();

            float currentVerticalHu = _surfer.MoveData.Velocity.y *
                GokzMovementRules.HammerUnitsPerUnityUnit;
            Assert.That(currentVerticalHu, Is.LessThan(initialVerticalHu));
            Assert.That(_playerObject.transform.position.y, Is.GreaterThan(0.1f));
        }

        [Test]
        public void HoldingJumpDoesNotPogoWithoutSourceAutoBhop()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.jumpHeld = true;
            _surfer.MoveData.wishJump = true;

            Tick();
            _surfer.MoveData.wishJump = false;

            for (int tick = 0; tick < 128 && !_core.Grounded; tick++)
                Tick();

            Assert.That(_core.Grounded, Is.True);
            Assert.That(_core.JumpCount, Is.EqualTo(1));

            // CCSGameMovement checks the previous IN_JUMP bit, so a held
            // space key cannot trigger another jump when sv_autobunnyhopping
            // is disabled.
            Tick();
            Assert.That(_core.JumpCount, Is.EqualTo(1));
        }

        [Test]
        public void ZeroHorizontalJumpFallsThroughTheApexInsteadOfHanging()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.Velocity = Vector3.zero;
            _surfer.MoveData.wishJump = true;

            Tick();
            _surfer.MoveData.wishJump = false;

            float startY = _playerObject.transform.position.y;
            float highestY = startY;
            for (int tick = 0; tick < 96; tick++)
            {
                Tick();
                highestY = Mathf.Max(highestY, _playerObject.transform.position.y);
            }

            Assert.That(highestY, Is.GreaterThan(startY + 0.5f),
                $"startY={startY}, highestY={highestY}, finalY={_playerObject.transform.position.y}, velocity={_surfer.MoveData.Velocity}");
            Assert.That(_core.Grounded, Is.True,
                $"highestY={highestY}, finalY={_playerObject.transform.position.y}, velocity={_surfer.MoveData.Velocity}");
            Assert.That(_playerObject.transform.position.y, Is.LessThanOrEqualTo(0.02f),
                $"startY={startY}, highestY={highestY}, finalY={_playerObject.transform.position.y}, velocity={_surfer.MoveData.Velocity}");
        }

        [Test]
        public void FeetWaterStillAppliesGravityLikeSourceFullWalkMove()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.waterLevel = GokzWaterLevel.Feet;
            _surfer.MoveData.underwater = false;
            _surfer.MoveData.Velocity = Vector3.zero;
            _playerObject.transform.position = new Vector3(0f, 0.2f, 0f);
            _surfer.MoveData.Origin = _playerObject.transform.position;

            Tick();

            // Source CheckWater() returns false at WL_Feet. FullWalkMove
            // therefore applies StartGravity and FinishGravity, while still
            // using the ordinary WalkMove/AirMove branch instead of
            // WaterMove().
            float expectedVerticalHu = -800f / 128f;
            Assert.That(
                _surfer.MoveData.Velocity.y *
                    GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.EqualTo(expectedVerticalHu).Within(0.02f));
            Assert.That(_playerObject.transform.position.y, Is.LessThan(0.2f));
        }

        [Test]
        public void SimpleKzHeldJumpCountsAsRecentJumpInput()
        {
            _core.SetMode(GokzMode.SimpleKz);
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.jumpHeld = false;
            _surfer.MoveData.wishJump = true;

            Tick();
            _surfer.MoveData.wishJump = false;

            bool sentRecentHeldJumpInput = false;
            for (int tick = 0; tick < 128 && !_core.Grounded; tick++)
            {
                float verticalSpeedHu = _surfer.MoveData.Velocity.y *
                    GokzMovementRules.HammerUnitsPerUnityUnit;
                if (!sentRecentHeldJumpInput && verticalSpeedHu < -100f &&
                    _playerObject.transform.position.y < 0.35f)
                {
                    // This reproduces a held-space command in the last few
                    // airborne ticks, followed by releasing the key before
                    // the landing command itself.
                    _surfer.MoveData.jumpHeld = true;
                    Tick();
                    _surfer.MoveData.jumpHeld = false;
                    sentRecentHeldJumpInput = true;
                }
                else
                {
                    Tick();
                }
            }

            Assert.That(_core.Grounded, Is.True);
            Assert.That(sentRecentHeldJumpInput, Is.True);
            _surfer.MoveData.Velocity = Vector3.forward *
                (300f / GokzMovementRules.HammerUnitsPerUnityUnit);
            _surfer.MoveData.wishJump = true;
            Tick();

            // The held IN_JUMP state is recorded by SimpleKZ's
            // OnPlayerRunCmdPost equivalent, so this landing-window jump is
            // marked as a perf jump even though the one-shot request arrived
            // on the landing command.
            Assert.That(_core.LastJumpWasPerfect, Is.True);
        }

        [Test]
        public void CrouchedPlayerCanAccelerateFromZero()
        {
            _surfer.MoveData.verticalAxis = 1f;
            _surfer.MoveData.moveForward = true;
            _surfer.MoveData.Velocity = Vector3.zero;
            _surfer.MoveData.crouching = true;

            Tick();

            Assert.That(HorizontalSpeedHu(), Is.GreaterThan(0f));
            Assert.That(_core.DuckAmount, Is.GreaterThan(0f));
        }

        [Test]
        public void NonPerfTakeoffDoesNotUseThePerfCap()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.Velocity = Vector3.right * (300f /
                GokzMovementRules.HammerUnitsPerUnityUnit);
            _surfer.MoveData.wishJump = true;

            Tick();

            Assert.That(_core.LastJumpWasPerfect, Is.False);
            Assert.That(_core.LastTakeoffSpeed, Is.EqualTo(300f).Within(0.01f));
        }

        [Test]
        public void HoldingForwardAfterLandingDoesNotFrictionStopThePlayer()
        {
            _surfer.MoveData.verticalAxis = 1f;
            _surfer.MoveData.moveForward = true;
            _surfer.MoveData.Velocity = Vector3.forward * (250f /
                GokzMovementRules.HammerUnitsPerUnityUnit);
            _surfer.MoveData.wishJump = true;

            Tick();
            _surfer.MoveData.wishJump = false;
            for (int tick = 0; tick < 128 && !_core.Grounded; tick++)
                Tick();

            Assert.That(_core.Grounded, Is.True);
            for (int tick = 0; tick < 32; tick++)
                Tick();

            Assert.That(HorizontalSpeedHu(), Is.GreaterThan(240f));
        }

        [Test]
        public void HoldingForwardAndDuckThroughLandingDoesNotStopThePlayer()
        {
            _surfer.MoveData.verticalAxis = 1f;
            _surfer.MoveData.moveForward = true;
            _surfer.MoveData.crouching = true;
            _surfer.MoveData.Velocity = Vector3.forward * (250f /
                GokzMovementRules.HammerUnitsPerUnityUnit);
            _surfer.MoveData.wishJump = true;

            Tick();
            _surfer.MoveData.wishJump = false;
            for (int tick = 0; tick < 128 && !_core.Grounded; tick++)
                Tick();

            Assert.That(_core.Grounded, Is.True);
            float landingSpeed = HorizontalSpeedHu();
            Tick();

            Assert.That(landingSpeed, Is.GreaterThan(200f));
            Assert.That(HorizontalSpeedHu(), Is.GreaterThan(180f));
        }

        [Test]
        public void DuckingOnLandingDoesNotInstantlyZeroHorizontalVelocity()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.crouching = true;
            _surfer.MoveData.Velocity = Vector3.forward * (250f /
                GokzMovementRules.HammerUnitsPerUnityUnit);
            _surfer.MoveData.wishJump = true;

            Tick();
            _surfer.MoveData.wishJump = false;
            for (int tick = 0; tick < 128 && !_core.Grounded; tick++)
                Tick();

            Assert.That(_core.Grounded, Is.True);
            float landingSpeed = HorizontalSpeedHu();
            Tick();

            Assert.That(landingSpeed, Is.GreaterThan(200f));
            Assert.That(HorizontalSpeedHu(), Is.GreaterThan(180f));
        }

        [Test]
        public void PressingDuckDuringDescentUsesTheCSGoDuckSpeedCrop()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.crouching = false;
            _surfer.MoveData.Velocity = Vector3.forward * (250f /
                GokzMovementRules.HammerUnitsPerUnityUnit);
            _surfer.MoveData.wishJump = true;

            Tick();
            _surfer.MoveData.wishJump = false;
            bool pressedDuck = false;
            for (int tick = 0; tick < 128 && !_core.Grounded; tick++)
            {
                float verticalSpeedHu = _surfer.MoveData.Velocity.y *
                    GokzMovementRules.HammerUnitsPerUnityUnit;
                if (!pressedDuck && verticalSpeedHu < -100f &&
                    _playerObject.transform.position.y < 0.35f)
                {
                    _surfer.MoveData.crouching = true;
                    pressedDuck = true;
                }
                Tick();
            }

            Assert.That(pressedDuck, Is.True);
            Assert.That(_core.Grounded, Is.True);
            Assert.That(HorizontalSpeedHu(), Is.GreaterThan(200f));
            Tick();
            // CCSGameMovement::HandleDuckingSpeedCrop + WalkMove's final
            // maxspeed clamp make a held duck command 0.34 * 250 = 85 HU/s.
            Assert.That(HorizontalSpeedHu(), Is.EqualTo(85f).Within(0.01f));
        }

        [Test]
        public void AirborneDuckFinishesTheCSGoDuckAndMovesTheEye()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.crouching = false;
            _surfer.MoveData.wishJump = true;

            Tick();
            _surfer.MoveData.wishJump = false;
            float eyeBeforeAirDuck = _core.EyeDuckAmount;

            _surfer.MoveData.crouching = true;
            Tick();

            BoxCollider hull = _playerObject.transform.Find("GokzSourceHull")
                .GetComponent<BoxCollider>();
            Assert.That(_core.Grounded, Is.False);
            Assert.That(_core.Crouched, Is.True);
            Assert.That(hull.size.y * GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.EqualTo(54f).Within(0.01f));
            Assert.That(_core.EyeDuckAmount, Is.EqualTo(1f).Within(0.000001f));
            Assert.That(_core.EyeDuckAmount, Is.GreaterThan(eyeBeforeAirDuck));
        }

        [Test]
        public void CrouchUses54HuHullAndCannotUnduckUnderCeiling()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.crouching = true;
            Tick();

            BoxCollider hull = _playerObject.transform.Find("GokzSourceHull")
                .GetComponent<BoxCollider>();
            Assert.That(_core.DuckAmount, Is.GreaterThan(0f));
            Assert.That(_core.DuckAmount, Is.LessThan(1f));
            Assert.That(hull.size.y * GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.EqualTo(72f).Within(0.01f));

            for (int tick = 0; tick < 32 && !_core.Crouched; tick++)
                Tick();

            Assert.That(hull.size.y * GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.EqualTo(54f).Within(0.01f));
            Assert.That(_core.Crouched, Is.True);

            GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.position = new Vector3(0f, 1.08f, 0f);
            ceiling.transform.localScale = new Vector3(4f, 0.1f, 4f);
            try
            {
                _surfer.MoveData.crouching = false;
                Tick();
                Assert.That(_core.Crouched, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(ceiling);
            }
        }

        [Test]
        public void EmbeddedHullIsRecoveredOnTheNextSourceCommand()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.horizontalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.moveBack = false;
            _surfer.MoveData.moveLeft = false;
            _surfer.MoveData.moveRight = false;

            _extraObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _extraObject.name = "GokzEmbeddedHullBlock";
            _extraObject.transform.position = new Vector3(0.2f, 0.7f, 0f);
            _extraObject.transform.localScale = new Vector3(0.5f, 1.4f, 2f);
            Physics.SyncTransforms();

            // The first command must use Source's stuck recovery offsets
            // immediately. A one-second recovery throttle would leave the
            // player with zero velocity and airborne for many ticks.
            Tick();
            Assert.That(
                _playerObject.transform.position.x,
                Is.LessThan(-0.05f).Or.GreaterThan(0.45f),
                $"position={_playerObject.transform.position}, " +
                $"velocity={_surfer.MoveData.Velocity}, " +
                $"grounded={_core.Grounded}");
            Assert.That(
                _core.Grounded,
                Is.True,
                $"position={_playerObject.transform.position}, " +
                $"velocity={_surfer.MoveData.Velocity}");
        }

        [Test]
        public void ReverseGravityCategorizesTheCeilingAsGround()
        {
            _extraObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _extraObject.transform.position = new Vector3(0f, 2.05f, 0f);
            _extraObject.transform.localScale = new Vector3(4f, 0.1f, 4f);
            _playerObject.transform.position = new Vector3(0f, 2f, 0f);
            _surfer.MoveData.reverseGravity = -1f;
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;

            Tick();

            Assert.That(_core.Grounded, Is.True);
            Assert.That(_surfer.MoveData.grounded, Is.True);
        }

        [Test]
        public void TransformDrivenGroundCarriesSourceBaseVelocity()
        {
            Object.DestroyImmediate(_floorObject);
            _floorObject = null;

            _extraObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _extraObject.transform.position = new Vector3(0f, -0.05f, 0f);
            _extraObject.transform.localScale = new Vector3(4f, 0.1f, 4f);

            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.horizontalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.moveBack = false;
            _surfer.MoveData.moveLeft = false;
            _surfer.MoveData.moveRight = false;
            _surfer.MoveData.ForwardMove = 0f;
            _surfer.MoveData.SideMove = 0f;

            // First command attaches the Source hull to the platform and
            // establishes the transform-velocity sample.
            Tick();
            float startX = _playerObject.transform.position.x;
            float platformSpeed = 100f /
                GokzMovementRules.HammerUnitsPerUnityUnit;

            for (int tick = 0; tick < 16; tick++)
            {
                _extraObject.transform.position +=
                    Vector3.right * platformSpeed * SourceMovementCore.TickInterval;
                Physics.SyncTransforms();
                Tick();
            }

            // The relative Source velocity remains zero, while the world
            // position is carried by the moving ground entity.
            Assert.That(_playerObject.transform.position.x,
                Is.GreaterThan(startX + 0.15f));
            Assert.That(HorizontalSpeedHu(), Is.LessThan(0.1f));
        }

        [Test]
        public void LadderUsesSourceClimbSpeedAndMarksLadderState()
        {
            _extraObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _extraObject.transform.position = new Vector3(0f, 0.7f, 0.38f);
            _extraObject.transform.localScale = new Vector3(2f, 1.4f, 0.1f);
            _extraObject.AddComponent<GokzLadder>();

            _playerObject.transform.position = new Vector3(0f, 0.2f, 0f);
            _surfer.MoveData.Origin = _playerObject.transform.position;

            _surfer.MoveData.laddersEnabled = true;
            _surfer.MoveData.verticalAxis = 1f;
            _surfer.MoveData.moveForward = true;

            Tick();

            Assert.That(_surfer.MoveData.climbingLadder, Is.True);
            Assert.That(
                _surfer.MoveData.Velocity.y * GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.EqualTo(200f).Within(0.1f));
        }

        [Test]
        public void LadderJumpReturnsToWalkMovementInTheSameTick()
        {
            _extraObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _extraObject.transform.position = new Vector3(0f, 0.7f, 0.38f);
            _extraObject.transform.localScale = new Vector3(2f, 1.4f, 0.1f);
            _extraObject.AddComponent<GokzLadder>();

            _surfer.MoveData.laddersEnabled = true;
            _surfer.MoveData.verticalAxis = 1f;
            _surfer.MoveData.moveForward = true;
            Tick();
            Assert.That(_surfer.MoveData.climbingLadder, Is.True);

            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            for (int tick = 0; tick < 30; tick++)
                Tick();

            _surfer.MoveData.wishJump = true;
            _surfer.MoveData.jumpHeld = true;
            Tick();

            // Source changes MOVETYPE_LADDER to WALK on the ladder jump,
            // then FullWalkMove applies its two gravity half-steps in the
            // same command instead of swallowing the tick.
            Assert.That(_surfer.MoveData.climbingLadder, Is.False);
            Assert.That(
                _core.MovementVelocity.y *
                    GokzMovementRules.HammerUnitsPerUnityUnit,
                Is.LessThan(0f));
        }

        [Test]
        public void PushVolumeMovesPlayerThroughSourceBaseVelocity()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.horizontalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.moveBack = false;
            _surfer.MoveData.moveLeft = false;
            _surfer.MoveData.moveRight = false;

            _extraObject = new GameObject("GokzPushVolumeTest");
            _extraObject.transform.position = new Vector3(0f, 3f, 0f);
            BoxCollider pushCollider =
                _extraObject.AddComponent<BoxCollider>();
            pushCollider.size = new Vector3(10f, 10f, 10f);
            pushCollider.isTrigger = true;
            GokzMovementVolume pushVolume =
                _extraObject.AddComponent<GokzMovementVolume>();

            SerializedObject serialized = new SerializedObject(pushVolume);
            serialized.FindProperty("movementType").enumValueIndex =
                (int)GokzMovementVolumeType.Push;
            serialized.FindProperty("localPushDirection").vector3Value =
                Vector3.right;
            serialized.FindProperty("pushSpeedHU").floatValue = 400f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();

            float startX = _playerObject.transform.position.x;
            for (int tick = 0; tick < 32; tick++)
                Tick();

            Assert.That(
                _playerObject.transform.position.x,
                Is.GreaterThan(startX + 0.5f));
        }

        [Test]
        public void HorizontalPushBecomesPlayerMomentumAfterLeavingTrigger()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.horizontalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.moveBack = false;
            _surfer.MoveData.moveLeft = false;
            _surfer.MoveData.moveRight = false;

            _extraObject = new GameObject("GokzPushMomentumTest");
            _extraObject.transform.position = new Vector3(0.1f, 3f, 0f);
            BoxCollider pushCollider = _extraObject.AddComponent<BoxCollider>();
            pushCollider.size = new Vector3(0.4f, 10f, 10f);
            pushCollider.isTrigger = true;
            GokzMovementVolume pushVolume =
                _extraObject.AddComponent<GokzMovementVolume>();

            SerializedObject serialized = new SerializedObject(pushVolume);
            serialized.FindProperty("movementType").enumValueIndex =
                (int)GokzMovementVolumeType.Push;
            serialized.FindProperty("localPushDirection").vector3Value =
                Vector3.right;
            serialized.FindProperty("pushSpeedHU").floatValue = 400f;
            serialized.FindProperty("pushOnce").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();

            bool leftTrigger = false;
            for (int tick = 0; tick < 64 && !leftTrigger; tick++)
            {
                Tick();
                leftTrigger = _playerObject.transform.position.x > 0.7f;
            }

            // The player has just left the narrow trigger. Source's next
            // command transfers the remaining BaseVelocity into real player
            // velocity; a trigger-only implementation leaves this at zero.
            Assert.That(leftTrigger, Is.True);
            Assert.That(
                HorizontalSpeedHu(),
                Is.GreaterThan(200f),
                $"x={_playerObject.transform.position.x}, " +
                $"velocity={_surfer.MoveData.Velocity}, " +
                $"sourceBase={_core.SourceTriggerBaseVelocity}");
        }

        [Test]
        public void UpwardPushOnceVolumeDetachesGroundEntityLikeSource()
        {
            _surfer.MoveData.verticalAxis = 0f;
            _surfer.MoveData.horizontalAxis = 0f;
            _surfer.MoveData.moveForward = false;
            _surfer.MoveData.moveBack = false;
            _surfer.MoveData.moveLeft = false;
            _surfer.MoveData.moveRight = false;

            _extraObject = new GameObject("GokzUpwardPushVolumeTest");
            _extraObject.transform.position = new Vector3(0f, 3f, 0f);
            BoxCollider pushCollider =
                _extraObject.AddComponent<BoxCollider>();
            pushCollider.size = new Vector3(10f, 10f, 10f);
            pushCollider.isTrigger = true;
            GokzMovementVolume pushVolume =
                _extraObject.AddComponent<GokzMovementVolume>();

            SerializedObject serialized = new SerializedObject(pushVolume);
            serialized.FindProperty("movementType").enumValueIndex =
                (int)GokzMovementVolumeType.Push;
            serialized.FindProperty("localPushDirection").vector3Value =
                Vector3.up;
            // Source's SF_TRIG_PUSH_ONCE branch calls
            // ApplyAbsVelocityImpulse directly. This is the jump-pad case;
            // the ordinary branch is covered by the horizontal BaseVelocity
            // test above.
            serialized.FindProperty("pushSpeedHU").floatValue = 400f;
            serialized.FindProperty("pushOnce").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();

            float startY = _playerObject.transform.position.y;
            for (int tick = 0; tick < 16; tick++)
                Tick();

            Assert.That(
                _playerObject.transform.position.y,
                Is.GreaterThan(startY + 0.1f),
                $"startY={startY}, finalY={_playerObject.transform.position.y}, " +
                $"velocity={_surfer.MoveData.Velocity}, grounded={_core.Grounded}, " +
                $"sourceBase={_core.SourceTriggerBaseVelocity}");
        }

        private void Tick()
        {
            _core.Process(_surfer);
            _core.Move(_surfer);
            _core.Finish(_surfer);
            _surfer.MoveData.Origin = _playerObject.transform.position;
        }

        private float HorizontalSpeedHu()
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(
                _surfer.MoveData.Velocity,
                Vector3.up);
            return horizontal.magnitude * GokzMovementRules.HammerUnitsPerUnityUnit;
        }

        private sealed class TestSurfer : ISurfControllable
        {
            public TestSurfer(
                CharacterController characterController,
                CapsuleCollider capsuleCollider,
                MoveData moveData)
            {
                CC = characterController;
                Collider = capsuleCollider;
                MoveData = moveData;
            }

            public MoveType MoveType => MoveType.Walk;
            public MoveData MoveData { get; }
            public CharacterController CC { get; }
            public CapsuleCollider Collider { get; }
            public Vector3 Forward => Vector3.forward;
            public Vector3 Right => Vector3.right;
            public Vector3 Up => Vector3.up;
            public Vector3 BaseVelocity => Vector3.zero;
        }
    }
}
