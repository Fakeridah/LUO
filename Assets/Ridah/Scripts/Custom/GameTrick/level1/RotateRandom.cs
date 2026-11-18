using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

public class RotateRandom : MonoBehaviour
{
    public enum Action
    {
        randomRotate

    }
    public Action action = Action.randomRotate;
    public int randomNum=100;
    //[ShowIf("timeInternal")]
    public float InternalTime=12;

    int length;
    int[] randomNumber;
    Transform[] childs;

    public Material[] materials;
    void Start()
    {
        length = transform.childCount;
        childs = new Transform[length];
        for(int i =0;i<length;i++)
        {
            childs[i] = transform.GetChild(i);
            if(materials!=null)
            childs[i].GetComponent<MeshRenderer>().material = materials[Random.Range(0,materials.Length)];
        }
        if (randomNum > length) randomNum = length;
        randomNumber = new int[randomNum];
        for (int i =0;i<randomNum;i++)
        {
            int rand = (int)Random.Range(0, length);
            
                randomNumber[i] = rand;
        }
        InvokeRepeating("updateRandom",0, InternalTime);
    }

    // Update is called once per frame
    void Update()
    {
            switch (action)
            {
                default:
                case Action.randomRotate:
                randomRotate();
                    break;           
            }       
    }
    void randomRotate()
    {
        for (int i = 0; i < randomNum; i++)
        {
            int index = (int)randomNumber[i];
            childs[index].Rotate(0,30*Time.deltaTime,0);
        }
    }
    void updateRandom()
    {
        for (int i = 0; i < randomNum; i++)
        {
            int rand = (int)Random.Range(0, length);

            randomNumber[i] = rand;
            if (materials != null)
                childs[i].GetComponent<MeshRenderer>().material = materials[Random.Range(0, materials.Length)];
        }
    }
}
