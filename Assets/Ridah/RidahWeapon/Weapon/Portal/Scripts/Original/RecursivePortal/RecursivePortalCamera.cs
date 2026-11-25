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
    public static RecursivePortalCamera Active { get; private set; }

    private const int PortalCount = 2;
    private const int SourceDefaultRecursionDepth = 2;
    private const int SourceMaxRecursionDepth = 10;
    private const float PortalNearClipUnity = 0.01905f;
    private const float ExitClipOffsetUnity = 0.0381f;
    private const float ClipCameraClearanceUnity = 0.01905f;
    private const float FrustumEpsilonUnity = 0.0001905f;
    private const float NearCapClipOverlapUnity = 0.005715f;
    private const float NearCapProjectionOffsetUnity = 0.0001905f;
    private const float NearCapCloseDistanceUnity = 0.00762f;
    private const float NearCapDecalOffsetMinUnity = 0.02f / 52.4934383202f;
    private const float NearCapDecalOffsetMaxUnity = 0.251f / 52.4934383202f;
    private const float NearCapDecalOffsetBiasUnity = 0.04f / 52.4934383202f;
    private const float NearCapDepthFixOffsetUnity = 0.275f / 52.4934383202f;
    // Source initializes the close-eye cap at NDC +/-1.01, which is +/-0.005
    // in Unity viewport coordinates.
    private const float NearCapViewportMargin = 0.005f;

    private sealed class PortalCapGeometry
    {
        public GameObject gameObject;
        public Mesh mesh;
        public MeshRenderer renderer;
    }

    private sealed class PortalNearPlaneCap
    {
        public PortalCapGeometry stencil;
        public PortalCapGeometry depthFix;
    }

    [SerializeField]
    private Portal[] portals = new Portal[PortalCount];

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private Camera portalCamera;

    [SerializeField, Range(1, SourceMaxRecursionDepth)]
    private int recursionDepth = SourceDefaultRecursionDepth;

    [SerializeField, Range(0.25f, 1f)]
    private float portalTextureScale = 0.75f;

    private RenderTexture[,] portalTextures = new RenderTexture[PortalCount, 0];
    private MaterialPropertyBlock[] portalPropertyBlocks;
    private readonly List<Vector3> portalViewPositions = new List<Vector3>();
    private readonly List<Quaternion> portalViewRotations =
        new List<Quaternion>();
    private readonly List<Vector3> nearCapPolygon = new List<Vector3>(12);
    private readonly List<Vector3> nearCapScratch = new List<Vector3>(12);
    private readonly List<Vector3> nearCapLocalVertices = new List<Vector3>(12);
    private readonly List<Vector2> nearCapScreenPolygon = new List<Vector2>(12);
    private readonly List<int> nearCapTriangles = new List<int>(48);
    private readonly List<Vector3> apertureCorners = new List<Vector3>(4);
    private readonly List<Vector3> portalFrustumPolygon =
        new List<Vector3>(12);
    private readonly List<Vector3> portalFrustumScratch =
        new List<Vector3>(12);
    private readonly Plane[] reusableFrustumPlanes = new Plane[6];
    private PortalNearPlaneCap[] nearPlaneCaps =
        new PortalNearPlaneCap[PortalCount];
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
        for (int i = 0; i < PortalCount; ++i)
            EnsurePortalNearPlaneCap(i);
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
        float scale = Mathf.Clamp(portalTextureScale, 0.25f, 1f);
        int sourceWidth = mainCamera != null ? mainCamera.pixelWidth : Screen.width;
        int sourceHeight = mainCamera != null ? mainCamera.pixelHeight : Screen.height;
        int width = Mathf.RoundToInt(sourceWidth * scale);
        int height = Mathf.RoundToInt(sourceHeight * scale);
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
            portals[portalIndex] == null || portalPropertyBlocks == null)
            return;

        Renderer surface = portals[portalIndex].Renderer;
        if (surface != null)
        {
            MaterialPropertyBlock block = portalPropertyBlocks[portalIndex];
            surface.GetPropertyBlock(block);
            block.SetTexture(MainTextureId, texture);
            surface.SetPropertyBlock(block);
        }

        EnsurePortalNearPlaneCap(portalIndex);
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

    private void EnsurePortalNearPlaneCap(int portalIndex)
    {
        if (portalIndex < 0 || portalIndex >= PortalCount ||
            portals == null || portalIndex >= portals.Length ||
            portals[portalIndex] == null ||
            portals[portalIndex].Renderer == null)
            return;

        PortalNearPlaneCap cap = nearPlaneCaps[portalIndex];
        if (cap == null)
        {
            cap = new PortalNearPlaneCap
            {
                stencil = CreatePortalCapGeometry(
                    portalIndex, "Stencil"),
                depthFix = CreatePortalCapGeometry(
                    portalIndex, "Depth Fix")
            };
            nearPlaneCaps[portalIndex] = cap;
        }

        Renderer sourceRenderer = portals[portalIndex].Renderer;
        ConfigurePortalCapGeometry(cap.stencil, sourceRenderer);
        ConfigurePortalCapGeometry(cap.depthFix, sourceRenderer);
    }

    private PortalCapGeometry CreatePortalCapGeometry(
        int portalIndex,
        string geometryName)
    {
        GameObject capObject = new GameObject(
            "Portal " + geometryName + " Near Plane Cap " + portalIndex)
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer = portals[portalIndex].gameObject.layer
        };
        capObject.transform.SetParent(transform, false);

        Mesh capMesh = new Mesh
        {
            name = "Portal " + geometryName + " Near Plane Cap Mesh " +
                portalIndex,
            hideFlags = HideFlags.HideAndDontSave
        };
        capMesh.MarkDynamic();

        MeshFilter filter = capObject.AddComponent<MeshFilter>();
        filter.sharedMesh = capMesh;
        MeshRenderer capRenderer = capObject.AddComponent<MeshRenderer>();
        capRenderer.shadowCastingMode = ShadowCastingMode.Off;
        capRenderer.receiveShadows = false;
        capRenderer.lightProbeUsage = LightProbeUsage.Off;
        capRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        capRenderer.allowOcclusionWhenDynamic = false;
        capRenderer.enabled = false;

        return new PortalCapGeometry
        {
            gameObject = capObject,
            mesh = capMesh,
            renderer = capRenderer
        };
    }

    private static void ConfigurePortalCapGeometry(
        PortalCapGeometry cap,
        Renderer sourceRenderer)
    {
        if (cap == null || cap.renderer == null || sourceRenderer == null)
            return;

        cap.renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        cap.renderer.renderingLayerMask = sourceRenderer.renderingLayerMask;
        cap.gameObject.layer = sourceRenderer.gameObject.layer;
    }

    private void DisablePortalNearPlaneCaps()
    {
        if (nearPlaneCaps == null)
            return;

        for (int i = 0; i < nearPlaneCaps.Length; ++i)
        {
            PortalNearPlaneCap cap = nearPlaneCaps[i];
            if (cap == null)
                continue;

            if (cap.stencil != null && cap.stencil.renderer != null)
                cap.stencil.renderer.enabled = false;
            if (cap.depthFix != null && cap.depthFix.renderer != null)
                cap.depthFix.renderer.enabled = false;
        }
    }

    private void ReleasePortalNearPlaneCaps()
    {
        if (nearPlaneCaps == null)
            return;

        for (int i = 0; i < nearPlaneCaps.Length; ++i)
        {
            PortalNearPlaneCap cap = nearPlaneCaps[i];
            if (cap == null)
                continue;

            DestroyPortalCapGeometry(cap.stencil);
            DestroyPortalCapGeometry(cap.depthFix);
            nearPlaneCaps[i] = null;
        }
    }

    private static void DestroyPortalCapGeometry(PortalCapGeometry cap)
    {
        if (cap == null)
            return;

        if (cap.gameObject != null)
            Destroy(cap.gameObject);
        if (cap.mesh != null)
            Destroy(cap.mesh);
    }

    public Camera MainCamera
    {
        get
        {
            ResolveCameras();
            return mainCamera;
        }
    }

    public bool TryGetPortalAperture(
        int portalIndex,
        out Renderer surfaceRenderer,
        out Renderer stencilCapRenderer,
        out Renderer depthFixCapRenderer,
        out Texture portalTexture)
    {
        surfaceRenderer = null;
        stencilCapRenderer = null;
        depthFixCapRenderer = null;
        portalTexture = null;

        if (portalIndex < 0 || portalIndex >= PortalCount ||
            portals == null || portalIndex >= portals.Length ||
            portals[portalIndex] == null || !portals[portalIndex].isPlaced ||
            portals[portalIndex].otherPortal == null ||
            !portals[portalIndex].otherPortal.isPlaced)
            return false;

        surfaceRenderer = portals[portalIndex].Renderer;
        if (surfaceRenderer == null || !surfaceRenderer.enabled)
            return false;

        portalTexture = GetPortalTexture(portalIndex);
        if (nearPlaneCaps != null && portalIndex < nearPlaneCaps.Length)
        {
            PortalNearPlaneCap cap = nearPlaneCaps[portalIndex];
            if (cap != null)
            {
                if (cap.stencil != null && cap.stencil.renderer != null &&
                    cap.stencil.renderer.enabled)
                    stencilCapRenderer = cap.stencil.renderer;
                if (cap.depthFix != null && cap.depthFix.renderer != null &&
                    cap.depthFix.renderer.enabled)
                    depthFixCapRenderer = cap.depthFix.renderer;
            }
        }

        return portalTexture != null;
    }

    public void GetPortalRenderOrder(
        Camera viewer,
        out int farPortalIndex,
        out int nearPortalIndex)
    {
        nearPortalIndex = GetFrontPortalIndex(viewer);
        farPortalIndex = 1 - nearPortalIndex;
    }

    public bool ArePortalAperturesCoincident(
        Camera viewer,
        out int frontPortalIndex)
    {
        frontPortalIndex = GetFrontPortalIndex(viewer);
        if (portals == null || portals.Length < PortalCount ||
            portals[0] == null || portals[1] == null ||
            portals[0].otherPortal != portals[1] ||
            !Portal.IsPortalPairActive(portals[0]) ||
            portals[0].Renderer == null || portals[1].Renderer == null)
            return false;

        const float cornerTolerance = 0.001f;
        float toleranceSquared = cornerTolerance * cornerTolerance;
        Bounds bounds0 = portals[0].Renderer.localBounds;
        Bounds bounds1 = portals[1].Renderer.localBounds;
        int matchedCorners = 0;
        for (int cornerA = 0; cornerA < 4; ++cornerA)
        {
            Vector3 pointA = GetApertureCornerWorld(
                portals[0], bounds0, cornerA);
            for (int cornerB = 0; cornerB < 4; ++cornerB)
            {
                int cornerBit = 1 << cornerB;
                if ((matchedCorners & cornerBit) != 0)
                    continue;

                Vector3 pointB = GetApertureCornerWorld(
                    portals[1], bounds1, cornerB);
                if ((pointA - pointB).sqrMagnitude <= toleranceSquared)
                {
                    matchedCorners |= cornerBit;
                    break;
                }
            }
        }

        if (matchedCorners != 0xF)
            return false;

        frontPortalIndex = GetCoincidentFrontPortalIndex();
        return true;
    }

    public Color GetMostRecentlyPlacedPortalColor(Color fallback)
    {
        int latestPortalIndex = GetCoincidentFrontPortalIndex();
        Portal latestPortal = portals != null &&
            latestPortalIndex < portals.Length
                ? portals[latestPortalIndex]
                : null;
        return latestPortal != null
            ? latestPortal.GetOverlapMaterialColor(fallback)
            : fallback;
    }

    private int GetFrontPortalIndex(Camera viewer)
    {
        if (viewer == null || portals == null || portals.Length < PortalCount ||
            portals[0] == null || portals[1] == null)
            return 0;

        Transform view = viewer.transform;
        float depth0 = Vector3.Dot(
            view.forward, portals[0].transform.position - view.position);
        float depth1 = Vector3.Dot(
            view.forward, portals[1].transform.position - view.position);
        if (Mathf.Abs(depth0 - depth1) > 0.0001f)
            return depth0 < depth1 ? 0 : 1;

        Vector3 toPortal0 = portals[0].transform.position - view.position;
        Vector3 toPortal1 = portals[1].transform.position - view.position;
        float facing0 = toPortal0.sqrMagnitude > 0.000001f
            ? Vector3.Dot(
                Portal.GetPortalPlaneNormal(portals[0]).normalized,
                -toPortal0.normalized)
            : 0f;
        float facing1 = toPortal1.sqrMagnitude > 0.000001f
            ? Vector3.Dot(
                Portal.GetPortalPlaneNormal(portals[1]).normalized,
                -toPortal1.normalized)
            : 0f;
        if (Mathf.Abs(facing0 - facing1) > 0.001f)
            return facing0 > facing1 ? 0 : 1;

        // Stable tie-break for coincident portals with matching orientation.
        return 0;
    }

    private int GetCoincidentFrontPortalIndex()
    {
        if (portals == null || portals.Length < PortalCount ||
            portals[0] == null || portals[1] == null)
            return 0;

        long sequence0 = portals[0].PlacementSequence;
        long sequence1 = portals[1].PlacementSequence;
        if (sequence0 != sequence1)
            return sequence0 > sequence1 ? 0 : 1;

        // Authored portals without runtime placement history have no reliable
        // "last placed" value. Use portal 1 as a stable tie-break.
        return 1;
    }

    private static Vector3 GetApertureCornerWorld(
        Portal portal,
        Bounds bounds,
        int cornerIndex)
    {
        float x = (cornerIndex & 1) == 0
            ? bounds.min.x
            : bounds.max.x;
        float y = (cornerIndex & 2) == 0
            ? bounds.min.y
            : bounds.max.y;
        return portal.transform.TransformPoint(
            new Vector3(x, y, bounds.center.z));
    }

    private void OnEnable()
    {
        Active = this;
        RenderPipeline.beginCameraRendering += UpdateCamera;
        RenderPipeline.endCameraRendering += RestoreMainCameraAfterRender;
    }

    private void OnDisable()
    {
        RenderPipeline.beginCameraRendering -= UpdateCamera;
        RenderPipeline.endCameraRendering -= RestoreMainCameraAfterRender;
        if (Active == this)
            Active = null;
        DisablePortalNearPlaneCaps();
        RestoreMainCameraPose();
    }

    private void OnDestroy()
    {
        RenderPipeline.beginCameraRendering -= UpdateCamera;
        RenderPipeline.endCameraRendering -= RestoreMainCameraAfterRender;
        ReleasePortalNearPlaneCaps();
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

        DisablePortalNearPlaneCaps();

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

        UpdatePortalNearPlaneCaps();
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
        {
            DisablePortalNearPlaneCaps();
            RestoreMainCameraPose();
        }
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
        bool aperturesCoincident = ArePortalAperturesCoincident(
            sourceViewCamera, out _);

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
            bool entrySurfaceWasEnabled =
                entrySurface != null && entrySurface.enabled;
            bool exitSurfaceWasEnabled =
                exitSurface != null && exitSurface.enabled;
            Texture childTexture = viewIndex + 1 < portalViewPositions.Count
                ? (Texture)portalTextures[entryIndex, absoluteDepth + 1]
                : terminalStaticTexture;

            BindPortalTexture(entryIndex, childTexture);
            if (aperturesCoincident && entrySurface != null)
                entrySurface.enabled = false;
            if (exitSurface != null)
                exitSurface.enabled = false;

            int previousCullingMask = portalCamera.cullingMask;
            try
            {
                // The proxy uses a layer hidden from the player camera. Keep
                // it included in this offscreen portal view.
                portalCamera.cullingMask = previousCullingMask |
                    PortalableObject.PortalGhostLayerMask;
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
                portalCamera.cullingMask = previousCullingMask;
                if (entrySurface != null)
                {
                    BindPortalTexture(entryIndex, previousEntryTexture);
                    entrySurface.enabled = entrySurfaceWasEnabled;
                }
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

        // Source offsets the exit portal's forward clip plane by 0.0381
        // Unity units to avoid z-fighting with crossing geometry.
        // PortalPlacement stores local +Z into the host surface. Source's
        // m_vForward is the visible/front normal, so use the matching outward
        // normal here rather than Unity's local +Z direction.
        Vector3 clipNormal = Portal.GetPortalPlaneNormal(exitPortal);
        float planeW = Vector3.Dot(
            clipNormal, exitPortal.transform.position) - ExitClipOffsetUnity;

        // Keep the mapped camera behind the clip plane. Source moves the plane
        // when it approaches the eye so the oblique projection cannot become
        // degenerate at the portal crossing.
        float cameraPlaneDistance =
            Vector3.Dot(clipNormal, virtualPosition) - planeW;
        if (cameraPlaneDistance > -ClipCameraClearanceUnity)
            planeW += cameraPlaneDistance + ClipCameraClearanceUnity;

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
        portalCamera.nearClipPlane = Mathf.Max(
            mainCamera.nearClipPlane, PortalNearClipUnity);
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

    private void UpdatePortalNearPlaneCaps()
    {
        if (mainCamera == null || portals == null ||
            portals.Length < PortalCount || portals[0] == null ||
            portals[1] == null || !portals[0].isPlaced ||
            !portals[1].isPlaced)
        {
            DisablePortalNearPlaneCaps();
            return;
        }

        for (int i = 0; i < PortalCount; ++i)
        {
            EnsurePortalNearPlaneCap(i);
            PortalNearPlaneCap cap = nearPlaneCaps[i];
            if (cap == null || cap.stencil == null || cap.depthFix == null)
            {
                continue;
            }

            Portal portal = portals[i];
            Vector3 frontNormal = Portal.GetPortalPlaneNormal(portal);
            frontNormal.Normalize();
            float cameraSideDistance = Vector3.Dot(
                frontNormal,
                mainCamera.transform.position - portal.transform.position);
            float stencilOffset = Mathf.Clamp(
                cameraSideDistance,
                NearCapDecalOffsetMinUnity,
                NearCapDecalOffsetMaxUnity) + NearCapDecalOffsetBiasUnity;

            bool stencilCapBuilt = TryBuildPortalNearPlaneCap(
                i, cap.stencil, stencilOffset, false);
            bool depthFixCapBuilt = TryBuildPortalNearPlaneCap(
                i, cap.depthFix, NearCapDepthFixOffsetUnity, true);
            cap.stencil.renderer.enabled = stencilCapBuilt;
            cap.depthFix.renderer.enabled = depthFixCapBuilt;
        }
    }

    private bool TryBuildPortalNearPlaneCap(
        int portalIndex,
        PortalCapGeometry cap,
        float portalOffset,
        bool requirePortalCenterClose)
    {
        Portal portal = portals[portalIndex];
        Renderer sourceRenderer = portal != null ? portal.Renderer : null;
        if (portal == null || !portal.isPlaced || sourceRenderer == null ||
            !sourceRenderer.enabled || cap == null || cap.mesh == null)
            return false;

        Bounds localBounds = sourceRenderer.localBounds;
        Vector3 cameraPosition = mainCamera.transform.position;
        Vector3 frontNormal = Portal.GetPortalPlaneNormal(portal);
        if (frontNormal.sqrMagnitude < 0.000001f)
            return false;
        frontNormal.Normalize();

        float cameraSideDistance = Vector3.Dot(
            frontNormal, cameraPosition - portal.transform.position);
        if (requirePortalCenterClose &&
            cameraSideDistance < -PortalNearClipUnity)
            return false;

        GetPortalApertureCorners(portal, localBounds, frontNormal, portalOffset);
        Vector3 cameraLocal = portal.transform.InverseTransformPoint(
            cameraPosition);
        Vector3 closestLocal = new Vector3(
            Mathf.Clamp(cameraLocal.x, localBounds.min.x, localBounds.max.x),
            Mathf.Clamp(cameraLocal.y, localBounds.min.y, localBounds.max.y),
            localBounds.center.z);
        Vector3 closestWorld = portal.transform.TransformPoint(closestLocal) +
            frontNormal * portalOffset;
        float distanceToAperture = Vector3.Distance(
            cameraPosition, closestWorld);
        float apertureHalfHeight = localBounds.extents.y *
            Mathf.Abs(portal.transform.lossyScale.y);

        // DrawRenderFixMesh only runs within one portal half-height. The
        // CreateMeshForPortals stencil cap has no such gate; the near-plane
        // and frustum clipping below determine whether it has any area.
        if (requirePortalCenterClose &&
            (cameraPosition - portal.transform.position).sqrMagnitude >=
            apertureHalfHeight * apertureHalfHeight)
            return false;

        bool built = distanceToAperture < NearCapCloseDistanceUnity
            ? BuildClosePortalNearPlaneCap(
                portal, frontNormal, portalOffset)
            : BuildRegularPortalNearPlaneCap();
        if (!built || nearCapScreenPolygon.Count < 3)
            return false;

        nearCapLocalVertices.Clear();
        float capDepth = mainCamera.nearClipPlane +
            NearCapProjectionOffsetUnity;
        for (int i = 0; i < nearCapScreenPolygon.Count; ++i)
        {
            Vector2 screen = nearCapScreenPolygon[i];
            Vector3 world = mainCamera.ViewportToWorldPoint(
                new Vector3(screen.x, screen.y, capDepth));
            nearCapLocalVertices.Add(transform.InverseTransformPoint(world));
        }

        if (nearCapLocalVertices.Count < 3)
            return false;

        BuildDoubleSidedFan(nearCapLocalVertices.Count);
        cap.mesh.Clear(false);
        cap.mesh.SetVertices(nearCapLocalVertices);
        cap.mesh.SetTriangles(nearCapTriangles, 0, true);
        cap.mesh.RecalculateBounds();
        cap.renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        cap.renderer.renderingLayerMask = sourceRenderer.renderingLayerMask;
        return true;
    }

    private void GetPortalApertureCorners(
        Portal portal,
        Bounds localBounds,
        Vector3 frontNormal,
        float portalOffset)
    {
        apertureCorners.Clear();
        float z = localBounds.center.z;
        apertureCorners.Add(portal.transform.TransformPoint(
            new Vector3(localBounds.min.x, localBounds.min.y, z)) +
            frontNormal * portalOffset);
        apertureCorners.Add(portal.transform.TransformPoint(
            new Vector3(localBounds.max.x, localBounds.min.y, z)) +
            frontNormal * portalOffset);
        apertureCorners.Add(portal.transform.TransformPoint(
            new Vector3(localBounds.max.x, localBounds.max.y, z)) +
            frontNormal * portalOffset);
        apertureCorners.Add(portal.transform.TransformPoint(
            new Vector3(localBounds.min.x, localBounds.max.y, z)) +
            frontNormal * portalOffset);
    }

    private bool BuildRegularPortalNearPlaneCap()
    {
        nearCapPolygon.Clear();
        nearCapPolygon.AddRange(apertureCorners);

        Transform cameraTransform = mainCamera.transform;
        Plane flippedNearPlane = new Plane(
            -cameraTransform.forward,
            cameraTransform.position + cameraTransform.forward *
                (mainCamera.nearClipPlane + NearCapClipOverlapUnity));
        if (!ClipNearCapPolygon(flippedNearPlane, 0f))
            return false;

        // CPortalRenderable_FlatBasic clips the polygon against all four
        // side planes, with 0.01 HU of slack, before projecting it back onto
        // the near plane. Clip in world space so the margin stays a distance
        // in Source units instead of a resolution-dependent viewport margin.
        GeometryUtility.CalculateFrustumPlanes(
            mainCamera, reusableFrustumPlanes);
        for (int i = 0; i < 4; ++i)
        {
            if (!ClipNearCapPolygon(
                    reusableFrustumPlanes[i], FrustumEpsilonUnity))
                return false;
        }

        nearCapScreenPolygon.Clear();
        for (int i = 0; i < nearCapPolygon.Count; ++i)
        {
            Vector3 viewport = mainCamera.WorldToViewportPoint(
                nearCapPolygon[i]);
            if (viewport.z <= 0.0001f ||
                float.IsNaN(viewport.x) || float.IsNaN(viewport.y) ||
                float.IsInfinity(viewport.x) || float.IsInfinity(viewport.y))
                return false;
            nearCapScreenPolygon.Add(new Vector2(viewport.x, viewport.y));
        }

        return nearCapScreenPolygon.Count >= 3;
    }

    private bool BuildClosePortalNearPlaneCap(
        Portal portal,
        Vector3 frontNormal,
        float portalOffset)
    {
        nearCapPolygon.Clear();
        float near = mainCamera.nearClipPlane;
        nearCapPolygon.Add(mainCamera.ViewportToWorldPoint(
            new Vector3(-NearCapViewportMargin, -NearCapViewportMargin, near)));
        nearCapPolygon.Add(mainCamera.ViewportToWorldPoint(
            new Vector3(1f + NearCapViewportMargin,
                -NearCapViewportMargin, near)));
        nearCapPolygon.Add(mainCamera.ViewportToWorldPoint(
            new Vector3(1f + NearCapViewportMargin,
                1f + NearCapViewportMargin, near)));
        nearCapPolygon.Add(mainCamera.ViewportToWorldPoint(
            new Vector3(-NearCapViewportMargin,
                1f + NearCapViewportMargin, near)));

        // CPortalRenderable_FlatBasic::Internal_DrawRenderFixMesh's close-eye
        // branch clips the camera near plane against the offset portal plane.
        Plane destinationSide = new Plane(
            -frontNormal,
            portal.transform.position +
                frontNormal * portalOffset);
        if (!ClipNearCapPolygon(destinationSide, 0f))
            return false;

        // Source's close-eye branch stops here: it clips the expanded camera
        // near-plane polygon against the portal plane, then projects it back
        // onto the near plane. Do not add aperture-side clipping here; that
        // shrinks the render-fix cap relative to the Source stencil path.
        nearCapScreenPolygon.Clear();
        for (int i = 0; i < nearCapPolygon.Count; ++i)
        {
            Vector3 viewport = mainCamera.WorldToViewportPoint(
                nearCapPolygon[i]);
            nearCapScreenPolygon.Add(new Vector2(viewport.x, viewport.y));
        }

        return nearCapScreenPolygon.Count >= 3;
    }

    private bool ClipNearCapPolygon(Plane plane, float epsilon)
    {
        nearCapScratch.Clear();
        if (!ClipPolygonToPlane(
                nearCapPolygon, nearCapScratch, plane, epsilon))
            return false;

        nearCapPolygon.Clear();
        nearCapPolygon.AddRange(nearCapScratch);
        return nearCapPolygon.Count >= 3;
    }

    private void BuildDoubleSidedFan(int vertexCount)
    {
        nearCapTriangles.Clear();
        for (int i = 1; i < vertexCount - 1; ++i)
        {
            nearCapTriangles.Add(0);
            nearCapTriangles.Add(i);
            nearCapTriangles.Add(i + 1);
            nearCapTriangles.Add(0);
            nearCapTriangles.Add(i + 1);
            nearCapTriangles.Add(i);
        }
    }

    private bool ShouldUpdatePortalView(
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
        List<Vector3> clippedCorners = portalFrustumPolygon;
        List<Vector3> scratch = portalFrustumScratch;
        clippedCorners.Clear();
        clippedCorners.Add(portal.transform.TransformPoint(new Vector3(
            localBounds.min.x, localBounds.min.y, z)));
        clippedCorners.Add(portal.transform.TransformPoint(new Vector3(
            localBounds.min.x, localBounds.max.y, z)));
        clippedCorners.Add(portal.transform.TransformPoint(new Vector3(
            localBounds.max.x, localBounds.max.y, z)));
        clippedCorners.Add(portal.transform.TransformPoint(new Vector3(
            localBounds.max.x, localBounds.min.y, z)));
        GeometryUtility.CalculateFrustumPlanes(
            viewCamera, reusableFrustumPlanes);
        float epsilon = FrustumEpsilonUnity;

        for (int planeIndex = 0;
             planeIndex < reusableFrustumPlanes.Length;
             ++planeIndex)
        {
            if (!ClipPolygonToPlane(
                    clippedCorners,
                    scratch,
                    reusableFrustumPlanes[planeIndex],
                    epsilon))
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
        float previousDistance =
            plane.GetDistanceToPoint(previous) + epsilon;
        bool previousInside = previousDistance >= 0f;

        for (int i = 0; i < input.Count; ++i)
        {
            Vector3 current = input[i];
            float currentDistance =
                plane.GetDistanceToPoint(current) + epsilon;
            bool currentInside = currentDistance >= 0f;

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
