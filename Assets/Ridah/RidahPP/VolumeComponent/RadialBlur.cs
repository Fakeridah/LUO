using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Test/RadialBlur")]
    public sealed class RadialBlur : PostProcessVolumeComponent
    {
        public string ShaderName => "Hidden/PostProcessing/Blur/RadialBlur";
        private Material blitMaterial;
        public override void Initialize()
        {
            blitMaterial = CoreUtils.CreateEngineMaterial(ShaderName);
        }
        protected override void CleanUp()
        {
            if (!blitMaterial) CoreUtils.Destroy(blitMaterial);
        }


        public override bool IsActive() => BlurRadius.value > 0;
        public FloatParameter BlurRadius = new ClampedFloatParameter(0f, 0f, 1f);
        public IntParameter Iteration = new ClampedIntParameter(10, 2, 30);
        public FloatParameter RadialCenterX = new ClampedFloatParameter(0.5f, 0f, 1f);
        public FloatParameter RadialCenterY = new ClampedFloatParameter(0.5f, 0f, 1f);
    





        static class ShaderIDs
        {
            internal static readonly int Params = Shader.PropertyToID("_Params");
        }


    public override void Render(CommandBuffer cmd, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier target)
    {
        blitMaterial.SetVector(ShaderIDs.Params, new Vector4(BlurRadius.value * 0.02f, Iteration.value, RadialCenterX.value, RadialCenterY.value));
            cmd.Blit(source, target, blitMaterial, 0);
        }
    }

}