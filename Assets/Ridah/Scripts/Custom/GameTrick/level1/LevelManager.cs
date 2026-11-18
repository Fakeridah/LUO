using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
public class LevelManager : MonoBehaviour
{
    public LensFlareComponentSRP lens;
    public GameObject[] objs;
    public GameObject[] toggleFalse1;
    public GameObject[] toggleFalse2;
    public GameObject[] toggleFalse3;
    Material[] materials;
    float nowTime;
    bool timeSet = true;
    float time = 0f;
    private void OnEnable()
    {
        if (lens == null) return;
        if(timeSet)
        { nowTime = Time.time; timeSet = false; }
        materials = new Material[objs.Length];
        for(int i =0;i<objs.Length;i++)
        {
            materials[i] = objs[i].GetComponent<MeshRenderer>().material;
        }
        for (int i = 0; i < toggleFalse1.Length; i++)
        {
            toggleFalse1[i].SetActive(false);
        }
    }
    private void Update()
    {
        time = (Time.time - nowTime)/1.5f ;
        if (time < 4)
        {
            lens.lensFlareData.elements[0].localIntensity = Mathf.Clamp(time / 10, 0, 0.3f);
            lens.lensFlareData.elements[1].localIntensity = Mathf.Clamp(time / 10, 0, 0.18f);
        }
        else
        {
            lens.lensFlareData.elements[0].localIntensity = .7f- time / 10;
            lens.lensFlareData.elements[1].localIntensity = .58f-time / 10;
        }
        lens.lensFlareData.elements[0].rotation += time * 50;
       
       if (time > 1.2 && time < 4.7)
        {
            materials[1].SetFloat("_iTime", 4.5f - (time - 1.2f));
            materials[2].SetFloat("_iTime", 4.5f - (time - 1.2f));
            materials[3].SetFloat("_iTime", 4.5f - (time - 1.2f));
        }
        else if (time > 4.7 && time < 6)
        {
            gameObject.GetComponent<MeshRenderer>().enabled = false;
            materials[0].SetFloat("_Square_Offset", (time - 4.7f) / 2);
            for (int i = 0; i < toggleFalse2.Length; i++)
            {
                toggleFalse2[i].SetActive(false);
            }
            materials[1].SetFloat("_iTime", 1f);
            materials[2].SetFloat("_iTime", 1f);
            materials[3].SetFloat("_iTime", 1f);
        }
        else if (time > 6 && time < 8)
        {
            materials[1].SetFloat("_iTime", 4f - time/2);
            materials[2].SetFloat("_iTime", 4f - time/2);
            materials[3].SetFloat("_iTime", 4f - time/2);
        }
        if (time > 8)
        {
            for (int i = 0; i < toggleFalse3.Length; i++)
            {
                toggleFalse3[i].SetActive(false);
            }
        }

    }
}
