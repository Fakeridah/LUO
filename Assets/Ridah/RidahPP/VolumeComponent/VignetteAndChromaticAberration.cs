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
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/Vignette and Chromatic Aberration")]
    public sealed class VignetteAndChromaticAberration : PostProcessVolumeComponent
    {
        public AberrationParameter mode = new AberrationParameter(AberrationMode.Simple);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0.0f,0.0f,1f);                    // intensity == 0 disables pre pass (optimization)
        public FloatParameter chromaticAberration = new FloatParameter(0.2f);
        public FloatParameter axialAberration = new FloatParameter(0.5f);
        public ClampedFloatParameter blur = new ClampedFloatParameter(0.0f,0.0f,1f);                           // blur == 0 disables blur pass (optimization)
        public FloatParameter blurSpread = new FloatParameter(0.75f);
        public FloatParameter luminanceDependency = new FloatParameter(0.25f);
        public FloatParameter blurDistance = new FloatParameter(2.5f);

        private static int _VignetteID = Shader.PropertyToID("_VignetteTex");
        private static int color = Shader.PropertyToID("color");
        private static int color2A = Shader.PropertyToID("color2A");
        private static int color2B = Shader.PropertyToID("color2B");

        private GraphicsFormat defaultHDRFormat;
        private Material m_VignetteMaterial;
        private Material m_SeparableBlurMaterial;
        private Material m_ChromAberrationMaterial;

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => intensity != 0.0f;

        public override void Initialize()
        {
            m_VignetteMaterial = CoreUtils.CreateEngineMaterial("Ridah/Vignetting");
            m_SeparableBlurMaterial = CoreUtils.CreateEngineMaterial("Ridah/SeparableBlur");
            m_ChromAberrationMaterial = CoreUtils.CreateEngineMaterial("Ridah/ChromaticAberration");

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
            int rtW = (int)(pixelRect.width * scale);
            int rtH = (int)(pixelRect.height * scale);

            bool doPrepass = (Mathf.Abs(blur.value) > 0.0f || Mathf.Abs(intensity.value) > 0.0f);

            float widthOverHeight = (1.0f * rtW) / (1.0f * rtH);
            const float oneOverBaseSize = 1.0f / 512.0f;
            m_ChromAberrationMaterial.SetFloat("_Blur", (1.0f / (1.0f - blur.value)) - 1.0f);
            m_ChromAberrationMaterial.SetFloat("_Intensity", (1.0f / (1.0f - intensity.value) - 1.0f));		// intensity for vignette

            cb.GetTemporaryRT(color, rtW, rtH, 0, FilterMode.Bilinear, defaultHDRFormat);

            if (doPrepass)
            {
                // Blur corners
                if (Mathf.Abs(blur.value) > 0.0f)
                {
                    cb.GetTemporaryRT(color2A, rtW / 2, rtH / 2, 0, FilterMode.Bilinear, defaultHDRFormat);
                    cb.GetTemporaryRT(color2B, rtW / 2, rtH / 2, 0, FilterMode.Bilinear, defaultHDRFormat);

                    cb.SetPostProcessSourceTexture(source);
                    cb.DrawFullScreenTriangle(m_ChromAberrationMaterial, color2A, 0);

                    for (int i = 0; i < 2; i++)
                    {	// maybe make iteration count tweakable
                        cb.SetGlobalVector("offsets", new Vector4(0.0f, blurSpread.value * oneOverBaseSize, 0.0f, 0.0f));
                        cb.SetPostProcessSourceTexture(color2A);
                        cb.DrawFullScreenTriangle(m_SeparableBlurMaterial, color2B);

                        cb.SetGlobalVector("offsets", new Vector4(blurSpread.value * oneOverBaseSize / widthOverHeight, 0.0f, 0.0f, 0.0f));
                        cb.SetPostProcessSourceTexture(color2B);
                        cb.DrawFullScreenTriangle(m_SeparableBlurMaterial, color2A);
                    }
                }
				// blur intensity
                cb.SetGlobalTexture(_VignetteID, color2A);  // blurred texture

                cb.SetPostProcessSourceTexture(source);
                cb.DrawFullScreenTriangle(m_VignetteMaterial, color);
            }

            m_ChromAberrationMaterial.SetFloat("_ChromaticAberration", chromaticAberration.value);
            m_ChromAberrationMaterial.SetFloat("_AxialAberration", axialAberration.value);
            m_ChromAberrationMaterial.SetVector("_BlurDistance", new Vector2(-blurDistance.value, blurDistance.value));
            m_ChromAberrationMaterial.SetFloat("_Luminance", 1.0f / Mathf.Max(Mathf.Epsilon, luminanceDependency.value));

            //if (doPrepass) color.wrapMode = TextureWrapMode.Clamp;
            //else source.wrapMode = TextureWrapMode.Clamp;
            cb.SetPostProcessSourceTexture(doPrepass ? color : source);
            cb.DrawFullScreenTriangle(m_ChromAberrationMaterial, destination, mode == AberrationMode.Advanced ? 2 : 1);

            cb.ReleaseTemporaryRT(color);
            cb.ReleaseTemporaryRT(color2A);
            cb.ReleaseTemporaryRT(color2B);
        }
    }
}
namespace UnityEngine.Rendering.Universal
{
#if UNITY_EDITOR
    [CustomEditor(typeof(VignetteAndChromaticAberration))]
    class AberrationEditor : VolumeComponentEditor
    {
        SerializedDataParameter m_Mode;
        SerializedDataParameter m_Intensity;            
        SerializedDataParameter m_ChromaticAberration;
        SerializedDataParameter m_AxialAberration;
        SerializedDataParameter m_Blur;                 
        SerializedDataParameter m_BlurSpread;
        SerializedDataParameter m_BlurDistance;
        SerializedDataParameter m_LuminanceDependency;

