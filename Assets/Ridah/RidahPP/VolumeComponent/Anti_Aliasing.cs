using Ridah.URPPostProcessing;
using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/Anti-Aliasing/Anti-Aliasing")]
    public sealed class Anti_Aliasing : PostProcessVolumeComponent
    {
        public BoolParameter visible = new BoolParameter(false);
        public AAParameter mode = new AAParameter(AAMode.FXAA3Console);
        public BoolParameter showGeneratedNormals = new BoolParameter(false);
        public FloatParameter offsetScale = new FloatParameter(.2f);
        public FloatParameter blurRadius = new FloatParameter(18.0f);
        public FloatParameter edgeThresholdMin = new FloatParameter(.05f);
        public FloatParameter edgeThreshold = new FloatParameter(.2f);
        public FloatParameter edgeSharpness = new FloatParameter(4.0f);
        public BoolParameter dlaaSharp = new BoolParameter(false);

        private Material ssaa;
        private Material dlaa;
        private Material nfaa;
        private Material materialFXAAPreset2;
        private Material materialFXAAPreset3;
        private Material materialFXAAII;
        private Material materialFXAAIII;

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => visible != false;

        public override void Initialize()
        {
            ssaa = CoreUtils.CreateEngineMaterial("Ridah/SSAA");
            dlaa = CoreUtils.CreateEngineMaterial("Ridah/DLAA");
            nfaa = CoreUtils.CreateEngineMaterial("Ridah/NFAA");
            materialFXAAPreset2 = CoreUtils.CreateEngineMaterial("Ridah/FXAA Preset 2");
            materialFXAAPreset3 = CoreUtils.CreateEngineMaterial("Ridah/FXAA Preset 3");
            materialFXAAII = CoreUtils.CreateEngineMaterial("Ridah/FXAA II");
            materialFXAAIII = CoreUtils.CreateEngineMaterial("Ridah/FXAA III (Console)");
        }
       
        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            var pixelRect = cameraData.camera.pixelRect;
            float scale = cameraData.isSceneViewCamera ? 1 : cameraData.renderScale;
            int width = (int)(pixelRect.width * scale);
            int height = (int)(pixelRect.height * scale);
            // ----------------------------------------------------------------
            // FXAA antialiasing modes

            if (mode == AAMode.FXAA3Console && (materialFXAAIII != null))
            {
                materialFXAAIII.SetFloat("_EdgeThresholdMin", edgeThresholdMin.value);
                materialFXAAIII.SetFloat("_EdgeThreshold", edgeThreshold.value);
                materialFXAAIII.SetFloat("_EdgeSharpness", edgeSharpness.value);

                cb.Blit(source, destination, materialFXAAIII);
            }
            else if (mode == AAMode.FXAA1PresetB && (materialFXAAPreset3 != null))
            {
                cb.Blit(source, destination, materialFXAAPreset3);
            }
            else if (mode == AAMode.FXAA1PresetA && materialFXAAPreset2 != null)
            {
                //source.anisoLevel = 4;
                cb.Blit(source, destination, materialFXAAPreset2);
                //source.anisoLevel = 0;
            }
            else if (mode == AAMode.FXAA2 && materialFXAAII != null)
            {
                cb.Blit(source, destination, materialFXAAII);
            }
            else if (mode == AAMode.SSAA && ssaa != null)
            {
                // ----------------------------------------------------------------
                // SSAA antialiasing
                cb.Blit(source, destination, ssaa);
            }
            else if (mode == AAMode.DLAA && dlaa != null)
            {
                // ----------------------------------------------------------------
                // DLAA antialiasing

                //source.anisoLevel = 0;
                RenderTexture interim = RenderTexture.GetTemporary(width, height);
                cb.Blit(source, interim, dlaa, 0);
                cb.Blit(interim, destination, dlaa, dlaaSharp.value ? 2 : 1);
                RenderTexture.ReleaseTemporary(interim);
            }
            else if (mode == AAMode.NFAA && nfaa != null)
            {
                // ----------------------------------------------------------------
                // nfaa antialiasing

                //source.anisoLevel = 0;

                nfaa.SetFloat("_OffsetScale", offsetScale.value);
                nfaa.SetFloat("_BlurRadius", blurRadius.value);

                cb.Blit(source, destination, nfaa, showGeneratedNormals.value ? 1 : 0);
            }
            else
            {
                // none of the AA is supported, fallback to a simple blit
                cb.Blit(source, destination);
            }

        }
    }
}
namespace UnityEngine.Rendering.Universal
{
#if UNITY_EDITOR
    [CustomEditor(typeof(Anti_Aliasing))]
    class AntiAliasingEditor : VolumeComponentEditor
    {
        SerializedDataParameter visible;
        SerializedDataParameter mode;
        SerializedDataParameter showGeneratedNormals;
        SerializedDataParameter offsetScale;
        SerializedDataParameter blurRadius;
        SerializedDataParameter dlaaSharp;
        SerializedDataParameter edgeThresholdMin;
        SerializedDataParameter edgeThreshold;
        SerializedDataParameter edgeSharpness;
        public override void OnEnable()
        {
            var o = new PropertyFetcher<Anti_Aliasing>(serializedObject);

            visible = Unpack(o.Find(x => x.visible));
            mode = Unpack(o.Find(x => x.mode));
            showGeneratedNormals = Unpack(o.Find(x => x.showGeneratedNormals));
            offsetScale = Unpack(o.Find(x => x.offsetScale));
            blurRadius = Unpack(o.Find(x => x.blurRadius));
            dlaaSharp = Unpack(o.Find(x => x.dlaaSharp));
            edgeThresholdMin = Unpack(o.Find(x => x.edgeThresholdMin));
            edgeThreshold = Unpack(o.Find(x => x.edgeThreshold));
            edgeSharpness = Unpack(o.Find(x => x.edgeSharpness));

        }

