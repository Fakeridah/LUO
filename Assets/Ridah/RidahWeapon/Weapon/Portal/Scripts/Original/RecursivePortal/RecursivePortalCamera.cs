using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;

public class RecursivePortalCamera : MonoBehaviour
{
    [SerializeField]
    private Portal[] portals = new Portal[2];

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private Camera portalCamera;

    // Number of recursive views per portal. Each portal surface shows its
    // depth-0 view, and inside that view the same portal shows depth 1, and so
    // on, up to this limit. Source caps this at 10.
    [SerializeField, Range(1, 10)]
    private int recursionDepth = 3;

    private RenderTexture[,] portalTextures = new RenderTexture[2, 0];
    private int allocatedDepth;
    private int textureWidth;
    private int textureHeight;
    private bool rendering;
    private Texture2D terminalTexture;

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

        int depth = Mathf.Clamp(recursionDepth, 1, 10);
        if (portalTextures.GetLength(1) == depth &&
            textureWidth == width && textureHeight == height &&
            portalTextures[0, 0] != null && portalTextures[1, 0] != null)
            return;

        ReleaseRenderTextures();
        textureWidth = width;
        textureHeight = height;
        allocatedDepth = depth;
        portalTextures = new RenderTexture[2, depth];
        for (int p = 0; p < 2; ++p)
        {
            for (int d = 0; d < depth; ++d)
            {
                portalTextures[p, d] = CreateTexture(
                    "PortalView_" + p + "_Depth_" + d, width, height);
            }
        }

        if (terminalTexture == null)
        {
            terminalTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "PortalTerminal",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            terminalTexture.SetPixel(0, 0, new Color(0.25f, 0.25f, 0.25f, 1f));
            terminalTexture.Apply(false, true);
        }
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

    // This is the binding scheme the working single-layer renderer used:
    // set material.mainTexture directly on the portal's renderer. Keeping it
    // (instead of MaterialPropertyBlock) preserves the known-good display.
    private void SetSurfaceTexture(int portalIndex, Texture texture)
    {
        if (portals == null || portalIndex >= portals.Length ||
            portals[portalIndex] == null ||
            portals[portalIndex].Renderer == null)
            return;
        portals[portalIndex].Renderer.material.mainTexture = texture;
    }

    private Texture GetSurfaceTexture(int portalIndex)
    {
        if (portals == null || portalIndex >= portals.Length ||
            portals[portalIndex] == null ||
            portals[portalIndex].Renderer == null)
            return null;
        return portals[portalIndex].Renderer.material.mainTexture;
    }

    private void BindTextures()
    {
        // Final display: each surface shows its own depth-0 view.
        if (allocatedDepth <= 0)
            return;
        SetSurfaceTexture(0, portalTextures[0, 0]);
        SetSurfaceTexture(1, portalTextures[1, 0]);
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
        ReleaseRenderTextures();
        if (terminalTexture != null)
        {
            Destroy(terminalTexture);
            terminalTexture = null;
        }
    }

    private void ReleaseRenderTextures()
    {
        if (portalTextures == null)
            return;
        for (int p = 0; p < portalTextures.GetLength(0); ++p)
        {
            for (int d = 0; d < portalTextures.GetLength(1); ++d)
            {
                if (portalTextures[p, d] != null)
                {
                    portalTextures[p, d].Release();
                    portalTextures[p, d] = null;
                }
            }
        }
        allocatedDepth = 0;
    }

    private void UpdateCamera(ScriptableRenderContext context, Camera camera)
    {
        ResolveCameras();
        if (camera != mainCamera || mainCamera == null || portalCamera == null ||
            portals == null || portals.Length < 2 || portals[0] == null ||
            portals[1] == null || !portals[0].isPlaced ||
            !portals[1].isPlaced)
            return;

        EnsureRenderTextures();
        if (allocatedDepth <= 0)
            return;
        if (rendering)
            return;

        rendering = true;
        RenderTexture previousTarget = portalCamera.targetTexture;
        Texture surface0 = GetSurfaceTexture(0);
        Texture surface1 = GetSurfaceTexture(1);
        try
        {
            // Render deepest-first so a shallower layer can show the deeper
            // layer on the portal surface. This is the only structural change
            // from the working single-view renderer: each layer goes to its
            // own RT instead of all layers overwriting one texture.
            for (int depth = allocatedDepth - 1; depth >= 0; --depth)
            {
                RenderLayer(0, depth, context);
                RenderLayer(1, depth, context);
            }
        }
        finally
        {
            // Restore the surfaces to their final display textures.
            SetSurfaceTexture(0, surface0);
            SetSurfaceTexture(1, surface1);
            portalCamera.targetTexture = previousTarget;
            portalCamera.ResetProjectionMatrix();
            rendering = false;
        }
    }

    private void RenderLayer(int entryIndex, int depth,
        ScriptableRenderContext context)
    {
        Portal inPortal = portals[entryIndex];
        Portal outPortal = portals[1 - entryIndex];

        // While drawing this layer, show the NEXT deeper view on the entry
        // surface, so looking through the portal reveals the recursion.
        Texture deeper = depth + 1 < allocatedDepth
            ? (Texture)portalTextures[entryIndex, depth + 1]
            : terminalTexture;
        SetSurfaceTexture(entryIndex, deeper);

        portalCamera.targetTexture = portalTextures[entryIndex, depth];

        int iterationID = depth;
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

            Quaternion relativeRotation = Quaternion.Inverse(inTransform.rotation) *
                cameraTransform.rotation;
            relativeRotation = Quaternion.Euler(0f, 180f, 0f) * relativeRotation;
            cameraTransform.rotation = outTransform.rotation * relativeRotation;
        }

        Plane plane = new Plane(-outTransform.forward, outTransform.position);
        Vector4 clipPlaneWorldSpace = new Vector4(
            plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
        Vector4 clipPlaneCameraSpace =
            Matrix4x4.Transpose(Matrix4x4.Inverse(
                portalCamera.worldToCameraMatrix)) * clipPlaneWorldSpace;

        portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(
            clipPlaneCameraSpace);
        UniversalRenderPipeline.RenderSingleCamera(context, portalCamera);
    }
}
