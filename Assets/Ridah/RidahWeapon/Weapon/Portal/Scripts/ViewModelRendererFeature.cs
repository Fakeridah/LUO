using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Draws first-person weapon geometry in a dedicated viewmodel pass.
/// World/portal passes exclude the ViewModel layer; this pass clears depth
/// after them and redraws that layer with its own near clip plane so very
/// close weapon parts are never clipped by the gameplay camera.
/// </summary>
public sealed class ViewModelRendererFeature : ScriptableRendererFeature
{
    private const string DefaultLayerName = "ViewModel";
    private const int FallbackLayerIndex = 14;

    // Reused every frame: avoids a fresh Plane[6] allocation in the
    // viewmodel near-plane override path.
    private static readonly Plane[] FrustumPlanes = new Plane[6];

    private static readonly FieldInfo ProjectionNearField =
        GetCameraPropertiesField("projectionNear");
    private static readonly FieldInfo CameraNearField =
        GetCameraPropertiesField("cameraNear");
    private static readonly FieldInfo CameraWorldToClipField =
        GetCameraPropertiesField("cameraWorldToClip");
    private static readonly FieldInfo ActualWorldToClipField =
        GetCameraPropertiesField("actualWorldToClip");
    private static readonly FieldInfo ImplicitProjectionField =
        GetCameraPropertiesField("implicitProjection");

    [SerializeField]
    private string viewModelLayerName = DefaultLayerName;

    [SerializeField]
    [Min(0.0001f)]
    private float viewModelNearClipPlane = 0.001f;

    private ViewModelPass viewModelPass;
    private Camera gameplayCamera;

    public override void Create()
    {
        int layerIndex = LayerMask.NameToLayer(viewModelLayerName);
        if (layerIndex < 0)
            layerIndex = FallbackLayerIndex;

        var shaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly")
        };

        viewModelPass = new ViewModelPass(
            shaderTags,
            1 << layerIndex,
            Mathf.Max(0.0001f, viewModelNearClipPlane))
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (viewModelPass == null ||
            renderingData.cameraData.cameraType == CameraType.Preview)
            return;

        // Portal render textures must not contain the local weapon model.
        // The gameplay camera must be resolved independently from the portal
        // pair: weapons still need rendering when portals are not placed or
        // when RecursivePortalPair is disabled.
        Camera targetCamera = ResolveGameplayCamera();
        if (targetCamera == null ||
            renderingData.cameraData.camera != targetCamera)
            return;

