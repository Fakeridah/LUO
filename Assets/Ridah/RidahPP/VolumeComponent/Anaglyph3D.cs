#pragma warning disable 0619
#if !(UNITY_IOS || UNITY_TVOS)
#define ANAGLYPH_INTERMEDIATE_TEXTURE
#endif
using Ridah.URPPostProcessing;
using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
namespace Ridah.URPPostProcessing
{                   //ʧ�ܵ��࣡��������
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/Anaglyph3D")]
    public sealed class Anaglyph : PostProcessVolumeComponent
    {
        [Header("Transform")]
        [Tooltip("The spacing between the red and cyan channels.\nA value of '0' will ignore the focal point.  This is useful for orthographic cameras.\nA negative value will swap the red and cyan.")]
        public FloatParameter spacing = new FloatParameter(0.0f);
        [Tooltip("The point 'x' units in front of the camera where the red and cyan channels meet.")]
        public FloatParameter focalPoint = new FloatParameter(10f);

        [Header("Blending")]
        [Tooltip("'None' - Replace the background with the effect.  This is ideal for rendering the entire screen with the effect.\n'Opacity' - Overlay the effect based on its opacity.\n'Depth' - Overlay the effect based on its depth.")]
        public OverlayParameter overlayMode = new OverlayParameter(OverlayMode.Opacity);
        [Tooltip("'None' - Do not blend the effect onto the background.\n'Additive' - Perform stylistic blending by adding the effect to the background.\n'Channel' - Perform correct blending based on each eye's channels.")]
        public BlendParameter blendMode = new BlendParameter(AnaglyphBlendMode.None);

        [Header("Rendering")]
        [Tooltip("The render texture format to use when Overlay Mode is set to Opacity.")]
        public RenderTextureFormatParameter opacityOverlayRenderTextureFormat = new RenderTextureFormatParameter(RenderTextureFormat.ARGB32);
        [Tooltip("The depth buffer bit count to use when Overlay Mode is set to Depth.")]
        public DepthBufferBitCountParameter depthOverlayBufferBitCount = new DepthBufferBitCountParameter(DepthBufferBitCount._24);
        
        
        private static readonly string[] RenderTargetColorNames = new string[2]  {
            "_AnaglyphLeft",
            "_AnaglyphRight"
        };
        private static readonly string[] RenderTargetDepthNames = new string[2] {
            "_AnaglyphLeftDepth",
            "_AnaglyphRightDepth"
        };
        private bool SingleChannel => spacing == 0;
        private bool NeedsDepth => overlayMode == OverlayMode.Depth;
        private Matrix4x4[] offsetMatrices = null;
        private RTHandleGroup[] renderTargetHandles = null;


        private LocalKeyword singleChannelKeyword;
        private LocalKeyword overlayModeOpacityKeyword;
        private LocalKeyword overlayModeDepthKeyword;
        private LocalKeyword blendModeAdditiveKeyword;
        private LocalKeyword blendModeChannelKeyword;

        private Material material = default;
        private GraphicsFormat defaultHDRFormat;

        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool visibleInSceneView => true;
        public override bool IsActive() => spacing != 0.0f;

        public override void Initialize()
        {
            material = CoreUtils.CreateEngineMaterial("Ridah/Anaglyph");

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
            offsetMatrices = new Matrix4x4[2];
            renderTargetHandles = new RTHandleGroup[2];

            singleChannelKeyword = new LocalKeyword(material.shader, "_SINGLE_CHANNEL");
            overlayModeOpacityKeyword = new LocalKeyword(material.shader, "_OVERLAY_MODE_OPACITY");
            overlayModeDepthKeyword = new LocalKeyword(material.shader, "_OVERLAY_MODE_DEPTH");
            blendModeAdditiveKeyword = new LocalKeyword(material.shader, "_BLEND_MODE_ADDITIVE");
            blendModeChannelKeyword = new LocalKeyword(material.shader, "_BLEND_MODE_CHANNEL");

            CreateOffsetMatrix(spacing.value, focalPoint.value, -1, ref offsetMatrices[0]);
            CreateOffsetMatrix(spacing.value, focalPoint.value, 1, ref offsetMatrices[1]);
        }
        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            Camera camera = cameraData.camera;

