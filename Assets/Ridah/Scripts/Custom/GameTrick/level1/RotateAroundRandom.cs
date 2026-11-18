using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateAroundRandom : MonoBehaviour
{
    public Transform target;
    public bool isRandom = true;
    public float speed;
    float[] randomSpeed;
    int length;
    int[] randomNumber;
    Transform[] childs;
    // Start is called before the first frame update
    void Start()
    {
        length = transform.childCount;
        childs = new Transform[length];
        randomSpeed = new float[length];
        for (int i = 0; i < length; i++)
        {
            childs[i] = transform.GetChild(i);
            childs[i].RotateAround(target.position, Vector3.up, (float)i / length*360);
            if(i>0)
            childs[i] .RotateAround(childs[i].position,Vector3.up, (float)i / length * 360);
            randomSpeed[i] = (float)Random.Range(20, 30)/500;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (isRandom)
        {
            for (int i = 0; i < length; i++)
            {
                childs[i].RotateAround(target.position, Vector3.up, randomSpeed[i]* speed);
            }
        }
        else
        {
            for (int i = 0; i < length; i++)
            {
                childs[i].RotateAround(target.position, Vector3.up, speed);
            }
        }
    }
}