        renderer.EnqueuePass(viewModelPass);
    }

    private Camera ResolveGameplayCamera()
    {
        RecursivePortalCamera portalCamera = RecursivePortalCamera.Active;
        if (portalCamera != null && portalCamera.MainCamera != null)
            return portalCamera.MainCamera;

        if (gameplayCamera != null)
            return gameplayCamera;

        PortablePlayer player =
            UnityEngine.Object.FindFirstObjectByType<PortablePlayer>(
                FindObjectsInactive.Include);
        if (player != null)
            gameplayCamera = player.GetComponentInChildren<Camera>(true);

        if (gameplayCamera == null)
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; ++i)
            {
                Camera camera = cameras[i];
                if (camera == null || camera.transform.parent == null ||
                    camera.transform.parent.name != "CameraHolder")
                    continue;
                gameplayCamera = camera;
                break;
            }
        }
        return gameplayCamera;
    }

    private static FieldInfo GetCameraPropertiesField(string fieldName)
    {
        return typeof(CameraProperties).GetField(
            fieldName,
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic);
    }

    protected override void Dispose(bool disposing)
    {
        viewModelPass = null;
    }

    private sealed class ViewModelPass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            public RendererListHandle opaqueList;
            public RendererListHandle transparentList;
            public Matrix4x4 viewMatrix;
            public Matrix4x4 originalProjection;
            public Matrix4x4 viewModelProjection;
            public bool overrideNearClipPlane;
        }

        private readonly List<ShaderTagId> shaderTags;
        private readonly int viewModelLayerMask;
        private readonly float viewModelNearClipPlane;

        public ViewModelPass(
            List<ShaderTagId> shaderTags,
            int viewModelLayerMask,
            float viewModelNearClipPlane)
        {
            this.shaderTags = shaderTags;
            this.viewModelLayerMask = viewModelLayerMask;
            this.viewModelNearClipPlane = viewModelNearClipPlane;
            profilingSampler = new ProfilingSampler(
                "ViewModel Clear Depth Redraw");
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            UniversalRenderingData renderingData =
                frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData =
                frameData.Get<UniversalCameraData>();
            UniversalLightData lightData =
                frameData.Get<UniversalLightData>();
            UniversalResourceData resourceData =
                frameData.Get<UniversalResourceData>();
            CullContextData cullContextData =
                frameData.Get<CullContextData>();

            Camera camera = cameraData.camera;
            Matrix4x4 viewMatrix = cameraData.GetViewMatrix();
            bool renderIntoTexture = !resourceData.isActiveTargetBackBuffer;
            Matrix4x4 originalProjection = GL.GetGPUProjectionMatrix(
                cameraData.GetProjectionMatrix(0), renderIntoTexture);

            float viewNear = Mathf.Min(
                viewModelNearClipPlane, camera.nearClipPlane);
            bool overrideNear = !camera.orthographic &&
                viewNear < camera.nearClipPlane - 0.000001f;
            Matrix4x4 viewProjection = overrideNear
                ? Matrix4x4.Perspective(
                    camera.fieldOfView,
                    camera.aspect,
                    viewNear,
                    camera.farClipPlane)
                : cameraData.GetProjectionMatrix(0);

            CullingResults cullingResults = renderingData.cullResults;

            // The gameplay cull already contains the ViewModel layer when
            // no near-plane override is needed. Only pay for a second cull
            // when the weapon projection differs from the gameplay camera.
            if (overrideNear &&
                camera.TryGetCullingParameters(
                    false, out ScriptableCullingParameters cullingParameters))
            {
                Matrix4x4 cullingMatrix = viewProjection * viewMatrix;
                cullingParameters.cullingMatrix = cullingMatrix;

                // Cull only the dedicated weapon layer. This is the main
                // CPU optimisation: the second cull no longer walks every
                // object visible to the gameplay camera.
                cullingParameters.cullingMask = (uint)viewModelLayerMask;

                CameraProperties cameraProperties =
                    cullingParameters.cameraProperties;
                cameraProperties = WithInternalField(
                    cameraProperties, ProjectionNearField, viewNear);
                cameraProperties = WithInternalField(
                    cameraProperties, CameraNearField, viewNear);
                cameraProperties = WithInternalField(
                    cameraProperties, CameraWorldToClipField, cullingMatrix);
                cameraProperties = WithInternalField(
                    cameraProperties, ActualWorldToClipField, cullingMatrix);
                cameraProperties = WithInternalField(
                    cameraProperties, ImplicitProjectionField, viewProjection);
                cullingParameters.cameraProperties = cameraProperties;

                // TryGetCullingParameters bakes the six frustum planes
                // from the gameplay camera's large near plane. Rebuild
                // them from the viewmodel projection, otherwise close
                // weapons are culled before the renderer list is built.
                GeometryUtility.CalculateFrustumPlanes(
                    cullingMatrix, FrustumPlanes);
                cullingParameters.cullingPlaneCount = FrustumPlanes.Length;
                for (int i = 0; i < FrustumPlanes.Length; ++i)
                    cullingParameters.SetCullingPlane(i, FrustumPlanes[i]);

                cullingResults = cullContextData.Cull(ref cullingParameters);
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                       "ViewModel Clear Depth Redraw",
                       out PassData passData,
                       profilingSampler))
            {
                bool hasDepth = resourceData.activeDepthTexture.IsValid();
                builder.SetRenderAttachment(
                    resourceData.activeColorTexture, 0, AccessFlags.Write);
                if (hasDepth)
                {
                    builder.SetRenderAttachmentDepth(
                        resourceData.activeDepthTexture,
                        AccessFlags.ReadWrite);
                }

                CreateRendererList(
                    renderGraph,
                    cullingResults,
                    renderingData,
                    cameraData,
                    lightData,
                    RenderQueueRange.opaque,
                    SortingCriteria.CommonOpaque,
                    out passData.opaqueList);
                builder.UseRendererList(passData.opaqueList);

                CreateRendererList(
                    renderGraph,
                    cullingResults,
                    renderingData,
                    cameraData,
                    lightData,
                    RenderQueueRange.transparent,
                    SortingCriteria.CommonTransparent,
                    out passData.transparentList);
                builder.UseRendererList(passData.transparentList);

                passData.viewMatrix = viewMatrix;
                passData.originalProjection = originalProjection;
                passData.viewModelProjection = GL.GetGPUProjectionMatrix(
                    viewProjection, renderIntoTexture);
                passData.overrideNearClipPlane = overrideNear;

                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(
                    (PassData data, RasterGraphContext context) =>
                    {
                        if (data.overrideNearClipPlane)
                        {
                            // Viewmodel-only projection: the world keeps the
                            // gameplay near plane and its depth precision.
                            RenderingUtils.SetViewAndProjectionMatrices(
                                context.cmd,
                                data.viewMatrix,
                                data.viewModelProjection,
                                false);
                        }

                        if (hasDepth)
                        {
                            // Source renders viewmodels after a depth clear.
                            // The weapon then owns a fresh depth range and can
                            // never be rejected by the world wall depth.
                            context.cmd.ClearRenderTarget(
                                true, false, Color.clear, 1f);
                        }
                        context.cmd.DrawRendererList(data.opaqueList);
                        context.cmd.DrawRendererList(data.transparentList);

                        if (data.overrideNearClipPlane)
                        {
                            RenderingUtils.SetViewAndProjectionMatrices(
                                context.cmd,
                                data.viewMatrix,
                                data.originalProjection,
                                false);
                        }
                    });
            }
        }

        private static T WithInternalField<T>(
            T value,
            FieldInfo field,
            object fieldValue)
        {
            if (field == null)
                return value;

            object boxed = value;
            field.SetValue(boxed, fieldValue);
            return (T)boxed;
        }

        private void CreateRendererList(
            RenderGraph renderGraph,
            CullingResults cullingResults,
            UniversalRenderingData renderingData,
            UniversalCameraData cameraData,
            UniversalLightData lightData,
            RenderQueueRange queueRange,
            SortingCriteria sortingCriteria,
            out RendererListHandle handle)
        {
            var filteringSettings = new FilteringSettings(
                queueRange, viewModelLayerMask);
            DrawingSettings drawingSettings =
                RenderingUtils.CreateDrawingSettings(
                    shaderTags,
                    renderingData,
                    cameraData,
                    lightData,
                    sortingCriteria);
            var listParams = new RendererListParams(
                cullingResults,
                drawingSettings,
                filteringSettings);
            handle = renderGraph.CreateRendererList(listParams);
        }
    }
}
