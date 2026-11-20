using System.Collections.Generic;
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

    [SerializeField, Range(1, 4)]
    private int recursionDepth = 2;

    private readonly Dictionary<string, RenderTexture> portalTextures =
        new Dictionary<string, RenderTexture>();
    private readonly List<int> renderPath = new List<int>(4);
    private readonly MaterialPropertyBlock portalPropertyBlock =
        new MaterialPropertyBlock();
    private int textureWidth;
    private int textureHeight;
    private int allocatedDepth;
    private bool rendering;

    private static readonly int MainTextureId =
        Shader.PropertyToID("_MainTex");
    private const float HammerUnitsPerUnityUnit = 52.4934383202f;

    private void Awake()
    {
        ResolveCameras();
        EnsureRenderTextures();
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

    private void EnsureRenderTextures()
    {
        int width = mainCamera != null ? mainCamera.pixelWidth : Screen.width;
        int height = mainCamera != null ? mainCamera.pixelHeight : Screen.height;
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        int depth = Mathf.Clamp(recursionDepth, 1, 4);

        if (textureWidth == width && textureHeight == height &&
            allocatedDepth == depth && portalTextures.Count > 0)
            return;

        ReleaseRenderTextures();
        textureWidth = width;
        textureHeight = height;
        allocatedDepth = depth;
        for (int portalIndex = 0; portalIndex < 2; ++portalIndex)
        {
            renderPath.Clear();
            renderPath.Add(portalIndex);
            AllocatePathAndChildren(renderPath);
        }
    }

    private void AllocatePathAndChildren(List<int> path)
    {
        string key = BuildPathKey(path);
        int divisor = 1 << (path.Count - 1);
        int width = Mathf.Max(1, textureWidth / divisor);
        int height = Mathf.Max(1, textureHeight / divisor);
        portalTextures.Add(key, CreateTexture("PortalView_" + key,
            width, height));

        if (path.Count >= allocatedDepth)
            return;

        for (int portalIndex = 0; portalIndex < 2; ++portalIndex)
        {
            path.Add(portalIndex);
            AllocatePathAndChildren(path);
            path.RemoveAt(path.Count - 1);
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
        foreach (RenderTexture texture in portalTextures.Values)
        {
            if (texture == null)
                continue;
            texture.Release();
            Destroy(texture);
        }
        portalTextures.Clear();
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
        if (portalTextures.Count == 0)
            return;

        rendering = true;
        RenderTexture previousTarget = portalCamera.targetTexture;
        Vector3 previousPosition = portalCamera.transform.position;
        Quaternion previousRotation = portalCamera.transform.rotation;
        Matrix4x4 previousView = portalCamera.worldToCameraMatrix;
        Matrix4x4 previousProjection = portalCamera.projectionMatrix;
        CameraClearFlags previousClearFlags = portalCamera.clearFlags;
        Color previousBackground = portalCamera.backgroundColor;
        int previousCullingMask = portalCamera.cullingMask;
        float previousNearClip = portalCamera.nearClipPlane;
        float previousFarClip = portalCamera.farClipPlane;
        try
        {
            portalCamera.clearFlags = mainCamera.clearFlags;
            portalCamera.backgroundColor = mainCamera.backgroundColor;
            portalCamera.cullingMask = mainCamera.cullingMask;
            portalCamera.nearClipPlane = mainCamera.nearClipPlane;
            portalCamera.farClipPlane = mainCamera.farClipPlane;

            for (int portalIndex = 0; portalIndex < 2; ++portalIndex)
            {
                renderPath.Clear();
                renderPath.Add(portalIndex);
                RenderPortalPath(context);
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
            portalCamera.cullingMask = previousCullingMask;
            portalCamera.nearClipPlane = previousNearClip;
            portalCamera.farClipPlane = previousFarClip;
            rendering = false;
        }
    }

    private void RenderPortalPath(ScriptableRenderContext context)
    {
        if (renderPath.Count < allocatedDepth)
        {
            for (int portalIndex = 0; portalIndex < 2; ++portalIndex)
            {
                renderPath.Add(portalIndex);
                RenderPortalPath(context);
                renderPath.RemoveAt(renderPath.Count - 1);
            }
        }

        string pathKey = BuildPathKey(renderPath);
        BindChildViewTextures(pathKey, renderPath.Count);
        if (!portalTextures.TryGetValue(pathKey, out RenderTexture target))
            return;

        Transform cameraTransform = portalCamera.transform;
        Vector3 cameraPosition = mainCamera.transform.position;
        Quaternion cameraRotation = mainCamera.transform.rotation;
        Portal exitPortal = null;

        for (int i = 0; i < renderPath.Count; ++i)
        {
            Portal entryPortal = portals[renderPath[i]];
            if (entryPortal == null || entryPortal.otherPortal == null)
                return;

            Transform entry = entryPortal.transform;
            Transform exit = entryPortal.otherPortal.transform;
            Quaternion entryToExit = exit.rotation *
                Quaternion.Euler(0f, 180f, 0f) *
                Quaternion.Inverse(entry.rotation);

            cameraPosition = exit.position + entryToExit *
                (cameraPosition - entry.position);
            cameraRotation = entryToExit * cameraRotation;
            exitPortal = entryPortal.otherPortal;
        }

        cameraTransform.SetPositionAndRotation(cameraPosition, cameraRotation);
        portalCamera.ResetWorldToCameraMatrix();
        portalCamera.projectionMatrix = mainCamera.projectionMatrix;
        portalCamera.targetTexture = target;

        if (exitPortal != null)
            ApplyExitClipPlane(exitPortal);

        UniversalRenderPipeline.RenderSingleCamera(context, portalCamera);
    }

    private void BindChildViewTextures(string pathKey, int pathLength)
    {
        bool hasChildren = pathLength < allocatedDepth;
        for (int portalIndex = 0; portalIndex < 2; ++portalIndex)
        {
            RenderTexture childTexture = null;
            if (hasChildren)
                portalTextures.TryGetValue(
                    pathKey + portalIndex, out childTexture);
            BindPortalTexture(portalIndex,
                childTexture != null
                    ? (Texture)childTexture
                    : Texture2D.blackTexture);
        }
    }

    private void BindMainViewTextures()
    {
        for (int portalIndex = 0; portalIndex < 2; ++portalIndex)
        {
            string pathKey = portalIndex.ToString();
            if (portalTextures.TryGetValue(pathKey, out RenderTexture texture))
                BindPortalTexture(portalIndex, texture);
        }
    }

    private void BindPortalTexture(int portalIndex, Texture texture)
    {
        Portal portal = portals[portalIndex];
        if (portal == null || portal.Renderer == null)
            return;

        portal.Renderer.GetPropertyBlock(portalPropertyBlock);
        portalPropertyBlock.SetTexture(MainTextureId, texture);
        portal.Renderer.SetPropertyBlock(portalPropertyBlock);
    }

    private void ApplyExitClipPlane(Portal exitPortal)
    {
        Transform exit = exitPortal.transform;

        // The Source renderer clips two Hammer units behind the exit plane.
        // Unity's oblique near plane needs its normal facing the virtual eye.
        Vector3 clipNormal = -exit.forward;
        if (Vector3.Dot(clipNormal,
                portalCamera.transform.position - exit.position) < 0f)
            clipNormal = -clipNormal;

        float planeOffset = 2f / HammerUnitsPerUnityUnit;
        Plane clipPlane = new Plane(
            clipNormal,
            exit.position + clipNormal * planeOffset);
        Vector4 planeWorld = new Vector4(
            clipPlane.normal.x,
            clipPlane.normal.y,
            clipPlane.normal.z,
            clipPlane.distance);
        Vector4 planeCamera = Matrix4x4.Transpose(Matrix4x4.Inverse(
            portalCamera.worldToCameraMatrix)) * planeWorld;
        portalCamera.projectionMatrix = portalCamera.CalculateObliqueMatrix(
            planeCamera);
    }

    private static string BuildPathKey(List<int> path)
    {
        char[] chars = new char[path.Count];
        for (int i = 0; i < path.Count; ++i)
            chars[i] = (char)('0' + path[i]);
        return new string(chars);
    }
}