        public override void OnEnable()
        {
            var o = new PropertyFetcher<VignetteAndChromaticAberration>(serializedObject);

            m_Mode                         = Unpack(o.Find(x => x.mode));
            m_Intensity                    = Unpack(o.Find(x => x.intensity));
            m_ChromaticAberration          = Unpack(o.Find(x => x.chromaticAberration));
            m_AxialAberration              = Unpack(o.Find(x => x.axialAberration));
            m_Blur                         = Unpack(o.Find(x => x.blur));
            m_BlurSpread                   = Unpack(o.Find(x => x.blurSpread));
            m_LuminanceDependency          = Unpack(o.Find(x => x.luminanceDependency));
            m_BlurDistance                 = Unpack(o.Find(x => x.blurDistance));

        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Simulates the common lens artifacts 'Vignette' and 'Aberration'", EditorStyles.miniLabel);

            PropertyField(m_Intensity, EditorGUIUtility.TrTextContent("Vignetting"));
            PropertyField(m_Blur, EditorGUIUtility.TrTextContent(" Blurred Corners"));
            if (m_Blur.value.floatValue > 0.0f)
            PropertyField(m_BlurSpread, EditorGUIUtility.TrTextContent(" Blur Distance"));

            EditorGUILayout.Separator();

            EditorGUILayout.PropertyField(m_Mode.value, new GUIContent("Aberration"));
            if (m_Mode.value.intValue > 0)
            {
                EditorGUILayout.Slider(m_ChromaticAberration.value, 0.0f, 50.0f, new GUIContent("  Tangential Aberration"));
                EditorGUILayout.Slider(m_AxialAberration.value, 0.0f, 50.0f, new GUIContent("  Axial Aberration"));
                m_LuminanceDependency.value.floatValue = EditorGUILayout.Slider("  Contrast Dependency", m_LuminanceDependency.value.floatValue, 0.001f, 1.0f);
                m_BlurDistance.value.floatValue = EditorGUILayout.Slider("  Blur Distance", m_BlurDistance.value.floatValue, 0.001f, 15.0f);
            }
            else
                EditorGUILayout.PropertyField(m_ChromaticAberration.value, new GUIContent(" Chromatic Aberration"));

        }
    }
#endif
    public enum AberrationMode
    {
        Simple = 0,
        Advanced = 1,
    }
    [Serializable]
    public sealed class AberrationParameter : VolumeParameter<AberrationMode>
    {
        public AberrationParameter(AberrationMode value, bool overrideState = false) : base(value, overrideState) { }
    }
}