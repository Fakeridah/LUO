using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomScale : MonoBehaviour
{
    struct Pillow
    {
        public GameObject[] randObj;
        public float[] randNum;
        public bool[] once;
        public int[] dir;
        
    }

    public GameObject[] randObj;
    Pillow pillow = new Pillow();
    int lenth;
    int randIndex;
    bool start =false;
    // Start is called before the first frame update
    void Start()
    {
        lenth = randObj.Length;
        pillow.randNum = new float[lenth];
        pillow.once = new bool[lenth];
        pillow.dir = new int[lenth]; 
        for (int i = 0; i < lenth; i++)
        {
            pillow.once[i] = true;
            pillow.dir[i] = 1;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (start) {
            for (int i = 0; i < lenth; i++)
            {
                if (randObj[i].transform.localScale.y <= pillow.randNum[i])
                    randObj[i].transform.localScale += Time.deltaTime * Vector3.up* .5f;
            }
        }
        else  {
            for (int i = 0; i < lenth; i++)
            {
                if (randObj[i].transform.localScale.y >= .1f)
                    randObj[i].transform.localScale -= Time.deltaTime * Vector3.up* .5f;
            }
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        start = true;
        for (int i=0;i<lenth;i++)
        {
            pillow.randNum[i] = Random.Range(.2f, 2.2f);
        }
        randIndex= Random.Range(0, lenth-1);
    }
    private void OnTriggerExit(Collider other)
    {
        start = false;
    }
}
