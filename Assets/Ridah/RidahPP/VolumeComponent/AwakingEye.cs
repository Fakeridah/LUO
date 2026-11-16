using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Test/AwakingEye")]
    public sealed class AwakingEye : PostProcessVolumeComponent
    {
        public string ShaderName => "Hidden/PostProcessing/AwakingEye";
        private Material blitMaterial;
        public override void Initialize()
        {
            blitMaterial = CoreUtils.CreateEngineMaterial(ShaderName);
        }
        protected override void CleanUp()
        {
            if (!blitMaterial) CoreUtils.Destroy(blitMaterial);
        }
        public override bool IsActive() => awakingEyeEnable.value;

        [Tooltip("启用")]
        public BoolParameter awakingEyeEnable = new BoolParameter(false, true);

        [Tooltip("眨眼幅度")]
        public ClampedFloatParameter awakingEyeOpenValue = new ClampedFloatParameter(0, 0, 1, true);
        [Tooltip("眼睛长度")]
        public ClampedFloatParameter awakingEyeOpenLength = new ClampedFloatParameter(0, 0, 1, true);



        static class ShaderIDs
        {
            public static readonly int awakingEyeOpenValueID = Shader.PropertyToID("_EyeOpenValue");
            public static readonly int awakingEyeOpenLengthID = Shader.PropertyToID("_EyeOpenLength");
        }


        public override void Render(CommandBuffer cmd, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier target)
        {
            blitMaterial.SetFloat(ShaderIDs.awakingEyeOpenValueID, awakingEyeOpenValue.value);
            blitMaterial.SetFloat(ShaderIDs.awakingEyeOpenLengthID,awakingEyeOpenLength.value);

            cmd.Blit(source, target, blitMaterial);
        }
    }




}