        public override void OnInspectorGUI()
        {
            GUILayout.Label("Luminance based fullscreen antialiasing", EditorStyles.miniBoldLabel);
            PropertyField(visible, EditorGUIUtility.TrTextContent("visible"));

            PropertyField(mode, EditorGUIUtility.TrTextContent("Technique"));

     
            //EditorGUILayout.HelpBox("This AA technique is currently not supported. Choose a different technique or disable the effect and use MSAA instead.", MessageType.Warning);

            if (mode.value.intValue == (int)AAMode.NFAA)
            {
                PropertyField(offsetScale, EditorGUIUtility.TrTextContent("Edge Detect Ofs"));
                PropertyField(blurRadius, EditorGUIUtility.TrTextContent("Blur Radius"));
                PropertyField(showGeneratedNormals, EditorGUIUtility.TrTextContent("Show Normals"));
            }
            else if (mode.value.intValue == (int)AAMode.DLAA)
            {
                PropertyField(dlaaSharp, EditorGUIUtility.TrTextContent("Sharp"));
            }
            else if (mode.value.intValue == (int)AAMode.FXAA3Console)
            {
                PropertyField(edgeThresholdMin, EditorGUIUtility.TrTextContent("Edge Min Threshhold"));
                PropertyField(edgeThreshold, EditorGUIUtility.TrTextContent("Edge Threshhold"));
                PropertyField(edgeSharpness, EditorGUIUtility.TrTextContent("Edge Sharpness"));
            }

        }
    }
#endif
    public enum AAMode
    {
        FXAA2 = 0,
        FXAA3Console = 1,
        FXAA1PresetA = 2,
        FXAA1PresetB = 3,
        NFAA = 4,
        SSAA = 5,
        DLAA = 6,
    }
    [Serializable]
    public sealed class AAParameter : VolumeParameter<AAMode>
    {
        public AAParameter(AAMode value, bool overrideState = false) : base(value, overrideState) { }
    }
}