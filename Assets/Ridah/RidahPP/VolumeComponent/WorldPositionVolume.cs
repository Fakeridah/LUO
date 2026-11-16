using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/World Position")]
    public sealed class WorldPositionVolume : PostProcessVolumeComponent
    {
        public BoolParameter ColorAddMode = new BoolParameter(true);
        public FloatParameter GridSize = new FloatParameter(0f);
        public FloatParameter GridLineWidth = new FloatParameter(2);

        Material material;
        Material sb;

        public override bool visibleInSceneView => true;
        public override InjectionPoint InjectionPoint => InjectionPoint.BeforePostProcess;
        public override bool IsActive()
        {
            return GridSize.value > 0 && GridLineWidth.value > 0;
        }

        static class IDs
        {
            internal static readonly int _UnitCubeGridCount = Shader.PropertyToID("_UnitCubeGridCount");
            internal static readonly int _GridLineWidth = Shader.PropertyToID("_GridLineWidth");
            internal static readonly int _InverseView = Shader.PropertyToID("_InverseView");
            internal static readonly string ADD_MODE = "ADD_MODE";
        }

        public override void Initialize()
        {
            material = CoreUtils.CreateEngineMaterial("Ridah/Post-Processing/World Position");
            sb = CoreUtils.CreateEngineMaterial("Ridah/Post-Processing/SSR");
        }

        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            material.SetMatrix(IDs._InverseView, cameraData.camera.cameraToWorldMatrix);
            sb.SetMatrix(IDs._InverseView, cameraData.camera.cameraToWorldMatrix);

            material.SetKeyWord(IDs.ADD_MODE, ColorAddMode.value);
            material.SetFloat(IDs._UnitCubeGridCount, 1 / GridSize.value);
            material.SetFloat(IDs._GridLineWidth, GridLineWidth.value);

            cb.SetPostProcessSourceTexture(source);
            cb.DrawFullScreenTriangle(material, destination);
        }
    }
}