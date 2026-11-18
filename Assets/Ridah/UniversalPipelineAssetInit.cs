using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
public class UniversalPipelineAssetInit : MonoBehaviour
{
    private UniversalRenderPipelineAsset pipelineAsset;
    [SerializeField] private float shadowDistance = 100;
    //[Tooltip("0 is deferred,1 is forward")]
    //[SerializeField] int rendererList=0;
    // Start is called before the first frame update
    void Start()
    {
        pipelineAsset = UniversalRenderPipeline.asset;
        pipelineAsset.shadowDistance = shadowDistance;
        //var cameraData = Camera.main.GetUniversalAdditionalCameraData();
        //cameraData.SetRenderer(rendererList);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
