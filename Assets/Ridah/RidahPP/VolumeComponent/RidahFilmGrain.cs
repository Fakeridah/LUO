using Ridah.URPPostProcessing;
using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/NoiseGrain/FilmGrain")]
    public sealed class RidahFilmGrain : PostProcessVolumeComponent
    {
        public FilmGrainParameter mode = new FilmGrainParameter(FilmGrainMode.SimpleGrain);
        public BoolParameter simpleGrainMode = new BoolParameter(false);
        public BoolParameter customScale = new BoolParameter(true);
        public ClampedIntParameter spreadGrainScaleX = new ClampedIntParameter(0,0,1920);
        public ClampedIntParameter spreadGrainScaleY = new ClampedIntParameter(0, 0, 1080);
        public ClampedFloatParameter TVhdr = new ClampedFloatParameter(0, 0, 1);
        public ClampedFloatParameter TVdesaturation = new ClampedFloatParameter(0, 0, 1);
        public ClampedFloatParameter TVdistortScale = new ClampedFloatParameter(0f, 0f, 10);



        private Material FilmGrain;


        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => simpleGrainMode != false;

        public override void Initialize()
        {
            FilmGrain = CoreUtils.CreateEngineMaterial("Ridah/FilmGrain");

            if (FilmGrain == null)
            {
                Debug.Log("Noise shaders are not set up! Disabling noise effect.");
                active = false;
            }
        }

        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            FilmGrain.SetInt("type", simpleGrainMode.value ? 1 : 0);
            FilmGrain.SetInt("customScale", customScale.value ? 0 : 1);
            FilmGrain.SetVector("spreadGrainScale", new Vector2(spreadGrainScaleX.value, spreadGrainScaleY.value));
            FilmGrain.SetVector("TVParmas", new Vector3(TVdesaturation.value, TVdistortScale.value, TVhdr.value));

            cb.SetPostProcessSourceTexture(source);
            cb.DrawFullScreenTriangle(FilmGrain, destination,(int)mode.value);
        }

        protected override void CleanUp()
        {
            if (FilmGrain)
                DestroyImmediate(FilmGrain);
        }
    }
}
namespace UnityEngine.Rendering.Universal
{
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(RidahFilmGrain))]
    class FilmGrainEditor : VolumeComponentEditor
    {
        SerializedDataParameter mode;
        SerializedDataParameter simpleGrainMode;
        SerializedDataParameter customScale;
        SerializedDataParameter spreadGrainScaleX;
        SerializedDataParameter spreadGrainScaleY;
        SerializedDataParameter TVdesaturation;
        SerializedDataParameter TVhdr;
        SerializedDataParameter TVdistortScale;
        public override void OnEnable()
        {
            var o = new PropertyFetcher<RidahFilmGrain>(serializedObject);

            mode = Unpack(o.Find(x => x.mode));
            simpleGrainMode = Unpack(o.Find(x => x.simpleGrainMode));
            customScale = Unpack(o.Find(x => x.customScale));
            spreadGrainScaleX = Unpack(o.Find(x => x.spreadGrainScaleX));
            spreadGrainScaleY = Unpack(o.Find(x => x.spreadGrainScaleY));
            TVdesaturation = Unpack(o.Find(x => x.TVdesaturation));
            TVhdr = Unpack(o.Find(x => x.TVhdr));
            TVdistortScale = Unpack(o.Find(x => x.TVdistortScale));
        }

        public override void OnInspectorGUI()
        {
            //GUILayout.Label("Detects spatial differences and converts into black outlines", EditorStyles.miniBoldLabel);
            PropertyField(mode);

            if (mode.value.intValue == 0)
            {
                PropertyField(simpleGrainMode, EditorGUIUtility.TrTextContent("Simple Grain Mode"));
            }
            if (mode.value.intValue == 1)
            {
                PropertyField(customScale, EditorGUIUtility.TrTextContent("Custom Scale"));
                PropertyField(spreadGrainScaleX, EditorGUIUtility.TrTextContent("Spread Grain Scalex"));
                PropertyField(spreadGrainScaleY, EditorGUIUtility.TrTextContent("Spread Grain Scaley"));
            }
            if (mode.value.intValue ==2)
            {
                PropertyField(TVdesaturation, EditorGUIUtility.TrTextContent("Desaturation"));
                PropertyField(TVhdr, EditorGUIUtility.TrTextContent("HDR"));
                PropertyField(TVdistortScale, EditorGUIUtility.TrTextContent("Vignette Scale"));
            }

        }
    }
#endif
    public enum FilmGrainMode
    {
        SimpleGrain = 0,
        SpreadingGrain = 1,
        TVGrain = 2
    }
    [Serializable]
    public sealed class FilmGrainParameter : VolumeParameter<FilmGrainMode>
    {
        public FilmGrainParameter(FilmGrainMode value, bool overrideState = false) : base(value, overrideState) { }
    }
}