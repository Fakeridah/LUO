using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Ridah.URPPostProcessing
{
    public class PostProcessRenderPass : ScriptableRenderPass
    {
        private string displayName;
        private int cycleRT_1 = Shader.PropertyToID("cycleRT_1");
        private int cycleRT_2 = Shader.PropertyToID("cycleRT_2");
        private List<Type> volumeTypeList;
        private List<PostProcessVolumeComponent> activeVolumeList;
        private GraphicsFormat defaultHDRFormat;
        private CameraData cameraData;

        private sealed class RenderGraphPassData
        {
            public TextureHandle source;
            public TextureHandle destination;
            public TextureHandle cameraDepth;
            public TextureHandle cameraNormals;
            public PostProcessVolumeComponent component;
            public CameraData cameraData;
        }

        public PostProcessRenderPass(RenderPassEvent passEvent, PostProcessOrderConfig config)
        {
            displayName = $"CustomPostProcessPass {passEvent}";//string.Format("CustomPostProcessPass {0} ",passEvent);
            renderPassEvent = passEvent;
            requiresIntermediateTexture = true;

            activeVolumeList = new List<PostProcessVolumeComponent>();
            volumeTypeList = new List<Type>();

            var piplineAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Linear | FormatUsage.Render)
                && piplineAsset.supportsHDR)
            {
                defaultHDRFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            }
            else
            {
                defaultHDRFormat = QualitySettings.activeColorSpace == ColorSpace.Linear
                    ? GraphicsFormat.R8G8B8A8_SRGB
                    : GraphicsFormat.R8G8B8A8_UNorm;//normalize rgba
            }

            //collect all custom postprocess volume belong this InjectionPoint
            var allVolumeTypes = VolumeManager.instance.baseComponentTypeArray;
            foreach (var volumeName in config.GetVolumeList((InjectionPoint)renderPassEvent))
            {
                var volumeType = allVolumeTypes.ToList().Find((t) => { return t.ToString() == volumeName; });

                //check volume type is valid 
                if (volumeType == null)
                {
                    Debug.LogError($"Can't find Volume : [{volumeName}] , Remove it from config");
                    continue;
                }

                volumeTypeList.Add(volumeType); //type return postprocess volume
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (volumeTypeList.Count == 0 || cameraData.camera == null || cameraData.postProcessEnabled == false)
                return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var activeColor = resourceData.activeColorTexture;
            if (!activeColor.IsValid())
                return;

            activeVolumeList.Clear();
            bool isSceneViewCamera = cameraData.isSceneViewCamera;
            foreach (var item in volumeTypeList)
            {
                var volumeComp = VolumeManager.instance.stack.GetComponent(item) as PostProcessVolumeComponent;
                if (volumeComp == null || volumeComp.IsActive() == false)
                    continue;
                if (isSceneViewCamera && volumeComp.visibleInSceneView == false)
                    continue;

                volumeComp.SetupIfNeed();
                activeVolumeList.Add(volumeComp);
            }

            if (activeVolumeList.Count == 0)
                return;

            var textureDesc = renderGraph.GetTextureDesc(activeColor);
            textureDesc.clearBuffer = false;

            TextureHandle source = renderGraph.CreateTexture(new TextureDesc(textureDesc)
            {
                name = "RidahCustomPostProcessSource"
            });
            renderGraph.AddBlitPass(activeColor, source, Vector2.one, Vector2.zero, passName: "Ridah Custom PostProcess Copy");

            for (int i = 0; i < activeVolumeList.Count; i++)
            {
                bool isFinalVolume = i == activeVolumeList.Count - 1;
                TextureHandle destination;

                if (isFinalVolume)
                {
                    destination = activeColor;
                }
                else
                {
                    destination = renderGraph.CreateTexture(new TextureDesc(textureDesc)
                    {
                        name = $"RidahCustomPostProcessTarget_{i}"
                    });
                }

                using (var builder = renderGraph.AddUnsafePass<RenderGraphPassData>(
                    $"{displayName} - {activeVolumeList[i].displayName}", out var passData))
                {
                    passData.source = source;
                    passData.destination = destination;
                    passData.cameraDepth = resourceData.cameraDepthTexture;
                    passData.cameraNormals = resourceData.cameraNormalsTexture;
                    passData.component = activeVolumeList[i];
                    passData.cameraData = cameraData;

                    builder.UseTexture(passData.source, AccessFlags.Read);
                    builder.SetRenderAttachment(passData.destination, 0, AccessFlags.Write);

                    if (passData.cameraDepth.IsValid())
                        builder.UseTexture(passData.cameraDepth, AccessFlags.Read);
                    if (passData.cameraNormals.IsValid())
                        builder.UseTexture(passData.cameraNormals, AccessFlags.Read);

                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc<RenderGraphPassData>(ExecuteRenderGraph);
                }

                source = destination;
            }
        }

        private static void ExecuteRenderGraph(RenderGraphPassData data, UnsafeGraphContext context)
        {
            CommandBuffer cb = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            RenderTargetIdentifier source = data.source;
            RenderTargetIdentifier destination = data.destination;

            cb.BeginSample(data.component.displayName);
            data.component.Render(cb, data.cameraData, source, destination);
            cb.EndSample(data.component.displayName);
        }
        public void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (volumeTypeList.Count == 0)
                return;

            if (renderingData.cameraData.postProcessEnabled == false)
                return;

            //collect active pp volume
            activeVolumeList.Clear();
            bool isSceneViewCamera = renderingData.cameraData.isSceneViewCamera;
            foreach (var item in volumeTypeList)
            {
                var volumeComp = VolumeManager.instance.stack.GetComponent(item) as PostProcessVolumeComponent;

                if (volumeComp.IsActive() == false)
                    continue;
                if (isSceneViewCamera && volumeComp.visibleInSceneView == false)
                    continue;

                activeVolumeList.Add(volumeComp);
                volumeComp.SetupIfNeed();// Initialize
            }

            if (activeVolumeList.Count <= 0)
                return;

            CommandBuffer cb = CommandBufferPool.Get();
            cb.name = displayName;

            var cameraData = renderingData.cameraData;
            var pixelRect = cameraData.camera.pixelRect;
            float scale = cameraData.isSceneViewCamera ? 1 : cameraData.renderScale;
            int width = (int)(pixelRect.width * scale);
            int height = (int)(pixelRect.height * scale);
            //claim two rendertexture for cycled use
            cb.GetTemporaryRT(cycleRT_1, width, height, 0, FilterMode.Bilinear, defaultHDRFormat);
            cb.GetTemporaryRT(cycleRT_2, width, height, 0, FilterMode.Bilinear, defaultHDRFormat);
            var target = cycleRT_1;
            var source = cycleRT_2;

            for (int i = 0; i < activeVolumeList.Count; i++)
            {
                var volumeComp = activeVolumeList[i];

                if (i == 0)
                {
                    cb.Blit(BuiltinRenderTextureType.CurrentActive, source);//initialize sourceRT with current cameraRT
                }
                else
                {
                    CoreUtils.Swap(ref target, ref source);
                }

                RenderTargetIdentifier renderTarget;
                bool isFinalVolume = i == activeVolumeList.Count - 1;
                if (isFinalVolume)
                {
                    bool renderToDefaultColorTexture =
                        renderPassEvent == RenderPassEvent.BeforeRenderingPostProcessing
                        || renderPassEvent == RenderPassEvent.AfterRenderingSkybox;

                    if (renderToDefaultColorTexture)
                    {
                        //stackalloc more high-performance
                        renderTarget = BuiltinRenderTextureType.CameraTarget;
                    }
                    else
                    {
                        renderTarget = BuiltinRenderTextureType.CameraTarget;
                    }
                }
                else
                {
                    renderTarget = target;
                }
                cb.SetRenderTarget(renderTarget);

                cb.BeginSample(volumeComp.displayName);
                volumeComp.Render(cb, renderingData.cameraData, source, renderTarget);//...
                cb.EndSample(volumeComp.displayName);
            }

            cb.ReleaseTemporaryRT(source);
            cb.ReleaseTemporaryRT(target);
            context.ExecuteCommandBuffer(cb);
            cb.Clear();
            CommandBufferPool.Release(cb);
        }
        //custom cameraNormal setup
        public void Setup(CameraData cameraData)
        {
            this.cameraData = cameraData;
            ConfigureInput(ScriptableRenderPassInput.Normal); // all of this to just call this one line
            return;
        }
    }
}
