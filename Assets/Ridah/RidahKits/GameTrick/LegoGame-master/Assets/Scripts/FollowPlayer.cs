using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    public GameObject player;
    public bool isRotate = false;

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.position = player.transform.position;
        if (isRotate) transform.rotation = player.transform.rotation;
    }
}
