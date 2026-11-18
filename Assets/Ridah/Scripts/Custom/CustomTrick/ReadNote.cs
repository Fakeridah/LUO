using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;
public class ReadNote : MonoBehaviour
{
    public enum Axis {x,y,z }
    public Axis axis = Axis.x; 
    public Transform readPos;
    Vector3 pos;
    float speed = 10;
    Transform cam;
    // Start is called before the first frame update
    void Start()
    {
        cam = Camera.main.transform;
        pos = Camera.main.transform.position + Camera.main.transform.forward * .1f;
    }

    // Update is called once per frame
    void Update()
    {
        pos = Camera.main.transform.position + Camera.main.transform.forward * .5f;
        transform.position = Vector3.MoveTowards(transform.position, pos, speed * Time.deltaTime);
        transform.LookAt(Camera.main.transform.position);
        switch (axis)
        {
            default:
            case Axis.x:
                transform.rotation = Quaternion.LookRotation(-cam.forward,cam.up);
                break;
            case Axis.y:
                transform.LookAt(Camera.main.transform.position);
                break;
            case Axis.z:
                transform.LookAt(Camera.main.transform.position);
                break;
        }
    }
}
