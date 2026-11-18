using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnOffScreen : MonoBehaviour
{
    public GameObject target;
    Material mt;
    Material targetMT;
    bool start = false;
    bool jishi = false;
    bool isChange = false;
    float ct = 0;
    private void Start()
    {
        if (target)
            targetMT = target.GetComponent<MeshRenderer>().material;
        else
            targetMT = GetComponent<MeshRenderer>().material;

        mt = new Material(Shader.Find("Shader Graphs/TurnOffTV")); ;
        targetMT.SetFloat("_TTime", 0);
    }
    private void Update()
    {
        if (target)
            targetMT = target.GetComponent<MeshRenderer>().material;
        else
            targetMT = GetComponent<MeshRenderer>().material;

        if (targetMT.shader == mt.shader)
        {
            if (Input.GetMouseButton(0) && isChange) {start = true; jishi = true;}
            if (jishi) {ct = Time.time; jishi = false; }
            if (start) targetMT.SetFloat("_TTime", Time.time- ct);
            if(Time.time - ct>1) isChange = false;
        }
        else
        {
            isChange = true;
            start = false;
        }
    }

}
