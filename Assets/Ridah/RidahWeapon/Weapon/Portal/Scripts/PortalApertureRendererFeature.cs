using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Composites recursive portal views after the opaque scene has established
/// depth. The aperture stencil keeps foreground geometry in front of a portal
/// while preventing the host wall from winning the final color draw.
/// </summary>
public sealed class PortalApertureRendererFeature : ScriptableRendererFeature
{
    private const int PortalCount = 2;
    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private static readonly int OverlapColorId =
        Shader.PropertyToID("_OverlapColor");
    private static readonly int CompositeStencilRefId =
        Shader.PropertyToID("_PortalCompositeStencilRef");
    private static readonly int CompositeStencilReadMaskId =
        Shader.PropertyToID("_PortalCompositeStencilReadMask");
    private static readonly int DepthStencilRefId =
        Shader.PropertyToID("_PortalDepthStencilRef");
    private static readonly int DepthStencilReadMaskId =
        Shader.PropertyToID("_PortalDepthStencilReadMask");
    // URP Deferred reserves bits 5 and 6 for material classification.
    // Keep portal IDs inside URP's user stencil mask (bits 0-3).
    private static readonly int[] StencilReferences = { 4, 8 };

    [SerializeField]
    private Color overlapFallbackColor =
        new Color(0f, 0.58431375f, 1f, 1f);

    [SerializeField]
    private Shader compositeShader;

    private Material[] materials;
    private PortalAperturePass portalPass;

    public override void Create()
    {
        ReleaseMaterials();
        Shader shader = compositeShader != null
            ? compositeShader
            : Shader.Find("Hidden/Ridah/PortalApertureComposite");
        if (shader == null)
        {
            portalPass = null;
            Debug.LogError(
                "Portal aperture composite shader was not found.");
            return;
        }

        materials = new Material[PortalCount];
        for (int i = 0; i < PortalCount; ++i)
        {
            materials[i] = CoreUtils.CreateEngineMaterial(shader);
            materials[i].SetFloat(
                "_PortalStencilRef", StencilReferences[i]);
            materials[i].SetFloat(
                "_PortalStencilWriteMask", StencilReferences[i]);
            materials[i].SetFloat(
                CompositeStencilRefId, StencilReferences[i]);
            materials[i].SetFloat(
                CompositeStencilReadMaskId, 12f);
            materials[i].SetFloat(
                DepthStencilRefId, StencilReferences[i]);
            materials[i].SetFloat(
                DepthStencilReadMaskId, StencilReferences[i]);
        }

        portalPass = new PortalAperturePass(
            materials, overlapFallbackColor)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        RecursivePortalCamera portalCamera = RecursivePortalCamera.Active;
        if (portalPass == null || portalCamera == null ||
            renderingData.cameraData.camera != portalCamera.MainCamera)
            return;

        portalPass.Setup(portalCamera);
        renderer.EnqueuePass(portalPass);
    }

    protected override void Dispose(bool disposing)
    {
        ReleaseMaterials();
        portalPass = null;
    }

    private void ReleaseMaterials()
    {
        if (materials == null)
            return;

        for (int i = 0; i < materials.Length; ++i)
        {
            if (materials[i] != null)
                CoreUtils.Destroy(materials[i]);
        }
        materials = null;
    }

    private sealed class PortalAperturePass : ScriptableRenderPass
    {
        private sealed class PassData
        {
            public readonly Renderer[] surfaces = new Renderer[PortalCount];
            public readonly Renderer[] stencilCaps = new Renderer[PortalCount];
            public readonly Renderer[] depthFixCaps = new Renderer[PortalCount];
            public readonly Texture[] textures = new Texture[PortalCount];
            public Material[] materials;
            public int farPortalIndex;
            public int nearPortalIndex;
            public int frontPortalIndex;
            public bool fullyCoincident;
        }

        private readonly Material[] materials;
        private readonly Color overlapFallbackColor;
        private RecursivePortalCamera portalCamera;

