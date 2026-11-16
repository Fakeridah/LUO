using System;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;
using Ridah.URPPostProcessing;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Rendering.Universal;
namespace Ridah.URPPostProcessing
{

    [Serializable, VolumeComponentMenu("Ridah PostProcessing/Glitch/GlitchVolume")]
    public class GlitchVolume : PostProcessVolumeComponent
    {
        public GlitchModeParameter mode = new GlitchModeParameter(GlitchMode.BlockGlitch);
        public FloatParameter Speed = new FloatParameter(10f);
        public FloatParameter BlockSize = new FloatParameter(0f);
        public FloatParameter BlockSizeJitter = new FloatParameter(0f);
        public FloatParameter MaxRGBSplitX = new FloatParameter(1f);
        public FloatParameter MaxRGBSplitY = new FloatParameter(1f);
        public FloatParameter rgbAMPLITUDE = new FloatParameter(1f);
        public FloatParameter rgbSPEED = new FloatParameter(1f);
        public Texture2DParameter rgbNoise = new Texture2DParameter(null);
        public ClampedFloatParameter mousePosX = new ClampedFloatParameter(0,0f,1f);
        public FloatParameter spScale = new  FloatParameter(1f);
        public ClampedFloatParameter cpGlitchAmount = new ClampedFloatParameter(0.01f, 0f, 5f);

        Material _material;

        static class IDs
        {
            internal static readonly int Speed = Shader.PropertyToID("_Speed");
            internal static readonly int BlockSize = Shader.PropertyToID("_BlockSize");
            internal static readonly int MaxRGBSplitX = Shader.PropertyToID("_MaxRGBSplitX");
            internal static readonly int MaxRGBSplitY = Shader.PropertyToID("_MaxRGBSplitY");
            internal static readonly int Seed = Shader.PropertyToID("_Seed");
            internal static readonly int rgbAMPLITUDE = Shader.PropertyToID("_rgbAMPLITUDE");
            internal static readonly int rgbSPEED = Shader.PropertyToID("_rgbSPEED");
            internal static readonly int rgbNoise = Shader.PropertyToID("_NoiseTex");
            internal static readonly int mousePosX = Shader.PropertyToID("_mousePosX");
            internal static readonly int spScale = Shader.PropertyToID("_spScale");
            internal static readonly int cpGlitchAmount = Shader.PropertyToID("_cpGlitchAmount");
        }

        public override bool IsActive() => BlockSize.value > 0;

        public override bool visibleInSceneView => false;

        public override void Initialize()
        {
            _material = CoreUtils.CreateEngineMaterial("Ridah/Post-Processing/Glitch");
        }

        public override void Render(CommandBuffer cb, CameraData camera, RenderTargetIdentifier source, RenderTargetIdentifier dest)
        {
            if (_material == null)
                return;

            var time = Time.time;
            _material.SetInt(IDs.Seed, (int)(time * 10000));

            float jitter = BlockSizeJitter.value * .5f;
            float size = Random.Range(BlockSize.value - jitter, BlockSize.value + jitter);
            _material.SetFloat(IDs.Speed, Speed.value);
            _material.SetFloat(IDs.BlockSize, size);
            _material.SetFloat(IDs.MaxRGBSplitX, MaxRGBSplitX.value);
            _material.SetFloat(IDs.MaxRGBSplitY, MaxRGBSplitY.value);
            _material.SetFloat(IDs.rgbAMPLITUDE, rgbAMPLITUDE.value);
            _material.SetFloat(IDs.rgbSPEED, rgbSPEED.value);
            _material.SetTexture(IDs.rgbNoise, rgbNoise.value);
            _material.SetFloat(IDs.mousePosX, mousePosX.value);
            _material.SetFloat(IDs.spScale, spScale.value);
            _material.SetFloat(IDs.cpGlitchAmount, cpGlitchAmount.value);

            cb.SetPostProcessSourceTexture(source);
            cb.DrawFullScreenTriangle(_material, dest,(int)mode.value);
        }
    }
}

