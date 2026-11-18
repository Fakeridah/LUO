using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using GameplayIngredients;
using GameOptionsUtility;
using Ridah.Weapon;
using UnityEngine.InputSystem;
using UnityEngine.Audio;
namespace RidahFPS.Controllers
{
    [AddComponentMenu("Ridah/Surf-Bhop-KZ")]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    public class LZM : MonoBehaviour, ISurfControllable
    {
        public static LZM Player { get; private set; }

        #region Player Settings
        [Header("Weapon")]
        public WeaponController []  weapon;
            ///// Fields /////  

            [Header("Physics Settings")]
        public int TickRate = 128;

        [Header("View Settings")]
        public Transform viewTransform;
        public Vector3 ViewOffset = new Vector3(0, 0.61f, 0);

        [Header("Crouching setup")]//any games need crouch!
        [Range(0.01f, 1)]
        public float crouchingHeight = 0.5f;
        public float crouchingSpeed = 10f;
        public float defaultHeight;

        [Header("Features")]
        public bool SpeedDisplay = true;
        public bool jumpEnabled = true;
        public bool crouchingEnabled = true;
        public bool slidingEnabled = false;
        public bool laddersEnabled = true;
        public bool IceEnabled = true;
        public bool ModifierEnabled = true;
        [Tooltip("Use the GoKZ movement rules. Disable to run the preserved legacy SurfController path.")]
        public bool UseGoKzMovement = true;
        [HideInInspector]
        public bool ResetEnabled = true;
        [HideInInspector]
        public bool MoveEnabled = true;


        [Header("Input Settings")]
        #region Old Input
        //public KeyCode Crouch = KeyCode.LeftControl;
        //public KeyCode JumpButton = KeyCode.Space;
        //public KeyCode MoveLeft = KeyCode.A;
        //public KeyCode MoveRight = KeyCode.D;
        //public KeyCode MoveForward = KeyCode.W;
        //public KeyCode MoveBack = KeyCode.S; 
        #endregion
        [SerializeField]  private PlayerInput playerInput;

        [Header("Audio Settings")]
        private AudioSource audioSource;
        [SerializeField] private AudioClip landAudio;
        [Header("UI Settings")]
        [SerializeField] internal GameObject Canvas;
        [SerializeField] private Text SpeedText; 
        #endregion

        #region Interface Realize
        ///// Properties /////
        [Header("Movement Config")]
        [SerializeField]
        public MovementConfig moveConfig;
        private MovementConfig _moveConfig = new MovementConfig();
        private MoveData _moveData = new MoveData();
        internal CharacterController cc;
        private CapsuleCollider collider;
        private SurfController _controller = new SurfController();

        public MoveType MoveType { get { return MoveType.Walk; } }
        public MovementConfig MoveConfig { get { return moveConfig; } }
        public MoveData MoveData { get { return _moveData; } }
        public CharacterController CC { get { return cc; } }
        public CapsuleCollider Collider { get { return collider; } }
        public Vector3 BaseVelocity { get { return _baseVelocity; } }
        public Vector3 Forward { get { return viewTransform.forward; } }
        public Vector3 Right { get { return viewTransform.right; } }
        public Vector3 Up { get { return viewTransform.up; } } 
        public bool GokzGrounded { get { return _controller.GoKzGrounded; } }
        public bool GokzCrouched { get { return _controller.GoKzCrouched; } }
        public float GokzDuckAmount { get { return _controller.GoKzDuckAmount; } }
        public float GokzEyeDuckAmount { get { return _controller.GoKzEyeDuckAmount; } }
        public bool GokzLastJumpWasPerfect { get { return _controller.GoKzLastJumpWasPerfect; } }
        public bool GokzLastJumpWasDuck { get { return _controller.GoKzLastJumpWasDuck; } }
        public int GokzJumpCount { get { return _controller.GoKzJumpCount; } }
        public float GokzLastTakeoffSpeed { get { return _controller.GoKzLastTakeoffSpeed; } }

        public void ResetGoKzMovementState()
        {
            _controller.ResetGoKzMovement();
        }
        #endregion

        #region Method Paramaters
        ///// Methods /////
        private Vector3 prevPosition;
        private Vector3 _baseVelocity;
        private Vector3 _angles;
        private float _lastGokzViewYaw;
        private bool _hasGokzViewYaw;
        private float _gokzCrouchVisual;
        private float centerLerp = 0f;
        private float angleLerp = 0f;

        internal Vector3 _startPosition;
        internal Quaternion _startRotation;
        ///// ResetMethods /////
        internal Vector3 _resetPosition;
        internal Vector3 _resetRotation;
        internal bool _isReset = false;
        internal Vector3 _followPosition;
        internal bool _isFollow = false;
        internal Vector3 speedFactor = Vector3.one;
        internal float scale = 1f;
        internal bool _isChangeScale = false;
        Vector3 ccCenter;
        float ccHeight;
        float ccRadius;
        float ccStepOffset;
        internal bool _reverseGravity = false;
        internal bool _useGravityEffect = false;
        internal float t_height;

        /////Trigger Event /////
        private bool underwater = false;
        private bool Booster = false;
        private bool Bouncer = false;
        private bool LoseWeight = false;
        private bool _queuedGokzJump;
        // The render loop samples device input, while the movement solver
        // consumes one command per fixed tick.  Keep the sampled state apart
        // from MoveData so multiple 128 Hz FixedUpdate calls cannot reuse a
        // one-shot jump request.
        private float _gokzVerticalAxis;
        private float _gokzHorizontalAxis;
        private float _gokzForwardMove;
        private float _gokzSideMove;
        private const float SourceClientMoveSpeed = 450f;
        private bool _gokzMoveLeft;
        private bool _gokzMoveRight;
        private bool _gokzMoveForward;
        private bool _gokzMoveBack;
        private bool _gokzJumpHeld;
        private bool _gokzCrouching;
        private bool _gokzSprinting;
        private List<Collider> triggers = new List<Collider>();
        #endregion

        private void OnDrawGizmos()
        {
        }
        private void OnGUI()
        {

        }
        private void Awake()
        {
            if (Player == null)
            {
                Player = this;
                //DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
            TransformInit();
            if (UseGoKzMovement)
                _controller.EnableGoKzCollision(this);
            //Application.targetFrameRate = 144;
            //QualitySettings.vSyncCount = 1;
            Time.fixedDeltaTime = UseGoKzMovement
                ? GokzKzTimerMovement.TickInterval
                : 1f / Mathf.Max(1, TickRate);
        }

        private void Start()
        {
            playerInput = GetComponent<PlayerInput>();
            MoveDataInit();
        }
        private void Update()
        {
            UpdateSpeedText();
            UpdateMoveData();
            UpdateTrigger();
            UpdateCameraTrigger();
            UpdateCharacterScale();
            UpdateTestBinds();
        }
        private void FixedUpdate()
        {
            // GOKZ movement is authored for one Source tick per movement
            // step.  The project setting is also 1/128, but keep this guard
            // here so an unrelated runtime script cannot silently move the
            // active controller back to a different step size.
            if (UseGoKzMovement && !Mathf.Approximately(
                    Time.fixedDeltaTime,
                    GokzKzTimerMovement.TickInterval))
            {
                Time.fixedDeltaTime = GokzKzTimerMovement.TickInterval;
            }

            foreach (var weapons in weapon)
            if(weapons != null && weapons.isActiveAndEnabled )
                    weapons.UpdateWeapon();


            UpdateMovement();
            UpdateGravityEFX();
            UpdateFollowing();
            UpdateResetPos();
            UpdateAudioData();
        }
        private void OnDisable()
        {
            DisableInput();
        }

        private void TransformInit()
        {
            audioSource = GetComponent<AudioSource>();
            cc = GetComponent<CharacterController>();
            ccCenter = cc.center;
            ccHeight=cc.height;
            ccRadius=cc.radius;
            ccStepOffset = cc.stepOffset;

            _controller.playerTransform = transform;
            prevPosition = transform.position;
            _startPosition = transform.position;
            _startRotation = transform.rotation;

            //camera view transform init
            if (viewTransform == null)
                viewTransform = Camera.main.transform;
            viewTransform.SetParent(transform, false);
            viewTransform.localPosition = UseGoKzMovement
                ? new Vector3(
                    ViewOffset.x,
                    64f / GokzKzTimerMovement.HammerUnitsPerUnityUnit,
                    ViewOffset.z)
                : ViewOffset;
            _gokzCrouchVisual = 0f;
            // CameraHolder is parented below the player. In the GoKZ path
            // keep its local rotation neutral so an authored root yaw is not
            // multiplied into the first movement command; PlayerAiming owns
            // the world-space view rotation after Awake.
            viewTransform.localRotation = UseGoKzMovement
                ? Quaternion.identity
                : transform.rotation;
            PlayerAiming aiming = GetComponentInChildren<PlayerAiming>();
            _lastGokzViewYaw = UseGoKzMovement && aiming != null
                ? aiming.realRotation.y
                : viewTransform.eulerAngles.y;
            _hasGokzViewYaw = true;
        }
        internal void MoveDataInit()
        {
            _moveConfig.JumpPower = moveConfig.JumpPower;
            _moveConfig.Gravity = moveConfig.Gravity;
            _moveConfig.walkSpeed = moveConfig.walkSpeed;
            _moveConfig.deceleration = moveConfig.deceleration;
            _moveConfig.crouchSpeed = moveConfig.crouchSpeed;
            _moveConfig.crouchDeceleration = moveConfig.crouchDeceleration;

            //normal trans to vector.y
            _moveData.slopeLimit = new Vector2(1, 1 * Mathf.Tan(Mathf.Deg2Rad * (90 - cc.slopeLimit))).normalized.y;

            _moveData.jumpEnabled = jumpEnabled;
            _moveData.crouchEnabled = crouchingEnabled;
            _moveData.slidingEnabled = slidingEnabled;
            _moveData.laddersEnabled = laddersEnabled;
            _moveData.IceEnabled = IceEnabled;
            _moveData.ModifierEnabled = ModifierEnabled;

            _moveData.playerTransform = transform;
            _moveData.viewTransform = viewTransform;
            _moveData.viewTransformDefaultLocalPos = viewTransform.localPosition;
            _moveData.viewTransformDefaultWorldPos = viewTransform.position;

            _moveData.defaultHeight = defaultHeight;
            _moveData.crouchingHeight = crouchingHeight;
            _moveData.crouchingSpeed = crouchingSpeed;

            _moveData.Origin = transform.position;
            _moveData.reverseGravity = UseGoKzMovement && !_useGravityEffect
                ? 1f
                : (_reverseGravity ? -1f : 1f);
        }

        private void UpdateMovement()
        {  
            if (UseGoKzMovement)
                ApplyGokzInputForTick();

            // Keep the movement origin synchronized with external teleports and
            // moving platforms before the controller consumes this tick.
            Vector3 positionalMovement = transform.position - prevPosition;
            _moveData.gameCodeMovedPlayer = positionalMovement.sqrMagnitude >
                0.00000001f;
            MoveData.Origin += positionalMovement;

            // PlayerAction changes these flags before the next physics tick. Do
            // this here so the movement solver uses the new gravity direction on
            // the same tick as the visual effect.
            if (UseGoKzMovement && !_useGravityEffect)
                _moveData.reverseGravity = 1f;
            else if (_useGravityEffect)
                _moveData.reverseGravity = _reverseGravity ? -1f : 1f;

            if (UseGoKzMovement)
            {
                // Source checks water contents during every PlayerMove. The
                // Unity trigger list can change between render Updates, so
                // refresh its flags and Source water level for each 128 Hz
                // movement command as well.
                UpdateTrigger();
                UpdateCameraTrigger();
            }

            if (UseGoKzMovement)
                _controller.EnableGoKzCollision(this);
            else
                _controller.DisableGoKzCollision(this);

            // The Source hull must exist before ProcessMovement performs its
            // initial ground categorization, duck update, or water sampling.
            _controller.ProcessMovement(this, moveConfig, Time.fixedDeltaTime, UseGoKzMovement);

            if (UseGoKzMovement)
                _queuedGokzJump = false;

            if (UseGoKzMovement)
            {
                if (MoveEnabled)
                    _controller.MoveGoKz(this);
                _controller.FinishGoKz(this);
                // wishJump is a per-command button request.  It must not
                // remain true until the next rendered Update, otherwise a
                // frame containing multiple fixed ticks can submit the same
                // jump to more than one movement tick.
                _moveData.wishJump = false;
                UpdateGoKzViewHeight();
            }
            else
            {
                if (MoveEnabled)
                    cc.Move(MoveData.Velocity * Time.fixedDeltaTime);
            }

            MoveData.Origin = transform.position;
            prevPosition = transform.position;
            //print(MoveData.Velocity.magnitude);
        }

        private void ApplyGokzInputForTick()
        {
            _moveData.verticalAxis = _gokzVerticalAxis;
            _moveData.horizontalAxis = _gokzHorizontalAxis;
            _moveData.ForwardMove = _gokzForwardMove;
            _moveData.SideMove = _gokzSideMove;
            _moveData.moveLeft = _gokzMoveLeft;
            _moveData.moveRight = _gokzMoveRight;
            _moveData.moveForward = _gokzMoveForward;
            _moveData.moveBack = _gokzMoveBack;
            _moveData.jumpHeld = _gokzJumpHeld;
            _moveData.crouching = _gokzCrouching;
            _moveData.sprinting = _gokzSprinting;

            // A jump press or wheel tick is latched until exactly one fixed
            // movement step consumes it.  Holding jump remains represented by
            // jumpHeld and is handled by the mode settings, not by repeating
            // a one-shot request here.
            _moveData.wishJump = _queuedGokzJump && jumpEnabled;
            _queuedGokzJump = false;

            InputButtons sourceButtons = InputButtons.None;
            if (_moveData.wishJump || _moveData.jumpHeld)
                sourceButtons |= InputButtons.Jump;
            if (_moveData.crouching)
                sourceButtons |= InputButtons.Duck;
            if (_moveData.sprinting)
                sourceButtons |= InputButtons.Speed;
            if (_moveData.moveLeft)
                sourceButtons |= InputButtons.MoveLeft;
            if (_moveData.moveRight)
                sourceButtons |= InputButtons.MoveRight;
            if (_moveData.moveForward)
                sourceButtons |= InputButtons.Forward;
            if (_moveData.moveBack)
                sourceButtons |= InputButtons.Back;
            _moveData.Buttons = (int)sourceButtons;
        }

        private void UpdateGoKzViewHeight()
        {
            if (viewTransform == null)
                return;

            // CSGO's Source player origin is at the feet. The eye is 64 HU
            // standing and 46 HU ducked; the collision hull is 72/54 HU.
            // ViewOffset.y is a project-specific value, so subtracting only
            // 18 HU from it does not reproduce the CSGO camera movement.
            float gravitySign = MoveData.reverseGravity < 0f ? -1f : 1f;
            // The physical hull and eye offset have separate Source state.
            // In an airborne FinishDuck, CS:GO raises the origin by 9 HU and
            // sets the eye offset to 46 HU in the same command.
            _gokzCrouchVisual = Mathf.Clamp01(GokzEyeDuckAmount);
            float eyeHeightHu = Mathf.Lerp(64f, 46f, _gokzCrouchVisual);
            float targetY = eyeHeightHu / GokzKzTimerMovement.HammerUnitsPerUnityUnit * gravitySign;
            Vector3 local = viewTransform.localPosition;
            local.x = ViewOffset.x;
            local.y = targetY;
            local.z = ViewOffset.z;
            viewTransform.localPosition = local;
        }
        private void UpdateSpeedText()
        {
            // GOKZ HUD owns the active movement display. The old single-line
            // display remains available when no GokzHud is present or when
            // the legacy controller is selected.
            if (UseGoKzMovement && GokzHud.Active != null)
                return;

            moveConfig.AutoBhop = GameOption.Get<GraphicOption>().autoBhop;

            SpeedDisplay = GameOption.Get<GraphicOption>().displaySpeed;
            if (SpeedDisplay && SpeedText != null)
            {
                SpeedText.fontSize = (int)(GameOption.Get<GraphicOption>().speedFontSize * 150);
                SpeedText.gameObject.SetActive(true);
                float displaySpeed = new Vector2(MoveData.Velocity.x, MoveData.Velocity.z).magnitude;
                if (UseGoKzMovement)
                    displaySpeed *= GokzKzTimerMovement.HammerUnitsPerUnityUnit;
                SpeedText.text = string.Format("{0:0.00}", displaySpeed);
            }
            else
                SpeedText?.gameObject.SetActive(false);
            //Canvas not derive from monobehavior  so this is wrong!
            /*GameObject[] gameObjects = null;
            if (RidahCanvas != null)
                gameObjects = RidahCanvas.GetComponentsInChildren<GameObject>();
            foreach (GameObject var in gameObjects)
            {
                if (var.name == "SpeedText" && SpeedDisplay)
                {
                    var.GetComponent<Text>().text = MoveData.Velocity.magnitude.ToString();
                }
            }*/
        }
        private void UpdateTestBinds()
        {
            if (Input.GetKeyDown(KeyCode.R)&& ResetEnabled)
            {
                MoveData.Velocity = Vector3.zero;
                if (UseGoKzMovement)
                    _controller.ResetGoKzMovement();
                transform.position = _startPosition;
                GetComponentInChildren<PlayerAiming>().realRotation = _startRotation.eulerAngles;
            }
        }
        private void UpdateResetPos()
        {
            //MoveData.isReset = _isReset;
            if (_isReset)
            {
                if (_resetPosition != null && _resetPosition != Vector3.zero)
                    transform.position = _resetPosition;
                if (_resetRotation != null && _resetRotation != Vector3.zero)
                    GetComponentInChildren<PlayerAiming>().realRotation = _resetRotation;
                _isReset = false;
            }
        }
        private void UpdateFollowing()
        {
            if (_isFollow&& _followPosition != null)
            {
                    transform.position += _followPosition;
            }
        }
        private void UpdateMoveData()
        {
            _moveData.verticalAxis = playerInput.actions["Forward"].ReadValue<float>() - playerInput.actions["Back"].ReadValue<float>();
            _moveData.horizontalAxis = (playerInput.actions["Right"].ReadValue<float>() - playerInput.actions["Left"].ReadValue<float>())
                * (UseGoKzMovement ? 1f : MoveData.reverseGravity);

            //_moveData.sprinting = Input.GetButton ("Sprint");

            var crouch = playerInput.actions["Crouch"].ReadValue<float>() > 0 &&
                crouchingEnabled && _moveData.crouchEnabled;
            var moveLeft = playerInput.actions["Left"].ReadValue<float>() > 0;
            var moveRight = playerInput.actions["Right"].ReadValue<float>() > 0;
            var moveFwd = playerInput.actions["Forward"].ReadValue<float>() > 0;
            var moveBack = playerInput.actions["Back"].ReadValue<float>() > 0;
            var jumpHeld = playerInput.actions["Jump"].ReadValue<float>() > 0;
            var speedHeld = playerInput.actions["SpeedUp"].ReadValue<float>() > 0;
            _moveData.moveLeft = moveLeft;
            _moveData.moveRight = moveRight;
            _moveData.moveForward = moveFwd;
            _moveData.moveBack = moveBack;
            _moveData.jumpHeld = jumpHeld;
            _moveData.sprinting = speedHeld;
            // GOKZ's sv_enablebunnyhopping is not an auto-jump switch. Keep
            // held jump separate; KZTimer's normal habit is a jump press or
            // mouse-wheel tick, not a hidden auto-bhop conversion.
            if (UseGoKzMovement && playerInput.actions["Jump"].triggered)
                _queuedGokzJump = true;

            var jump = UseGoKzMovement
                ? _queuedGokzJump
                : (moveConfig.AutoBhop ? playerInput.actions["Jump"].ReadValue<float>() > 0 : playerInput.actions["Jump"].triggered);
            // GOKZ players commonly bind jump to mouse-wheel-up for bhop
            // timing. Keep it permanently enabled in the pure KZTimer path;
            // the legacy path still follows the project's option.
            bool wheelUp = Input.GetAxis("Mouse ScrollWheel") > 0f;
            if (Mouse.current != null && Mouse.current.scroll.ReadValue().y > 0f)
                wheelUp = true;
            var MWjump = UseGoKzMovement
                ? wheelUp
                : (GameOption.Get<GraphicOption>().middleWheelJump && wheelUp);
            if (UseGoKzMovement && MWjump)
                _queuedGokzJump = true;

            if (!moveLeft && !moveRight)
                _moveData.SideMove = 0f;
            else if (moveLeft)
                _moveData.SideMove = -moveConfig.acceleration;
            else if (moveRight)
                _moveData.SideMove = moveConfig.acceleration;

            if (!moveFwd && !moveBack)
                _moveData.ForwardMove = 0f;
            else if (moveFwd)
                _moveData.ForwardMove = moveConfig.acceleration;
            else if (moveBack)
                _moveData.ForwardMove = -moveConfig.acceleration;

            if ((jump|| MWjump)&&jumpEnabled) _moveData.wishJump = true;
            else _moveData.wishJump = false;

            if (crouch) _moveData.crouching = true;
            else _moveData.crouching = false;

            if (UseGoKzMovement)
            {
                _gokzVerticalAxis = _moveData.verticalAxis;
                _gokzHorizontalAxis = _moveData.horizontalAxis;
                // CUserCmd defaults are 450 HU/s for forward and side
                // movement. Preserve the actual analog command magnitude
                // instead of feeding the legacy acceleration setting into
                // the Source movement core.
                _gokzForwardMove = Mathf.Clamp(
                    _moveData.verticalAxis,
                    -1f,
                    1f) * SourceClientMoveSpeed;
                _gokzSideMove = Mathf.Clamp(
                    _moveData.horizontalAxis,
                    -1f,
                    1f) * SourceClientMoveSpeed;
                _gokzMoveLeft = moveLeft;
                _gokzMoveRight = moveRight;
                _gokzMoveForward = moveFwd;
                _gokzMoveBack = moveBack;
                _gokzJumpHeld = jumpHeld;
                _gokzCrouching = crouch;
                _gokzSprinting = speedHeld;
            }

            float viewYaw = viewTransform != null ? viewTransform.eulerAngles.y : transform.eulerAngles.y;
            float viewYawDelta = _hasGokzViewYaw
                ? Mathf.DeltaAngle(_lastGokzViewYaw, viewYaw)
                : 0f;
            // Update runs at render rate while the movement solver runs at
            // 128 Hz. Accumulate all mouse yaw until the next movement tick;
            // overwriting this value loses turns whenever a frame contains
            // more than one input update or fixed tick.
            _moveData.ViewYawDelta += viewYawDelta;
            _lastGokzViewYaw = viewYaw;
            _hasGokzViewYaw = true;
            _moveData.ViewAngles = new Vector3(0f, viewYaw, 0f);
            
        }
        private void UpdateGravityEFX()
        {
            if (UseGoKzMovement)
            {
                if (_useGravityEffect)
                    _moveData.reverseGravity = _reverseGravity ? -1f : 1f;

                PlayerAiming aiming = GetComponentInChildren<PlayerAiming>();
                if (aiming != null)
                {
                    float targetRoll = _useGravityEffect && _reverseGravity ? -180f : 0f;
                    aiming.realRotation.z = Mathf.Lerp(
                        aiming.realRotation.z,
                        targetRoll,
                        Time.deltaTime * 5f);
                }
                return;
            }

            GetComponentInChildren<PlayerAiming>().realRotation.z = Mathf.Lerp(GetComponentInChildren<PlayerAiming>().realRotation.z, 0f, Time.deltaTime * 3f);
            if (_useGravityEffect)
            { 
                _moveData.reverseGravity = _reverseGravity ? -1 : 1;
                centerLerp = Mathf.Lerp(centerLerp, _reverseGravity ? 1f : 0f, Time.deltaTime);
                angleLerp = Mathf.Lerp(angleLerp, _reverseGravity ? 1f : 0f, Time.deltaTime*5);
                t_height = Mathf.Lerp(cc.height / 2, -cc.height / 2, centerLerp);
                cc.center = new Vector3(0, t_height, 0);
                float t_ViewOffset = Mathf.Lerp(ViewOffset.y, -ViewOffset.y, centerLerp);
                if (!_moveData.crouching&&_controller.crouchLerp<.01&&_controller.CrouchUp)
                {
                        viewTransform.localPosition = new Vector3(ViewOffset.x, t_ViewOffset, ViewOffset.z);
                }
                float t_Angle = Mathf.Lerp(0, -180, angleLerp);
                GetComponentInChildren<PlayerAiming>().realRotation.z = t_Angle;
            }
        }
        private void UpdateAudioData()
        {
            if (_moveData.hasJump && (MoveData.grounded || (cc.isGrounded&&!_reverseGravity))&& landAudio!= null)
            {
                audioSource.clip = landAudio;
                audioSource.Play();
                _moveData.hasJump = false;
            }
        }
        private void UpdateCharacterScale()
        {
            if (_isChangeScale)
            {if (scale < .3) _moveData.crouchEnabled = false;
                else _moveData.crouchEnabled = true;
                _moveData.scale = scale;
                Vector3 _ViewOffset = new Vector3(ViewOffset.x, ViewOffset.y, ViewOffset.z) * scale;
                float _defaultHeight = defaultHeight * scale;
                float _defaultCrouchHeight = crouchingHeight * scale;
                viewTransform.localPosition = _ViewOffset;
                _moveData.viewTransform = viewTransform;
                _moveData.viewTransformDefaultLocalPos = viewTransform.localPosition;
                _moveData.viewTransformDefaultWorldPos = viewTransform.position;
                _moveData.defaultHeight = _defaultHeight;
                _moveData.crouchingHeight = _defaultCrouchHeight;

                cc.center= ccCenter*scale;
                cc.height =  ccHeight*scale;
                cc.radius =  ccRadius*scale;
                cc.stepOffset= ccStepOffset* scale;

                moveConfig.JumpPower = _moveConfig.JumpPower * scale;
                moveConfig.Gravity = _moveConfig.Gravity * scale;
                moveConfig.walkSpeed = _moveConfig.walkSpeed * scale;
                moveConfig.deceleration = _moveConfig.deceleration * scale;
                moveConfig.crouchSpeed = _moveConfig.crouchSpeed * scale;
                moveConfig.crouchDeceleration = _moveConfig.crouchDeceleration * scale;
                _moveData.airAccel = _moveConfig.AirAccel * scale * scale;

                SuperEnginal.instance.normalPickDistance = 5 * scale;//change!!!!!!!!!
                _isChangeScale = false;
            }
        }
        private void UpdateTrigger()
        {
            underwater = false;
            Booster = false;
            Bouncer = false;
            LoseWeight = false;
            triggers.RemoveAll(item => item == null);
            foreach (Collider trigger in triggers)
            {
                if (trigger == null)
                    continue;

                if (trigger.tag == "Water")
                    underwater = true;
                if (trigger.tag == "Booster")
                    Booster = true;
                if (trigger.tag == "Bouncer")
                    Bouncer = true;
                if (trigger.tag == "LoseWeight")
                    LoseWeight = true;
            }
        }
        private void UpdateCameraTrigger()
        {
            MoveData.underwater = underwater;
            bool hasSourceWaterMetadata = false;
            foreach (Collider trigger in triggers)
            {
                if (trigger == null)
                    continue;

                GokzWaterVolume water = trigger.GetComponentInParent<GokzWaterVolume>();
                GokzMovementVolume movementVolume =
                    trigger.GetComponentInParent<GokzMovementVolume>();
                if (water != null ||
                    (movementVolume != null && movementVolume.IsWater))
                {
                    hasSourceWaterMetadata = true;
                    break;
                }
            }

            // Legacy Water-tag triggers have no surface plane, so retain their
            // previous full-submersion interpretation only when no explicit
            // Source-style water metadata is available.
            MoveData.waterLevel = underwater && !hasSourceWaterMetadata
                ? GokzWaterLevel.Eyes
                : GokzWaterLevel.NotInWater;
            MoveData.waterType = GokzWaterType.Water;
            foreach (Collider trigger in triggers)
            {
                if (trigger == null)
                    continue;

                GokzWaterVolume water = trigger.GetComponentInParent<GokzWaterVolume>();
                if (water != null)
                {
                    GokzWaterLevel level = water.GetLevel(
                        transform.position,
                        GokzCrouched,
                        GokzEyeDuckAmount);
                    if (level > MoveData.waterLevel)
                    {
                        MoveData.waterLevel = level;
                        MoveData.waterType = water.waterType;
                    }
                }

                GokzMovementVolume movementVolume =
                    trigger.GetComponentInParent<GokzMovementVolume>();
                if (movementVolume != null && movementVolume.IsWater)
                {
                    GokzWaterLevel level = movementVolume.GetWaterLevel(
                        transform.position,
                        GokzCrouched,
                        GokzEyeDuckAmount);
                    if (level > MoveData.waterLevel)
                    {
                        MoveData.waterLevel = level;
                        MoveData.waterType = movementVolume.WaterType;
                    }
                }
            }
            MoveData.underwater = MoveData.waterLevel >= GokzWaterLevel.Waist;
            MoveData.Booster = Booster;
            MoveData.Bouncer = Bouncer;
            MoveData.LoseWeight = LoseWeight;
        }
        private void DisableInput()
        {
            _moveData.Velocity = Vector3.zero;
            _moveData.verticalAxis = 0f;
            _moveData.horizontalAxis = 0f;
            _moveData.SideMove = 0f;
            _moveData.ForwardMove = 0f;
            _moveData.moveLeft = false;
            _moveData.moveRight = false;
            _moveData.moveForward = false;
            _moveData.moveBack = false;
            _moveData.jumpHeld = false;
            _moveData.crouching = false;
            _moveData.sprinting = false;
            _moveData.wishJump = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggers.Contains(other))
                triggers.Add(other);
        }
        private void OnTriggerExit(Collider other)
        {
            triggers.Remove(other);
        }

        /*private void OnCollisionStay(Collision collision)
        {

            if (collision.rigidbody == null)
                return;

            Vector3 relativeVelocity = collision.relativeVelocity * collision.rigidbody.mass / 50f;
            Vector3 impactVelocity = new Vector3(relativeVelocity.x * 0.0025f, relativeVelocity.y * 0.00025f, relativeVelocity.z * 0.0025f);

            float maxYVel = Mathf.Max(MoveData.Velocity.y, 10f);
            Vector3 newVelocity = new Vector3(MoveData.Velocity.x + impactVelocity.x, Mathf.Clamp(MoveData.Velocity.y + Mathf.Clamp(impactVelocity.y, -0.5f, 0.5f), -maxYVel, maxYVel), MoveData.Velocity.z + impactVelocity.z);

            newVelocity = Vector3.ClampMagnitude(newVelocity, Mathf.Max(MoveData.Velocity.magnitude, 30f));
            MoveData.Velocity = newVelocity;
        }*/

    }
}

