using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;

/// <summary>
/// Unity URP render-texture adaptation of Source's recursive portal views.
/// Each view is rendered deepest-first; the current exit portal is excluded
/// and the entrance portal samples the next view.
/// </summary>
public class RecursivePortalCamera : MonoBehaviour
{
    private const int PortalCount = 2;
    private const int SourceDefaultRecursionDepth = 2;
    private const int SourceMaxRecursionDepth = 10;
    private const float HammerUnitsPerUnityUnit = 52.4934383202f;
    private const float SourceClipOffsetHammerUnits = 2f;
    private const float SourceFrustumEpsilonHammerUnits = 0.01f;

    [SerializeField]
    private Portal[] portals = new Portal[PortalCount];

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private Camera portalCamera;

    [SerializeField, Range(1, SourceMaxRecursionDepth)]
    private int recursionDepth = SourceDefaultRecursionDepth;

    private RenderTexture[,] portalTextures = new RenderTexture[PortalCount, 0];
    private MaterialPropertyBlock[] portalPropertyBlocks;
    private readonly List<Vector3> portalViewPositions = new List<Vector3>();
    private readonly List<Quaternion> portalViewRotations =
        new List<Quaternion>();
    private Texture2D terminalStaticTexture;
    private int textureWidth;
    private int textureHeight;
    private int allocatedDepth;
    private bool rendering;
    private bool mainCameraPoseOverridden;
    private Vector3 previousMainCameraLocalPosition;
    private Quaternion previousMainCameraLocalRotation;

    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private static readonly Quaternion PortalHalfTurn =
        Quaternion.Euler(0f, 180f, 0f);

    private void Awake()
    {
        portalPropertyBlocks = new MaterialPropertyBlock[PortalCount];
        for (int i = 0; i < PortalCount; ++i)
            portalPropertyBlocks[i] = new MaterialPropertyBlock();

        ResolveCameras();
        CreateTerminalStaticTexture();
        EnsureRenderTextures();
        BindMainViewTextures();
    }

    private void Start()
    {
        ResolveCameras();
        EnsureRenderTextures();
        BindMainViewTextures();
    }

