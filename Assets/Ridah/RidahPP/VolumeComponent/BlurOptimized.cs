using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/Blur/BlurOptimized")]
    public sealed class BlurOptimized : PostProcessVolumeComponent
    {
        public ClampedFloatParameter blurSize = new ClampedFloatParameter(0.0f, 0.0f, 20.0f);
        public ClampedIntParameter blurIterations = new ClampedIntParameter(2, 1, 8);
        public ClampedIntParameter downsample = new ClampedIntParameter(1, 0, 3);
        public BlurTypeParameter blurType =new BlurTypeParameter(0);

        private Material blurMaterial = null;


        private int RT_1 = Shader.PropertyToID("RT_1");
        private int RT_2 = Shader.PropertyToID("RT_2");

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => blurSize.value != 0.0f;

        private GraphicsFormat defaultHDRFormat;

        public override void Initialize()
        {
            blurMaterial = CoreUtils.CreateEngineMaterial("Ridah/FastBlur");

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
                    : GraphicsFormat.R8G8B8A8_UNorm;
            }
        }

        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            var pixelRect = cameraData.camera.pixelRect;
            float scale = cameraData.isSceneViewCamera ? 1 : cameraData.renderScale;
            int width = (int)(pixelRect.width * scale);
            int height = (int)(pixelRect.height * scale);

            int rtW = width >> downsample.value;
            int rtH = height >> downsample.value;

            cb.GetTemporaryRT(RT_1, rtW, rtH, 0, FilterMode.Bilinear, defaultHDRFormat);
            cb.GetTemporaryRT(RT_2, rtW, rtH, 0, FilterMode.Bilinear, defaultHDRFormat);

            float widthMod = 1.0f / (1.0f * (1 << downsample.value));

            blurMaterial.SetVector("_Parameter", new Vector4(blurSize.value * widthMod, -blurSize.value * widthMod, 0.0f, 0.0f));

            // downsample
            cb.SetPostProcessSourceTexture(source);
            cb.DrawFullScreenTriangle(blurMaterial, RT_1, 0);
            //cb.Blit(source, RT_1, blurMaterial, 0);

            var passOffs = blurType == BlurType.StandardGauss ? 0 : 2;

            for (int i = 0; i < blurIterations.value; i++)
            {
                float iterationOffs = (i * 1.0f);
                cb.SetGlobalVector("_Parameter", new Vector4(blurSize.value * widthMod + iterationOffs, -blurSize.value * widthMod - iterationOffs, 0.0f, 0.0f));

                // vertical blur
                //cb.Blit(RT_1, RT_2, blurMaterial, 1 + passOffs);
                cb.SetPostProcessSourceTexture(RT_1);
                cb.DrawFullScreenTriangle(blurMaterial, RT_2, 1 + passOffs);

                //RenderTexture.ReleaseTemporary(rt);
                CoreUtils.Swap(ref RT_1, ref RT_2);

                // horizontal blur
                //cb.Blit(RT_1, RT_2, blurMaterial, 2 + passOffs);
                cb.SetPostProcessSourceTexture(RT_1);
                cb.DrawFullScreenTriangle(blurMaterial, RT_2, 2 + passOffs);
                //RenderTexture.ReleaseTemporary(rt);
                CoreUtils.Swap(ref RT_1, ref RT_2);
            }

            //cb.Blit(RT_1, destination);
            cb.SetPostProcessSourceTexture(RT_1);
            cb.DrawFullScreenTriangle(blurMaterial, destination);

            cb.ReleaseTemporaryRT(RT_1);
            cb.ReleaseTemporaryRT(RT_2);
        }

        protected override void CleanUp()
        {

        }
    }

}
namespace UnityEngine.Rendering.Universal
{
    public enum BlurType
    {
        StandardGauss = 0,
        SgxGauss = 1,
    }
    [Serializable]
    public sealed class BlurTypeParameter : VolumeParameter<BlurType>
    {
        public BlurTypeParameter(BlurType value, bool overrideState = false) : base(value, overrideState) { }
    }
}