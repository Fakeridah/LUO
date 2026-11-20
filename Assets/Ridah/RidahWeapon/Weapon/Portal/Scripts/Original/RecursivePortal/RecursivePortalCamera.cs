using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;

/// <summary>
/// Renders one remote view per portal into the project's two existing portal
/// textures. The camera mapping and exit clipping follow Source's
/// RenderPortalViewToTexture path. Source's back-buffer stencil recursion is a
/// separate renderer and is not implemented by this component.
/// </summary>
public class RecursivePortalCamera : MonoBehaviour
{
    [SerializeField]
    private Portal[] portals = new Portal[2];

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private Camera portalCamera;

    private RenderTexture[] portalTextures = new RenderTexture[2];
    private int textureWidth;
    private int textureHeight;
    private bool rendering;

    private static readonly Quaternion PortalHalfTurn =
        Quaternion.Euler(0f, 180f, 0f);

    private void Awake()
    {
        ResolveCameras();
        EnsureRenderTextures();
        BindTextures();
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
        if (portalTextures[0] != null && portalTextures[1] != null &&
            textureWidth == width && textureHeight == height)
            return;

        ReleaseRenderTextures();
        textureWidth = width;
        textureHeight = height;
        portalTextures[0] = CreateTexture("PortalViewA", width, height);
        portalTextures[1] = CreateTexture("PortalViewB", width, height);
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

    private void BindTextures()
    {
        if (portals == null || portals.Length < 2)
            return;

        for (int i = 0; i < 2; ++i)
        {
            if (portals[i] != null && portals[i].Renderer != null &&
                portalTextures[i] != null)
                portals[i].Renderer.material.mainTexture = portalTextures[i];
        }
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
    }

    private void ReleaseRenderTextures()
    {
        for (int i = 0; i < portalTextures.Length; ++i)
        {
            if (portalTextures[i] == null)
                continue;
            portalTextures[i].Release();
            Destroy(portalTextures[i]);
            portalTextures[i] = null;
        }
    }

    private void UpdateCamera(ScriptableRenderContext context, Camera camera)
    {
        ResolveCameras();
        if (rendering || camera != mainCamera || mainCamera == null ||
            portalCamera == null || portals == null || portals.Length < 2 ||
            portals[0] == null || portals[1] == null ||
            !portals[0].isPlaced || !portals[1].isPlaced)
            return;

        EnsureRenderTextures();
        BindTextures();
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
        try
        {
            // Keep the portal camera's prefab culling mask. It deliberately
            // excludes the Player layer, unlike the first-person camera.
            portalCamera.clearFlags = mainCamera.clearFlags;
            portalCamera.backgroundColor = mainCamera.backgroundColor;
            portalCamera.nearClipPlane = mainCamera.nearClipPlane;
            portalCamera.farClipPlane = mainCamera.farClipPlane;

            RenderPortalView(0, context);
            RenderPortalView(1, context);
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
            rendering = false;
        }
    }

    private void RenderPortalView(
        int entryIndex,
        ScriptableRenderContext context)
    {
        Portal entryPortal = portals[entryIndex];
        Portal exitPortal = entryPortal.otherPortal;
        if (exitPortal == null)
            return;

        Transform entry = entryPortal.transform;
        Transform exit = exitPortal.transform;
        Quaternion entryToExit = exit.rotation * PortalHalfTurn *
            Quaternion.Inverse(entry.rotation);

        Vector3 virtualPosition = exit.position + entryToExit *
            (mainCamera.transform.position - entry.position);
        Quaternion virtualRotation = entryToExit * mainCamera.transform.rotation;
        portalCamera.transform.SetPositionAndRotation(
            virtualPosition, virtualRotation);
        portalCamera.ResetWorldToCameraMatrix();

        portalCamera.targetTexture = portalTextures[entryIndex];
        portalCamera.projectionMatrix = mainCamera.projectionMatrix;

        // Source places its clip plane two Hammer units behind the exit
        // portal. Unity's oblique plane is oriented toward the virtual eye.
        Vector3 clipNormal = -exit.forward;
        if (Vector3.Dot(clipNormal, virtualPosition - exit.position) < 0f)
            clipNormal = -clipNormal;
        const float hammerUnitsPerUnityUnit = 52.4934383202f;
        float clipOffset = 2f / hammerUnitsPerUnityUnit;
        Plane clipPlane = new Plane(
            clipNormal,
            exit.position + clipNormal * clipOffset);
        Vector4 planeWorld = new Vector4(
            clipPlane.normal.x,
            clipPlane.normal.y,
            clipPlane.normal.z,
            clipPlane.distance);
        Vector4 planeCamera = Matrix4x4.Transpose(Matrix4x4.Inverse(
            portalCamera.worldToCameraMatrix)) * planeWorld;
        portalCamera.projectionMatrix = portalCamera.CalculateObliqueMatrix(
            planeCamera);

        // Source's texture path sets remaining view depth to zero. Do not let
        // either portal aperture sample a texture while that texture is being
        // rendered; the portal frames remain visible in the remote view.
        Renderer entrySurface = entryPortal.Renderer;
        Renderer exitSurface = exitPortal.Renderer;
        bool entrySurfaceWasEnabled =
            entrySurface != null && entrySurface.enabled;
        bool exitSurfaceWasEnabled =
            exitSurface != null && exitSurface.enabled;

        if (entrySurface != null)
            entrySurface.enabled = false;
        if (exitSurface != null)
            exitSurface.enabled = false;

        try
        {
            UniversalRenderPipeline.RenderSingleCamera(context, portalCamera);
        }
        finally
        {
            if (entrySurface != null)
                entrySurface.enabled = entrySurfaceWasEnabled;
            if (exitSurface != null)
                exitSurface.enabled = exitSurfaceWasEnabled;
        }
    }
}
