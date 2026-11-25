using UnityEngine;
using GameOptionsUtility;
namespace RidahFPS.Controllers {
	public class PlayerAiming : MonoBehaviour
	{
		public bool lockView = false;

		[Header("Camera")]
		[Tooltip("CS:GO view FOV expressed as Unity's vertical field of view, in degrees.")]
		[Range(1f, 179f)]
		public float csgoVerticalFieldOfView = 73.73979f;
		[Min(0.001f)]
		public float defaultNearClipPlane = 0.01f;
		[Min(0.001f)]
		public float portalNearClipPlane = 0.01905f;

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
		/// Wraps euler angles into (-180, 180]. LUO movement code stores
		/// source-style view angles here, so every writer must use the short
		/// representation. This is movement compatibility only; portal eye
		/// offset methods below remain exactly as in f5eaa77.
		/// </summary>
		public static Vector3 WrapViewAngles(Vector3 euler)
		{
			return new Vector3(
				Mathf.DeltaAngle(0f, euler.x),
				Mathf.DeltaAngle(0f, euler.y),
				Mathf.DeltaAngle(0f, euler.z));
		}

		private Vector3 portalEyeOffsetWorld;
		private Camera portalViewCamera;
		public Vector3 PortalEyeOffsetWorld => portalEyeOffsetWorld;

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
			currentScale = csgoVerticalFieldOfView;
			// Lock the mouse
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}

		private void Awake()
		{
			CachePortalViewCamera();
		}

		private void Update()
		{
			sensitivityMultiplier = GameOption.Get<GraphicOption>().sensitivity * 3 + .1f;

			//Unity wierd rotation
			if (realRotation.x > 180.0f)
			{
				realRotation.x -= 360.0f;
			}

			// Keep roll on the short side for the LUO movement lerp path.
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
			// C_Portal_Player::ClientThink also calls Reorient, independently
			// of the movement-command PostThink path advanced by FixedUpdate.
			AdvancePortalEyeOffset(Time.deltaTime);
		}

		private void LateUpdate()
		{
			ApplySourceCameraSettings();
		}

		private void ApplySourceCameraSettings()
		{
			CachePortalViewCamera();
			if (portalViewCamera == null)
				return;

			portalViewCamera.fieldOfView = csgoVerticalFieldOfView;
			bool portalPairActive = RecursivePortalCamera.Active != null &&
				RecursivePortalCamera.Active.HasActivePortalPair;
			float requiredNearClipPlane = portalPairActive
				? portalNearClipPlane
				: defaultNearClipPlane;
			if (!Mathf.Approximately(
					portalViewCamera.nearClipPlane,
					requiredNearClipPlane))
				portalViewCamera.nearClipPlane = requiredNearClipPlane;
		}

		public void SetPortalEyeOffset(Vector3 transformedOldEyePosition,
			Vector3 newEyePosition)
		{
			// Source CPortal_Player::SetEyeOffset only stores
			// vOldOrigin - vNewOrigin in m_PortalLocal.m_vEyeOffset. It never
			// writes the player/camera transform. EyePosition() adds this
			// render-only offset later, during CalcView.
			portalEyeOffsetWorld = transformedOldEyePosition - newEyePosition;
		}

		public Vector3 GetPortalBaseEyePosition()
		{
			CachePortalViewCamera();
			if (portalViewCamera == null)
				return transform.position;

			// Source reads the live GetViewOffset() every time CalcView runs.
			// LUO changes the child camera height while ducking/landing, so
			// caching it in Awake would make the base eye stale. The portal
			// offset is no longer written into this transform, so reading the
			// current localPosition is both live and free of feedback.
			Transform cameraTransform = portalViewCamera.transform;
			Transform cameraParent = cameraTransform.parent;
			return cameraParent != null
				? cameraParent.TransformPoint(cameraTransform.localPosition)
				: cameraTransform.localPosition;
		}

		public void AdvancePortalEyeOffset(float tickInterval)
		{
			if (tickInterval <= 0f)
				return;

			// Source ExponentialDecay(0.2f, frametime) treats 0.2 seconds as
			// the half-life. ClientThink and movement-command PostThink each
			// advance it with their own frame interval, as Source does.
			portalEyeOffsetWorld *= Mathf.Exp(
				-0.69314718f / 0.2f * tickInterval);
		}

		private void CachePortalViewCamera()
		{
			if (portalViewCamera != null)
				return;

			portalViewCamera = GetComponentInChildren<Camera>(true);
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
		}

	}
}
