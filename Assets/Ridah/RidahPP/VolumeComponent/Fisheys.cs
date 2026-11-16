using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/Fisheye")]
    public sealed class Fisheye : PostProcessVolumeComponent
    {
        public ClampedFloatParameter strengthX = new ClampedFloatParameter(0.05f, 0.0f, 1.5f);
        public ClampedFloatParameter strengthY = new ClampedFloatParameter(0.05f, 0.0f, 1.5f);

        private Material fisheyeMaterial;
        private Material stencilMaterial;

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
            fisheyeMaterial = CoreUtils.CreateEngineMaterial("Ridah/Fisheye");
            stencilMaterial = CoreUtils.CreateEngineMaterial("Ridah/StencilTest");
        }

        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            var pixelRect = cameraData.camera.pixelRect;
            float scale = cameraData.isSceneViewCamera ? 1 : cameraData.renderScale;
            int width = (int)(pixelRect.width * scale);
            int height = (int)(pixelRect.height * scale);

            float oneOverBaseSize = 80.0f / 512.0f; // to keep values more like in the old version of fisheye

            float ar = (width * 1.0f) / (height * 1.0f);
            fisheyeMaterial.SetVector(IDs._intensity, new Vector4(strengthX.value * ar * oneOverBaseSize, 
           strengthY.value * oneOverBaseSize, strengthX.value * ar * oneOverBaseSize, strengthY.value * oneOverBaseSize));

            //Set RenderTexture for shader use.
            cb.SetPostProcessSourceTexture(source);



            RenderTexture rt = new RenderTexture(width, height, 0);
            cb.Blit(source, rt);
            stencilMaterial.SetTexture("_MainTex", rt);
            rt.Release();

            //Set render target and draw.
            cb.DrawFullScreenTriangle(fisheyeMaterial, destination);
        }

        //Do something before Destroy(), if you need.
        protected override void CleanUp()
        {

        }
    }

}