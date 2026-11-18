using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class AutoLoadPipelineAsset : MonoBehaviour
{
    public UniversalRenderPipelineAsset pipelineAsset;
    //public CustomRenderPipelineAsset CustompPipelineAsset;

    // Start is called before the first frame update
    private void OnValidate()
    {
        if (pipelineAsset)
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
       // else if (CustompPipelineAsset)
            //GraphicsSettings.renderPipelineAsset = CustompPipelineAsset;

    }
    /*void OnEnable()
    {
        if (pipelineAsset)
        {
            GraphicsSettings.renderPipelineAsset = pipelineAsset;
        } 
        else if (CustompPipelineAsset)
            GraphicsSettings.renderPipelineAsset = CustompPipelineAsset;
    }*/
}
