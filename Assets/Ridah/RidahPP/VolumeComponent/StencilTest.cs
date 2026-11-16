using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/StencilTest")]
    public sealed class StencilTest : PostProcessVolumeComponent
    {
        public ClampedFloatParameter strengthX = new ClampedFloatParameter(0.05f, 0.0f, 1.5f);
        public ClampedFloatParameter strengthY = new ClampedFloatParameter(0.05f, 0.0f, 1.5f);

        private Material bliteMaterial;

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => strengthX.value != 0.05f;

        //Cache parameter ids to avoid do the same thing every update.
        static class IDs
        {
            internal readonly static int _intensity = Shader.PropertyToID("intensity");
        }

        //Create shader material.
        //You should put shader in the Resources folder, ensure it will be included in Asset Bundle, or add it use another way by yourself.
        public override void Initialize()
        {
            bliteMaterial = CoreUtils.CreateEngineMaterial("Ridah/StencilTest");
        }

        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            var pixelRect = cameraData.camera.pixelRect;
            float scale = cameraData.isSceneViewCamera ? 1 : cameraData.renderScale;
            int width = (int)(pixelRect.width * scale);
            int height = (int)(pixelRect.height * scale);

            //Set RenderTexture for shader use.
            cb.SetPostProcessSourceTexture(source);

            //Set render target and draw.
            cb.DrawFullScreenTriangle(bliteMaterial, destination);
        }

        //Do something before Destroy(), if you need.
        protected override void CleanUp()
        {

        }
    }

}