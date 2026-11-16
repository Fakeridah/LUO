using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class ToggleRenderFeature : MonoBehaviour
{
    public Renderer2DData renderer2DData;
    //public UniversalRendererData rendererData;
    public GameObject toggle;
    private void Update()
    {
        List<ScriptableRendererFeature> features = renderer2DData.rendererFeatures;

        foreach (var feature in features)
        {
            if (feature.GetType() == typeof(ScreenDisturbRendererFeature))
            {
                (feature as ScreenDisturbRendererFeature).SetActive(toggle.gameObject.activeInHierarchy) ;
            }
        }
    }
}