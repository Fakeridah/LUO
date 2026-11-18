using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HighlightAudio : MonoBehaviour,IPointerEnterHandler
{
    public AudioSource audio;
    public void OnPointerEnter(PointerEventData eventData)
    {
        audio.Play();
    }
}