namespace UnityEngine.Rendering.Universal
{
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(GlitchVolume))]
    class GlitchVolumeEditor : VolumeComponentEditor
    {
        SerializedDataParameter mode;
        SerializedDataParameter Speed;
        SerializedDataParameter BlockSize;
        SerializedDataParameter BlockSizeJitter;
        SerializedDataParameter MaxRGBSplitX ;
        SerializedDataParameter MaxRGBSplitY;
        SerializedDataParameter rgbAMPLITUDE;
        SerializedDataParameter rgbSPEED;
        SerializedDataParameter rgbNoise;
        SerializedDataParameter mousePosX;
        SerializedDataParameter spScale;
        SerializedDataParameter cpGlitchAmount;
        public override void OnEnable()
        {
            var o = new PropertyFetcher<GlitchVolume>(serializedObject);

            mode = Unpack(o.Find(x => x.mode));
            Speed = Unpack(o.Find(x => x.Speed));
            BlockSize = Unpack(o.Find(x => x.BlockSize));
            BlockSizeJitter = Unpack(o.Find(x => x.BlockSizeJitter));
            MaxRGBSplitX = Unpack(o.Find(x => x. MaxRGBSplitX));
            MaxRGBSplitY = Unpack(o.Find(x => x.MaxRGBSplitY));
            rgbAMPLITUDE = Unpack(o.Find(x => x.rgbAMPLITUDE));
            rgbSPEED = Unpack(o.Find(x => x.rgbSPEED));
            rgbNoise = Unpack(o.Find(x => x.rgbNoise));
            mousePosX = Unpack(o.Find(x => x.mousePosX));
            spScale = Unpack(o.Find(x => x.spScale));
            cpGlitchAmount = Unpack(o.Find(x => x.cpGlitchAmount));
        }

        public override void OnInspectorGUI()
        {
            //GUILayout.Label("Detects spatial differences and converts into black outlines", EditorStyles.miniBoldLabel);
            PropertyField(mode);

            if (mode.value.intValue == 0)
            {
                PropertyField(Speed, EditorGUIUtility.TrTextContent("Speed"));
                PropertyField(BlockSize, EditorGUIUtility.TrTextContent("BlockSize"));
                PropertyField(BlockSizeJitter, EditorGUIUtility.TrTextContent("BlockSizeJitter"));
                PropertyField(MaxRGBSplitX, EditorGUIUtility.TrTextContent("MaxRGBSplitX"));
                PropertyField(MaxRGBSplitY, EditorGUIUtility.TrTextContent("MaxRGBSplitY"));
            }
            else if (mode.value.intValue == 1)
            {
                PropertyField(rgbAMPLITUDE, EditorGUIUtility.TrTextContent("AMPLITUDE"));
                PropertyField(rgbSPEED, EditorGUIUtility.TrTextContent("SPEED"));
                PropertyField(rgbNoise, EditorGUIUtility.TrTextContent("NoiseTex"));
            }
            else if (mode.value.intValue == 2)
            {
                PropertyField(mousePosX, EditorGUIUtility.TrTextContent("RGB Scale"));
                PropertyField(spScale, EditorGUIUtility.TrTextContent("UV Scale"));
            }
            else if (mode.value.intValue == 3)
            {
                PropertyField(cpGlitchAmount, EditorGUIUtility.TrTextContent("Glitch Amount"));
            }
            else if (mode.value.intValue == 4)
            {
                PropertyField(cpGlitchAmount, EditorGUIUtility.TrTextContent("Glitch Amount"));
            }
            else if (mode.value.intValue == 5)
            {
                PropertyField(cpGlitchAmount, EditorGUIUtility.TrTextContent("Curve Scale"));
            }

        }
    }
#endif
    public enum GlitchMode
    {
        BlockGlitch = 0,
        RGBShiftGlitch = 1,
        SPGlitch = 2,
        CPGlitch =3,
        StretchGlitch = 4,
        LineGlitch = 5,
        OldSchoolGlitch = 6
    }
    [Serializable]
    public sealed class GlitchModeParameter : VolumeParameter<GlitchMode>
    {
        public GlitchModeParameter(GlitchMode value, bool overrideState = false) : base(value, overrideState) { }
    }
}