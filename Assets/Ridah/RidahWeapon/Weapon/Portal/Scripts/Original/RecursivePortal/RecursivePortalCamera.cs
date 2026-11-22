using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;

public class RecursivePortalCamera : MonoBehaviour
{
<<<<<<< HEAD
=======
    private const int PortalCount = 2;
    private const int SourceDefaultRecursionDepth = 2;
    private const int SourceMaxRecursionDepth = 10;
    private const float HammerUnitsPerUnityUnit = 52.4934383202f;
    private const float SourceClipOffsetHammerUnits = 2f;
    private const float SourceFrustumEpsilonHammerUnits = 0.01f;

>>>>>>> 0e6aa0ecb94d332d41d3f3596207ebe635e5ab67
    [SerializeField]
    private Portal[] portals = new Portal[2];

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private Camera portalCamera;

    private RenderTexture tempTexture1;
    private RenderTexture tempTexture2;
    private int textureWidth;
    private int textureHeight;
    private bool rendering;
    private bool mainCameraPoseOverridden;
    private Vector3 previousMainCameraLocalPosition;
    private Quaternion previousMainCameraLocalRotation;

    private const int iterations = 1;

    private void Awake()
    {
        ResolveCameras();
        EnsureRenderTextures();
    }

    private void Start()
    {
        ResolveCameras();
        EnsureRenderTextures();
        BindTextures();
    }

