using System;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Ridah.URPPostProcessing;
using UnityEditor;
namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/EdgeDetection")]
    public sealed class EdgeDetection : PostProcessVolumeComponent
    {
        public EdgeDetectParameter mode = new EdgeDetectParameter(EdgeDetectMode.SobelDepthThin);
        public FloatParameter sensitivityDepth = new FloatParameter(1.0f);
        public ClampedFloatParameter sensitivityNormals = new ClampedFloatParameter(1.0f, -1.0f, 1.0f);
        public MinFloatParameter lumThreshold = new MinFloatParameter(0.2f,0.0f);
        public ClampedFloatParameter edgeExp = new ClampedFloatParameter(0.0f, 0.0f, 5.0f);
        public FloatParameter sampleDist1 = new FloatParameter(1.0f);
        public FloatParameter sampleDist2 = new FloatParameter(1.0f);
        public FloatParameter sampleDist3 = new FloatParameter(1.0f);
        public ClampedFloatParameter edgesOnly = new ClampedFloatParameter(0.0f,-1.0f,5.0f);
        public ColorParameter edgesOnlyBgColor = new ColorParameter(Color.white);

        private Material edgeDetectMaterial = null;

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => edgeExp != 0.0f;

        public override void Initialize()
        {
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth))
            {
                this.active = false;
                return;
            }
            edgeDetectMaterial = CoreUtils.CreateEngineMaterial("Ridah/EdgeDetect");
        }

        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            edgeDetectMaterial.SetMatrix("_WorldToCameraMatrix", cameraData.camera.worldToCameraMatrix);
            /*if (CheckResources() == false)
            {
                Graphics.Blit(source, destination);
                return;
            }*/

            Vector2 sensitivity = new Vector2(sensitivityDepth.value, sensitivityNormals.value);
            edgeDetectMaterial.SetVector("_Sensitivity", new Vector4(sensitivity.x, sensitivity.y, 1.0f, sensitivity.y));
            edgeDetectMaterial.SetFloat("_BgFade", edgesOnly.value);
            edgeDetectMaterial.SetFloat("_SampleDistance1", sampleDist1.value);
            edgeDetectMaterial.SetFloat("_SampleDistance2", sampleDist2.value);
            edgeDetectMaterial.SetFloat("_SampleDistance3", sampleDist3.value);
            edgeDetectMaterial.SetVector("_BgColor", edgesOnlyBgColor.value);
            edgeDetectMaterial.SetFloat("_Exponent", edgeExp.value);
            edgeDetectMaterial.SetFloat("_Threshold", lumThreshold.value);

            cb.SetPostProcessSourceTexture(source);
            cb.DrawFullScreenTriangle(edgeDetectMaterial, destination, (int)mode.value);

        }

        protected override void CleanUp()
        {
            if (edgeDetectMaterial)
            {
                DestroyImmediate(edgeDetectMaterial);
                edgeDetectMaterial = null;
            }
        }
    }
}
namespace UnityEngine.Rendering.Universal
{
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(EdgeDetection))]
    class EdgeDetectionEditor : VolumeComponentEditor
    {
        SerializedDataParameter mode;
        SerializedDataParameter sensitivityDepth;
        SerializedDataParameter sensitivityNormals;
        SerializedDataParameter lumThreshold;
        SerializedDataParameter edgesOnly;
        SerializedDataParameter edgesOnlyBgColor;
        SerializedDataParameter edgeExp;
        SerializedDataParameter sampleDist1;
        SerializedDataParameter sampleDist2;
        SerializedDataParameter sampleDist3;
        public override void OnEnable()
        {
            var o = new PropertyFetcher<EdgeDetection>(serializedObject);

            mode = Unpack(o.Find(x => x.mode));
            sensitivityDepth = Unpack(o.Find(x => x.sensitivityDepth));
            sensitivityNormals = Unpack(o.Find(x => x.sensitivityNormals));
            lumThreshold = Unpack(o.Find(x => x.lumThreshold));
            edgesOnly = Unpack(o.Find(x => x.edgesOnly));
            edgesOnlyBgColor = Unpack(o.Find(x => x.edgesOnlyBgColor));
            edgeExp = Unpack(o.Find(x => x.edgeExp));
            sampleDist1 = Unpack(o.Find(x => x.sampleDist1));
            sampleDist2 = Unpack(o.Find(x => x.sampleDist2));
            sampleDist3 = Unpack(o.Find(x => x.sampleDist3));
        }

        public override void OnInspectorGUI()
        {
            //GUILayout.Label("Detects spatial differences and converts into black outlines", EditorStyles.miniBoldLabel);
            PropertyField(mode);

            if (mode.value.intValue < 2)
            {
                PropertyField(sensitivityDepth, EditorGUIUtility.TrTextContent(" Depth Sensitivity"));
                PropertyField(sensitivityNormals, EditorGUIUtility.TrTextContent(" Normals Sensitivity"));
                PropertyField(sampleDist1, EditorGUIUtility.TrTextContent(" Sample Distance"));
            }
            else if (mode.value.intValue < 4)
            {
                PropertyField(edgeExp, EditorGUIUtility.TrTextContent(" Edge Exponent"));
                PropertyField(sampleDist2, EditorGUIUtility.TrTextContent(" Sample Distance"));

            }
            else
            {
                // lum based mode
                PropertyField(lumThreshold, EditorGUIUtility.TrTextContent(" Luminance Threshold"));
                PropertyField(sampleDist3, EditorGUIUtility.TrTextContent(" Sample Distance"));
            }


            GUILayout.Space(4f);
            GUILayout.Label("Background Options");
            PropertyField(edgesOnly, EditorGUIUtility.TrTextContent("edgesOnly"));
            PropertyField(edgesOnlyBgColor, EditorGUIUtility.TrTextContent(" Color"));

        }
    }
#endif
    public enum EdgeDetectMode
    {
        TriangleDepthNormals = 0,
        RobertsCrossDepthNormals = 1,
        SobelDepth = 2,
        SobelDepthThin = 3,
        TriangleLuminance = 4,
    }
    [Serializable]
    public sealed class EdgeDetectParameter : VolumeParameter<EdgeDetectMode>
    {
        public EdgeDetectParameter(EdgeDetectMode value, bool overrideState = false) : base(value, overrideState) { }
    }
}