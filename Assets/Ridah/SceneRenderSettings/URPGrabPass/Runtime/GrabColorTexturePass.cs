using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#pragma warning disable 0619
using System.Collections.Generic;

namespace URPGrabPass.Runtime
{
    /// <summary>
    ///     Path that grabs the color texture of the camera.
    /// </summary>
    public class GrabColorTexturePass : ScriptableRenderPass
    {

        private readonly RTHandle _grabbedSceneTextureHandle;
        private readonly string _grabbedSceneTextureName;
        private readonly int _grabbedSceneTexturePropertyId;
        private readonly RTHandle _grabbedDepthTextureHandle;
        private readonly string _grabbedDepthTextureName;
        private readonly int _grabbedDepthTexturePropertyId;
        private FilteringSettings _filteringSettings;

        private ScriptableRenderer _renderer;

        public GrabColorTexturePass(GrabTiming timing, string grabbedSceneTextureName, string grabbedDepthTextureName)
        {
            renderPassEvent = timing.ToRenderPassEvent();
            _grabbedSceneTextureName = grabbedSceneTextureName;
            _grabbedSceneTextureHandle = RTHandles.Alloc(_grabbedSceneTextureName, _grabbedSceneTextureName);
            _grabbedSceneTexturePropertyId = Shader.PropertyToID(_grabbedSceneTextureName);


            _grabbedDepthTextureName = grabbedDepthTextureName;
            _grabbedDepthTextureHandle = RTHandles.Alloc(_grabbedDepthTextureName, _grabbedDepthTextureName);
            _grabbedDepthTexturePropertyId = Shader.PropertyToID(_grabbedDepthTextureName);
        }

        public void BeforeEnqueue(ScriptableRenderer renderer)
        {
            _renderer = renderer;
        }

        public void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cmd.GetTemporaryRT(_grabbedSceneTexturePropertyId, cameraTextureDescriptor);
            cmd.SetGlobalTexture(_grabbedSceneTextureName, _grabbedSceneTextureHandle.nameID);

            cmd.GetTemporaryRT(_grabbedDepthTexturePropertyId, cameraTextureDescriptor);
            cmd.SetGlobalTexture(_grabbedDepthTextureName, _grabbedDepthTextureHandle.nameID);
        }

        public void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get(nameof(GrabColorTexturePass));


            cmd.Clear();
            var cameraTarget = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
            cmd.Blit(cameraTarget, _grabbedSceneTextureHandle.nameID);
            cmd.Blit(cameraTarget, _grabbedDepthTextureHandle.nameID);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_grabbedSceneTexturePropertyId);
        }
    }
}