            cb.SetKeyword(material, singleChannelKeyword, SingleChannel);
            cb.SetKeyword(material, overlayModeOpacityKeyword, overlayMode == OverlayMode.Opacity);
            cb.SetKeyword(material, overlayModeDepthKeyword, overlayMode == OverlayMode.Depth);
            cb.SetKeyword(material, blendModeAdditiveKeyword, blendMode == AnaglyphBlendMode.Additive);
            cb.SetKeyword(material, blendModeChannelKeyword, blendMode == AnaglyphBlendMode.Channel);
       
            if (SingleChannel)
            { // render only left channel

                Draw(cb,camera.worldToCameraMatrix, RenderTargetColorNames[0], RenderTargetDepthNames[0], source);
            }
            else
            { // render both channels
                for (int i = 0; i < renderTargetHandles.Length; i++)
                {
                    Matrix4x4 viewMatrix = offsetMatrices[i] * camera.worldToCameraMatrix;
                    Draw(cb,viewMatrix, RenderTargetColorNames[i], RenderTargetDepthNames[i], source);
                }
            }
            cb.SetGlobalTexture("_SBTexture", source);
            cb.Blit( source, destination, material, 0);
        }
        void Draw(CommandBuffer cb,in Matrix4x4 matrix, in string colorTarget, in string depthTarget, RenderTargetIdentifier source)
        {
            cb.SetGlobalMatrix("Anaglyph_offset", matrix);

            cb.SetGlobalTexture(colorTarget, source);
            if (NeedsDepth)
            {
                cb.SetGlobalTexture(depthTarget, source);
            }
        }
        public static void CreateOffsetMatrix(float spacing, float lookTarget, int side, ref Matrix4x4 matrix)
        {
            float xOffset = spacing * side * 0.5f;
            Vector3 offset = Vector3.right * xOffset;
            if (lookTarget != 0)
            {
                Quaternion lookRotation = Quaternion.LookRotation(new Vector3(xOffset, 0, lookTarget).normalized, Vector3.up);
                matrix = Matrix4x4.TRS(offset, lookRotation, Vector3.one);
            }
            else
            {
                matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);
            }
        }
        internal struct RTHandleGroup
        {
            public RTHandle color;
            public RTHandle depth;

            public RTHandleGroup(RTHandle color, RTHandle depth)
            {
                this.color = color;
                this.depth = depth;
            }

            public void Release()
            {
                color?.Release();
                depth?.Release();
            }
        }
    }
}
namespace UnityEngine.Rendering.Universal
{
#if UNITY_EDITOR
    [CustomEditor(typeof(Anaglyph))]
    class AnaglyphEditor : VolumeComponentEditor
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
            var o = new PropertyFetcher<Anaglyph>(serializedObject);

        }

        public override void OnInspectorGUI()
        { 
        }
    }
#endif
    public enum OverlayMode : int
    {
        None = 0,
        Opacity = 1,
        Depth = 2
    }
    public enum AnaglyphBlendMode : int
    {
        None = 0,
        Additive = 1,
        Channel = 2
    }
    public enum DepthBufferBitCount : int
    {
        [InspectorName("0")] _0 = 0,
        [InspectorName("16")] _16 = 16,
        [InspectorName("24")] _24 = 24,
        [InspectorName("32")] _32 = 32,
    }
    [Serializable]
    public sealed class OverlayParameter : VolumeParameter<OverlayMode>
    {
        public OverlayParameter(OverlayMode value, bool overrideState = false) : base(value, overrideState) { }
    }
    [Serializable]
    public sealed class BlendParameter : VolumeParameter<AnaglyphBlendMode>
    {
        public BlendParameter(AnaglyphBlendMode value, bool overrideState = false) : base(value, overrideState) { }
    }
    [Serializable]
    public sealed class DepthBufferBitCountParameter : VolumeParameter<DepthBufferBitCount>
    {
        public DepthBufferBitCountParameter(DepthBufferBitCount value, bool overrideState = false) : base(value, overrideState) { }
    }
    [Serializable]
    public sealed class RenderTextureFormatParameter : VolumeParameter<RenderTextureFormat>
    {
        public RenderTextureFormatParameter(RenderTextureFormat value, bool overrideState = false) : base(value, overrideState) { }
    }
}