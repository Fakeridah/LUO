using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;
public class PlayerFollowHorizontal : MonoBehaviour
{
    Vector3 deltaPosition;
    Vector3 prePosition;
    bool active = false;

    Transform parent;
    // Start is called before the first frame update
    void Start()
    {
        parent = transform.parent;
        prePosition = parent.transform.position;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        TranslateUpDown();
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            active = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Player")
        {
            active = false;
            LZM.Player.MoveData.Velocity += deltaPosition/Time.deltaTime;
        }
    }
    bool once = false;
    void TranslateUpDown()
    {
        if (active)
        {
            once = true;
            LZM.Player._isFollow = true;
            LZM.Player._followPosition = deltaPosition;
        }
        else if (once) { LZM.Player._isFollow = false; once = false; }
        
        deltaPosition = parent.transform.position - prePosition;
        prePosition = parent.transform.position;
    }
}
