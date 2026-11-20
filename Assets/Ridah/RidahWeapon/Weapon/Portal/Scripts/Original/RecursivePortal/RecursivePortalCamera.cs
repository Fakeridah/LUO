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
    private const float HammerUnitsPerUnityUnit = 52.4934383202f;
    private const float SourceClipOffsetHammerUnits = 2f;
    private const float SourceClipCameraEpsilonHammerUnits = 1f;
    private const float SourceFrustumEpsilonHammerUnits = 0.01f;

    [SerializeField]
    private Portal[] portals = new Portal[PortalCount];

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private Camera portalCamera;

    [SerializeField, Range(1, SourceDefaultRecursionDepth)]
    private int recursionDepth = SourceDefaultRecursionDepth;

    private RenderTexture[,] portalTextures = new RenderTexture[PortalCount, 0];
    private MaterialPropertyBlock[] portalPropertyBlocks;
    private Texture2D terminalStaticTexture;
    private int textureWidth;
    private int textureHeight;
    private int allocatedDepth;
    private bool rendering;

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
        int depth = Mathf.Clamp(recursionDepth, 1, SourceDefaultRecursionDepth);

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
    }

    private void OnDisable()
    {
        RenderPipeline.beginCameraRendering -= UpdateCamera;
    }

    private void OnDestroy()
    {
        RenderPipeline.beginCameraRendering -= UpdateCamera;
        for (int i = 0; i < PortalCount; ++i)
            BindPortalTexture(i, null);
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
        if (rendering || camera != mainCamera || mainCamera == null ||
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
            portalCamera.nearClipPlane = Mathf.Max(
                mainCamera.nearClipPlane,
                1f / HammerUnitsPerUnityUnit);
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

        GetTransformedCameraPose(
            entryPortal, exitPortal, sourcePosition, sourceRotation,
            out Vector3 virtualPosition, out Quaternion virtualRotation);
        ConfigurePortalCamera(exitPortal, virtualPosition, virtualRotation);

        // Source's default stencil depth is two. In a portal view it skips the
        // exit portal, clips candidates against that view's frustum, and only
        // recurses through visible linked portals.
        if (recursionLevel + 1 < allocatedDepth &&
            ShouldUpdatePortalView(
                entryPortal, portalCamera, recursionLevel + 1))
        {
            RenderPortalView(
                entryIndex,
                recursionLevel + 1,
                portalCamera,
                virtualPosition,
                virtualRotation);
        }

        // Restore this view after its child, then draw it into its own target.
        ConfigurePortalCamera(exitPortal, virtualPosition, virtualRotation);
        portalCamera.targetTexture = portalTextures[entryIndex, recursionLevel];

        Renderer entrySurface = entryPortal.Renderer;
        Renderer exitSurface = exitPortal.Renderer;
        Texture previousEntryTexture = GetPortalTexture(entryIndex);
        bool exitSurfaceWasEnabled =
            exitSurface != null && exitSurface.enabled;

        Texture childTexture = recursionLevel + 1 < allocatedDepth
            ? (Texture)portalTextures[entryIndex, recursionLevel + 1]
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
        portalCamera.projectionMatrix = mainCamera.projectionMatrix;

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

        // Source moves the oblique plane forward if it would fall behind the
        // virtual eye, which would otherwise make the projection degenerate.
        float cameraDistance = Vector3.Dot(clipNormal, virtualPosition) - planeW;
        float cameraEpsilon = SourceClipCameraEpsilonHammerUnits /
            HammerUnitsPerUnityUnit;
        if (cameraDistance > -cameraEpsilon)
            planeW += cameraDistance + cameraEpsilon;

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

        // Source keeps the root view alive while the player is close to the
        // aperture so the near-plane render fix does not disappear.
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
