using UnityEngine;
using Unity.Cinemachine;
using GameOptionsUtility;
namespace RidahFPS.Controllers {
	public class PlayerAiming : MonoBehaviour
	{
		public bool lockView = false;
		[Header("VirtualCamera")]
		public CinemachineVirtualCamera virtualCamera;

		[Header("References")]
		public Transform bodyTransform;

		[Header("Sensitivity")]
		public float sensitivityMultiplier = 1f;
		public float horizontalSensitivity = 1f;
		public float verticalSensitivity = 1f;

		[Header("MiddleWheelZoom")]
		public float scaleSpeed = 10.0f;
		private float minScale = 1.0f;
		private float maxScale = 60.0f;
		private float currentScale;

		[Header("Restrictions")]
		public float minYRotation = -90f;
		public float maxYRotation = 90f;

		//The real rotation of the camera without recoil
		public Vector3 realRotation;

		/// <summary>
		/// Wraps euler angles into (-180, 180].
		/// </summary>
		/// <remarks>
		/// Quaternion.eulerAngles always reports [0, 360), so a rotation that is
		/// a hair short of a full turn comes back as 359.99 instead of -0.01.
		/// Anything that then eases that angle toward zero sweeps the entire
		/// circle on the way. Every writer that stores an euler decomposition
		/// into realRotation has to pass through here.
		/// </remarks>
		public static Vector3 WrapViewAngles(Vector3 euler)
		{
			return new Vector3(
				Mathf.DeltaAngle(0f, euler.x),
				Mathf.DeltaAngle(0f, euler.y),
				Mathf.DeltaAngle(0f, euler.z));
		}

		private Vector3 portalEyeOffsetWorld;

		[Header("Aimpunch")]
		[Tooltip("bigger number makes the response more damped, smaller is less damped, currently the system will overshoot, with larger damping values it won't")]
		public float punchDamping = 9.0f;

		[Tooltip("bigger number increases the speed at which the view corrects")]
		public float punchSpringConstant = 65.0f;

		[HideInInspector]
		public Vector2 punchAngle;

		[HideInInspector]
		public Vector2 punchAngleVel;

		LZM player;

		private void Start()
		{
			player = LZM.Player;
			currentScale = Camera.main.fieldOfView;
			if (virtualCamera != null)
				currentScale = virtualCamera.m_Lens.FieldOfView;
			// Lock the mouse
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}

		private void Update()
		{
			sensitivityMultiplier = GameOption.Get<GraphicOption>().sensitivity * 3 + .1f;

			//Unity wierd rotation
			if (realRotation.x > 180.0f)
			{
				realRotation.x -= 360.0f;
			}

			// SurfCharacter eases the roll toward zero every frame. A writer
			// that stored an euler decomposition can leave it on 359.99, and
			// Lerp(359.99, 0) then spins the view once before settling. Keep it
			// on the short side, whichever path set it.
			realRotation.z = Mathf.DeltaAngle(0f, realRotation.z);

			//MiddleMouseScrollScale();
			// Fix pausing
			if (Mathf.Abs(Time.timeScale) <= 0)
				return;

			// Input
			float xMovement = Input.GetAxisRaw("Mouse X") * horizontalSensitivity * sensitivityMultiplier * (lockView ? 0 : 1) *player.MoveData.reverseGravity;
			float yMovement = -Input.GetAxisRaw("Mouse Y") * verticalSensitivity * sensitivityMultiplier * (lockView ? 0 : 1) * player.MoveData.reverseGravity;

			// Calculate real rotation from input
			realRotation = new Vector3(Mathf.Clamp(realRotation.x + yMovement, minYRotation, maxYRotation), realRotation.y + xMovement, realRotation.z);
			//realRotation.z = Mathf.Lerp(realRotation.z, 0f, Time.deltaTime * 3f);

			//Apply real rotation to body
			bodyTransform.eulerAngles = Vector3.Scale(realRotation, new Vector3(0f, 1f, 0f));

			//Apply rotation and recoil
			realRotation.x += punchAngle.x;
			realRotation.y += punchAngle.y;

			transform.eulerAngles = realRotation;
		}

		public void SetPortalEyeOffset(Vector3 transformedOldEyePosition,
			Vector3 newEyePosition)
		{
			portalEyeOffsetWorld = transformedOldEyePosition - newEyePosition;
		}

		/// <summary>
		/// Portal movement compatibility value. The baseline portal camera reads
		/// the main camera transform directly and does not apply this offset.
		/// </summary>
		public Vector3 PortalEyeOffsetWorld => portalEyeOffsetWorld;

		/// <summary>
		/// Returns the current camera position for movement code. Portal view
		/// rendering uses the camera transform directly.
		/// </summary>
		public Vector3 GetPortalBaseEyePosition()
		{
			return transform.position;
		}

		public void ViewPunch(Vector2 punchAmount)
		{
			//Remove previous recoil
			punchAngle = Vector2.zero;

			//Recoil go up
			punchAngleVel -= punchAmount * 20;
		}
		private void DecayPunchAngle()
		{
			if (punchAngle.sqrMagnitude > 0.001 || punchAngleVel.sqrMagnitude > 0.001)
			{
				punchAngle += punchAngleVel * Time.deltaTime;
				float damping = 1 - (punchDamping * Time.deltaTime);

				if (damping < 0)
					damping = 0;

				punchAngleVel *= damping;

				float springForceMagnitude = punchSpringConstant * Time.deltaTime;
				punchAngleVel -= punchAngle * springForceMagnitude;
			}
			else
			{
				punchAngle = Vector2.zero;
				punchAngleVel = Vector2.zero;
			}
		}
		private void MiddleMouseScrollScale()
		{
			currentScale -= Input.GetAxis("Mouse ScrollWheel") * scaleSpeed;

			currentScale = Mathf.Clamp(currentScale, minScale, maxScale);
			if (Camera.main.orthographic == true)
			{
				Camera.main.orthographicSize = currentScale;
			}
			else
			{
				Camera.main.fieldOfView = currentScale;
			}
			if (virtualCamera != null)
			{
				virtualCamera.m_Lens.FieldOfView = currentScale;
			}
		}

	}
}
