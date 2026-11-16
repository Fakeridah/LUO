using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/ScreenOverlay")]
    public sealed class ScreenOverlay : PostProcessVolumeComponent
    {
        public ScreenOverlayParameter blendMode =new ScreenOverlayParameter(OverlayBlendMode.Overlay);
        public FloatParameter intensity = new FloatParameter(0.0f);
        public TextureParameter texture = new TextureParameter(null);

        private Material Overlay_Material = null;

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => intensity != 0.0f;

        public override void Initialize()
        {
            Overlay_Material = CoreUtils.CreateEngineMaterial("Ridah/BlendModesOverlay");
        }

        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {

            Vector4 UV_Transform = new Vector4(1, 0, 0, 1);

            #if UNITY_WP8
	    	// WP8 has no OS support for rotating screen with device orientation,
	    	// so we do those transformations ourselves.
			if (Screen.orientation == ScreenOrientation.LandscapeLeft) {
				UV_Transform = new Vector4(0, -1, 1, 0);
			}
			if (Screen.orientation == ScreenOrientation.LandscapeRight) {
				UV_Transform = new Vector4(0, 1, -1, 0);
			}
			if (Screen.orientation == ScreenOrientation.PortraitUpsideDown) {
				UV_Transform = new Vector4(-1, 0, 0, -1);
			}
            #endif

            Overlay_Material.SetVector("_UV_Transform", UV_Transform);
            Overlay_Material.SetFloat("_Intensity", intensity.value);
            Overlay_Material.SetTexture("_Overlay", texture.value);
            cb.SetPostProcessSourceTexture(source);
            cb.DrawFullScreenTriangle(Overlay_Material, destination,(int) blendMode.value);
        }

        protected override void CleanUp()
        {

        }
    }

}
namespace UnityEngine.Rendering.Universal
{
    public enum OverlayBlendMode
    {
        Additive = 0,
        ScreenBlend = 1,
        Multiply = 2,
        Overlay = 3,
        AlphaBlend = 4,
    }
    [Serializable]
    public sealed class ScreenOverlayParameter : VolumeParameter<OverlayBlendMode>
    {
        public ScreenOverlayParameter(OverlayBlendMode value, bool overrideState = false) : base(value, overrideState) { }
    }

}