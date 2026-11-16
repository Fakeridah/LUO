using UnityEngine;
using RidahFPS.TraceUtil;
namespace RidahFPS.Controllers
{
    public class SurfController
    {
        ///// Fields /////
        [HideInInspector] public Transform playerTransform;
        private ISurfControllable _surfer;
        private MovementConfig _config;
        private float _deltaTime;
        internal bool _Grounded;

        public bool crouching = false;
        public float speed = 0f;

        //public Transform camera;
        //public float cameraYPos = 0f;

        private float slideSpeedCurrent = 0f;
        private Vector3 slideDirection = Vector3.forward;

        private bool sliding = false;
        private bool wasSliding = false;
        private float slideDelay = 0f;

        internal float crouchLerp = 0f;
        internal bool CrouchUp = true;

        private float frictionMult = 1f;

        //t for temporarily changing parma between default and other kind of state
        private float t_Friction = 0f;
        private float t_iceAcel = 1f;
        private Vector3 t_HitPoint;
        private Transform t_Transform = null;
        private float t_JumpPower = 5.112f ;
        private bool isSlope = false;

        private readonly SourceMovementCore _gokzMovement = new SourceMovementCore();

        // GoKZ state. The original movement path is intentionally kept below as
        // a reference/fallback; this state belongs only to the new path.
        private bool _gokzGrounded;
        private Vector3 _gokzGroundNormal = Vector3.up;
        private RaycastHit _gokzGroundHit;
        private float _gokzBouncerJumpPower;

        ///// Methods /////
        Vector3 groundNormal = Vector3.up;

        /// <summary>
        /// 
        /// </summary>
       
        // Keep the old three-argument entry point for existing callers.
        public void ProcessMovement(ISurfControllable surfer, MovementConfig config, float deltaTime)
        {
            ProcessMovement(surfer, config, deltaTime, false);
        }

        public void ProcessMovement(ISurfControllable surfer, MovementConfig config, float deltaTime, bool useGoKzMovement)
        {
            if (useGoKzMovement)
            {
                ProcessGoKzMovement(surfer, config, deltaTime);
                return;
            }

            ProcessMovementLegacy(surfer, config, deltaTime);
        }

        /// <summary>
        /// GoKZ-style movement: friction and ground acceleration are separated
        /// from air acceleration, and gravity is expressed along the active up
        /// direction so reverse-gravity volumes use the same rules.
        /// </summary>
        private void ProcessGoKzMovement(ISurfControllable surfer, MovementConfig config, float deltaTime)
        {
            // The active KZTimer implementation is self-contained and does
            // not read MovementConfig. The former hybrid implementation below
            // remains in this file as a legacy reference only.
            _deltaTime = GokzKzTimerMovement.TickInterval;
            GokzRuntime runtime = GokzRuntime.Instance;
            _gokzMovement.SetMode(runtime != null ? runtime.Mode : GokzMode.KzTimer);
            _gokzMovement.Process(surfer);
            speed = _gokzMovement.Speed;
        }

        public Vector3 GetGoKzMovementVelocity()
        {
            return _gokzMovement.MovementVelocity;
        }

        public bool GoKzGrounded => _gokzMovement.Grounded;
        public bool GoKzCrouched => _gokzMovement.Crouched;
        public float GoKzDuckAmount => _gokzMovement.DuckAmount;
        public float GoKzEyeDuckAmount => _gokzMovement.EyeDuckAmount;
        public bool GoKzLastJumpWasPerfect => _gokzMovement.LastJumpWasPerfect;
        public bool GoKzLastJumpWasDuck => _gokzMovement.LastJumpWasDuck;
        public int GoKzJumpCount => _gokzMovement.JumpCount;
        public float GoKzLastTakeoffSpeed => _gokzMovement.LastTakeoffSpeed;

        public void EnableGoKzCollision(ISurfControllable surfer)
        {
            _gokzMovement.EnableSourceCollision(surfer);
        }

        public void DisableGoKzCollision(ISurfControllable surfer)
        {
            _gokzMovement.DisableSourceCollision(surfer);
        }

        public void MoveGoKz(ISurfControllable surfer)
        {
            _gokzMovement.Move(surfer);
        }

        public void FinishGoKz(ISurfControllable surfer)
        {
            _gokzMovement.Finish(surfer);
        }

        public void ResetGoKzMovement()
        {
            _gokzMovement.ResetState();
        }

        private Vector3 GetGoKzUp()
        {
            return Vector3.up * (_surfer.MoveData.reverseGravity < 0f ? -1f : 1f);
        }

        private bool ProbeGoKzGround(Vector3 up, out RaycastHit hit)
        {
            hit = default;
            CharacterController character = _surfer.CC;
            Vector3 center = character.transform.position + character.center;
            float halfSegment = Mathf.Max(0f, character.height * 0.5f - character.radius);
            Vector3 bottom = center - up * halfSegment;
            float radius = Mathf.Max(0.01f, character.radius * 0.95f);
            float probeDistance = Mathf.Max(0.08f, character.skinWidth * 2f + 0.04f);
            int layerMask = SurfPhysics.GroundLayerMask;

            bool found = Physics.SphereCast(
                bottom + up * 0.02f,
                radius,
                -up,
                out hit,
                probeDistance + 0.02f,
                layerMask,
                QueryTriggerInteraction.Ignore);

            if (found)
            {
                float slopeCosine = Mathf.Clamp01(_surfer.MoveData.slopeLimit);
                return Vector3.Dot(hit.normal, up) >= slopeCosine;
            }

            // CharacterController.isGrounded is reliable for normal gravity and
            // provides a useful fallback when the controller is already touching.
            if (_surfer.MoveData.reverseGravity > 0f && character.isGrounded)
            {
                hit.normal = up;
                return true;
            }

            return false;
        }

