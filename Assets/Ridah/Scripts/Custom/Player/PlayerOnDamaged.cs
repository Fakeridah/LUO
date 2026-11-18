using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Ridah.URPPostProcessing;
public class PlayerOnDamaged : MonoBehaviour
{
    [SerializeField] private Volume onDamagedVolume;
    [SerializeField] private VolumeProfile onDamagedVolumeProfile;
    ColorAdjustments colorAdjustments;
    GlitchVolume glitchVolume;
    VignetteAndChromaticAberration vignette;
    // Start is called before the first frame update
    private void Awake()
    {
        onDamagedVolumeProfile.TryGet(out colorAdjustments);
        onDamagedVolumeProfile.TryGet(out glitchVolume);
        onDamagedVolumeProfile.TryGet(out vignette);
    }
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        colorAdjustments.colorFilter.value = Color.white;

    }
}
