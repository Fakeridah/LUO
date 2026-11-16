using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/Tone/GrayScale")]
    public sealed class GrayScale : PostProcessVolumeComponent
    {
        public BoolParameter visible = new BoolParameter(false);
        public TextureParameter textureRamp = new TextureParameter(null);
        public ClampedFloatParameter rampOffset = new ClampedFloatParameter(0f, -1.0f, 1.0f);

        private Material GrayScale_Material = null;

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => visible.value;
        public override bool IsActive() => visible != false;
        
        public override void Initialize()
        {
            GrayScale_Material = CoreUtils.CreateEngineMaterial("Ridah/GrayScale");
        }

        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            GrayScale_Material.SetTexture("_RampTex", textureRamp.value);
            GrayScale_Material.SetFloat("_RampOffset", rampOffset.value);
            cb.SetPostProcessSourceTexture(source);
            cb.DrawFullScreenTriangle(GrayScale_Material, destination);
        }

        protected override void CleanUp()
        {

        }
    }

}