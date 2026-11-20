using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using GameplayIngredients;
using GameOptionsUtility;
using Ridah.Weapon;

namespace RidahFPS.Controllers
{
    [AddComponentMenu("Ridah/GOKZ KZTimer Character")]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    public class LZM : MonoBehaviour, IGokzMovementPlayer
    {
        public static LZM Player { get; private set; }

        [Header("Weapon")]
        public WeaponController[] weapon;

        [Header("Movement")]
        [Tooltip("KZTimer movement always runs at the Source/GOKZ 128 tick interval.")]
        public bool jumpEnabled = true;
        public bool crouchingEnabled = true;
        [HideInInspector] public bool ResetEnabled = true;
        [HideInInspector] public bool MoveEnabled = true;

        [Header("View")]
        public Transform viewTransform;

        [Header("HUD")]
        public bool SpeedDisplay = true;
        [SerializeField] private Text SpeedText;
        [SerializeField] internal GameObject Canvas;

        [Header("Input")]
        [SerializeField] private PlayerInput playerInput;

        [Header("Audio")]
        [SerializeField] private AudioClip landAudio;
        private AudioSource audioSource;

        private readonly MoveData _moveData = new MoveData();
        internal CharacterController cc;
        private readonly GokzMovementDriver _movement = new GokzMovementDriver();
        private GokzPlayerFeatures gokzFeatures;
        private Vector3 _baseVelocity;
        private Vector3 prevPosition;
        internal Vector3 _startPosition;
        internal Quaternion _startRotation;
        internal Vector3 _resetPosition;
        internal Vector3 _resetRotation;
        internal bool _isReset;
        internal Vector3 _followPosition;
        internal bool _isFollow;
        internal float scale = 1f;
        internal bool _isChangeScale;
        private Vector3 _initialCharacterCenter;
        private float _initialCharacterHeight;
        private float _initialCharacterRadius;
        private float _initialCharacterStepOffset;
        internal bool _reverseGravity;
        internal bool _useGravityEffect;

        private bool _underwater;
        private bool _loseWeight;
        private bool _queuedGokzJump;
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
        private readonly List<Collider> _triggers = new List<Collider>();
        private float _lastGokzViewYaw;
        private bool _hasGokzViewYaw;
        private PlayerAiming playerAiming;
        private PortablePlayer portablePlayer;

        public MoveData MoveData => _moveData;
        public CharacterController CC => cc;
        public Vector3 BaseVelocity => _baseVelocity;
        public Vector3 Forward => viewTransform != null ? viewTransform.forward : transform.forward;
        public Vector3 Right => viewTransform != null ? viewTransform.right : transform.right;
        public bool GokzGrounded => _movement.Grounded;
        public bool GokzCrouched => _movement.Crouched;
        public float GokzEyeDuckAmount => _movement.EyeDuckAmount;
        public bool GokzLastJumpWasPerfect => _movement.LastJumpWasPerfect;
        public bool GokzLastJumpWasDuck => _movement.LastJumpWasDuck;
        public int GokzJumpCount => _movement.JumpCount;
        public float GokzLastTakeoffSpeed => _movement.LastTakeoffSpeed;

        public void ResetGoKzMovementState()
        {
            _movement.ResetState();
        }

        private void Awake()
        {
            if (Player == null)
                Player = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            TransformInit();
            playerAiming = GetComponentInChildren<PlayerAiming>();
            portablePlayer = GetComponent<PortablePlayer>();
            gokzFeatures = GetComponent<GokzPlayerFeatures>();
            _movement.EnableSourceCollision(this);
            Time.fixedDeltaTime = SourceMovementCore.TickInterval;
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
            if (!Mathf.Approximately(Time.fixedDeltaTime, SourceMovementCore.TickInterval))
                Time.fixedDeltaTime = SourceMovementCore.TickInterval;

            foreach (WeaponController weaponController in weapon)
            {
                if (weaponController != null && weaponController.isActiveAndEnabled)
                    weaponController.UpdateWeapon();
            }

            UpdateMovement();
            if (playerAiming != null)
                playerAiming.AdvancePortalEyeOffset(
                    SourceMovementCore.TickInterval);
            UpdateGravityEFX();
            UpdateFollowing();
            UpdateResetPos();
            UpdateAudioData();
        }

        private void OnDisable()
        {
            DisableInput();
            _movement.DisableSourceCollision(this);
        }

        private void TransformInit()
        {
            audioSource = GetComponent<AudioSource>();
            cc = GetComponent<CharacterController>();
            _initialCharacterCenter = cc.center;
            _initialCharacterHeight = cc.height;
            _initialCharacterRadius = cc.radius;
            _initialCharacterStepOffset = cc.stepOffset;

            prevPosition = transform.position;
            _startPosition = transform.position;
            _startRotation = transform.rotation;

            if (viewTransform == null && Camera.main != null)
                viewTransform = Camera.main.transform;
            if (viewTransform != null)
            {
                viewTransform.SetParent(transform, false);
                viewTransform.localPosition = new Vector3(
                    0f,
                    64f / SourceMovementCore.HammerUnitsPerUnityUnit,
                    0f);
                viewTransform.localRotation = Quaternion.identity;
            }

            PlayerAiming aiming = GetComponentInChildren<PlayerAiming>();
            _lastGokzViewYaw = aiming != null
                ? aiming.realRotation.y
                : (viewTransform != null ? viewTransform.eulerAngles.y : transform.eulerAngles.y);
            _hasGokzViewYaw = true;
        }

        internal void MoveDataInit()
        {
            _moveData.playerTransform = transform;
            _moveData.Origin = transform.position;
            _moveData.reverseGravity = _useGravityEffect && _reverseGravity ? -1f : 1f;
        }

        private void UpdateMovement()
        {
            ApplyGokzInputForTick();

            Vector3 positionBeforeMovement = transform.position;
            Vector3 velocityBeforeMovement = _moveData.Velocity;
            float viewYawDeltaForStats = _moveData.ViewYawDelta;

            Vector3 externalMovement = transform.position - prevPosition;
            _moveData.gameCodeMovedPlayer = externalMovement.sqrMagnitude > 0.00000001f;
            _moveData.Origin += externalMovement;

            _moveData.reverseGravity = _useGravityEffect && _reverseGravity ? -1f : 1f;
            UpdateTrigger();
            UpdateCameraTrigger();
            _movement.EnableSourceCollision(this);

            GokzRuntime runtime = GokzRuntime.Instance;
            _movement.SetMode(runtime != null ? runtime.Mode : GokzMode.KzTimer);
            _movement.Process(this);
            if (MoveEnabled)
                _movement.Move(this);
            _movement.Finish(this);

            if (gokzFeatures != null)
                gokzFeatures.RecordMovementTick(positionBeforeMovement, velocityBeforeMovement, viewYawDeltaForStats);

            _moveData.wishJump = false;
            UpdateGoKzViewHeight();
            if (portablePlayer != null)
                portablePlayer.ApplyPendingPortalEyeOffset();
            _moveData.Origin = transform.position;
            prevPosition = transform.position;
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
            _moveData.jumpHeld = _gokzJumpHeld && jumpEnabled;
            _moveData.crouching = _gokzCrouching;
            _moveData.sprinting = _gokzSprinting;
            _moveData.wishJump = _queuedGokzJump && jumpEnabled;
            _queuedGokzJump = false;

            InputButtons buttons = InputButtons.None;
            if (_moveData.wishJump || _moveData.jumpHeld)
                buttons |= InputButtons.Jump;
            if (_moveData.crouching)
                buttons |= InputButtons.Duck;
            if (_moveData.sprinting)
                buttons |= InputButtons.Speed;
            if (_moveData.moveLeft)
                buttons |= InputButtons.MoveLeft;
            if (_moveData.moveRight)
                buttons |= InputButtons.MoveRight;
            if (_moveData.moveForward)
                buttons |= InputButtons.Forward;
            if (_moveData.moveBack)
                buttons |= InputButtons.Back;
            _moveData.Buttons = (int)buttons;
        }

        private void UpdateGoKzViewHeight()
        {
            if (viewTransform == null)
                return;

            float gravitySign = _moveData.reverseGravity < 0f ? -1f : 1f;
            float eyeHeightHu = Mathf.Lerp(64f, 46f, Mathf.Clamp01(GokzEyeDuckAmount));
            float height = eyeHeightHu / SourceMovementCore.HammerUnitsPerUnityUnit;
            height *= scale * gravitySign;
            viewTransform.localPosition = new Vector3(0f, height, 0f);
        }

        private void UpdateSpeedText()
        {
            if (GokzHud.Active != null)
                return;

            SpeedDisplay = GameOption.Get<GraphicOption>().displaySpeed;
            if (SpeedDisplay && SpeedText != null)
            {
                SpeedText.fontSize = (int)(GameOption.Get<GraphicOption>().speedFontSize * 150);
                SpeedText.gameObject.SetActive(true);
                float displaySpeed = new Vector2(_moveData.Velocity.x, _moveData.Velocity.z).magnitude;
                displaySpeed *= SourceMovementCore.HammerUnitsPerUnityUnit;
                SpeedText.text = string.Format("{0:0.00}", displaySpeed);
            }
            else if (SpeedText != null)
            {
                SpeedText.gameObject.SetActive(false);
            }
        }

        private void UpdateTestBinds()
        {
            if (Input.GetKeyDown(KeyCode.R) && ResetEnabled)
            {
                _moveData.Velocity = Vector3.zero;
                _movement.ResetState();
                transform.position = _startPosition;
                PlayerAiming aiming = GetComponentInChildren<PlayerAiming>();
                if (aiming != null)
                    aiming.realRotation = _startRotation.eulerAngles;
            }
        }

        private void UpdateResetPos()
        {
            if (!_isReset)
                return;

            if (_resetPosition != Vector3.zero)
                transform.position = _resetPosition;
            if (_resetRotation != Vector3.zero)
            {
                PlayerAiming aiming = GetComponentInChildren<PlayerAiming>();
                if (aiming != null)
                    aiming.realRotation = _resetRotation;
            }
            _isReset = false;
        }

        private void UpdateFollowing()
        {
            if (_isFollow)
                transform.position += _followPosition;
        }

        private void UpdateMoveData()
        {
            if (playerInput == null)
                return;

            float forward = playerInput.actions["Forward"].ReadValue<float>();
            float back = playerInput.actions["Back"].ReadValue<float>();
            float right = playerInput.actions["Right"].ReadValue<float>();
            float left = playerInput.actions["Left"].ReadValue<float>();

            _gokzVerticalAxis = forward - back;
            _gokzHorizontalAxis = right - left;
            _gokzForwardMove = Mathf.Clamp(_gokzVerticalAxis, -1f, 1f) * SourceClientMoveSpeed;
            _gokzSideMove = Mathf.Clamp(_gokzHorizontalAxis, -1f, 1f) * SourceClientMoveSpeed;
            _gokzMoveLeft = left > 0f;
            _gokzMoveRight = right > 0f;
            _gokzMoveForward = forward > 0f;
            _gokzMoveBack = back > 0f;
            _gokzJumpHeld = jumpEnabled && playerInput.actions["Jump"].ReadValue<float>() > 0f;
            _gokzCrouching = crouchingEnabled && scale >= 0.3f &&
                playerInput.actions["Crouch"].ReadValue<float>() > 0f;
            _gokzSprinting = playerInput.actions["SpeedUp"].ReadValue<float>() > 0f;

            if (!jumpEnabled)
                _queuedGokzJump = false;
            else if (playerInput.actions["Jump"].triggered || IsWheelUp())
                _queuedGokzJump = true;

            float viewYaw = viewTransform != null
                ? viewTransform.eulerAngles.y
                : transform.eulerAngles.y;
            float yawDelta = _hasGokzViewYaw
                ? Mathf.DeltaAngle(_lastGokzViewYaw, viewYaw)
                : 0f;
            _moveData.ViewYawDelta += yawDelta;
            _lastGokzViewYaw = viewYaw;
            _hasGokzViewYaw = true;
        }

        private static bool IsWheelUp()
        {
            if (Mouse.current != null && Mouse.current.scroll.ReadValue().y > 0f)
                return true;
            return Input.GetAxis("Mouse ScrollWheel") > 0f;
        }

        private void UpdateGravityEFX()
        {
            PlayerAiming aiming = GetComponentInChildren<PlayerAiming>();
            if (aiming == null)
                return;

            float targetRoll = _useGravityEffect && _reverseGravity ? -180f : 0f;
            aiming.realRotation.z = Mathf.Lerp(
                aiming.realRotation.z,
                targetRoll,
                Time.deltaTime * 5f);
        }

        private void UpdateAudioData()
        {
            if (_moveData.hasJump && _moveData.grounded && landAudio != null)
            {
                audioSource.clip = landAudio;
                audioSource.Play();
                _moveData.hasJump = false;
            }
        }

        private void UpdateCharacterScale()
        {
            if (!_isChangeScale)
                return;

            cc.center = _initialCharacterCenter * scale;
            cc.height = _initialCharacterHeight * scale;
            cc.radius = _initialCharacterRadius * scale;
            cc.stepOffset = _initialCharacterStepOffset * scale;

            SuperEnginal engine = SuperEnginal.instance;
            if (engine != null)
                engine.normalPickDistance = 5f * scale;
            _isChangeScale = false;
        }

        private void UpdateTrigger()
        {
            _underwater = false;
            _loseWeight = false;
            _triggers.RemoveAll(item => item == null);
            foreach (Collider trigger in _triggers)
            {
                if (trigger == null)
                    continue;
                if (trigger.CompareTag("Water"))
                    _underwater = true;
                if (trigger.CompareTag("LoseWeight"))
                    _loseWeight = true;
            }
        }

        private void UpdateCameraTrigger()
        {
            _moveData.underwater = _underwater;
            bool hasSourceWaterMetadata = false;
            foreach (Collider trigger in _triggers)
            {
                if (trigger == null)
                    continue;
                GokzWaterVolume water = trigger.GetComponentInParent<GokzWaterVolume>();
                GokzMovementVolume movementVolume = trigger.GetComponentInParent<GokzMovementVolume>();
                if (water != null || (movementVolume != null && movementVolume.IsWater))
                {
                    hasSourceWaterMetadata = true;
                    break;
                }
            }

            _moveData.waterLevel = _underwater && !hasSourceWaterMetadata
                ? GokzWaterLevel.Eyes
                : GokzWaterLevel.NotInWater;
            _moveData.waterType = GokzWaterType.Water;
            foreach (Collider trigger in _triggers)
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
                    if (level > _moveData.waterLevel)
                    {
                        _moveData.waterLevel = level;
                        _moveData.waterType = water.waterType;
                    }
                }

                GokzMovementVolume movementVolume = trigger.GetComponentInParent<GokzMovementVolume>();
                if (movementVolume != null && movementVolume.IsWater)
                {
                    GokzWaterLevel level = movementVolume.GetWaterLevel(
                        transform.position,
                        GokzCrouched,
                        GokzEyeDuckAmount);
                    if (level > _moveData.waterLevel)
                    {
                        _moveData.waterLevel = level;
                        _moveData.waterType = movementVolume.WaterType;
                    }
                }
            }

            _moveData.underwater = _moveData.waterLevel >= GokzWaterLevel.Waist;
            _moveData.LoseWeight = _loseWeight;
        }

        private void DisableInput()
        {
            _moveData.Velocity = Vector3.zero;
            _gokzVerticalAxis = 0f;
            _gokzHorizontalAxis = 0f;
            _gokzForwardMove = 0f;
            _gokzSideMove = 0f;
            _gokzMoveLeft = false;
            _gokzMoveRight = false;
            _gokzMoveForward = false;
            _gokzMoveBack = false;
            _gokzJumpHeld = false;
            _gokzCrouching = false;
            _gokzSprinting = false;
            _queuedGokzJump = false;
            _moveData.wishJump = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!_triggers.Contains(other))
                _triggers.Add(other);
        }

        private void OnTriggerExit(Collider other)
        {
            _triggers.Remove(other);
        }
    }
}
