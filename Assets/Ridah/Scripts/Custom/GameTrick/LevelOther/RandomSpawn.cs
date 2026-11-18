using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class RandomSpawn : MonoBehaviour
{
    public Transform[] objs;
    // Start is called before the first frame update
    void Start()
    {
        List<int> indexs = new List<int>(objs.Length);
        List<Vector3> pos = new List<Vector3>(objs.Length);
        for (int i = 0; i < objs.Length; i++)
        {
            indexs.Add(i);
            pos.Add(objs[i].position);
        }
        for (int i = 0; i < objs.Length; i++)
        {
            var index = Random.Range(i, objs.Length);
            int num = indexs[i];
            indexs[i] = indexs[index];
            indexs[index] = num;
        }
        for (int i = 0; i < objs.Length; i++)
        {
            objs[i].position = pos[indexs[i]];
        }
    }

}
