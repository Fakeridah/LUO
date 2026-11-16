using Ridah.URPPostProcessing;
using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/SSAO")]
    public sealed class SSAO : PostProcessVolumeComponent
    {
        public ClampedFloatParameter  m_Radius               = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);
        public SSAOParameter          m_SampleCount          = new SSAOParameter(SSAOSamples.Medium);
        public ClampedFloatParameter  m_OcclusionIntensity   = new ClampedFloatParameter(1.5f, 0.5f, 4.0f);
        public ClampedIntParameter    m_Blur                 =  new ClampedIntParameter(2, 0, 4);
        [Tooltip("NO USE!")]
        public ClampedIntParameter    m_Downsampling         = new ClampedIntParameter(2, 1,6);
        public ClampedFloatParameter  m_OcclusionAttenuation = new ClampedFloatParameter(1.0f, 0.2f, 2.0f);
        public ClampedFloatParameter  m_MinZ                 = new ClampedFloatParameter(0.01f, 0.00001f, 0.5f);
        public Texture2DParameter     m_RandomTexture        = new Texture2DParameter(null);

        private static int _TexelOffsetScaleID = Shader.PropertyToID("_TexelOffsetScale");
        private static int rtAO                = Shader.PropertyToID("rtAO");
        private static int rtBlurX             = Shader.PropertyToID("rtBlurX");
        private static int rtBlurY             = Shader.PropertyToID("rtBlurY");

        private Material m_SSAOMaterial;
        private bool m_Supported;
        private GraphicsFormat defaultHDRFormat;

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => m_Radius != 0.0f;

        public override void Initialize()
        {
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
            {
                m_Supported = false;
                this.active = false;
                return;
            }
            m_SSAOMaterial = CoreUtils.CreateEngineMaterial("Ridah/SSAO");
            m_SSAOMaterial.SetTexture("_RandomTexture", m_RandomTexture.value);
            m_Supported = true;

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
            SanitizeParameters();
            if (!m_Supported)
            {
                this.active = false;
                return;
            }
            m_SSAOMaterial.SetMatrix("_WorldToCameraMatrix",cameraData.camera.worldToCameraMatrix);
            var pixelRect = cameraData.camera.pixelRect;
            float scale = cameraData.isSceneViewCamera ? 1 : cameraData.renderScale;
            int width = (int)(pixelRect.width * scale);
            int height = (int)(pixelRect.height * scale);

            cb.GetTemporaryRT(rtAO, width / m_Downsampling.value, height / m_Downsampling.value, 0, FilterMode.Bilinear, defaultHDRFormat);

            // Render SSAO term into a smaller texture
            //RenderTexture rtAO = RenderTexture.GetTemporary(width / m_Downsampling.value, height / m_Downsampling.value, 0);
            float fovY = cameraData.camera.fieldOfView;
            float far = cameraData.camera.farClipPlane;
            float y = Mathf.Tan(fovY * Mathf.Deg2Rad * 0.5f) * far;
            float x = y * cameraData.camera.aspect;
            m_SSAOMaterial.SetVector("_FarCorner", new Vector3(x, y, far));
            int noiseWidth, noiseHeight;
            if (m_RandomTexture.value)
            {
                noiseWidth = m_RandomTexture.value.width;
                noiseHeight = m_RandomTexture.value.height;
            }
            else
            {
                noiseWidth = 1; noiseHeight = 1;
            }
            m_SSAOMaterial.SetVector("_NoiseScale", new Vector3((float)width / m_Downsampling.value / noiseWidth, (float)height / m_Downsampling.value / noiseHeight, 0.0f));
            m_SSAOMaterial.SetVector("_Params", new Vector4(
                                                     m_Radius.value,
                                                     m_MinZ.value,
                                                     1.0f / m_OcclusionAttenuation.value,
                                                     m_OcclusionIntensity.value));
            bool doBlur = m_Blur.value > 0;
            //cb.Blit(doBlur ? 0:source , rtAO, m_SSAOMaterial, (int)m_SampleCount.value);
            cb.DrawFullScreenTriangle(m_SSAOMaterial, rtAO, (int)m_SampleCount.value);

            if (doBlur)
            {
                // Blur SSAO horizontally
                cb.GetTemporaryRT(rtBlurX, width, height, 0, FilterMode.Bilinear, defaultHDRFormat);
                cb.SetGlobalVector(_TexelOffsetScaleID,
                                          new Vector4((float)m_Blur / width, 0, 0, 0));
                cb.SetPostProcessSourceTexture(rtAO);
                cb.DrawFullScreenTriangle(m_SSAOMaterial, rtBlurX, 3);
                cb.ReleaseTemporaryRT(rtAO); // original rtAO not needed anymore
           
                // Blur SSAO vertically
                //RenderTexture rtBlurY = RenderTexture.GetTemporary(width, height, 0);
                cb.GetTemporaryRT(rtBlurY, width, height, 0, FilterMode.Bilinear, defaultHDRFormat);
                cb.SetGlobalVector(_TexelOffsetScaleID,
                                          new Vector4(0, (float)m_Blur / height, 0, 0));
                cb.SetPostProcessSourceTexture(rtBlurX);
                cb.DrawFullScreenTriangle(m_SSAOMaterial, rtBlurY,3);
                cb.ReleaseTemporaryRT(rtBlurX); // original rtAO not needed anymore

                rtAO = rtBlurY; // AO is the blurred one now
                cb.ReleaseTemporaryRT(rtBlurY);
            }

            cb.SetPostProcessSourceTexture(rtAO);
            cb.SetGlobalTexture("_PostTex", source);
            cb.DrawFullScreenTriangle(m_SSAOMaterial, destination, 4);
            cb.ReleaseTemporaryRT(rtAO);

        }

        protected override void CleanUp()
        {
            if (m_SSAOMaterial)
            {
                DestroyImmediate(m_SSAOMaterial);
                m_SSAOMaterial = null;
            }
        }
        private void SanitizeParameters()
        {
            m_Downsampling.value = Mathf.Clamp(m_Downsampling.value, 1, 6);
            m_Radius.value = Mathf.Clamp(m_Radius.value, 0.05f, 1.0f);
            m_MinZ.value = Mathf.Clamp(m_MinZ.value, 0.00001f, 0.5f);
            m_OcclusionIntensity.value = Mathf.Clamp(m_OcclusionIntensity.value, 0.5f, 4.0f);
            m_OcclusionAttenuation.value = Mathf.Clamp(m_OcclusionAttenuation.value, 0.2f, 2.0f);
            m_Blur.value = Mathf.Clamp(m_Blur.value, 0, 4);
        }
    }
}
namespace UnityEngine.Rendering.Universal
{
#if UNITY_EDITOR
    /*[UnityEditor.CustomEditor(typeof(SSAO))]
    class SSAOEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_monochrome;
        SerializedDataParameter m_grainIntensityMin;
        SerializedDataParameter m_grainIntensityMax;
        SerializedDataParameter m_grainSize;
        SerializedDataParameter m_scratchIntensityMin;
        SerializedDataParameter m_scratchIntensityMax;
        SerializedDataParameter m_scratchFPS;
        SerializedDataParameter m_scratchJitter;
        SerializedDataParameter m_grainTexture;
        SerializedDataParameter m_scratchTexture;
        SerializedDataParameter m_intensities;
        SerializedDataParameter m_intensityMultiplier;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<SSAO>(serializedObject);

            //m_monochrome = Unpack(o.Find(x => x.monochrome));
            //m_grainIntensityMin = Unpack(o.Find(x => x.grainIntensityMin));
            //m_grainIntensityMax = Unpack(o.Find(x => x.grainIntensityMax));
            //m_grainSize = Unpack(o.Find(x => x.grainSize));
            //m_scratchIntensityMin = Unpack(o.Find(x => x.scratchIntensityMin));
            //m_scratchIntensityMax = Unpack(o.Find(x => x.scratchIntensityMax));
            //m_scratchFPS = Unpack(o.Find(x => x.scratchFPS));
            //m_scratchJitter = Unpack(o.Find(x => x.scratchJitter));
            //m_grainTexture = Unpack(o.Find(x => x.grainTexture));
            //m_scratchTexture = Unpack(o.Find(x => x.scratchTexture));
            //m_intensities = Unpack(o.Find(x => x.intensities));
            //m_intensityMultiplier = Unpack(o.Find(x => x.intensityMultiplier));
        }

        public override void OnInspectorGUI()
        {
            //PropertyField(m_monochrome);
            //
            //if (m_monochrome.value.boolValue)
            //{
            //    PropertyField(m_grainIntensityMin, EditorGUIUtility.TrTextContent("grainIntensityMin"));
            //    PropertyField(m_grainIntensityMax, EditorGUIUtility.TrTextContent("grainIntensityMax"));
            //    PropertyField(m_grainSize, EditorGUIUtility.TrTextContent("grainSize"));
            //    PropertyField(m_scratchIntensityMin, EditorGUIUtility.TrTextContent("scratchIntensityMin"));
            //    PropertyField(m_scratchIntensityMax, EditorGUIUtility.TrTextContent("scratchIntensityMax"));
            //    PropertyField(m_scratchFPS, EditorGUIUtility.TrTextContent("scratchFPS"));
            //    PropertyField(m_scratchJitter, EditorGUIUtility.TrTextContent("scratchJitter"));
            //    PropertyField(m_grainTexture, EditorGUIUtility.TrTextContent("grainTexture"));
            //    PropertyField(m_scratchTexture, EditorGUIUtility.TrTextContent("scratchTexture"));
            //}
            //else
            //{
            //    PropertyField(m_intensityMultiplier, EditorGUIUtility.TrTextContent("Intensity"));
            //    PropertyField(m_intensities, EditorGUIUtility.TrTextContent("ColorMix"));
            //}
        }
    }*/
#endif
    public enum SSAOSamples
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }
    [Serializable]
    public sealed class SSAOParameter : VolumeParameter<SSAOSamples>
    {
        public SSAOParameter(SSAOSamples value, bool overrideState = false) : base(value, overrideState) { }
    }
}