using Ridah.URPPostProcessing;
using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/DepthOfFieldDeprecated")]
    public sealed class DepthOfFieldDeprecated : PostProcessVolumeComponent
    {
        public Dof34QualityParameter quality = new Dof34QualityParameter(Dof34QualitySetting.OnlyBackground);
        public DofResolutionParameter resolution = new DofResolutionParameter(DofResolution.Low);
        public BoolParameter simpleTweakMode = new BoolParameter(true);
        public FloatParameter focalPoint = new FloatParameter(1.0f);
        public MinFloatParameter smoothness = new MinFloatParameter(0.5f,0.0f);
        public FloatParameter focalZDistance = new FloatParameter(0.0f);
        public FloatParameter focalZStartCurve = new FloatParameter(1.0f);
        public FloatParameter focalZEndCurve = new FloatParameter(1.0f);
        public TransformParameter objectFocus = new TransformParameter(null);
        public FloatParameter focalSize = new FloatParameter(0.0f);
        public DofBlurrinessParameter bluriness = new DofBlurrinessParameter(DofBlurriness.High);
        public MinFloatParameter maxBlurSpread = new MinFloatParameter(0.0f, 0.0f);
        public FloatParameter foregroundBlurExtrude = new FloatParameter(1.15f);
        public BoolParameter visualize = new BoolParameter(false);
        public BokehDestinationParameter bokehDestination = new BokehDestinationParameter(BokehDestination.Background);
        public BoolParameter bokeh = new BoolParameter(false);
        public BoolParameter bokehSupport = new BoolParameter(true);
        public Texture2DParameter bokehTexture = new Texture2DParameter(null);
        public FloatParameter bokehScale = new FloatParameter(2.4f);
        public FloatParameter bokehIntensity = new FloatParameter(0.15f);
        public FloatParameter bokehThresholdContrast = new FloatParameter(0.1f);
        public FloatParameter bokehThresholdLuminance = new FloatParameter(0.55f);
        public IntParameter bokehDownsample = new IntParameter(1);

        static private int SMOOTH_DOWNSAMPLE_PASS = 6;
        static private float BOKEH_EXTRA_BLUR = 2.0f;
        static private int RT_0 = Shader.PropertyToID("RT_0");

        private float focalStartCurve = 2.0f;
        private float focalEndCurve = 2.0f;
        private float focalDistance01 = 0.1f;
        private float widthOverHeight = 1.25f;
        private float oneOverBaseSize = 1.0f / 512.0f;

        private Material dofBlurMaterial = null;
        private Material dofMaterial = null;
        private Material bokehMaterial;
        private GraphicsFormat defaultHDRFormat;

        private Camera _camera;

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => maxBlurSpread != 0.0f;

        public override void Initialize()
        {
            dofBlurMaterial = CoreUtils.CreateEngineMaterial("Ridah/SeparableWeightedBlurDof34");
            dofMaterial = CoreUtils.CreateEngineMaterial("Ridah/DepthOfField34");
            bokehMaterial = CoreUtils.CreateEngineMaterial("Ridah/Bokeh34");

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
        float FocalDistance01(float worldDist, Camera camera)
        {
            return camera.WorldToViewportPoint((worldDist - camera.nearClipPlane) * camera.transform.forward + camera.transform.position).z / (camera.farClipPlane - camera.nearClipPlane);
        }

        int GetDividerBasedOnQuality()
        {
            int divider = 1;
            if (resolution == DofResolution.Medium)
                divider = 2;
            else if (resolution == DofResolution.Low)
                divider = 2;
            return divider;
        }

        int GetLowResolutionDividerBasedOnQuality(int baseDivider)
        {
            int lowTexDivider = baseDivider;
            if (resolution == DofResolution.High)
                lowTexDivider *= 2;
            if (resolution == DofResolution.Low)
                lowTexDivider *= 2;
            return lowTexDivider;
        }
        private RenderTexture sb = null;
        private RenderTexture foregroundTexture = null;
        private RenderTexture mediumRezWorkTexture = null;
        private RenderTexture finalDefocus = null;
        private RenderTexture lowRezWorkTexture = null;
        private RenderTexture bokehSource = null;
        private RenderTexture bokehSource2 = null;
        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            _camera = cameraData.camera;
            var pixelRect = _camera.pixelRect;
            float scale = cameraData.isSceneViewCamera ? 1 : cameraData.renderScale;
            int width = (int)(pixelRect.width * scale);
            int height = (int)(pixelRect.height * scale);
            cb.GetTemporaryRT(RT_0, width, height, 0, FilterMode.Bilinear, defaultHDRFormat);

            if (smoothness.value < 0.1f)
                smoothness.value = 0.1f;

            float bokehBlurAmplifier = bokeh.value ? BOKEH_EXTRA_BLUR : 1.0f;

            bool blurForeground = quality.value > Dof34QualitySetting.OnlyBackground;
            float focal01Size = focalSize.value / (_camera.farClipPlane - _camera.nearClipPlane); ;

            if (simpleTweakMode.value)
            {
                focalDistance01 = objectFocus.value ? (_camera.WorldToViewportPoint(objectFocus.value.transform.position)).z / (_camera.farClipPlane) : FocalDistance01(focalPoint.value, _camera);
                focalStartCurve = focalDistance01 * smoothness.value;
                focalEndCurve = focalStartCurve;
                blurForeground = blurForeground && (focalPoint.value > (_camera.nearClipPlane + Mathf.Epsilon));
            }
            else
            {
                if (objectFocus.value)
                {
                    var vpPoint = _camera.WorldToViewportPoint(objectFocus.value.transform.position);
                    vpPoint.z = (vpPoint.z) / (_camera.farClipPlane);
                    focalDistance01 = vpPoint.z;
                }
                else
                    focalDistance01 = FocalDistance01(focalZDistance.value, _camera);//viewPos Z from 0~1

                focalStartCurve = focalZStartCurve.value;
                focalEndCurve = focalZEndCurve.value;
                blurForeground = blurForeground && (focalPoint.value > (_camera.nearClipPlane + Mathf.Epsilon));
            }

            widthOverHeight = (1.0f * width) / (1.0f * height);
            oneOverBaseSize = 1.0f / 512.0f;

            dofMaterial.SetFloat("_ForegroundBlurExtrude", foregroundBlurExtrude.value);
            dofMaterial.SetVector("_CurveParams", new Vector4(simpleTweakMode.value ? 1.0f / focalStartCurve : focalStartCurve, simpleTweakMode.value ? 1.0f / focalEndCurve : focalEndCurve, focal01Size * 0.5f, focalDistance01));
            dofMaterial.SetVector("_InvRenderTargetSize", new Vector4(1.0f / (1.0f * width), 1.0f / (1.0f * height), 0.0f, 0.0f));

            int divider = GetDividerBasedOnQuality();
            int lowTexDivider = GetLowResolutionDividerBasedOnQuality(divider);

            AllocateTextures(blurForeground, source, divider, lowTexDivider, width, height);
            sb = RenderTexture.GetTemporary(width , height , 0, RenderTextureFormat.ARGBHalf);//Alpha needed������
            // WRITE COC to alpha channel
            // source is only being bound to detect y texcoord flip
            cb.Blit(source, sb);
            cb.Blit(sb, sb, dofMaterial, 3);

            // better DOWNSAMPLE (could actually be weighted for higher quality)
            Downsample(cb, sb, mediumRezWorkTexture, width, height);
            //cb.ReleaseTemporaryRT(RT_0);

            // BLUR A LITTLE first, which has two purposes
            // 1.) reduce jitter, noise, aliasing
            // 2.) produce the little-blur buffer used in composition later
            Blur(cb, mediumRezWorkTexture, mediumRezWorkTexture, DofBlurriness.Low, 4, maxBlurSpread.value, width, height, divider);

            if ((bokeh.value) && ((BokehDestination.Foreground & bokehDestination.value) != 0))
            {
                dofMaterial.SetVector("_Threshhold", new Vector4(bokehThresholdContrast.value, bokehThresholdLuminance.value, 0.95f, 0.0f));

                // add and mark the parts that should end up as bokeh shapes
                cb.Blit(mediumRezWorkTexture, bokehSource2, dofMaterial, 11);

                // remove those parts (maybe even a little tittle bittle more) from the regurlarly blurred buffer
                //Graphics.Blit (mediumRezWorkTexture, lowRezWorkTexture, dofMaterial, 10);
                cb.Blit(mediumRezWorkTexture, lowRezWorkTexture);//, dofMaterial, 10);

                // maybe you want to reblur the small blur ... but not really needed.
                //Blur (mediumRezWorkTexture, mediumRezWorkTexture, DofBlurriness.Low, 4, maxBlurSpread);

                // bigger BLUR
                Blur(cb, lowRezWorkTexture, lowRezWorkTexture, bluriness.value, 0, maxBlurSpread.value * bokehBlurAmplifier, width, height, lowTexDivider);
            }
            else
            {
                // bigger BLUR
                Downsample(cb, mediumRezWorkTexture, lowRezWorkTexture, width, height);
                Blur(cb, lowRezWorkTexture, lowRezWorkTexture, bluriness.value, 0, maxBlurSpread.value, width, height, lowTexDivider);
            }

            dofBlurMaterial.SetTexture("_TapLow", lowRezWorkTexture);
            dofBlurMaterial.SetTexture("_TapMedium", mediumRezWorkTexture);
            cb.Blit(null, finalDefocus, dofBlurMaterial, 3);

            // we are only adding bokeh now if the background is the only part we have to deal with
            if ((bokeh.value) && ((BokehDestination.Foreground & bokehDestination.value) != 0))
                AddBokeh(cb, bokehSource2, bokehSource, finalDefocus, width, height,lowTexDivider);

            dofMaterial.SetTexture("_TapLowBackground", finalDefocus);
            dofMaterial.SetTexture("_TapMedium", mediumRezWorkTexture); // needed for debugging/visualization

            // FINAL DEFOCUS (background)
            cb.Blit(sb, blurForeground ? foregroundTexture : destination, dofMaterial, visualize.value ? 2 : 0);

            // FINAL DEFOCUS (foreground)
            if (blurForeground)
            {
                // WRITE COC to alpha channel
                cb.Blit(foregroundTexture, sb, dofMaterial, 5);

                // DOWNSAMPLE (unweighted)
                Downsample(cb, sb, mediumRezWorkTexture, width, height);

                // BLUR A LITTLE first, which has two purposes
                // 1.) reduce jitter, noise, aliasing
                // 2.) produce the little-blur buffer used in composition later
                BlurFg(cb, mediumRezWorkTexture, mediumRezWorkTexture, DofBlurriness.Low, 2, maxBlurSpread.value, width, height,divider);

                if ((bokeh.value) && ((BokehDestination.Foreground & bokehDestination.value) != 0))
                {
                    dofMaterial.SetVector("_Threshhold", new Vector4(bokehThresholdContrast.value * 0.5f, bokehThresholdLuminance.value, 0.0f, 0.0f));

                    // add and mark the parts that should end up as bokeh shapes
                    cb.Blit(mediumRezWorkTexture, bokehSource2, dofMaterial, 11);

                    // remove the parts (maybe even a little tittle bittle more) that will end up in bokeh space
                    //Graphics.Blit (mediumRezWorkTexture, lowRezWorkTexture, dofMaterial, 10);
                    cb.Blit(mediumRezWorkTexture, lowRezWorkTexture);//, dofMaterial, 10);

                    // big BLUR
                    BlurFg(cb, lowRezWorkTexture, lowRezWorkTexture, bluriness.value, 1, maxBlurSpread.value * bokehBlurAmplifier, width, height, lowTexDivider);
                }
                else
                {
                    // big BLUR
                    BlurFg(cb, mediumRezWorkTexture, lowRezWorkTexture, bluriness.value, 1, maxBlurSpread.value, width, height, lowTexDivider);
                }

                // simple upsample once
                cb.Blit(lowRezWorkTexture, finalDefocus);

                dofMaterial.SetTexture("_TapLowForeground", finalDefocus);
                cb.Blit(sb, destination, dofMaterial, visualize.value ? 1 : 4);

                if ((bokeh.value) && ((BokehDestination.Foreground & bokehDestination.value) != 0))
                    AddBokeh(cb, bokehSource2, bokehSource, destination, width, height, lowTexDivider);
            }
            RenderTexture.ReleaseTemporary(sb);

            ReleaseTextures();
        }
        void Blur(CommandBuffer cb, RenderTargetIdentifier from, RenderTargetIdentifier to, DofBlurriness iterations, int blurPass, float spread, int width, int height,int divider)
        {
            RenderTexture tmp = RenderTexture.GetTemporary(width/ divider, height/ divider);
            if ((int)iterations > 1)
            {
                BlurHex(cb, from, to, blurPass, spread, tmp);
                if ((int)iterations > 2)
                {
                    cb.SetGlobalVector("SWB_offsets", new Vector4(0.0f, spread * oneOverBaseSize, 0.0f, 0.0f));
                    cb.Blit(to, tmp, dofBlurMaterial, blurPass);
                    cb.SetGlobalVector("SWB_offsets", new Vector4(spread / widthOverHeight * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
                    cb.Blit(tmp, to, dofBlurMaterial, blurPass);
                }
            }
            else
            {
                cb.SetGlobalVector("SWB_offsets", new Vector4(0.0f, spread * oneOverBaseSize, 0.0f, 0.0f));
                cb.Blit(from, tmp, dofBlurMaterial, blurPass);
                cb.SetGlobalVector("SWB_offsets", new Vector4(spread / widthOverHeight * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
                cb.Blit(tmp, to, dofBlurMaterial, blurPass);
            }
            RenderTexture.ReleaseTemporary(tmp);
        }

        void BlurFg(CommandBuffer cb, RenderTargetIdentifier from, RenderTargetIdentifier to, DofBlurriness iterations, int blurPass, float spread, int width, int height, int divider)
        {
            // we want a nice, big coc, hence we need to tap once from this (higher resolution) texture
            cb.SetGlobalTexture("_TapHigh", from);

            RenderTexture tmp = RenderTexture.GetTemporary(width/ divider, height/ divider);
            if ((int)iterations > 1)
            {
                BlurHex(cb, from, to, blurPass, spread, tmp);
                if ((int)iterations > 2)
                {
                    cb.SetGlobalVector("SWB_offsets", new Vector4(0.0f, spread * oneOverBaseSize, 0.0f, 0.0f));
                    cb.Blit(to, tmp, dofBlurMaterial, blurPass);
                    cb.SetGlobalVector("SWB_offsets", new Vector4(spread / widthOverHeight * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
                    cb.Blit(tmp, to, dofBlurMaterial, blurPass);
                }
            }
            else
            {
                cb.SetGlobalVector("SWB_offsets", new Vector4(0.0f, spread * oneOverBaseSize, 0.0f, 0.0f));
                cb.Blit(from, tmp, dofBlurMaterial, blurPass);
                cb.SetGlobalVector("SWB_offsets", new Vector4(spread / widthOverHeight * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
                cb.Blit(tmp, to, dofBlurMaterial, blurPass);
            }
            RenderTexture.ReleaseTemporary(tmp);
        }

        void BlurHex(CommandBuffer cb, RenderTargetIdentifier from, RenderTargetIdentifier to, int blurPass, float spread, RenderTexture tmp)
        {
            cb.SetGlobalVector("SWB_offsets", new Vector4(0.0f, spread * oneOverBaseSize, 0.0f, 0.0f));
            cb.Blit(from, tmp, dofBlurMaterial, blurPass);
            cb.SetGlobalVector("SWB_offsets", new Vector4(spread / widthOverHeight * oneOverBaseSize, 0.0f, 0.0f, 0.0f));
            cb.Blit(tmp, to, dofBlurMaterial, blurPass);
            cb.SetGlobalVector("SWB_offsets", new Vector4(spread / widthOverHeight * oneOverBaseSize, spread * oneOverBaseSize, 0.0f, 0.0f));
            cb.Blit(to, tmp, dofBlurMaterial, blurPass);
            cb.SetGlobalVector("SWB_offsets", new Vector4(spread / widthOverHeight * oneOverBaseSize, -spread * oneOverBaseSize, 0.0f, 0.0f));
            cb.Blit(tmp, to, dofBlurMaterial, blurPass);
        }

        void Downsample(CommandBuffer cb, RenderTargetIdentifier from, RenderTargetIdentifier to, int width, int height)
        {
            dofMaterial.SetVector("_InvRenderTargetSize", new Vector4(1.0f / (1.0f * width), 1.0f / (1.0f * height), 0.0f, 0.0f));
            cb.Blit(from, to, dofMaterial, SMOOTH_DOWNSAMPLE_PASS);
        }

        void AddBokeh(CommandBuffer cb, RenderTexture bokehInfo, RenderTargetIdentifier tempTex, RenderTargetIdentifier finalTarget, int width, int height, int lowTexDivider)
        {
            if (bokehMaterial)
            {
                var meshes = Quads.GetMeshes(width/ (lowTexDivider * bokehDownsample.value), height/ (lowTexDivider * bokehDownsample.value));	// quads: exchanging more triangles with less overdraw

                //RenderTexture.active = tempTex;
                //GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));
                cb.SetRenderTarget(tempTex);
                cb.ClearRenderTarget(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));
                GL.PushMatrix();
                GL.LoadIdentity();

                // point filter mode is important, otherwise we get bokeh shape & size artefacts
                //bokehInfo.filterMode = FilterMode.Point;
                float arW = (width * 1.0f) / (height * 1.0f);
                float sc = 2.0f / (1.0f * width);
                sc += bokehScale.value * maxBlurSpread.value * BOKEH_EXTRA_BLUR * oneOverBaseSize;

                bokehMaterial.SetTexture("_Source", bokehInfo);
                bokehMaterial.SetTexture("_MainTex", bokehTexture.value);
                bokehMaterial.SetVector("_ArScale", new Vector4(sc, sc * arW, 0.5f, 0.5f * arW));
                bokehMaterial.SetFloat("_Intensity", bokehIntensity.value);
                bokehMaterial.SetPass(0);

                foreach (Mesh m in meshes)
                    if (m) cb.DrawMesh(m, Matrix4x4.identity, bokehMaterial);

                GL.PopMatrix();

                cb.Blit(tempTex, finalTarget, dofMaterial, 8);

                // important to set back as we sample from this later on
                //bokehInfo.filterMode = FilterMode.Bilinear;
            }
        }


        void ReleaseTextures()
        {
            if (foregroundTexture) RenderTexture.ReleaseTemporary(foregroundTexture);
            if (finalDefocus) RenderTexture.ReleaseTemporary(finalDefocus);
            if (mediumRezWorkTexture) RenderTexture.ReleaseTemporary(mediumRezWorkTexture);
            if (lowRezWorkTexture) RenderTexture.ReleaseTemporary(lowRezWorkTexture);
            if (bokehSource) RenderTexture.ReleaseTemporary(bokehSource);
            if (bokehSource2) RenderTexture.ReleaseTemporary(bokehSource2);
        }

        void AllocateTextures(bool blurForeground, RenderTargetIdentifier source, int divider, int lowTexDivider, int width, int height)
        {
            foregroundTexture = null;
            if (blurForeground)
                foregroundTexture = RenderTexture.GetTemporary(width, height, 0);
            mediumRezWorkTexture = RenderTexture.GetTemporary(width / divider, height / divider, 0);
            finalDefocus = RenderTexture.GetTemporary(width / divider, height / divider, 0);
            lowRezWorkTexture = RenderTexture.GetTemporary(width / lowTexDivider, height / lowTexDivider, 0);
            bokehSource = null;
            bokehSource2 = null;
            if (bokeh.value)
            {
                bokehSource = RenderTexture.GetTemporary(width / (lowTexDivider * bokehDownsample.value), height / (lowTexDivider * bokehDownsample.value), 0, RenderTextureFormat.ARGBHalf);
                bokehSource2 = RenderTexture.GetTemporary(width / (lowTexDivider * bokehDownsample.value), height / (lowTexDivider * bokehDownsample.value), 0, RenderTextureFormat.ARGBHalf);
                bokehSource.filterMode = FilterMode.Bilinear;
                bokehSource2.filterMode = FilterMode.Bilinear;
                RenderTexture.active = bokehSource2;
                GL.Clear(false, true, new Color(0.0f, 0.0f, 0.0f, 0.0f));
            }

            // to make sure: always use bilinear filter setting

            //source.filterMode = FilterMode.Bilinear;
            finalDefocus.filterMode = FilterMode.Bilinear;
            mediumRezWorkTexture.filterMode = FilterMode.Bilinear;
            lowRezWorkTexture.filterMode = FilterMode.Bilinear;
            if (foregroundTexture)
                foregroundTexture.filterMode = FilterMode.Bilinear;
        }
        protected override void CleanUp()
        {
            Quads.Cleanup();
        }
    }
}
namespace UnityEngine.Rendering.Universal
{
#if UNITY_EDITOR
    [CustomEditor(typeof(DepthOfFieldDeprecated))]
    class DOFEditor : VolumeComponentEditor
    {
        SerializedDataParameter simpleTweakMode;
        SerializedDataParameter focalPoint;
        SerializedDataParameter smoothness;
        SerializedDataParameter focalSize;
        SerializedDataParameter focalZDistance;
        SerializedDataParameter focalStartCurve;
        SerializedDataParameter focalEndCurve;
        SerializedDataParameter visualizeCoc;
        SerializedDataParameter resolution;
        SerializedDataParameter quality;
        SerializedDataParameter objectFocus;
        SerializedDataParameter bokeh;
        SerializedDataParameter bokehScale;
        SerializedDataParameter bokehIntensity;
        SerializedDataParameter bokehThresholdLuminance;
        SerializedDataParameter bokehThresholdContrast;
        SerializedDataParameter bokehDownsample;
        SerializedDataParameter bokehTexture;
        SerializedDataParameter bokehDestination;
        SerializedDataParameter bluriness;
        SerializedDataParameter maxBlurSpread;
        SerializedDataParameter foregroundBlurExtrude;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<DepthOfFieldDeprecated>(serializedObject);

            simpleTweakMode             = Unpack(o.Find(x => x.simpleTweakMode));
            focalPoint                  = Unpack(o.Find(x => x.focalPoint));
            smoothness                  = Unpack(o.Find(x => x.smoothness));
            focalSize                   = Unpack(o.Find(x => x.focalSize));
            focalZDistance              = Unpack(o.Find(x => x.focalZDistance));
            focalStartCurve             = Unpack(o.Find(x => x.focalZStartCurve));
            focalEndCurve               = Unpack(o.Find(x => x.focalZEndCurve));
            visualizeCoc                = Unpack(o.Find(x => x.visualize));
            resolution                  = Unpack(o.Find(x => x.resolution));
            quality                     = Unpack(o.Find(x => x.quality));
            objectFocus                 = Unpack(o.Find(x => x.objectFocus));
            bokeh                       = Unpack(o.Find(x => x.bokeh));
            bokehScale                  = Unpack(o.Find(x => x.bokehScale));
            bokehIntensity              = Unpack(o.Find(x => x.bokehIntensity));
            bokehThresholdLuminance     = Unpack(o.Find(x => x.bokehThresholdLuminance));
            bokehThresholdContrast      = Unpack(o.Find(x => x.bokehThresholdContrast));
            bokehDownsample             = Unpack(o.Find(x => x.bokehDownsample));
            bokehTexture                = Unpack(o.Find(x => x.bokehTexture));
            bokehDestination            = Unpack(o.Find(x => x.bokehDestination));
            bluriness                   = Unpack(o.Find(x => x.bluriness));
            maxBlurSpread               = Unpack(o.Find(x => x.maxBlurSpread));
            foregroundBlurExtrude       = Unpack(o.Find(x => x.foregroundBlurExtrude));
        }

        public override void OnInspectorGUI()
        {

            if (!Camera.main)
                return;

            if (simpleTweakMode.value.boolValue)
                GUILayout.Label("Current: " + Camera.main.name + ", near " + Camera.main.nearClipPlane + ", far: " + Camera.main.farClipPlane + ", focal: " + focalPoint.value.floatValue, EditorStyles.miniBoldLabel);
            else                                                                                            
                GUILayout.Label("Current: " + Camera.main.name + ", near " + Camera.main.nearClipPlane + ", far: " + Camera.main.farClipPlane + ", focal: " + focalZDistance.value.floatValue, EditorStyles.miniBoldLabel);

            PropertyField(resolution, new GUIContent("Resolution"));
            PropertyField(quality, new GUIContent("Quality"));

            PropertyField(simpleTweakMode, EditorGUIUtility.TrTextContent("Simple tweak"));
            PropertyField(visualizeCoc, EditorGUIUtility.TrTextContent("Visualize focus"));
            PropertyField(bokeh, EditorGUIUtility.TrTextContent("Enable bokeh"));


            EditorGUILayout.Separator();

            GUILayout.Label("Focal Settings", EditorStyles.boldLabel);

            if (simpleTweakMode.value.boolValue)
            {
                focalPoint.value.floatValue = EditorGUILayout.Slider("Focal distance", focalPoint.value.floatValue, Camera.main.nearClipPlane, Camera.main.farClipPlane);
                PropertyField(objectFocus, EditorGUIUtility.TrTextContent("Transform"));
                PropertyField(smoothness, EditorGUIUtility.TrTextContent("Smoothness"));
                focalSize.value.floatValue = EditorGUILayout.Slider("Focal size", focalSize.value.floatValue, 0.0f, (Camera.main.farClipPlane - Camera.main.nearClipPlane));
            }
            else
            {
                focalZDistance.value.floatValue = EditorGUILayout.Slider("Distance", focalZDistance.value.floatValue, Camera.main.nearClipPlane, Camera.main.farClipPlane);
                PropertyField(objectFocus, EditorGUIUtility.TrTextContent("Transform"));
                focalSize.value.floatValue = EditorGUILayout.Slider("Size", focalSize.value.floatValue, 0.0f, (Camera.main.farClipPlane - Camera.main.nearClipPlane));
                focalStartCurve.value.floatValue = EditorGUILayout.Slider("Start curve", focalStartCurve.value.floatValue, 0.05f, 20.0f);
                focalEndCurve.value.floatValue = EditorGUILayout.Slider("End curve", focalEndCurve.value.floatValue, 0.05f, 20.0f);
            }

            EditorGUILayout.Separator();

            GUILayout.Label("Blur (Fore- and Background)", EditorStyles.boldLabel);
            PropertyField(bluriness, EditorGUIUtility.TrTextContent("Blurriness"));
            PropertyField(maxBlurSpread, EditorGUIUtility.TrTextContent("Blur spread"));

            if (quality.value.enumValueIndex > 0)
            {
                PropertyField(foregroundBlurExtrude, EditorGUIUtility.TrTextContent("Foreground size"));
            }

            EditorGUILayout.Separator();

            if (bokeh.value.boolValue)
            {
                EditorGUILayout.Separator();
                GUILayout.Label("Bokeh Settings", EditorStyles.boldLabel);
                PropertyField(bokehDestination, EditorGUIUtility.TrTextContent("Destination"));
                bokehIntensity.value.floatValue = EditorGUILayout.Slider("Intensity", bokehIntensity.value.floatValue, 0.0f, 1.0f);
                bokehThresholdLuminance.value.floatValue = EditorGUILayout.Slider("Min luminance", bokehThresholdLuminance.value.floatValue, 0.0f, 0.99f);
                bokehThresholdContrast.value.floatValue = EditorGUILayout.Slider("Min contrast", bokehThresholdContrast.value.floatValue, 0.0f, 0.25f);
                bokehDownsample.value.intValue = EditorGUILayout.IntSlider("Downsample", bokehDownsample.value.intValue, 1, 3);
                bokehScale.value.floatValue = EditorGUILayout.Slider("Size scale", bokehScale.value.floatValue, 0.0f, 20.0f);
                PropertyField(bokehTexture, EditorGUIUtility.TrTextContent("Texture mask"));
            }

        }
    }
#endif
    public enum Dof34QualitySetting
    {
        OnlyBackground = 1,
        BackgroundAndForeground = 2,
    }

    public enum DofResolution
    {
        High = 2,
        Medium = 3,
        Low = 4,
    }

    public enum DofBlurriness
    {
        Low = 1,
        High = 2,
        VeryHigh = 4,
    }

    public enum BokehDestination
    {
        Background = 0x1,
        Foreground = 0x2,
        BackgroundAndForeground = 0x3,
    }
    [Serializable]
    public sealed class Dof34QualityParameter : VolumeParameter<Dof34QualitySetting>
    {
        public Dof34QualityParameter(Dof34QualitySetting value, bool overrideState = false) : base(value, overrideState) { }
    }
    [Serializable]
    public sealed class DofResolutionParameter : VolumeParameter<DofResolution>
    {
        public DofResolutionParameter(DofResolution value, bool overrideState = false) : base(value, overrideState) { }
    }
    [Serializable]
    public sealed class DofBlurrinessParameter : VolumeParameter<DofBlurriness>
    {
        public DofBlurrinessParameter(DofBlurriness value, bool overrideState = false) : base(value, overrideState) { }
    }
    [Serializable]
    public sealed class BokehDestinationParameter : VolumeParameter<BokehDestination>
    {
        public BokehDestinationParameter(BokehDestination value, bool overrideState = false) : base(value, overrideState) { }
    }
    [Serializable]
    public sealed class TransformParameter : VolumeParameter<GameObject>
    {
        public TransformParameter(GameObject value, bool overrideState = false) : base(value, overrideState) { }
    }
}