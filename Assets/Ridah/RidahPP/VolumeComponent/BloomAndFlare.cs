using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Ridah.URPPostProcessing
{

    [Serializable, VolumeComponentMenu("Ridah PostProcessing/BloomAndFlare")]
    public sealed class BloomAndFlare : PostProcessVolumeComponent
    {
        public TweakModeParameter tweakMode = new TweakModeParameter(0);
        public BloomScreenBlendModeParameter screenBlendMode = new BloomScreenBlendModeParameter(0);

        public HDRBloomModeParameter hdr = new HDRBloomModeParameter(0);
        private bool doHdr = true;
        public FloatParameter sepBlurSpread = new FloatParameter(1.5f);
        public FloatParameter useSrcAlphaAsMask = new FloatParameter(0.5f);

        public FloatParameter bloomIntensity = new FloatParameter(0.0f);
        public MinFloatParameter bloomThreshold = new MinFloatParameter(0.5f,0);
        public ClampedIntParameter bloomBlurIterations = new ClampedIntParameter(2,1,8);

        public BoolParameter lensflares = new BoolParameter(false);
        public IntParameter hollywoodFlareBlurIterations = new IntParameter(2);
        public LensflareStyleParameter lensflareMode = new LensflareStyleParameter(0);
        public FloatParameter hollyStretchWidth = new FloatParameter(3.5f);
        public FloatParameter lensflareIntensity = new FloatParameter(1.0f);
        public FloatParameter lensflareThreshold = new FloatParameter(0.3f);
        public ColorParameter flareColorA = new ColorParameter(new Color(0.4f, 0.4f, 0.8f, 0.75f));
        public ColorParameter flareColorB = new ColorParameter(new Color(0.4f, 0.8f, 0.8f, 0.75f));
        public ColorParameter flareColorC = new ColorParameter(new Color(0.8f, 0.4f, 0.8f, 0.75f));
        public ColorParameter flareColorD = new ColorParameter(new Color(0.8f, 0.4f, 0.0f, 0.75f));
        public Texture2DParameter lensFlareVignetteMask = new Texture2DParameter(null);

        private GraphicsFormat defaultHDRFormat;

        private int RT_1 = Shader.PropertyToID("RT_1");
        private int RT_2 = Shader.PropertyToID("RT_2");
        private int RT_3 = Shader.PropertyToID("RT_3");
        private int RT_4 = Shader.PropertyToID("RT_4");
        private int _ColorBufferID = Shader.PropertyToID("_ColorBuffer");
        private int _SbwyBufferID = Shader.PropertyToID("_SbwyBuffer");
        private int offset = Shader.PropertyToID("offsets");

        private Material lensFlareMaterial;
        private Material vignetteMaterial;
        private Material separableBlurMaterial;
        private Material addBrightStuffBlendOneOneMaterial;
        private Material screenBlend;
        private Material hollywoodFlaresMaterial;
        private Material brightPassFilterMaterial;

        //Define this Post-Processing will be executed in which timing in URP pipeline.
        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => bloomIntensity.value != 0.0f;

        //Cache parameter ids to avoid do the same thing every update.
        static class IDs
        {
        }

        //Create shader material.
        //You should put shader in the Resources folder, ensure it will be included in Asset Bundle, or add it use another way by yourself.
        public override void Initialize()
        {
            lensFlareMaterial = CoreUtils.CreateEngineMaterial("Ridah/Post-Processing/LensFlareCreate");
            vignetteMaterial = CoreUtils.CreateEngineMaterial("Ridah/Post-Processing/VignetteShader");
            separableBlurMaterial = CoreUtils.CreateEngineMaterial("Ridah/Post-Processing/SeparableBlurPlus");
            addBrightStuffBlendOneOneMaterial = CoreUtils.CreateEngineMaterial("Ridah/Post-Processing/BlendOneOne");
            screenBlend = CoreUtils.CreateEngineMaterial("Ridah/Post-Processing/Blend");
            hollywoodFlaresMaterial = CoreUtils.CreateEngineMaterial("Ridah/Post-Processing/MultipassHollywoodFlares");
            brightPassFilterMaterial = CoreUtils.CreateEngineMaterial("Ridah/Post-Processing/BrightPassFilterForBloom");

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

            BloomScreenBlendMode realBlendMode = screenBlendMode.value;
            realBlendMode = BloomScreenBlendMode.Add;

            var pixelRect = cameraData.camera.pixelRect;
            float scale = cameraData.isSceneViewCamera ? 1 : cameraData.renderScale;
            int width = (int)(pixelRect.width * scale);
            int height = (int)(pixelRect.height * scale);

            cb.GetTemporaryRT(RT_1, width / 2, height / 2, 0, FilterMode.Bilinear, defaultHDRFormat);
            cb.GetTemporaryRT(RT_2, width / 4, height / 4, 0, FilterMode.Bilinear, defaultHDRFormat);
            cb.GetTemporaryRT(RT_3, width / 4, height / 4, 0, FilterMode.Bilinear, defaultHDRFormat);
            cb.GetTemporaryRT(RT_4, width / 4, height / 4, 0, FilterMode.Bilinear, defaultHDRFormat);
            var halfRezColor = RT_1;
            var quarterRezColor = RT_2;
            var secondQuarterRezColor = RT_3;
            var thirdQuarterRezColor = RT_4;


            float widthOverHeight = width / height;
            float oneOverBaseSize = 1.0f / 512.0f;

            // downsample

            cb.Blit(source, halfRezColor, screenBlend, 2); // <- 2 is stable downsample
            cb.Blit(halfRezColor, quarterRezColor, screenBlend, 2); // <- 2 is stable downsample

            cb.ReleaseTemporaryRT(halfRezColor);

            // cut colors (thresholding)

            BrightFilter(cb, bloomThreshold.value, useSrcAlphaAsMask.value, quarterRezColor, secondQuarterRezColor);
            cb.ReleaseTemporaryRT(quarterRezColor);

            // blurring
            for (int iter = 0; iter < bloomBlurIterations.value; iter++)
            {
                float spreadForPass = (1.0f + (iter * 0.5f)) * sepBlurSpread.value;
                cb.SetGlobalVector(offset, new Vector4(0.0f, spreadForPass * oneOverBaseSize, 0.0f, 0.0f));
                cb.Blit(secondQuarterRezColor, thirdQuarterRezColor, separableBlurMaterial);
                CoreUtils.Swap(ref secondQuarterRezColor, ref thirdQuarterRezColor);
                
                cb.SetGlobalVector(offset, new Vector4((spreadForPass / widthOverHeight) * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
                cb.Blit(secondQuarterRezColor, thirdQuarterRezColor, separableBlurMaterial);
                CoreUtils.Swap(ref secondQuarterRezColor, ref thirdQuarterRezColor);
            }
            // lens flares: ghosting, anamorphic or a combination

            /*if (lensflares.value)
            {

                if (lensflareMode == 0)
                {

                    BrightFilter(cb, lensflareThreshold.value, 0.0f, quarterRezColor, thirdQuarterRezColor);
                    cb.ReleaseTemporaryRT(quarterRezColor);

                    Vignette(cb, 0.975f, thirdQuarterRezColor, secondQuarterRezColor);
                    cb.ReleaseTemporaryRT(thirdQuarterRezColor);

                    BlendFlares(cb, secondQuarterRezColor, quarterRezColor);
                    cb.ReleaseTemporaryRT(secondQuarterRezColor);
                }

                // (b) hollywood/anamorphic flares?

                else
                {

                    // thirdQuarter has the brightcut unblurred colors
                    // quarterRezColor is the blurred, brightcut buffer that will end up as bloom

                    hollywoodFlaresMaterial.SetVector("_threshold", new Vector4(lensflareThreshold.value, 1.0f / (1.0f - lensflareThreshold.value), 0.0f, 0.0f));
                    hollywoodFlaresMaterial.SetVector("tintColor", new Vector4(flareColorA.value.r, flareColorA.value.g, flareColorA.value.b, flareColorA.value.a) * flareColorA.value.a * lensflareIntensity.value);
                    cb.Blit(thirdQuarterRezColor, secondQuarterRezColor, hollywoodFlaresMaterial, 2);
                    cb.ReleaseTemporaryRT(thirdQuarterRezColor);

                    cb.Blit(secondQuarterRezColor, thirdQuarterRezColor, hollywoodFlaresMaterial, 3);
                    cb.ReleaseTemporaryRT(secondQuarterRezColor);

                    hollywoodFlaresMaterial.SetVector("offsets", new Vector4((sepBlurSpread.value * 1.0f / widthOverHeight) * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
                    hollywoodFlaresMaterial.SetFloat("stretchWidth", hollyStretchWidth.value);
                    cb.Blit(thirdQuarterRezColor, secondQuarterRezColor, hollywoodFlaresMaterial, 1);
                    cb.ReleaseTemporaryRT(thirdQuarterRezColor);

                    hollywoodFlaresMaterial.SetFloat("stretchWidth", hollyStretchWidth.value * 2.0f);
                    cb.Blit(secondQuarterRezColor, thirdQuarterRezColor, hollywoodFlaresMaterial, 1);
                    cb.ReleaseTemporaryRT(secondQuarterRezColor);

                    hollywoodFlaresMaterial.SetFloat("stretchWidth", hollyStretchWidth.value * 4.0f);
                    cb.Blit(thirdQuarterRezColor, secondQuarterRezColor, hollywoodFlaresMaterial, 1);
                    cb.ReleaseTemporaryRT(thirdQuarterRezColor);

                    if (lensflareMode == (LensflareStyle34)1)
                    {
                        for (int itera = 0; itera < hollywoodFlareBlurIterations.value; itera++)
                        {
                            separableBlurMaterial.SetVector("offsets", new Vector4((hollyStretchWidth.value * 2.0f / widthOverHeight) * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
                            cb.Blit(secondQuarterRezColor, thirdQuarterRezColor, separableBlurMaterial);
                            cb.ReleaseTemporaryRT(secondQuarterRezColor);

                            separableBlurMaterial.SetVector("offsets", new Vector4((hollyStretchWidth.value * 2.0f / widthOverHeight) * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
                            cb.Blit(thirdQuarterRezColor, secondQuarterRezColor, separableBlurMaterial);
                            cb.ReleaseTemporaryRT(thirdQuarterRezColor);
                        }

                        AddTo(cb, 1.0f, secondQuarterRezColor, quarterRezColor);
                        cb.ReleaseTemporaryRT(secondQuarterRezColor);
                    }
                    else
                    {

                        // (c) combined

                        for (int ix = 0; ix < hollywoodFlareBlurIterations.value; ix++)
                        {
                            separableBlurMaterial.SetVector("offsets", new Vector4((hollyStretchWidth.value * 2.0f / widthOverHeight) * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
                            cb.Blit(secondQuarterRezColor, thirdQuarterRezColor, separableBlurMaterial);
                            cb.ReleaseTemporaryRT(secondQuarterRezColor);

                            separableBlurMaterial.SetVector("offsets", new Vector4((hollyStretchWidth.value * 2.0f / widthOverHeight) * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
                            cb.Blit(thirdQuarterRezColor, secondQuarterRezColor, separableBlurMaterial);
                            cb.ReleaseTemporaryRT(thirdQuarterRezColor);
                        }

                        Vignette(cb, 1.0f, secondQuarterRezColor, thirdQuarterRezColor);
                        cb.ReleaseTemporaryRT(secondQuarterRezColor);

                        BlendFlares(cb, thirdQuarterRezColor, secondQuarterRezColor);
                        cb.ReleaseTemporaryRT(thirdQuarterRezColor);

                        AddTo(cb, 1.0f, secondQuarterRezColor, quarterRezColor);
                        cb.ReleaseTemporaryRT(secondQuarterRezColor);
                    }
                }
            }*/

            // screen blend bloom results to color buffer
            screenBlend.SetFloat("_Intensity", bloomIntensity.value);
            cb.SetGlobalTexture(_ColorBufferID, source);
            cb.GetTemporaryRT(_SbwyBufferID, width/4, height/4, 0, FilterMode.Bilinear, defaultHDRFormat);
            cb.Blit(thirdQuarterRezColor, _SbwyBufferID);
            cb.Blit(_SbwyBufferID, destination, screenBlend, (int)realBlendMode);
            cb.ReleaseTemporaryRT(quarterRezColor);
            cb.ReleaseTemporaryRT(secondQuarterRezColor);
            cb.ReleaseTemporaryRT(thirdQuarterRezColor);
        }

        //Do something before Destroy(), if you need.
        protected override void CleanUp()
        {
        }
        private void AddTo(CommandBuffer cb, float intensity_, int from, int to)
        {
            addBrightStuffBlendOneOneMaterial.SetFloat("_Intensity", intensity_);
            cb.Blit(from, to, addBrightStuffBlendOneOneMaterial);
        }

        private void BlendFlares(CommandBuffer cb, int from, int to)
        {
            lensFlareMaterial.SetVector("colorA", new Vector4(flareColorA.value.r, flareColorA.value.g, flareColorA.value.b, flareColorA.value.a) * lensflareIntensity.value);
            lensFlareMaterial.SetVector("colorB", new Vector4(flareColorB.value.r, flareColorB.value.g, flareColorB.value.b, flareColorB.value.a) * lensflareIntensity.value);
            lensFlareMaterial.SetVector("colorC", new Vector4(flareColorC.value.r, flareColorC.value.g, flareColorC.value.b, flareColorC.value.a) * lensflareIntensity.value);
            lensFlareMaterial.SetVector("colorD", new Vector4(flareColorD.value.r, flareColorD.value.g, flareColorD.value.b, flareColorD.value.a) * lensflareIntensity.value);
            cb.Blit(from, to, lensFlareMaterial);
        }

        private void BrightFilter(CommandBuffer cb, float thresh, float useAlphaAsMask, int from, int to)
        {
            if (doHdr)
                brightPassFilterMaterial.SetVector("threshold", new Vector4(thresh, 1.0f, 0.0f, 0.0f));
            else
                brightPassFilterMaterial.SetVector("threshold", new Vector4(thresh, 1.0f / (1.0f - thresh), 0.0f, 0.0f));
            brightPassFilterMaterial.SetFloat("useSrcAlphaAsMask", useAlphaAsMask);
            cb.Blit(from, to, brightPassFilterMaterial);
        }

        private void Vignette(CommandBuffer cb, float amount, int from, int to)
        {
            if (lensFlareVignetteMask.value)
            {
                screenBlend.SetTexture("_ColorBuffer", lensFlareVignetteMask.value);
                cb.Blit(from, to, screenBlend, 3);
            }
            else
            {
                vignetteMaterial.SetFloat("vignetteIntensity", amount);
                cb.Blit(from, to, vignetteMaterial);
            }
        }
    }

}
namespace UnityEngine.Rendering.Universal
{
    public enum HDRBloomMode
    {
        Auto = 0,
        On = 1,
        Off = 2,
    }
    public enum LensflareStyle34
    {
        Ghosting = 0,
        Anamorphic = 1,
        Combined = 2,
    }

    public enum TweakMode34
    {
        Basic = 0,
        Complex = 1,
    }

    public enum BloomScreenBlendMode
    {
        Screen = 0,
        Add = 1,
    }
    

    [Serializable]
    public sealed class HDRBloomModeParameter : VolumeParameter<HDRBloomMode>
    {
        public HDRBloomModeParameter(HDRBloomMode value, bool overrideState = false) : base(value, overrideState) { }
    }
    [Serializable]
    public sealed class LensflareStyleParameter : VolumeParameter<LensflareStyle34>
    {
        public LensflareStyleParameter(LensflareStyle34 value, bool overrideState = false) : base(value, overrideState) { }
    }
    [Serializable]
    public sealed class TweakModeParameter : VolumeParameter<TweakMode34>
    {
        public TweakModeParameter(TweakMode34 value, bool overrideState = false) : base(value, overrideState) { }
    }
    [Serializable]
    public sealed class BloomScreenBlendModeParameter : VolumeParameter<BloomScreenBlendMode>
    {
        public BloomScreenBlendModeParameter(BloomScreenBlendMode value, bool overrideState = false) : base(value, overrideState) { }
    }
    
}