        private void GetGoKzWish(Vector3 up, float targetSpeed, out Vector3 wishDirection, out float wishSpeed)
        {
            Vector3 forward = Vector3.ProjectOnPlane(_surfer.Forward, up);
            Vector3 right = Vector3.ProjectOnPlane(_surfer.Right, up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(playerTransform.forward, up);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.ProjectOnPlane(playerTransform.right, up);

            forward.Normalize();
            right.Normalize();

            Vector2 input = Vector2.ClampMagnitude(
                new Vector2(_surfer.MoveData.horizontalAxis, _surfer.MoveData.verticalAxis), 1f);
            Vector3 wish = forward * input.y + right * input.x;
            wishDirection = wish.sqrMagnitude > 0.0001f ? wish.normalized : Vector3.zero;
            wishSpeed = wish.magnitude * targetSpeed;
        }

        private void ProcessGoKzGroundAir(Vector3 up, Vector3 down, float friction, float accelerationMultiplier, float gravityMultiplier)
        {
            if (!_gokzGrounded)
            {
                if (_surfer.MoveData.isGravity)
                    _surfer.MoveData.Velocity += down * (_config.Gravity * _surfer.MoveData.GravityFactor * gravityMultiplier * _deltaTime);

                ApplyGoKzAirAcceleration(up);
                return;
            }

            if (TryProcessGoKzSlide(up, down))
                return;

            Vector3 groundVelocity = Vector3.ProjectOnPlane(_surfer.MoveData.Velocity, _gokzGroundNormal);
            if (_surfer.MoveData.wishJump && _surfer.MoveData.jumpEnabled)
            {
                ApplyGoKzFriction(ref groundVelocity, up, friction, true);
                _surfer.MoveData.Velocity = groundVelocity;
                ApplyGoKzJump(up);
                return;
            }

            ApplyGoKzFriction(ref groundVelocity, up, friction, true);
            float targetSpeed = _surfer.MoveData.sprinting ? _config.sprintSpeed : _config.walkSpeed;
            if (crouching)
                targetSpeed = _config.crouchSpeed;
            targetSpeed *= Mathf.Max(0.01f, _surfer.MoveData.WalkFactor);

            GetGoKzWish(_gokzGroundNormal, targetSpeed, out Vector3 wishDirection, out float wishSpeed);
            groundVelocity += SurfPhysics.Accelerate(
                groundVelocity,
                wishDirection,
                wishSpeed,
                _config.acceleration * accelerationMultiplier,
                _deltaTime,
                Mathf.Max(0f, _surfer.MoveData.SurfaceFriction));

            _surfer.MoveData.Velocity = groundVelocity + down * 0.001f;
        }

        private void ApplyGoKzAirAcceleration(Vector3 up)
        {
            float targetSpeed = _surfer.MoveData.sprinting ? _config.sprintSpeed : _config.walkSpeed;
            if (crouching)
                targetSpeed = _config.crouchSpeed;

            GetGoKzWish(up, targetSpeed, out Vector3 wishDirection, out float wishSpeed);
            if (_config.ClampAirSpeed && wishSpeed > _config.MaxSpeed)
            {
                wishSpeed = _config.MaxSpeed;
            }

            Vector3 airVelocity = Vector3.ProjectOnPlane(_surfer.MoveData.Velocity, up);
            airVelocity += SurfPhysics.AirAccelerate(
                airVelocity,
                wishDirection,
                wishSpeed,
                _config.AirAccel * Mathf.Max(0.01f, _surfer.MoveData.scale),
                _config.AirCap,
                _deltaTime);
            float gravityVelocity = Vector3.Dot(_surfer.MoveData.Velocity, up);
            _surfer.MoveData.Velocity = airVelocity + up * gravityVelocity;
        }

        private void ProcessGoKzWater(Vector3 up, Vector3 down, float friction, float accelerationMultiplier)
        {
            _surfer.MoveData.Velocity = Vector3.Lerp(
                _surfer.MoveData.Velocity,
                Vector3.zero,
                Mathf.Clamp01(_config.underwaterVelocityDampening * _deltaTime));

            if (!_gokzGrounded && _surfer.MoveData.isGravity)
                _surfer.MoveData.Velocity += down * (_config.underwaterGravity * _deltaTime);
            if (_surfer.MoveData.wishJump)
                _surfer.MoveData.Velocity += up * (_config.swimUpSpeed * _deltaTime);

            GetGoKzWish(up, _config.underwaterSwimSpeed, out Vector3 wishDirection, out float wishSpeed);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(_surfer.MoveData.Velocity, up);
            ApplyGoKzFriction(ref planarVelocity, up, _config.underwaterFriction, false);
            planarVelocity += SurfPhysics.Accelerate(
                planarVelocity,
                wishDirection,
                wishSpeed,
                _config.underwaterAcceleration * accelerationMultiplier,
                _deltaTime,
                Mathf.Max(0f, _surfer.MoveData.SurfaceFriction));
            float verticalVelocity = Vector3.Dot(_surfer.MoveData.Velocity, up);
            _surfer.MoveData.Velocity = planarVelocity + up * verticalVelocity;
        }

        private void ProcessGoKzWeightless(Vector3 up, Vector3 down, float friction, float accelerationMultiplier)
        {
            _surfer.MoveData.Velocity = Vector3.Lerp(
                _surfer.MoveData.Velocity,
                Vector3.zero,
                Mathf.Clamp01(_config.underwaterVelocityDampening * _deltaTime));

            if (_surfer.MoveData.wishJump)
                _surfer.MoveData.Velocity += up * (_config.swimUpSpeed * _deltaTime);
            if (_surfer.MoveData.crouching)
                _surfer.MoveData.Velocity += down * (_config.swimUpSpeed * _deltaTime);

            ApplyGoKzAirAcceleration(up);
        }

        private void ApplyGoKzFriction(ref Vector3 velocity, Vector3 up, float friction, bool grounded)
        {
            if (!grounded || friction <= 0f)
                return;

            Vector3 planar = Vector3.ProjectOnPlane(velocity, up);
            SurfPhysics.Friction(ref planar, _config.StopSpeed, friction, _deltaTime);
            velocity = planar;
        }

        private void ApplyGoKzJump(Vector3 up)
        {
            float jumpPower = _config.JumpPower;
            if (_surfer.MoveData.Bouncer && _surfer.MoveData.ModifierEnabled)
            {
                if (_gokzBouncerJumpPower < jumpPower)
                    _gokzBouncerJumpPower = jumpPower;
                _gokzBouncerJumpPower += 2f;
                jumpPower = _gokzBouncerJumpPower;
            }
            else
            {
                _gokzBouncerJumpPower = jumpPower;
            }

            Vector3 planar = Vector3.ProjectOnPlane(_surfer.MoveData.Velocity, up);
            _surfer.MoveData.Velocity = planar + up * jumpPower;
            _surfer.MoveData.hasJump = true;
            _gokzGrounded = false;
            _surfer.MoveData.grounded = false;
        }

        private bool TryProcessGoKzSlide(Vector3 up, Vector3 down)
        {
            Vector3 planar = Vector3.ProjectOnPlane(_surfer.MoveData.Velocity, _gokzGroundNormal);
            if (!wasSliding)
            {
                slideDirection = planar.sqrMagnitude > 0.0001f ? planar.normalized : Vector3.ProjectOnPlane(playerTransform.forward, _gokzGroundNormal).normalized;
                slideSpeedCurrent = Mathf.Max(_config.minimumSlideSpeed, planar.magnitude);
            }

            if (_surfer.MoveData.slidingEnabled && _surfer.MoveData.crouching &&
                planar.magnitude > _config.minimumSlideSpeed && slideDelay <= 0f)
            {
                if (!wasSliding)
                    slideSpeedCurrent = Mathf.Clamp(slideSpeedCurrent * _config.slideSpeedMultiplier, _config.minimumSlideSpeed, _config.maximumSlideSpeed);

                Vector3 downhill = Vector3.ProjectOnPlane(down, _gokzGroundNormal);
                if (downhill.sqrMagnitude > 0.0001f)
                    slideDirection = (slideDirection + downhill.normalized * _deltaTime * _config.downhillSlideSpeedMultiplier).normalized;

                slideSpeedCurrent = Mathf.Clamp(
                    slideSpeedCurrent - _config.slideFriction * _deltaTime,
                    0f,
                    _config.maximumSlideSpeed);
                _surfer.MoveData.Velocity = slideDirection * slideSpeedCurrent + down * 0.001f;
                wasSliding = true;

                if (_surfer.MoveData.wishJump && slideSpeedCurrent < _config.minimumSlideSpeed * _config.slideSpeedMultiplier)
                    ApplyGoKzJump(up);
                return true;
            }

            if (slideDelay > 0f)
                slideDelay -= _deltaTime;
            if (wasSliding)
                slideDelay = _config.slideDelay;
            wasSliding = false;
            return false;
        }

        private void ApplyGoKzBooster(Vector3 up)
        {
            if (!_surfer.MoveData.Booster)
                return;

            Vector3 planar = Vector3.ProjectOnPlane(_surfer.MoveData.Velocity, up);
            if (planar.sqrMagnitude > 0.0001f)
                _surfer.MoveData.Velocity += planar.normalized * (5f * _deltaTime);
        }

        private void ProcessMovementLegacy(ISurfControllable surfer, MovementConfig config, float deltaTime)
        {
            // cache instead of passing around parameters
            _surfer = surfer;
            _config = config;
            _deltaTime = deltaTime;
            _Grounded = (_surfer.CC.isGrounded && _surfer.MoveData.reverseGravity == 1) || (CheckGrounded() && _surfer.MoveData.reverseGravity == -1);
            _surfer.MoveData.groundTransform = t_Transform;
            //ladder
            if (_surfer.MoveData.laddersEnabled && !_surfer.MoveData.climbingLadder)
            {
                // Look for ladders
                LadderCheck(_surfer.MoveData.Velocity * Mathf.Clamp(Time.deltaTime * 2f, 0.025f, 0.25f));
            }
            if (_surfer.MoveData.laddersEnabled && _surfer.MoveData.climbingLadder)
            {
                LadderPhysics();
            }

            //ground-water-lose weight
            else if (!_surfer.MoveData.underwater&& !_surfer.MoveData.LoseWeight)
            {
                //Ice
                t_Friction = _config.Friction;
                t_iceAcel = 1;
                if (_surfer.MoveData.IceEnabled)
                {
                    // Look for ice
                    IceCheck();
                }

                // apply gravity
                if (!_Grounded)
                {
                    _surfer.MoveData.Velocity.y -= ((_surfer.MoveData.GravityFactor) * _config.Gravity * _deltaTime) * (_surfer.MoveData.isGravity?1:0)* _surfer.MoveData.reverseGravity;
                    //_surfer.MoveData.Velocity.y += _surfer.BaseVelocity.y * _deltaTime;

                }
                else if (!isSlope && _surfer.MoveData.isGravity
                    && (t_Transform == null || !t_Transform.CompareTag("Portal")))
                    _surfer.MoveData.Velocity.y = -0.0001f * _surfer.MoveData.reverseGravity;//must do this to keep isGround!
                if (_surfer.MoveData.Booster)
                {
                    _surfer.MoveData.Velocity.x += _surfer.MoveData.Velocity.normalized.x *Time.deltaTime*5;
                    _surfer.MoveData.Velocity.z += _surfer.MoveData.Velocity.normalized.z *Time.deltaTime*5;
                }


                CalculateMovementVelocity();
            }
            else if(!_surfer.MoveData.LoseWeight)
            {
                // Do underwater logic
                UnderwaterPhysics();
                //_surfer.MoveData.SurfaceFriction = 0;
                SurfPhysics.Reflect(ref _surfer.MoveData.Velocity, _surfer.CC, _surfer.MoveData.Origin, _deltaTime, _surfer.MoveData.slopeLimit);

            }
            else
            {
                SpacePhysics();
                SurfPhysics.Reflect(ref _surfer.MoveData.Velocity, _surfer.CC, _surfer.MoveData.Origin, _deltaTime, _surfer.MoveData.slopeLimit);

            }

            float yVel = _surfer.MoveData.Velocity.y;
            _surfer.MoveData.Velocity.y = 0f;
            _surfer.MoveData.Velocity = Vector3.ClampMagnitude (_surfer.MoveData.Velocity, _config.MaxVelocity);
            speed = _surfer.MoveData.Velocity.magnitude;
            _surfer.MoveData.Velocity.y = yVel;

            /*if (_surfer.MoveData.Velocity.sqrMagnitude != 0f){

                float maxDistPerFrame = 0.2f;
                Vector3 velocityThisFrame = _surfer.MoveData.Velocity * _deltaTime;
                float velocityDistLeft = velocityThisFrame.magnitude;
                float initialVel = velocityDistLeft;
                while (velocityDistLeft > 0f)
                {

                    float amountThisLoop = Mathf.Min(maxDistPerFrame, velocityDistLeft);
                    velocityDistLeft -= amountThisLoop;

                    // increment origin
                    Vector3 velThisLoop = velocityThisFrame * (amountThisLoop / initialVel);
                    _surfer.MoveData.Origin += velThisLoop;
                }
            }*/

            //something wrong with the grounded audio
            surfer.MoveData.grounded = (CheckGrounded() && surfer.MoveData.reverseGravity == -1);
            _surfer = null;
        }
        /// <summary>
        /// 
        /// </summary>
        private void CalculateMovementVelocity()
        {
            switch (_surfer.MoveType)
            {
                case MoveType.Walk:
                    if (_surfer.MoveData.crouchEnabled)
                        Crouch(_surfer, _config, Time.deltaTime);
                    if (!_Grounded)
                    {
                        // apply movement from input
                        _surfer.MoveData.Velocity += AirInputMovement();
                        // let the magic happen
                        if(_surfer.MoveData.reverseGravity == 1&& _surfer.MoveData.scale>.3)
                        SurfPhysics.Reflect(ref _surfer.MoveData.Velocity, _surfer.CC, _surfer.MoveData.Origin, _deltaTime,_surfer.MoveData.slopeLimit);
                    }
                    else{
                        // Sliding
                        if (!wasSliding)
                        {
                            slideDirection = new Vector3(_surfer.MoveData.Velocity.x, 0f, _surfer.MoveData.Velocity.z).normalized;
                            slideSpeedCurrent = Mathf.Max(_config.maximumSlideSpeed, new Vector3(_surfer.MoveData.Velocity.x, 0f, _surfer.MoveData.Velocity.z).magnitude);
                        }

                        sliding = false;
                        if (_surfer.MoveData.Velocity.magnitude > _config.minimumSlideSpeed && _surfer.MoveData.slidingEnabled && _surfer.MoveData.crouching && slideDelay <= 0f)
                        {
                            if (!wasSliding)
                                slideSpeedCurrent = Mathf.Clamp(slideSpeedCurrent * _config.slideSpeedMultiplier, _config.minimumSlideSpeed, _config.maximumSlideSpeed);

                            sliding = true;
                            wasSliding = true;
                            SlideMovement();
                            return;
                        }
                        else
                        {
                            if (slideDelay > 0f)
                                slideDelay -= _deltaTime;

                            if (wasSliding)
                                slideDelay = _config.slideDelay;

                            wasSliding = false;
                        }


                        float accel = crouching ? _config.crouchAcceleration* t_iceAcel : _config.acceleration* t_iceAcel;
                        //float fric = crouching ? _config.crouchFriction : _config.Friction;
                        //float decel = crouching ? _config.crouchDeceleration : _config.deceleration;
                        //var friction = _surfer.MoveData.SurfaceFriction * fric;
                        //var stopSpeed = _config.StopSpeed;
                        //SurfPhysics.Friction(ref _surfer.MoveData.Velocity, stopSpeed, friction, _deltaTime);

                        // Get movement directions
                        Vector3 forward = Vector3.Cross(groundNormal, -playerTransform.right);
                        Vector3 right = Vector3.Cross(groundNormal, forward);

                        float speed = _surfer.MoveData.sprinting ? _config.sprintSpeed : _config.walkSpeed;
                        if (crouching)
                            speed = _config.crouchSpeed;

                        Vector3 _wishDir;
                        // Jump and friction
                        if (_surfer.MoveData.wishJump&& _Grounded)
                        {
                            ApplyFriction(0.0f, true, true);
                            Jump();
                            return;
                        }
                        else
                        {
                            ApplyFriction(1.0f * frictionMult, false, true);
                        }

                        float forwardMove = _surfer.MoveData.verticalAxis;
                        float rightMove = _surfer.MoveData.horizontalAxis;

                        _wishDir = forwardMove * forward + rightMove * right;
                        _wishDir.Normalize();
                        

                        // Set the target speed of the player
                        float _wishSpeed = _wishDir.magnitude;
                        _wishSpeed *= speed;

                        // Accelerate
                        float yVel = _surfer.MoveData.Velocity.y;
                        Accelerate(_wishDir, _wishSpeed, accel * Mathf.Min(frictionMult, 1f), false);

                        float maxVelocityMagnitude = _config.MaxVelocity;
                        _surfer.MoveData.Velocity = Vector3.ClampMagnitude(new Vector3(_surfer.MoveData.Velocity.x, 0f, _surfer.MoveData.Velocity.z), maxVelocityMagnitude);
                        _surfer.MoveData.Velocity.y = yVel;

                        //Vector3 moveDirNorm = _wishDir;
                        //Vector3 forwardVelocity = Vector3.Cross(groundNormal, Quaternion.AngleAxis(-90, Vector3.up) * new Vector3(_surfer.MoveData.Velocity.x, 0f, _surfer.MoveData.Velocity.z));
                        // Calculate how much slopes should affect movement
                        //float yVelocityNew = forwardVelocity.normalized.y * new Vector3(_surfer.MoveData.Velocity.x, 0f, _surfer.MoveData.Velocity.z).magnitude;
                        // Apply the Y-movement from slopes
                        //_surfer.MoveData.Velocity.y = yVelocityNew * (_wishDir.y < 0f ? 1.2f : 1.0f);
                        //float removableYVelocity = _surfer.MoveData.Velocity.y - yVelocityNew;
                    }

                    break;
            }
        }


        //no need but leave it here
        /*private Vector3 GroundInputMovement()
        {
            Vector3 wishVel, wishDir;
            float wishSpeed;
            GetWishValues(out wishVel, out wishDir, out wishSpeed);

            if ((wishSpeed != 0.0f) && (wishSpeed > _config.MaxSpeed))
            {
                wishVel *= _config.MaxSpeed / wishSpeed;
                wishSpeed = _config.MaxSpeed;
            }

            wishSpeed *= _surfer.MoveData.WalkFactor;

            return SurfPhysics.Accelerate(_surfer.MoveData.Velocity, wishDir, 
                wishSpeed, _config.Accel, _deltaTime, _surfer.MoveData.SurfaceFriction);
        }*/

        private Vector3 AirInputMovement()
        {
            Vector3 wishVel, wishDir;
            float wishSpeed;
            GetWishValues(out wishVel, out wishDir, out wishSpeed);

            if (_config.ClampAirSpeed && (wishSpeed != 0f && (wishSpeed > _config.MaxSpeed)))
            {
                wishVel = wishVel * (_config.MaxSpeed / wishSpeed);
                wishSpeed = _config.MaxSpeed;
            }

            return SurfPhysics.AirAccelerate(_surfer.MoveData.Velocity, wishDir, 
                wishSpeed, _surfer.MoveData.airAccel, _config.AirCap, _deltaTime);
        }
        private void GetWishValues(out Vector3 wishVel, out Vector3 wishDir, out float wishSpeed)
        {
            wishVel = Vector3.zero;
            wishDir = Vector3.zero;
            wishSpeed = 0f;

            Vector3 forward = _surfer.Forward,
                right = _surfer.Right;

            forward[1] = 0;
            right[1] = 0;
            forward.Normalize();
            right.Normalize();

            for (int i = 0; i < 3; i++)
                wishVel[i] = forward[i] * _surfer.MoveData.ForwardMove + right[i] * _surfer.MoveData.SideMove;
            wishVel[1] = 0;

            wishSpeed = wishVel.magnitude;
            wishDir = wishVel.normalized;
        }
        private bool CheckGrounded()
        {
            //reverse gravity check ground
            //_surfer.MoveData.SurfaceFriction = 1f;
            var movingDown = _surfer.MoveData.Velocity.y < 0f;
            var trace = TraceToFloor();
            if (trace.PlaneNormal.y > -0.7f
                || movingDown)
            {
                //SetGround(null);
                //if (movingUp && _surfer.MoveType != MoveType.Noclip)
                //{
                //    _surfer.MoveData.SurfaceFriction = _config.AirFriction;
                //}
                return false;
            }
            else
            {
                //SetGround(trace.HitCollider.gameObject);
                return true;
            }
        }
        private void SetGround(GameObject obj)
        {
            if(obj != null)
            {
                //_surfer.GroundObject = obj;
                _surfer.MoveData.Velocity.y = 0;
            }
            else
            {
                //_surfer.GroundObject = null;
            }
        }
        private Trace TraceBounds(Vector3 start, Vector3 end, int layerMask)
        {
            return Tracer.TraceCollider(_surfer.CC, start, end, layerMask);
        }
        private Trace TraceToFloor()
        {
            var down = _surfer.MoveData.Origin;
            down.y -= 0.01f* _surfer.MoveData.reverseGravity;//something weird between this and Reflect()
            return Tracer.TraceCollider(_surfer.CC, _surfer.MoveData.Origin, down, SurfPhysics.GroundLayerMask);
        }

        private void Jump()
        {
            _surfer.MoveData.hasJump = true;
            _surfer.MoveData.Velocity.y = _config.JumpPower * _surfer.MoveData.reverseGravity;
            if (_surfer.MoveData.Bouncer && _surfer.MoveData.ModifierEnabled)
            {
                t_JumpPower += 2;
                _surfer.MoveData.Velocity.y = t_JumpPower * _surfer.MoveData.reverseGravity;
            }
            else t_JumpPower = _config.JumpPower;
        }
        private void Crouch(ISurfControllable surfer, MovementConfig config, float deltaTime)
        {
            _surfer = surfer;
            _config = config;
            _deltaTime = deltaTime;
            if (_surfer == null || _surfer.CC == null)
                return;
            
            bool wantsToCrouch = _surfer.MoveData.crouching;
            bool canUncrouch = true;

            float crouchingHeight =_surfer.MoveData.crouchingHeight;
            //float heightDifference = _surfer.MoveData.defaultHeight - _surfer.MoveData.defaultHeight * crouchingHeight;
            float deltaHeight = 0f;
            Vector3 CameraCrouchHeight = _surfer.MoveData.viewTransformDefaultLocalPos * crouchingHeight * _surfer.MoveData.reverseGravity/ _surfer.MoveData.scale;
            float CC_crouchHeight = _surfer.MoveData.defaultHeight * crouchingHeight;

            // Crouching input
            //if (grounded)
            crouchLerp = Mathf.Lerp(crouchLerp, wantsToCrouch ? 1f : 0f, _deltaTime * _surfer.MoveData.crouchingSpeed);


            if (wantsToCrouch && !crouching)
            {
                crouching = true;
                _surfer.CC.height *= crouchingHeight; 
                if (_surfer.CC.isGrounded)
                    _surfer.CC.center = new Vector3(0, _surfer.CC.height / 2 * _surfer.MoveData.reverseGravity, 0) ;
            }
            else if (crouching)
            {
                // Capsule collider
                Vector3 startPos = _surfer.CC.transform.position+_surfer.CC.height *Vector3.up * _surfer.MoveData.reverseGravity;
                Vector3 endPos = _surfer.CC.transform.position + _surfer.MoveData.defaultHeight * Vector3.up*_surfer.MoveData.reverseGravity;
                //Debug.DrawLine(startPos, endPos, Color.white);
                RaycastHit hit;
                CrouchUp = true;//avoid anti weight issue
                if (Physics.Linecast(startPos,endPos, out hit, SurfPhysics.GroundLayerMask))
                {
                    CrouchUp = false;
                    canUncrouch = false;
                    if (!wantsToCrouch)
                    {
                        deltaHeight = hit.point.y - startPos.y - 0.01f * _surfer.MoveData.reverseGravity;
                        _surfer.CC.height +=  deltaHeight * _surfer.MoveData.reverseGravity;
                        _surfer.CC.center = new Vector3(0, _surfer.CC.height / 2 * _surfer.MoveData.reverseGravity, 0) ;
                        //need to optimize
                        Vector3 t_cameraPos = new Vector3(_surfer.MoveData.viewTransform.position.x, _surfer.CC.transform.position.y + 
                            (_surfer.CC.height -(_surfer.MoveData.defaultHeight - _surfer.MoveData.viewTransformDefaultLocalPos.y))
                            * _surfer.MoveData.reverseGravity, _surfer.MoveData.viewTransform.position.z);
                        _surfer.MoveData.viewTransform.position = t_cameraPos;
                    }
                    else 
                    { 
                        _surfer.CC.height = CC_crouchHeight;
                        _surfer.CC.center = new Vector3(0, _surfer.CC.height / 2 * _surfer.MoveData.reverseGravity, 0) ;
                        _surfer.MoveData.viewTransform.localPosition = Vector3.Lerp(_surfer.MoveData.viewTransform.localPosition, CameraCrouchHeight, crouchLerp);
                    }

                }

                if (canUncrouch && !wantsToCrouch)
                {
                    crouching = false;
                    _surfer.CC.height = Mathf.Abs(_surfer.MoveData.defaultHeight);
                    _surfer.CC.center = new Vector3(0, _surfer.CC.height / 2 * _surfer.MoveData.reverseGravity, 0) ;
                }

            }
                // Changing camera position
                if (canUncrouch )//change!!!!!!!
                _surfer.MoveData.viewTransform.localPosition = Vector3.Lerp(_surfer.MoveData.viewTransformDefaultLocalPos * _surfer.MoveData.reverseGravity
                    , CameraCrouchHeight+new Vector3(0,_surfer.CC.center.y- crouchingHeight*_surfer.MoveData.reverseGravity, 0), crouchLerp);
        }
        private void SlideMovement()
        {

            // Gradually change direction
            slideDirection += new Vector3(groundNormal.x, 0f, groundNormal.z) * slideSpeedCurrent * _deltaTime;
            slideDirection = slideDirection.normalized;

            // Set direction
            Vector3 slideForward = Vector3.Cross(groundNormal, Quaternion.AngleAxis(-90, Vector3.up) * slideDirection);

            // Set the Velocity
            slideSpeedCurrent -= _config.slideFriction * _deltaTime;
            slideSpeedCurrent = Mathf.Clamp(slideSpeedCurrent, 0f, _config.maximumSlideSpeed);
            slideSpeedCurrent -= (slideForward * slideSpeedCurrent).y * _deltaTime * _config.downhillSlideSpeedMultiplier; // Accelerate downhill (-y = downward, - * - = +)

            _surfer.MoveData.Velocity = slideForward * slideSpeedCurrent;

            // Jump
            if (_surfer.MoveData.wishJump && slideSpeedCurrent < _config.minimumSlideSpeed * _config.slideSpeedMultiplier)
            {

                Jump();
                return;

            }

        }
        private void UnderwaterPhysics () {

            if (_surfer.MoveData.crouchEnabled)
                Crouch(_surfer, _config, Time.deltaTime);

            _surfer.MoveData.Velocity.y = Vector3.Lerp (_surfer.MoveData.Velocity, Vector3.zero, _config.underwaterVelocityDampening * _deltaTime).y;
            _surfer.MoveData.Velocity += AirInputMovement();
            // Gravity
            if ( !_Grounded)
                _surfer.MoveData.Velocity.y -= _config.underwaterGravity * _deltaTime;

            // Swimming upwards
            if (_surfer.MoveData.wishJump)
                _surfer.MoveData.Velocity.y += _config.swimUpSpeed * _deltaTime;

            //float fric = _config.underwaterFriction;
            float accel = _config.underwaterAcceleration;
            //float decel = _config.underwaterDeceleration;

            // Get movement directions
            Vector3 forward = Vector3.Cross (groundNormal, -playerTransform.right);
            Vector3 right = Vector3.Cross (groundNormal, forward);
            float speed = _config.underwaterSwimSpeed;
            Vector3 _wishDir;

            ApplyFriction(1f, true, false);

            float forwardMove = _surfer.MoveData.verticalAxis;
            float rightMove = _surfer.MoveData.horizontalAxis;

            _wishDir = forwardMove * forward + rightMove * right;
            _wishDir.Normalize ();
            //Vector3 moveDirNorm = _wishDir;

            Vector3 forwardVelocity = Vector3.Cross (groundNormal, Quaternion.AngleAxis (-90, Vector3.up) * new Vector3 (_surfer.MoveData.Velocity.x, 0f, _surfer.MoveData.Velocity.z));

            // Set the target speed of the player
            float _wishSpeed = _wishDir.magnitude;
            _wishSpeed *= speed;

            // Accelerate
            float yVel = _surfer.MoveData.Velocity.y;
            Accelerate (_wishDir, _wishSpeed, accel, false);

            float maxVelocityMagnitude = _config.MaxVelocity;
            _surfer.MoveData.Velocity = Vector3.ClampMagnitude (new Vector3 (_surfer.MoveData.Velocity.x, 0f, _surfer.MoveData.Velocity.z), maxVelocityMagnitude);
            _surfer.MoveData.Velocity.y = yVel;

            // Jumping out of water
            bool movingForwards = playerTransform.InverseTransformVector (_surfer.MoveData.Velocity).z > 0f;
            Trace waterJumpTrace = TraceBounds (playerTransform.position, playerTransform.position + playerTransform.forward * 0.1f, SurfPhysics.GroundLayerDefault);
            if (waterJumpTrace.HitCollider != null && Vector3.Angle (Vector3.up, waterJumpTrace.PlaneNormal) >= _surfer.MoveData.slopeLimit && _surfer.MoveData.wishJump && !_surfer.MoveData.underwater && movingForwards)
                _surfer.MoveData.Velocity.y = Mathf.Max (_surfer.MoveData.Velocity.y, _config.JumpPower);

        }
        private void SpacePhysics()
        {
            if (_surfer.MoveData.crouchEnabled)
                Crouch(_surfer, _config, Time.deltaTime);

            _surfer.MoveData.Velocity.y = Vector3.Lerp(_surfer.MoveData.Velocity, Vector3.zero, _config.underwaterVelocityDampening * _deltaTime).y;
            _surfer.MoveData.Velocity += AirInputMovement();

            if (_surfer.MoveData.wishJump)
                _surfer.MoveData.Velocity.y += _config.swimUpSpeed * _deltaTime;
            if (_surfer.MoveData.crouching)
                _surfer.MoveData.Velocity.y -= _config.swimUpSpeed * _deltaTime;

            float accel = _config.underwaterAcceleration;
            Vector3 forward = Vector3.Cross(groundNormal, -playerTransform.right);
            Vector3 right = Vector3.Cross(groundNormal, forward);
            float speed = _config.underwaterSwimSpeed;
            Vector3 _wishDir;

            ApplyFriction(1f, true, false);

            float forwardMove = _surfer.MoveData.verticalAxis;
            float rightMove = _surfer.MoveData.horizontalAxis;

            _wishDir = forwardMove * forward + rightMove * right;
            _wishDir.Normalize();

            float _wishSpeed = _wishDir.magnitude;
            _wishSpeed *= speed;

            float yVel = _surfer.MoveData.Velocity.y;
            Accelerate(_wishDir, _wishSpeed, accel, false);

            float maxVelocityMagnitude = _config.MaxVelocity;
            _surfer.MoveData.Velocity = Vector3.ClampMagnitude(new Vector3(_surfer.MoveData.Velocity.x, 0f, _surfer.MoveData.Velocity.z), maxVelocityMagnitude);
            _surfer.MoveData.Velocity.y = yVel;

        }
        private void ApplyFriction(float t, bool yAffected, bool grounded)
        {

            // Initialise variables
            Vector3 _vel = _surfer.MoveData.Velocity;
            float _speed;
            float _newSpeed;
            float _control;
            float _drop;

            // Set Y to 0, speed to the magnitude of movement and drop to 0. I think drop is the amount of speed that is lost, but I just stole this from the internet, idk.
            _vel.y = 0.0f;
            _speed = _vel.magnitude;
            _drop = 0.0f;

            float fric = crouching ? _config.crouchFriction : t_Friction;// _config.Friction
            //float accel = crouching ? _config.crouchAcceleration : _config.acceleration;
            float decel = crouching ? _config.crouchDeceleration : _config.deceleration;

            // Only apply friction if the player is grounded
            if (grounded)
            {

                // i honestly have no idea what this does tbh
                _vel.y = _surfer.MoveData.Velocity.y;
                _control = _speed < decel ? decel : _speed;
                _drop = _control * fric * _deltaTime * t;

            }

            // again, no idea, but comments look cool
            _newSpeed = Mathf.Max(_speed - _drop, 0f);
            if (_speed > 0.0f)
                _newSpeed /= _speed;

            // Set the end-Velocity
            _surfer.MoveData.Velocity.x *= _newSpeed;
            if (yAffected == true) { _surfer.MoveData.Velocity.y *= _newSpeed; }
            _surfer.MoveData.Velocity.z *= _newSpeed;

        }
        private void Accelerate(Vector3 wishDir, float wishSpeed, float acceleration, bool yMovement)
        {

            // Initialise variables
            float _addSpeed;
            float _accelerationSpeed;
            float _currentSpeed;

            // again, no idea
            _currentSpeed = Vector3.Dot(_surfer.MoveData.Velocity, wishDir);
            _addSpeed = wishSpeed - _currentSpeed;

            // If you're not actually increasing your speed, stop here.
            if (_addSpeed <= 0)
                return;

            // won't bother trying to understand any of this, really
            _accelerationSpeed = Mathf.Min(acceleration * _deltaTime * wishSpeed, _addSpeed);

            // Add the Velocity.
            _surfer.MoveData.Velocity.x += _accelerationSpeed * wishDir.x;
            if (yMovement) { _surfer.MoveData.Velocity.y += _accelerationSpeed * wishDir.y; }
            _surfer.MoveData.Velocity.z += _accelerationSpeed * wishDir.z;

        }
        private void LadderCheck(Vector3 direction)
        {
            /*if (_surfer.MoveData.Velocity.sqrMagnitude <= 0f)
                return;*/

            bool foundLadder = false;

            Vector3 point1, point2;
            SurfPhysics.GetCapsulePoints(_surfer.CC, _surfer.MoveData.Origin, out point1, out point2);

            RaycastHit hit;
            if (Physics.CapsuleCast(point1, point2, _surfer.CC.radius,
                Vector3.Scale(direction, new Vector3(1f, 0f, 1f)), out hit,
                direction.magnitude, SurfPhysics.GroundLayerDefault, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.tag == "Ladder")
                {
                    foundLadder = true;
                    t_HitPoint = hit.point;
                    if (_surfer.MoveData.climbingLadder == false)
                    {
                   
                        _surfer.MoveData.climbingLadder = true;
                        _surfer.MoveData.ladderNormal = hit.normal;
                        _surfer.MoveData.ladderDirection = -hit.normal * direction.magnitude * 2f;
                                 
                        Vector3 sideDir = hit.normal;
                        sideDir.y = 0f;
                        sideDir = Quaternion.AngleAxis(-90f, Vector3.up) * sideDir;    
                        _surfer.MoveData.ladderClimbDir = Quaternion.AngleAxis(90f, sideDir) * hit.normal;
                        _surfer.MoveData.ladderClimbDir *= 1f / _surfer.MoveData.ladderClimbDir.y; // Make sure Y is always 1
                    }
                   
                }

            }

            if (!foundLadder)
            {

                _surfer.MoveData.ladderNormal = Vector3.zero;
                _surfer.MoveData.ladderVelocity = Vector3.zero;
                _surfer.MoveData.climbingLadder = false;
                _surfer.MoveData.ladderClimbDir = Vector3.up;

            }

        }
        private void LadderPhysics()
        {
            _surfer.MoveData.ladderVelocity = _surfer.MoveData.ladderClimbDir * _surfer.MoveData.verticalAxis * 3f;

            _surfer.MoveData.Velocity = _surfer.MoveData.ladderVelocity; //Vector3.Lerp(_surfer.MoveData.Velocity, _surfer.MoveData.ladderVelocity, Time.deltaTime * 100f);

            //Vector3 center = _surfer.MoveData.playerTransform.position + _surfer.CC.height * Vector3.up / 2;
            LadderCheck( _surfer.MoveData.ladderDirection);
            //Debug.Log((center - t_HitPoint).magnitude);
            //Debug.DrawLine(center, t_HitPoint, Color.blue);

            if (_surfer.MoveData.verticalAxis < 0f && _Grounded)
            {
                _surfer.MoveData.Velocity = _surfer.MoveData.ladderNormal * 0.5f;
                _surfer.MoveData.ladderVelocity = Vector3.zero;
                _surfer.MoveData.climbingLadder = false;
            }
            if (_surfer.MoveData.wishJump || _surfer.MoveData.crouching)
            {
                _surfer.MoveData.Velocity = _surfer.MoveData.ladderNormal * 4f;
                _surfer.MoveData.ladderVelocity = Vector3.zero;
                _surfer.MoveData.climbingLadder = false;
            }
            if (_surfer.MoveData.horizontalAxis != 0)
            {
                float dir = _surfer.MoveData.horizontalAxis > 0 ? 1 : -1;
                Vector3 moveDir = Quaternion.AngleAxis(dir*90, _surfer.MoveData.ladderNormal) * _surfer.MoveData.ladderClimbDir * 1f;
                _surfer.MoveData.Velocity = moveDir; 

                if (_surfer.MoveData.horizontalAxis != 0 && _surfer.MoveData.verticalAxis > 0f)
                {
                    //Mathf.Atan(_surfer.MoveData.verticalAxis / _surfer.MoveData.horizontalAxis) * Mathf.Rad2Deg
                    _surfer.MoveData.Velocity = Quaternion.AngleAxis(dir * 30, _surfer.MoveData.ladderNormal) * _surfer.MoveData.ladderClimbDir * 6f;
                    _surfer.MoveData.ladderVelocity = Vector3.zero;
                    _surfer.MoveData.climbingLadder = false;
                }
            }
            

        }
        private void IceCheck()
        {
            RaycastHit hit; _surfer.MoveData.GravityFactor = 1;
            if (Physics.Linecast(_surfer.MoveData.Origin, _surfer.MoveData.Origin - Vector3.up*.1f, out hit))
            {
                t_Transform = hit.transform;
                isSlope = Vector3.Angle(hit.normal, Vector3.up) != 0 ? true : false;
                if (t_Transform.tag == "Ice")
                IcePhysics();
            }
                
        }
        private void IcePhysics()
        {
            t_Friction = 0; 
            t_iceAcel = .2f;
            _surfer.MoveData.GravityFactor = 7;
            _surfer.MoveData.Origin -=new Vector3(0, Time.deltaTime*3.0f,0);
        }
    }
}
