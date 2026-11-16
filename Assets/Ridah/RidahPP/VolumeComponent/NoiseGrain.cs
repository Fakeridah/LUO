using Ridah.URPPostProcessing;
using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/NoiseGrain/NoiseAndScratches")]
    public sealed class NoiseAndScratches : PostProcessVolumeComponent
    {
        public BoolParameter         monochrome          = new BoolParameter(true);
        public ClampedFloatParameter grainIntensityMin   = new ClampedFloatParameter(0.1f, 0.0f, 5.0f);
        public ClampedFloatParameter grainIntensityMax   = new ClampedFloatParameter(0.2f, 0.0f, 5.0f);
        public ClampedFloatParameter grainSize           = new ClampedFloatParameter(2.0f, 0.1f, 50.0f);
        public ClampedFloatParameter scratchIntensityMin = new ClampedFloatParameter(0.05f, 0.0f, 5.0f);
        public ClampedFloatParameter scratchIntensityMax = new ClampedFloatParameter(0.25f, 0.0f, 5.0f);
        public ClampedFloatParameter scratchFPS          = new ClampedFloatParameter(10.0f, 1.0f, 30.0f);
        public ClampedFloatParameter scratchJitter       = new ClampedFloatParameter(0.01f, 0.0f, 1.0f);
        public TextureParameter      grainTexture        = new TextureParameter(null);
        public TextureParameter      scratchTexture      = new TextureParameter(null);
        public ColorParameter        intensities         = new ColorParameter(Color.white);
        public ClampedFloatParameter intensityMultiplier = new ClampedFloatParameter(0.25f,0.0f,2.0f);

        private Material m_MaterialRGB;
        private Material m_MaterialYUV;

        private float scratchTimeLeft = 0.0f;
        private float scratchX, scratchY;

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => grainIntensityMin != 0.1f;

        public override void Initialize()
        {
            m_MaterialRGB = CoreUtils.CreateEngineMaterial("Ridah/Noise Shader RGB");
            m_MaterialYUV = CoreUtils.CreateEngineMaterial("Ridah/Noise Shader YUV");

            if (m_MaterialRGB == null || m_MaterialYUV == null)
            {
                Debug.Log("Noise shaders are not set up! Disabling noise effect.");
                this.active = false;
            }
        }
        protected Material material
        {
            get
            {
                if (m_MaterialRGB == null)
                {
                    m_MaterialRGB = CoreUtils.CreateEngineMaterial("Ridah/Noise Shader RGB");
                    m_MaterialRGB.hideFlags = HideFlags.HideAndDontSave;
                }
                if (m_MaterialYUV == null)
                {
                    m_MaterialYUV = CoreUtils.CreateEngineMaterial("Ridah/Noise Shader YUV");
                    m_MaterialYUV.hideFlags = HideFlags.HideAndDontSave;
                }
                return (!monochrome.value) ? m_MaterialYUV : m_MaterialRGB;
            }
        }
        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            SanitizeParameters();
            if (scratchTimeLeft <= 0.0f)
            {
                scratchTimeLeft = UnityEngine.Random.value * 2 / scratchFPS.value;
                scratchX = UnityEngine.Random.value;
                scratchY = UnityEngine.Random.value;
            }
            scratchTimeLeft -= Time.deltaTime;

            Material mat = material;

            m_MaterialYUV.SetVector("_NoisePerChannel",intensities.value);
            m_MaterialYUV.SetFloat("_DX11NoiseTime", Time.frameCount);
            m_MaterialYUV.SetFloat("_NoiseAmount", intensityMultiplier.value);
            mat.SetTexture("_GrainTex", grainTexture.value);
            mat.SetTexture("_ScratchTex", scratchTexture.value);
            float grainScale = 1.0f / grainSize.value; 
            mat.SetVector("_GrainOffsetScale", new Vector4(
                                                   UnityEngine.Random.value,
                                                   UnityEngine.Random.value,
                                                   (float)Screen.width / (float)grainTexture.value.width * grainScale,
                                                   (float)Screen.height / (float)grainTexture.value.height * grainScale
                                                   ));
            mat.SetVector("_ScratchOffsetScale", new Vector4(
                                                     scratchX + UnityEngine.Random.value * scratchJitter.value,
                                                     scratchY + UnityEngine.Random.value * scratchJitter.value,
                                                     (float)Screen.width / (float)scratchTexture.value.width,
                                                     (float)Screen.height / (float)scratchTexture.value.height
                                                     ));
            mat.SetVector("_Intensity", new Vector4(
                                            UnityEngine.Random.Range(grainIntensityMin.value, grainIntensityMax.value),
                                            UnityEngine.Random.Range(scratchIntensityMin.value, scratchIntensityMax.value),
                                            0, 0));
            cb.SetPostProcessSourceTexture(source);
            cb.DrawFullScreenTriangle(mat,destination);
        }

        protected override void CleanUp()
        {
            if (m_MaterialRGB)
                DestroyImmediate(m_MaterialRGB);
            if (m_MaterialYUV)
                DestroyImmediate(m_MaterialYUV);
        }
        private void SanitizeParameters()
        {
            grainIntensityMin.value = Mathf.Clamp(grainIntensityMin.value, 0.0f, 5.0f);
            grainIntensityMax.value = Mathf.Clamp(grainIntensityMax.value, 0.0f, 5.0f);
            scratchIntensityMin.value = Mathf.Clamp(scratchIntensityMin.value, 0.0f, 5.0f);
            scratchIntensityMax.value = Mathf.Clamp(scratchIntensityMax.value, 0.0f, 5.0f);
            scratchFPS.value = Mathf.Clamp(scratchFPS.value, 1, 30);
            scratchJitter.value = Mathf.Clamp(scratchJitter.value, 0.0f, 1.0f);
            grainSize.value = Mathf.Clamp(grainSize.value, 0.1f, 50.0f);
        }
    }
}
namespace UnityEngine.Rendering.Universal
{
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(NoiseAndScratches))]
    class NoiseAndScratchesEditor : VolumeComponentEditor
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
            var o = new PropertyFetcher<NoiseAndScratches>(serializedObject);

            m_monochrome = Unpack(o.Find(x => x.monochrome));
            m_grainIntensityMin = Unpack(o.Find(x => x.grainIntensityMin));
            m_grainIntensityMax = Unpack(o.Find(x => x.grainIntensityMax));
            m_grainSize = Unpack(o.Find(x => x.grainSize));
            m_scratchIntensityMin = Unpack(o.Find(x => x.scratchIntensityMin));
            m_scratchIntensityMax = Unpack(o.Find(x => x.scratchIntensityMax));
            m_scratchFPS = Unpack(o.Find(x => x.scratchFPS));
            m_scratchJitter = Unpack(o.Find(x => x.scratchJitter));
            m_grainTexture = Unpack(o.Find(x => x.grainTexture));
            m_scratchTexture = Unpack(o.Find(x => x.scratchTexture));
            m_intensities = Unpack(o.Find(x => x.intensities));
            m_intensityMultiplier = Unpack(o.Find(x => x.intensityMultiplier));
        }

        public override void OnInspectorGUI()
        {
            PropertyField(m_monochrome);

            if (m_monochrome.value.boolValue)
            {
                PropertyField(m_grainIntensityMin, EditorGUIUtility.TrTextContent("grainIntensityMin"));
                PropertyField(m_grainIntensityMax, EditorGUIUtility.TrTextContent("grainIntensityMax"));
                PropertyField(m_grainSize, EditorGUIUtility.TrTextContent("grainSize"));
                PropertyField(m_scratchIntensityMin, EditorGUIUtility.TrTextContent("scratchIntensityMin"));
                PropertyField(m_scratchIntensityMax, EditorGUIUtility.TrTextContent("scratchIntensityMax"));
                PropertyField(m_scratchFPS, EditorGUIUtility.TrTextContent("scratchFPS"));
                PropertyField(m_scratchJitter, EditorGUIUtility.TrTextContent("scratchJitter"));
                PropertyField(m_grainTexture, EditorGUIUtility.TrTextContent("grainTexture"));
                PropertyField(m_scratchTexture, EditorGUIUtility.TrTextContent("scratchTexture"));
            }
            else
            {
                PropertyField(m_intensityMultiplier, EditorGUIUtility.TrTextContent("Intensity"));
                PropertyField(m_intensities, EditorGUIUtility.TrTextContent("ColorMix"));
            }
        }
    }
#endif
}