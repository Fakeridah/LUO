using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SliderMagnetControl : MonoBehaviour
{
    [SerializeField] public RectTransform fill;
    [SerializeField] public RectTransform handle;
    [SerializeField] public Slider slider;
    [SerializeField] public MagnetWorld mw;
    internal float sliderValue=0;
    void Start()
    {
        slider.value = 1;
        sliderValue = 1;
        fill.anchorMax = new Vector2(1.1f, 1);
        handle.anchorMin =new Vector2(1,0);
        handle.anchorMax = new Vector2(1,1);
    }

    // Update is called once per frame
    void Update()
    {
    }
}
