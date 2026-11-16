using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RidahRF : ScriptableRendererFeature
{
    [System.Serializable]
    public class ScreenDisturbSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Range(0, 1)] public float flashTime = 1;//�Ŷ���˸��ʱ�䣬ԽС��˸Ƶ��Խ��
        [Range(0, 1)] public float flashTimeAdjust = .8f;// �Ŷ���˸��ʱ�������0-1,0��ʾ��˸�ļ��ʱ�����˸ʱ����ͬ��Խ�ӽ�1��˸ʱ��Խ�̣�1����˸��
        public float noiseIntensity = .7f;
    }

    public ScreenDisturbSettings settings = new ScreenDisturbSettings();

    class ScreenDisturbPass : ScriptableRenderPass
    {
        public Material screenDisturbMaterial;
        string profilerTag;
        private int Convolution = 3;
        [Range(1, 10)]
        private int LowPixel = 1;
        private bool Rot_Key;

        int tmpId1;
        int tmpId2;
        RenderTargetIdentifier tmpRT1;
        RenderTargetIdentifier tmpRT2;

        //RenderTexture temp1;
        //RenderTexture temp2;

        ScriptableRenderer render;
        private RenderTargetIdentifier source { get; set; }

        public void Setup(ScriptableRenderer render)
        {
            this.render = render;
        }
        public ScreenDisturbPass(string profilerTag)
        {
            this.profilerTag = profilerTag;
        }
        public void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }
        //Ҳ������OnCameraSetup������
        public void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            cameraTextureDescriptor.depthBufferBits = 0;

            this.source = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);

            var width = cameraTextureDescriptor.width;
            var height = cameraTextureDescriptor.height;

            //������ȡrt��׼д��
            tmpId1 = Shader.PropertyToID("tmpRT1");
            tmpId2 = Shader.PropertyToID("tmpRT2");
            cmd.GetTemporaryRT(tmpId1, width/ LowPixel, height/ LowPixel, 0, FilterMode.Bilinear, cameraTextureDescriptor.colorFormat);
            cmd.GetTemporaryRT(tmpId2, width/ LowPixel, height/ LowPixel, 0, FilterMode.Bilinear, cameraTextureDescriptor.colorFormat);
            tmpRT1 = new RenderTargetIdentifier(tmpId1);
            tmpRT2 = new RenderTargetIdentifier(tmpId2);

            //ֱ�ӻ�ȡ��ʱrtҲ����
            //temp1 = RenderTexture.GetTemporary(width / LowPixel, height / LowPixel, 0, cameraTextureDescriptor.colorFormat);
            //temp2 = RenderTexture.GetTemporary(width / LowPixel, height / LowPixel, 0, cameraTextureDescriptor.colorFormat);
        }

        //Execute���Խ��ƿ���OnRenderImage����
        public void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

            //����һ��Shader��Ⱦ����ͼ�񴫵ݸ���ʱ����temp1��
            cmd.Blit(source, tmpRT1, screenDisturbMaterial);

            //��ʱ���� temp1 ѭ����
            for (int i = 0; i < Convolution; i++)
            {
                //��������Shader��Ⱦ�������ݴ�����ʱ���� temp1
                cmd.Blit(tmpRT1, tmpRT2, screenDisturbMaterial);
                cmd.Blit(tmpRT2, tmpRT1, screenDisturbMaterial);
            }

            cmd.Blit(tmpRT1, source, screenDisturbMaterial);

            context.ExecuteCommandBuffer(cmd);

            cmd.ReleaseTemporaryRT(tmpId1);
            cmd.ReleaseTemporaryRT(tmpId2);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }

    ScreenDisturbPass scriptablePass;

    //��ʼ��
    public override void Create()
    {
        scriptablePass = new ScreenDisturbPass("ScreenDisturb");
        Shader screenDisturbShader = Shader.Find("Custom/ScreenDisturb");
        if (screenDisturbShader == null) return;
        scriptablePass.screenDisturbMaterial = CoreUtils.CreateEngineMaterial(screenDisturbShader);
        scriptablePass.screenDisturbMaterial.SetFloat("_FlashTime", settings.flashTime);
        scriptablePass.screenDisturbMaterial.SetFloat("_FlashTimeAdjust", settings.flashTimeAdjust);
        scriptablePass.screenDisturbMaterial.SetFloat("_Noise2Intensity", settings.noiseIntensity);
        scriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    //���뽫pass���ӵ���Ⱦ�����вŻᱻִ��
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        scriptablePass.Setup(renderer);
        renderer.EnqueuePass(scriptablePass);
    }
    protected override void Dispose(bool disposing)
    {
        DestroyImmediate(scriptablePass.screenDisturbMaterial);
        scriptablePass = null;
    }
}