    private void ResolveCameras()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (portalCamera == null)
            portalCamera = GetComponentInChildren<Camera>(true);
    }

    private void EnsureRenderTextures()
    {
        int width = mainCamera != null ? mainCamera.pixelWidth : Screen.width;
        int height = mainCamera != null ? mainCamera.pixelHeight : Screen.height;
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        if (tempTexture1 != null && tempTexture2 != null &&
            textureWidth == width && textureHeight == height)
            return;

        if (tempTexture1 != null)
            tempTexture1.Release();
        if (tempTexture2 != null)
            tempTexture2.Release();

        textureWidth = width;
        textureHeight = height;
        tempTexture1 = CreateTexture("PortalViewA", width, height);
        tempTexture2 = CreateTexture("PortalViewB", width, height);
    }

    private static RenderTexture CreateTexture(string textureName, int width,
        int height)
    {
        RenderTexture texture = new RenderTexture(width, height, 24,
            RenderTextureFormat.ARGB32)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        texture.Create();
        return texture;
    }

    private void BindTextures()
    {
        if (portals != null && portals.Length > 0 && portals[0] != null &&
            portals[0].Renderer != null)
            portals[0].Renderer.material.mainTexture = tempTexture1;
        if (portals != null && portals.Length > 1 && portals[1] != null &&
            portals[1].Renderer != null)
            portals[1].Renderer.material.mainTexture = tempTexture2;
    }

    private void OnEnable()
    {
        RenderPipeline.beginCameraRendering += UpdateCamera;
        RenderPipeline.endCameraRendering += RestoreMainCameraAfterRender;
    }

    private void OnDisable()
    {
        RenderPipeline.beginCameraRendering -= UpdateCamera;
        RenderPipeline.endCameraRendering -= RestoreMainCameraAfterRender;
        RestoreMainCameraPose();
    }

    private void OnDestroy()
    {
        RenderPipeline.beginCameraRendering -= UpdateCamera;
<<<<<<< HEAD
        if (tempTexture1 != null)
            tempTexture1.Release();
        if (tempTexture2 != null)
            tempTexture2.Release();
=======
        RenderPipeline.endCameraRendering -= RestoreMainCameraAfterRender;
        RestoreMainCameraPose();
        for (int i = 0; i < PortalCount; ++i)
        {
            if (portals != null && i < portals.Length &&
                portals[i] != null && portals[i].Renderer != null)
            {
                // MaterialPropertyBlock.SetTexture rejects null textures.
                // This component owns these per-renderer overrides, so remove
                // the block itself before releasing its render textures.
                portals[i].Renderer.SetPropertyBlock(null);
            }
        }
        ReleaseRenderTextures();

        if (terminalStaticTexture != null)
        {
            Destroy(terminalStaticTexture);
            terminalStaticTexture = null;
        }
    }

    private void ReleaseRenderTextures()
    {
        if (portalTextures == null)
            return;

        for (int portalIndex = 0;
             portalIndex < portalTextures.GetLength(0);
             ++portalIndex)
        {
            for (int viewDepth = 0;
                 viewDepth < portalTextures.GetLength(1);
                 ++viewDepth)
            {
                RenderTexture texture = portalTextures[portalIndex, viewDepth];
                if (texture == null)
                    continue;

                texture.Release();
                Destroy(texture);
                portalTextures[portalIndex, viewDepth] = null;
            }
        }

        portalTextures = new RenderTexture[PortalCount, 0];
        allocatedDepth = 0;
>>>>>>> 0e6aa0ecb94d332d41d3f3596207ebe635e5ab67
    }

    private void UpdateCamera(ScriptableRenderContext context, Camera camera)
    {
        ResolveCameras();
<<<<<<< HEAD
        if (camera != mainCamera || mainCamera == null || portalCamera == null ||
            portals == null || portals.Length < 2 || portals[0] == null ||
=======
        if (camera != mainCamera || mainCamera == null)
            return;

        // CalcPortalView changes the rendered eye pose without moving the
        // player entity. Apply that pose before either the portal RTs or the
        // main camera are culled, then restore the gameplay camera afterward.
        RestoreMainCameraPose();
        ApplyMainCameraPortalPose();

        if (rendering ||
            portalCamera == null || portals == null ||
            portals.Length < PortalCount || portals[0] == null ||
>>>>>>> 0e6aa0ecb94d332d41d3f3596207ebe635e5ab67
            portals[1] == null || !portals[0].isPlaced ||
            !portals[1].isPlaced)
            return;

        EnsureRenderTextures();
        BindTextures();
        if (rendering)
            return;

        rendering = true;
        try
        {
<<<<<<< HEAD
            portalCamera.targetTexture = tempTexture1;
            for (int i = iterations - 1; i >= 0; --i)
                RenderCamera(portals[0], portals[1], i, context);
=======
            // Keep the prefab culling mask: it intentionally excludes Player.
            portalCamera.clearFlags = mainCamera.clearFlags == CameraClearFlags.Depth ||
                mainCamera.clearFlags == CameraClearFlags.Nothing
                    ? CameraClearFlags.SolidColor
                    : mainCamera.clearFlags;
            portalCamera.backgroundColor = mainCamera.backgroundColor;
            portalCamera.nearClipPlane = mainCamera.nearClipPlane;
            portalCamera.farClipPlane = mainCamera.farClipPlane;
            portalCamera.aspect = mainCamera.aspect;
>>>>>>> 0e6aa0ecb94d332d41d3f3596207ebe635e5ab67

            portalCamera.targetTexture = tempTexture2;
            for (int i = iterations - 1; i >= 0; --i)
                RenderCamera(portals[1], portals[0], i, context);
        }
        finally
        {
            portalCamera.targetTexture = null;
            portalCamera.ResetProjectionMatrix();
            rendering = false;
        }
    }

<<<<<<< HEAD
    private void RenderCamera(Portal inPortal, Portal outPortal,
        int iterationID, ScriptableRenderContext context)
=======
    private void ApplyMainCameraPortalPose()
    {
        PortablePlayer player =
            mainCamera.GetComponentInParent<PortablePlayer>();
        if (player == null)
            return;

        // PlayerAiming applies Source's decaying eye offset to the child
        // camera on both movement and render ticks. Use that actual rendered
        // eye once; adding the offset again would double it.
        Vector3 eyePosition = player.GetEyePosition();
        Quaternion eyeRotation = mainCamera.transform.rotation;
        bool portalViewMapped = Portal.TryGetPortalViewPose(
            player,
            eyePosition,
            eyeRotation,
            out Vector3 viewPosition,
            out Quaternion viewRotation);
        if (!portalViewMapped &&
            player.GetPortalEyeOffset().sqrMagnitude <= 0.00000001f)
            return;

        Transform cameraTransform = mainCamera.transform;
        previousMainCameraLocalPosition = cameraTransform.localPosition;
        previousMainCameraLocalRotation = cameraTransform.localRotation;
        mainCameraPoseOverridden = true;
        cameraTransform.SetPositionAndRotation(viewPosition, viewRotation);
    }

    private void RestoreMainCameraAfterRender(
        ScriptableRenderContext context,
        Camera camera)
    {
        if (camera == mainCamera)
            RestoreMainCameraPose();
    }

    private void RestoreMainCameraPose()
    {
        if (!mainCameraPoseOverridden || mainCamera == null)
            return;

        mainCamera.transform.localPosition = previousMainCameraLocalPosition;
        mainCamera.transform.localRotation = previousMainCameraLocalRotation;
        mainCameraPoseOverridden = false;
    }

    private void RenderPortalView(
        int entryIndex,
        int recursionLevel,
        Camera sourceViewCamera,
        Vector3 sourcePosition,
        Quaternion sourceRotation)
>>>>>>> 0e6aa0ecb94d332d41d3f3596207ebe635e5ab67
    {
        Transform inTransform = inPortal.transform;
        Transform outTransform = outPortal.transform;

        Transform cameraTransform = portalCamera.transform;
        cameraTransform.position = mainCamera.transform.position;
        cameraTransform.rotation = mainCamera.transform.rotation;

        for (int i = 0; i <= iterationID; ++i)
        {
            Vector3 relativePosition = Quaternion.Inverse(inTransform.rotation) *
                (cameraTransform.position - inTransform.position);
            relativePosition = Quaternion.Euler(0f, 180f, 0f) * relativePosition;
            cameraTransform.position = outTransform.position +
                outTransform.rotation * relativePosition;

<<<<<<< HEAD
            Quaternion relativeRotation = Quaternion.Inverse(inTransform.rotation) *
                cameraTransform.rotation;
            relativeRotation = Quaternion.Euler(0f, 180f, 0f) * relativeRotation;
            cameraTransform.rotation = outTransform.rotation * relativeRotation;
        }

        portalCamera.projectionMatrix = mainCamera.projectionMatrix;
=======
            ConfigurePortalCamera(
                exitPortal, virtualPosition, virtualRotation);
            if (depth + 1 >= allocatedDepth ||
                !ShouldUpdatePortalView(
                    entryPortal, portalCamera, depth + 1))
                break;

            currentPosition = virtualPosition;
            currentRotation = virtualRotation;
        }

        Renderer entrySurface = entryPortal.Renderer;
        Renderer exitSurface = exitPortal.Renderer;

        for (int viewIndex = portalViewPositions.Count - 1;
             viewIndex >= 0; --viewIndex)
        {
            int absoluteDepth = recursionLevel + viewIndex;
            ConfigurePortalCamera(
                exitPortal,
                portalViewPositions[viewIndex],
                portalViewRotations[viewIndex]);
            portalCamera.targetTexture =
                portalTextures[entryIndex, absoluteDepth];

            Texture previousEntryTexture = GetPortalTexture(entryIndex);
            bool exitSurfaceWasEnabled =
                exitSurface != null && exitSurface.enabled;
            Texture childTexture = viewIndex + 1 < portalViewPositions.Count
                ? (Texture)portalTextures[entryIndex, absoluteDepth + 1]
                : terminalStaticTexture;

            BindPortalTexture(entryIndex, childTexture);
            if (exitSurface != null)
                exitSurface.enabled = false;

            try
            {
                UniversalRenderPipeline.SingleCameraRequest request =
                    new UniversalRenderPipeline.SingleCameraRequest
                    {
                        destination = portalCamera.targetTexture
                    };
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(
                    portalCamera, request);
            }
            finally
            {
                if (entrySurface != null)
                    BindPortalTexture(entryIndex, previousEntryTexture);
                if (exitSurface != null)
                    exitSurface.enabled = exitSurfaceWasEnabled;
            }
        }
    }

    private static void GetTransformedCameraPose(
        Portal entryPortal,
        Portal exitPortal,
        Vector3 sourcePosition,
        Quaternion sourceRotation,
        out Vector3 virtualPosition,
        out Quaternion virtualRotation)
    {
        Transform entry = entryPortal.transform;
        Transform exit = exitPortal.transform;
        Quaternion entryToExit = exit.rotation * PortalHalfTurn *
            Quaternion.Inverse(entry.rotation);

        virtualPosition = exit.position + entryToExit *
            (sourcePosition - entry.position);
        virtualRotation = entryToExit * sourceRotation;
    }

    private void ConfigurePortalCamera(
        Portal exitPortal,
        Vector3 virtualPosition,
        Quaternion virtualRotation)
    {
        portalCamera.transform.SetPositionAndRotation(
            virtualPosition, virtualRotation);
        portalCamera.ResetWorldToCameraMatrix();
        portalCamera.targetTexture = null;
        ConfigurePortalProjection();
>>>>>>> 0e6aa0ecb94d332d41d3f3596207ebe635e5ab67

        Plane clipPlane = new Plane(-outTransform.forward,
            outTransform.position);
        Vector4 clipPlaneCameraSpace =
            Matrix4x4.Transpose(Matrix4x4.Inverse(
                portalCamera.worldToCameraMatrix)) *
            new Vector4(clipPlane.normal.x, clipPlane.normal.y,
                clipPlane.normal.z, clipPlane.distance);
        portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(
            clipPlaneCameraSpace);

<<<<<<< HEAD
        UniversalRenderPipeline.RenderSingleCamera(context, portalCamera);
=======
        Vector4 planeWorld = new Vector4(
            clipNormal.x,
            clipNormal.y,
            clipNormal.z,
            -planeW);
        Vector4 planeCamera = Matrix4x4.Transpose(Matrix4x4.Inverse(
            portalCamera.worldToCameraMatrix)) * planeWorld;
        portalCamera.projectionMatrix = portalCamera.CalculateObliqueMatrix(
            planeCamera);
    }

    private void ConfigurePortalProjection()
    {
        portalCamera.orthographic = mainCamera.orthographic;
        portalCamera.aspect = mainCamera.aspect;
        portalCamera.nearClipPlane = mainCamera.nearClipPlane;
        portalCamera.farClipPlane = mainCamera.farClipPlane;
        // Source adds the exit portal to its visibility set before drawing a
        // portal view. Unity has no matching per-view PVS injection, so baked
        // occlusion can hide valid geometry in the remote scene.
        portalCamera.useOcclusionCulling = false;

        if (mainCamera.usePhysicalProperties)
        {
            portalCamera.usePhysicalProperties = true;
            portalCamera.sensorSize = mainCamera.sensorSize;
            portalCamera.focalLength = mainCamera.focalLength;
            portalCamera.lensShift = mainCamera.lensShift;
            portalCamera.gateFit = mainCamera.gateFit;
        }
        else
        {
            portalCamera.usePhysicalProperties = false;
            if (mainCamera.orthographic)
                portalCamera.orthographicSize = mainCamera.orthographicSize;
            else
                portalCamera.fieldOfView = mainCamera.fieldOfView;
        }

        // Source's portal view copies cameraView.zNear. Rebuild the projection
        // from the matching camera settings before applying the exit plane.
        portalCamera.ResetProjectionMatrix();
    }

    private static bool ShouldUpdatePortalView(
        Portal portal,
        Camera viewCamera,
        int recursionLevel)
    {
        if (portal == null || portal.otherPortal == null ||
            viewCamera == null || portal.Renderer == null)
            return false;

        Bounds localBounds = portal.Renderer.localBounds;
        float halfHeight = localBounds.extents.y *
            Mathf.Abs(portal.transform.lossyScale.y);

        // Source skips its complex through-portal frustum while the view is
        // close to the aperture; keep the root portal view eligible here.
        if (recursionLevel == 0 &&
            (portal.transform.position - viewCamera.transform.position)
                .sqrMagnitude < halfHeight * halfHeight)
            return true;

        // Match Source's front-side test. The portal root's +Z points into its
        // host wall; its visible side is the opposite normal.
        Vector3 frontNormal = Portal.GetPortalPlaneNormal(portal);
        if (Vector3.Dot(
                frontNormal,
                viewCamera.transform.position - portal.transform.position) <= 0f)
            return false;

        float z = localBounds.center.z;
        List<Vector3> clippedCorners = new List<Vector3>(4)
        {
            portal.transform.TransformPoint(new Vector3(
                localBounds.min.x, localBounds.min.y, z)),
            portal.transform.TransformPoint(new Vector3(
                localBounds.min.x, localBounds.max.y, z)),
            portal.transform.TransformPoint(new Vector3(
                localBounds.max.x, localBounds.max.y, z)),
            portal.transform.TransformPoint(new Vector3(
                localBounds.max.x, localBounds.min.y, z))
        };
        List<Vector3> scratch = new List<Vector3>(12);
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(viewCamera);
        float epsilon = SourceFrustumEpsilonHammerUnits /
            HammerUnitsPerUnityUnit;

        for (int planeIndex = 0; planeIndex < frustumPlanes.Length; ++planeIndex)
        {
            if (!ClipPolygonToPlane(
                    clippedCorners, scratch, frustumPlanes[planeIndex], epsilon))
                return false;

            List<Vector3> swap = clippedCorners;
            clippedCorners = scratch;
            scratch = swap;
        }

        return clippedCorners.Count >= 3;
    }

    private static bool ClipPolygonToPlane(
        List<Vector3> input,
        List<Vector3> output,
        Plane plane,
        float epsilon)
    {
        output.Clear();
        if (input.Count == 0)
            return false;

        Vector3 previous = input[input.Count - 1];
        float previousDistance = plane.GetDistanceToPoint(previous);
        bool previousInside = previousDistance >= -epsilon;

        for (int i = 0; i < input.Count; ++i)
        {
            Vector3 current = input[i];
            float currentDistance = plane.GetDistanceToPoint(current);
            bool currentInside = currentDistance >= -epsilon;

            if (currentInside != previousInside)
            {
                float denominator = previousDistance - currentDistance;
                if (Mathf.Abs(denominator) > 0.000001f)
                {
                    float fraction = previousDistance / denominator;
                    output.Add(previous + (current - previous) * fraction);
                }
            }

            if (currentInside)
                output.Add(current);

            previous = current;
            previousDistance = currentDistance;
            previousInside = currentInside;
        }

        return output.Count >= 3;
>>>>>>> 0e6aa0ecb94d332d41d3f3596207ebe635e5ab67
    }
}
