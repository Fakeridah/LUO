using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Ridah.URPPostProcessing
{
    [Serializable, VolumeComponentMenu("Ridah PostProcessing/GodRay")]
    public sealed class GodRay : PostProcessVolumeComponent
    {
        public ColorParameter color = new ColorParameter(Color.white,true,true,true);
        public IntParameter maxIterations = new IntParameter(64);
        public FloatParameter maxDistance = new FloatParameter(12f);
        public FloatParameter minDistance = new FloatParameter(0.4f);
        public FloatParameter DecreaseValue = new FloatParameter(1f);
        public FloatParameter DecreaseSpeed = new FloatParameter(1f);
        //public FloatParameter DecreasePower = new FloatParameter(1f);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 5f);

        public IntParameter blurStep = new IntParameter(5);
        public IntParameter blurIter = new IntParameter(5);
        public FloatParameter blurRange = new FloatParameter(0.5f);
        private static readonly int LightShaftTempId = UnityEngine.Shader.PropertyToID("_LightShaftTempTex");
        private static readonly int TempBlurBuffer1 = UnityEngine.Shader.PropertyToID("_TempBlurBuffer1");
        private static readonly int TempBlurBuffer2 = UnityEngine.Shader.PropertyToID("_TempBlurBuffer2");

        Material GodRayMaterial;
        RenderTargetIdentifier currentTarget;

        public override bool IsActive() => intensity.value > 0;
        public override void Initialize()
        {
            GodRayMaterial = CoreUtils.CreateEngineMaterial("Ridah/GodRay");
        }
        public override void Render(CommandBuffer cb, CameraData cameraData, RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            var camera = cameraData.camera; 
            var pixelRect = camera.pixelRect;
            float scale = cameraData.isSceneViewCamera ? 1 : cameraData.renderScale;
            int width = (int)(pixelRect.width * scale);
            int height = (int)(pixelRect.height * scale);

            GodRayMaterial.SetColor("_Color", color.value);
            GodRayMaterial.SetMatrix("_CamFrustum", FrustumCorners(camera));
            GodRayMaterial.SetMatrix("_CamToWorld", camera.cameraToWorldMatrix);
            GodRayMaterial.SetVector("_CamWorldSpace", camera.transform.position);
            GodRayMaterial.SetInt("_MaxIterations", maxIterations.value);
            GodRayMaterial.SetFloat("_MaxDistance", maxDistance.value);
            GodRayMaterial.SetFloat("_MinDistance", minDistance.value);
            GodRayMaterial.SetFloat("_Intensity", intensity.value);
            GodRayMaterial.SetFloat("_BlurScale", blurRange.value);
            GodRayMaterial.SetFloat("_DepthOutsideDecreaseValue", DecreaseValue.value);
            GodRayMaterial.SetFloat("_DepthOutsideDecreaseSpeed", DecreaseSpeed.value);
            //GodRayMaterial.SetFloat("_DepthOutsideDecreasePower", DecreasePower.value);

            cb.GetTemporaryRT(LightShaftTempId, width, height);
            cb.GetTemporaryRT(TempBlurBuffer1, width, height);
            cb.GetTemporaryRT(TempBlurBuffer2, width / 2, height / 2);

            // LightShaft生成
            cb.Blit(null, TempBlurBuffer1, GodRayMaterial, 0);

            for (int i = 0; i < blurStep.value; i++)
            {
                for (int j = 0; j < blurIter.value; j++)
                {
                   cb.Blit(TempBlurBuffer1, TempBlurBuffer2, GodRayMaterial, 2);
                   cb.ReleaseTemporaryRT(TempBlurBuffer1);
                   cb.GetTemporaryRT(TempBlurBuffer1, width / (2 * (i + 1)), height / (2 * (i + 1)));
                   cb.Blit(TempBlurBuffer2, TempBlurBuffer1, GodRayMaterial, 3);
                   cb.ReleaseTemporaryRT(TempBlurBuffer2);
                   cb.GetTemporaryRT(TempBlurBuffer2, width / (2 * (i + 1)), height / (2 * (i + 1)));
                }
            }
            
            for (int i = 0; i < blurStep.value; i++)
            {
                for (int j = 0; j < blurIter.value; j++)
                {
                    cb.Blit(TempBlurBuffer1, TempBlurBuffer2, GodRayMaterial, 2);
                    cb.ReleaseTemporaryRT(TempBlurBuffer1);
                    cb.GetTemporaryRT(TempBlurBuffer1, width / (2 * (blurStep.value - i)), height / (2 * (blurStep.value - i)));
                    cb.Blit(TempBlurBuffer2, TempBlurBuffer1, GodRayMaterial, 3);
                    cb.ReleaseTemporaryRT(TempBlurBuffer2);
                    cb.GetTemporaryRT(TempBlurBuffer2, width / (2 * (blurStep.value - i)), height / (2 * (blurStep.value - i)));
                }
            }

            cb.SetGlobalTexture(LightShaftTempId, new RenderTargetIdentifier(TempBlurBuffer1));

            cb.Blit(source, destination, GodRayMaterial, 1);

            cb.ReleaseTemporaryRT(LightShaftTempId);
            cb.ReleaseTemporaryRT(TempBlurBuffer1);
            cb.ReleaseTemporaryRT(TempBlurBuffer2);

        }
        private Matrix4x4 FrustumCorners(Camera cam)
        {
            Transform camtr = cam.transform;

            Vector3[] frustumCorners = new Vector3[4];
            cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1),
                cam.farClipPlane, cam.stereoActiveEye, frustumCorners);

            Matrix4x4 frustumVectorsArray = Matrix4x4.identity;

            frustumVectorsArray.SetRow(0, camtr.TransformVector(frustumCorners[0]));
            frustumVectorsArray.SetRow(1, camtr.TransformVector(frustumCorners[3]));
            frustumVectorsArray.SetRow(2, camtr.TransformVector(frustumCorners[1]));
            frustumVectorsArray.SetRow(3, camtr.TransformVector(frustumCorners[2]));

            return frustumVectorsArray;
        }
    }
}