        public PortalAperturePass(
            Material[] materials,
            Color overlapFallbackColor)
        {
            this.materials = materials;
            this.overlapFallbackColor = overlapFallbackColor;
        }

        public void Setup(RecursivePortalCamera portalCamera)
        {
            this.portalCamera = portalCamera;
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            UniversalCameraData cameraData =
                frameData.Get<UniversalCameraData>();
            UniversalResourceData resourceData =
                frameData.Get<UniversalResourceData>();
            if (portalCamera == null || materials == null ||
                cameraData.camera != portalCamera.MainCamera ||
                !resourceData.activeColorTexture.IsValid() ||
                !resourceData.activeDepthTexture.IsValid())
                return;

            PassData passData = new PassData
            {
                materials = materials
            };
            portalCamera.GetPortalRenderOrder(
                cameraData.camera,
                out passData.farPortalIndex,
                out passData.nearPortalIndex);
            passData.fullyCoincident =
                portalCamera.ArePortalAperturesCoincident(
                    cameraData.camera, out passData.frontPortalIndex);
            if (!passData.fullyCoincident)
                passData.frontPortalIndex = passData.nearPortalIndex;

            for (int portalIndex = 0;
                 portalIndex < PortalCount;
                 ++portalIndex)
            {
                if (!portalCamera.TryGetPortalAperture(
                        portalIndex,
                        out passData.surfaces[portalIndex],
                        out passData.stencilCaps[portalIndex],
                        out passData.depthFixCaps[portalIndex],
                        out passData.textures[portalIndex]))
                {
                    passData.surfaces[portalIndex] = null;
                    passData.stencilCaps[portalIndex] = null;
                    passData.depthFixCaps[portalIndex] = null;
                    passData.textures[portalIndex] = null;
                }

                materials[portalIndex].SetTexture(
                    MainTextureId, passData.textures[portalIndex]);
                bool selectedFullOverlapView = passData.fullyCoincident &&
                    portalIndex == passData.frontPortalIndex;
                float selectedStencilRef = StencilReferences[portalIndex];
                materials[portalIndex].SetFloat(
                    CompositeStencilRefId, selectedStencilRef);
                materials[portalIndex].SetFloat(
                    CompositeStencilReadMaskId,
                    selectedFullOverlapView ? selectedStencilRef : 12f);
                materials[portalIndex].SetFloat(
                    DepthStencilRefId, selectedStencilRef);
                materials[portalIndex].SetFloat(
                    DepthStencilReadMaskId, selectedStencilRef);
            }
            materials[0].SetColor(
                OverlapColorId,
                portalCamera.GetMostRecentlyPlacedPortalColor(
                    overlapFallbackColor));

            using (var builder = renderGraph.AddUnsafePass<PassData>(
                       "Source Style Portal Aperture Composite",
                       out PassData graphPassData))
            {
                graphPassData.materials = passData.materials;
                for (int i = 0; i < PortalCount; ++i)
                {
                    graphPassData.surfaces[i] = passData.surfaces[i];
                    graphPassData.stencilCaps[i] = passData.stencilCaps[i];
                    graphPassData.depthFixCaps[i] = passData.depthFixCaps[i];
                    graphPassData.textures[i] = passData.textures[i];
                }
                graphPassData.farPortalIndex = passData.farPortalIndex;
                graphPassData.nearPortalIndex = passData.nearPortalIndex;
                graphPassData.frontPortalIndex = passData.frontPortalIndex;
                graphPassData.fullyCoincident = passData.fullyCoincident;

                builder.SetRenderAttachment(
                    resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(
                    resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(
                    (PassData data, UnsafeGraphContext context) =>
                        ExecutePass(
                            data,
                            CommandBufferHelpers.GetNativeCommandBuffer(
                                context.cmd)));
            }
        }

        private static void ExecutePass(
            PassData data,
            CommandBuffer commandBuffer)
        {
            // Clear only the two user stencil bits owned by this feature so
            // portal masks can accumulate into 4, 8, or 12 every frame.
            commandBuffer.DrawProcedural(
                Matrix4x4.identity,
                data.materials[0],
                6,
                MeshTopology.Triangles,
                3,
                1);

            // Source first marks each portal quad and its separately generated
            // near-plane stencil cap. Their independent bits accumulate so
            // intersecting apertures can be handled as a distinct region.
            for (int orderIndex = 0; orderIndex < PortalCount; ++orderIndex)
            {
                int portalIndex = orderIndex == 0
                    ? data.farPortalIndex
                    : data.nearPortalIndex;
                DrawRenderer(
                    commandBuffer, data.surfaces[portalIndex],
                    data.materials[portalIndex], 0);
                DrawRenderer(
                    commandBuffer, data.stencilCaps[portalIndex],
                    data.materials[portalIndex], 4);
            }

            // Source clears only the portal aperture's depth before drawing
            // the remote view. The RT camera has already rendered that view,
            // so clear the masked depth here before compositing its pixels.
            for (int orderIndex = 0; orderIndex < PortalCount; ++orderIndex)
            {
                int portalIndex = orderIndex == 0
                    ? data.farPortalIndex
                    : data.nearPortalIndex;
                if (data.textures[portalIndex] == null)
                    continue;

                commandBuffer.DrawProcedural(
                    Matrix4x4.identity,
                    data.materials[portalIndex],
                    2,
                    MeshTopology.Triangles,
                    3,
                    1);
            }

            // Source recursively draws directly into the whole stencil
            // aperture. The Unity RT equivalent therefore composites a
            // fullscreen triangle constrained by that portal's stencil bit.
            for (int orderIndex = 0; orderIndex < PortalCount; ++orderIndex)
            {
                int portalIndex = orderIndex == 0
                    ? data.farPortalIndex
                    : data.nearPortalIndex;
                if (data.fullyCoincident &&
                    portalIndex != data.frontPortalIndex)
                    continue;
                if (data.textures[portalIndex] == null)
                    continue;

                commandBuffer.DrawProcedural(
                    Matrix4x4.identity,
                    data.materials[portalIndex],
                    1,
                    MeshTopology.Triangles,
                    3,
                    1);
            }

            // A partial overlap uses the rear portal's material accent color.
            // Full coincidence instead keeps the chosen front-facing view.
            if (!data.fullyCoincident)
            {
                commandBuffer.DrawProcedural(
                    Matrix4x4.identity,
                    data.materials[0],
                    5,
                    MeshTopology.Triangles,
                    3,
                    1);
            }

            // Match DrawPostStencilFixes: restore depth using the portal plane
            // and its separate DrawRenderFixMesh near-plane cap.
            for (int orderIndex = 0; orderIndex < PortalCount; ++orderIndex)
            {
                int portalIndex = orderIndex == 0
                    ? data.farPortalIndex
                    : data.nearPortalIndex;
                if (data.fullyCoincident &&
                    portalIndex != data.frontPortalIndex)
                    continue;
                if (data.textures[portalIndex] == null)
                    continue;

                DrawRenderer(
                    commandBuffer, data.surfaces[portalIndex],
                    data.materials[portalIndex], 3);
                DrawRenderer(
                    commandBuffer, data.depthFixCaps[portalIndex],
                    data.materials[portalIndex], 3);
            }
        }

        private static void DrawRenderer(
            CommandBuffer commandBuffer,
            Renderer target,
            Material material,
            int shaderPass)
        {
            if (target == null || !target.enabled || material == null)
                return;

            Mesh mesh = null;
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter != null)
                mesh = filter.sharedMesh;
            if (target is SkinnedMeshRenderer skinned)
                mesh = skinned.sharedMesh;

            int submeshCount = mesh != null ? mesh.subMeshCount : 1;
            for (int submesh = 0; submesh < submeshCount; ++submesh)
            {
                commandBuffer.DrawRenderer(
                    target, material, submesh, shaderPass);
            }
        }
    }
}
