using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugWireframe : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Debug.DrawLine(this.transform.position, this.transform.position + Vector3.up, Color.red);
    }
}