    private void ResolveCameras()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (portalCamera == null)
            portalCamera = GetComponentInChildren<Camera>(true);
    }

    private void CreateTerminalStaticTexture()
    {
        // Source's portal static overlay falls back to a quarter-gray surface
        // when its optional static texture is absent.
        terminalStaticTexture = new Texture2D(
            1, 1, TextureFormat.RGBA32, false, true)
        {
            name = "PortalTerminalStatic",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        terminalStaticTexture.SetPixel(0, 0,
            new Color(0.25f, 0.25f, 0.25f, 1f));
        terminalStaticTexture.Apply(false, true);
    }

    private void EnsureRenderTextures()
    {
        int width = mainCamera != null ? mainCamera.pixelWidth : Screen.width;
        int height = mainCamera != null ? mainCamera.pixelHeight : Screen.height;
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        int depth = Mathf.Clamp(recursionDepth, 1, SourceMaxRecursionDepth);

        if (portalTextures.GetLength(1) == depth &&
            portalTextures[0, 0] != null &&
            portalTextures[1, 0] != null &&
            textureWidth == width && textureHeight == height)
            return;

        ReleaseRenderTextures();
        textureWidth = width;
        textureHeight = height;
        allocatedDepth = depth;
        portalTextures = new RenderTexture[PortalCount, allocatedDepth];

        for (int portalIndex = 0; portalIndex < PortalCount; ++portalIndex)
        {
            for (int viewDepth = 0; viewDepth < allocatedDepth; ++viewDepth)
            {
                portalTextures[portalIndex, viewDepth] = CreateTexture(
                    "PortalView_" + portalIndex + "_Depth_" + viewDepth,
                    width, height);
            }
        }
    }

    private static RenderTexture CreateTexture(
        string textureName,
        int width,
        int height)
    {
        RenderTexture texture = new RenderTexture(width, height, 24,
            RenderTextureFormat.ARGB32)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = 1
        };
        texture.Create();
        return texture;
    }

    private void BindMainViewTextures()
    {
        if (portals == null || portals.Length < PortalCount ||
            portalPropertyBlocks == null || portalTextures.GetLength(1) == 0)
            return;

        for (int i = 0; i < PortalCount; ++i)
            BindPortalTexture(i, portalTextures[i, 0]);
    }

    private void BindPortalTexture(int portalIndex, Texture texture)
    {
        if (portals == null || portalIndex >= portals.Length ||
            portals[portalIndex] == null ||
            portals[portalIndex].Renderer == null ||
            portalPropertyBlocks == null)
            return;

        Renderer surface = portals[portalIndex].Renderer;
        MaterialPropertyBlock block = portalPropertyBlocks[portalIndex];
        surface.GetPropertyBlock(block);
        block.SetTexture(MainTextureId, texture);
        surface.SetPropertyBlock(block);
    }

    private Texture GetPortalTexture(int portalIndex)
    {
        if (portals == null || portalIndex >= portals.Length ||
            portals[portalIndex] == null ||
            portals[portalIndex].Renderer == null ||
            portalPropertyBlocks == null)
            return null;

        Renderer surface = portals[portalIndex].Renderer;
        MaterialPropertyBlock block = portalPropertyBlocks[portalIndex];
        surface.GetPropertyBlock(block);
        return block.GetTexture(MainTextureId);
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
    }

    private void UpdateCamera(ScriptableRenderContext context, Camera camera)
    {
        ResolveCameras();
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
            portals[1] == null || !portals[0].isPlaced ||
            !portals[1].isPlaced)
            return;

        EnsureRenderTextures();
        if (allocatedDepth == 0)
            return;

        BindMainViewTextures();
        rendering = true;

        RenderTexture previousTarget = portalCamera.targetTexture;
        Vector3 previousPosition = portalCamera.transform.position;
        Quaternion previousRotation = portalCamera.transform.rotation;
        Matrix4x4 previousView = portalCamera.worldToCameraMatrix;
        Matrix4x4 previousProjection = portalCamera.projectionMatrix;
        CameraClearFlags previousClearFlags = portalCamera.clearFlags;
        Color previousBackground = portalCamera.backgroundColor;
        float previousNearClip = portalCamera.nearClipPlane;
        float previousFarClip = portalCamera.farClipPlane;
        float previousAspect = portalCamera.aspect;

        try
        {
            // Keep the prefab culling mask: it intentionally excludes Player.
            portalCamera.clearFlags = mainCamera.clearFlags == CameraClearFlags.Depth ||
                mainCamera.clearFlags == CameraClearFlags.Nothing
                    ? CameraClearFlags.SolidColor
                    : mainCamera.clearFlags;
            portalCamera.backgroundColor = mainCamera.backgroundColor;
            portalCamera.nearClipPlane = mainCamera.nearClipPlane;
            portalCamera.farClipPlane = mainCamera.farClipPlane;
            portalCamera.aspect = mainCamera.aspect;

            for (int portalIndex = 0; portalIndex < PortalCount; ++portalIndex)
            {
                RenderPortalView(
                    portalIndex,
                    0,
                    mainCamera,
                    mainCamera.transform.position,
                    mainCamera.transform.rotation);
            }

            BindMainViewTextures();
        }
        finally
        {
            portalCamera.targetTexture = previousTarget;
            portalCamera.transform.SetPositionAndRotation(
                previousPosition, previousRotation);
            portalCamera.worldToCameraMatrix = previousView;
            portalCamera.projectionMatrix = previousProjection;
            portalCamera.clearFlags = previousClearFlags;
            portalCamera.backgroundColor = previousBackground;
            portalCamera.nearClipPlane = previousNearClip;
            portalCamera.farClipPlane = previousFarClip;
            portalCamera.aspect = previousAspect;
            BindMainViewTextures();
            rendering = false;
        }
    }

    private void ApplyMainCameraPortalPose()
    {
        PortablePlayer player =
            mainCamera.GetComponentInParent<PortablePlayer>();
        if (player == null)
            return;

        // Source C_Portal_Player::CalcView starts from EyePosition(), which
        // adds the render-only m_vEyeOffset to the live view offset. The main
        // camera transform itself is not moved, so GetEyePosition() already
        // returns exactly this eye; do not add the offset a second time.
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
    {
        Portal entryPortal = portals[entryIndex];
        Portal exitPortal = entryPortal != null ? entryPortal.otherPortal : null;
        if (exitPortal == null || !exitPortal.isPlaced ||
            !ShouldUpdatePortalView(
                entryPortal, sourceViewCamera, recursionLevel))
            return;

        // Build the visible view chain first, then submit each camera request
        // deepest-first. Recursively submitting this same Camera from inside
        // its own URP render request corrupts culling state at deeper levels.
        portalViewPositions.Clear();
        portalViewRotations.Clear();
        if (portalViewPositions.Capacity < allocatedDepth)
            portalViewPositions.Capacity = allocatedDepth;
        if (portalViewRotations.Capacity < allocatedDepth)
            portalViewRotations.Capacity = allocatedDepth;
        Vector3 currentPosition = sourcePosition;
        Quaternion currentRotation = sourceRotation;

        for (int depth = recursionLevel; depth < allocatedDepth; ++depth)
        {
            GetTransformedCameraPose(
                entryPortal, exitPortal, currentPosition, currentRotation,
                out Vector3 virtualPosition, out Quaternion virtualRotation);
            portalViewPositions.Add(virtualPosition);
            portalViewRotations.Add(virtualRotation);

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

        // Source clips against the exit portal's forward plane, offset two
        // Hammer units behind it to avoid z-fighting with crossing geometry.
        // PortalPlacement stores local +Z into the host surface. Source's
        // m_vForward is the visible/front normal, so use the matching outward
        // normal here rather than Unity's local +Z direction.
        Vector3 clipNormal = Portal.GetPortalPlaneNormal(exitPortal);
        float clipOffset = SourceClipOffsetHammerUnits /
            HammerUnitsPerUnityUnit;
        float planeW = Vector3.Dot(
            clipNormal, exitPortal.transform.position) - clipOffset;

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
    }
}
