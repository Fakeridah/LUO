using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine;
namespace Ridah.URPPostProcessing
{
    public class PostProcessRenderFeature : ScriptableRendererFeature
    {
        public PostProcessOrderConfig config;

        private PostProcessRenderPass afterSkyboxPass;
        private PostProcessRenderPass beforeNativePostProcessPass;
        private PostProcessRenderPass afterNativePostProcessPass;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if(afterSkyboxPass != null)
            {
                afterSkyboxPass.Setup(renderingData.cameraData);
                renderer.EnqueuePass(afterSkyboxPass);
            }

            if (beforeNativePostProcessPass != null)
            {
                beforeNativePostProcessPass.Setup(renderingData.cameraData);
                renderer.EnqueuePass(beforeNativePostProcessPass);
            }

            if(afterNativePostProcessPass != null)
            {
                afterNativePostProcessPass.Setup(renderingData.cameraData);
                renderer.EnqueuePass(afterNativePostProcessPass);
            }

        }

        public override void Create() 
        {
            this.name = "Ridah_PostProcess";

            afterSkyboxPass = null;
            beforeNativePostProcessPass = null;
            afterNativePostProcessPass = null;
#if UNITY_EDITOR
            if (config == null)
                return;
            config.OnDataChange = Create;//OnValidate() call this
#endif

            if (config.afterSkybox.Count > 0)
            {
                afterSkyboxPass = new PostProcessRenderPass(RenderPassEvent.AfterRenderingSkybox, config);
            }

            if (config.beforePostProcess.Count > 0)
            {
                beforeNativePostProcessPass = new PostProcessRenderPass(RenderPassEvent.BeforeRenderingPostProcessing, config);
            }

            if (config.afterPostProcess.Count > 0)
            {
                afterNativePostProcessPass = new PostProcessRenderPass(RenderPassEvent.AfterRenderingPostProcessing, config);
            }
        }
    }
}
