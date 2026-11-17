using UnityEngine;

namespace GameplayIngredients.Controllers
{
    [AddComponentMenu(ComponentMenu.controllerPath + "First Person Controller")]
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : GameplayIngredientsBehaviour
    {
        public bool Paused = false;

        public int TickRate = 128;

        [Header("Objects")]
        [NonNullCheck]
        public Transform m_Camera;
        [NonNullCheck]
        public PlayerInputs m_Input;

        [Header("Metrics")]
        [Range(1.0f, 2.5f)]
        public float PlayerHeight = 1.82f;
        [Range(0.35f, 2.0f)]
        public float PlayerRadius = 0.5f;
        public float PlayerWeight = 3.0f;

        [Header("Movement")]
        [Range(0.0f, 12.5f)]
        public float MoveSpeed = 3.0f;
        public float MaximumFallVelocity = 12f;

        [Header("Jump")]
        public bool EnableJump = true;
        public float JumpImpulse = 7.0f;

        [Header("Look")]
        public float Sensitivity = 1f;
        public float VSensitivity = 360f;
        public float HSensitivity = 360f;
        public float MaxPitch = 80;

        private CharacterController m_Controller;
        private float m_Fall = 0.0f;
        private float m_Pitch = 0.0f;
        private bool m_Grounded = false;

        private void Awake()
        {

            //Application.targetFrameRate = 144;
            //QualitySettings.vSyncCount = 1;
            Time.fixedDeltaTime = 1f / TickRate;
            VSensitivity *= Sensitivity;
            HSensitivity *= Sensitivity;
            m_Input.Init();
        }
        public void Start()
        {
            m_Controller = GetComponent<CharacterController>();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        public void FixedUpdate()
        {
            if (m_Camera == null) return;
            if (m_Input == null) return;
            if (!Paused)
            {
                m_Input.UpdateInput();
                UpdateRotation();
                UpdatePlayerMovement();
            }

        }
        public void Update()
        {
        }
        public void SetPaused(bool paused)
        {
            Paused = paused;
        }

        public void OnValidate()
        {
            float realHeight = PlayerHeight + PlayerRadius;

            var controller = GetComponent<CharacterController>();
            var center = Vector3.zero;

            if (controller.center != center)
                controller.center = center;

            if (controller.height != realHeight)
                controller.height = realHeight;

            if(controller.radius != PlayerRadius)
                controller.radius = PlayerRadius;

            if(m_Camera != null)
                m_Camera.transform.localPosition = new Vector3(0, PlayerHeight, 0);
        }

        public void UpdateRotation()
        {
            m_Pitch = Mathf.Clamp(m_Pitch - (Time.deltaTime * VSensitivity * m_Input.Look.y), -MaxPitch, MaxPitch);
            m_Camera.transform.localEulerAngles = new Vector3(m_Pitch, 0, 0);
            transform.Rotate(transform.up, Time.deltaTime * HSensitivity * m_Input.Look.x);
        }

        public void UpdatePlayerMovement()
        {

            Vector3 move = (transform.forward * m_Input.Movement.y + transform.right * m_Input.Movement.x) * MoveSpeed;

            m_Fall += PlayerWeight * 9.80665f * Time.deltaTime;
            m_Fall = Mathf.Min(m_Fall, MaximumFallVelocity);
            move += m_Fall * (-transform.up);

            if (m_Grounded)
            {
                if (EnableJump && m_Input.Jump == PlayerInputs.ButtonState.JustPressed)
                {
                    m_Fall = -JumpImpulse;
                }
            }

            m_Controller.Move(move * Time.deltaTime);
            m_Grounded = m_Controller.isGrounded;
        }

        public void SetPlayerHeight(float value)
        {
            PlayerHeight = value;
            OnValidate();
        }

        public void SetMoveSpeed(float value)
        {
            MoveSpeed = value;
        }
    }
}
