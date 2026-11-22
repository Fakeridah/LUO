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

    private RenderTexture tempTexture1;
    private RenderTexture tempTexture2;
    private int textureWidth;
    private int textureHeight;
    private bool rendering;

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
    }

    private void OnDisable()
    {
        RenderPipeline.beginCameraRendering -= UpdateCamera;
    }

    private void OnDestroy()
    {
        RenderPipeline.beginCameraRendering -= UpdateCamera;
        if (tempTexture1 != null)
            tempTexture1.Release();
        if (tempTexture2 != null)
            tempTexture2.Release();
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
        BindTextures();
        if (rendering)
            return;

        rendering = true;
        try
        {
            portalCamera.targetTexture = tempTexture1;
            for (int i = iterations - 1; i >= 0; --i)
                RenderCamera(portals[0], portals[1], i, context);

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

    private void RenderCamera(Portal inPortal, Portal outPortal,
        int iterationID, ScriptableRenderContext context)
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

            Quaternion relativeRotation = Quaternion.Inverse(inTransform.rotation) *
                cameraTransform.rotation;
            relativeRotation = Quaternion.Euler(0f, 180f, 0f) * relativeRotation;
            cameraTransform.rotation = outTransform.rotation * relativeRotation;
        }

        portalCamera.projectionMatrix = mainCamera.projectionMatrix;

        Plane clipPlane = new Plane(-outTransform.forward,
            outTransform.position);
        Vector4 clipPlaneCameraSpace =
            Matrix4x4.Transpose(Matrix4x4.Inverse(
                portalCamera.worldToCameraMatrix)) *
            new Vector4(clipPlane.normal.x, clipPlane.normal.y,
                clipPlane.normal.z, clipPlane.distance);
        portalCamera.projectionMatrix = mainCamera.CalculateObliqueMatrix(
            clipPlaneCameraSpace);

        UniversalRenderPipeline.RenderSingleCamera(context, portalCamera);
    }
}
