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

		private Vector3 portalEyeOffsetWorld;
		private Vector3 appliedPortalEyeOffsetLocal;

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
			ApplyPortalEyeOffset();
		}

		public void SetPortalEyeOffset(Vector3 transformedOldEyePosition,
			Vector3 newEyePosition)
		{
			portalEyeOffsetWorld = transformedOldEyePosition - newEyePosition;
		}

		private void ApplyPortalEyeOffset()
		{
			Transform parent = transform.parent;
			Vector3 previousOffsetWorld = parent != null
				? parent.TransformVector(appliedPortalEyeOffsetLocal)
				: appliedPortalEyeOffsetLocal;
			Vector3 unoffsetPosition = transform.position - previousOffsetWorld;

			// CPortal_Player::DecayEyeOffset uses a 0.2 second exponential
			// decay so the eye does not visibly snap when the player origin is
			// transformed through a portal.
			portalEyeOffsetWorld *= Mathf.Exp(-Time.deltaTime / 0.2f);
			transform.position = unoffsetPosition + portalEyeOffsetWorld;
			appliedPortalEyeOffsetLocal = parent != null
				? parent.InverseTransformVector(portalEyeOffsetWorld)
				: portalEyeOffsetWorld;
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
