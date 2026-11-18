using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;
public class PlayerFollowVerticle : MonoBehaviour
{
    public float height = 0f;
    public float speed = 3f;
    public bool up = true;
    Vector3 deltaPosition;
    Vector3 prePosition;
    Vector3 startPosition;
    bool startAnimation = true;
    bool active = false;

    Transform parent;
    // Start is called before the first frame update
    void Start(){
        parent = transform.parent;
        prePosition = parent.transform.position;
        startPosition = parent.transform.position;
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
            startAnimation = true;
            active = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Player")
        {
            startAnimation = false;
            active = false;
            LZM.Player.MoveData.isGravity = true;
        }
    }
    void TranslateUpDown()
    {
        int dir = up ? 1 : -1;
        Vector3 dist = (parent.transform.position - startPosition);
        if ((active || !startAnimation))
            parent.transform.position += speed * Time.deltaTime * Vector3.up * (startAnimation ? 1 : -1) * dir;
        if ((dist.y < -0.01 || dist.magnitude > (height+0.01)) && up)
        {
            if (dist.y < 0)
                parent.transform.position = startPosition;
            else parent.transform.position = startPosition + height * Vector3.up;
            active = false;
            startAnimation = true;
        }
        else if ((dist.y > 0.01 || dist.magnitude > (height + 0.01)) && !up)
        {
            if (dist.y > 0)
                parent.transform.position = startPosition;
            else parent.transform.position = startPosition + height * Vector3.up * dir;
            active = false;
            startAnimation = true;
        }
        if (!up && (active) && LZM.Player.CC.isGrounded)
        {
            LZM.Player.MoveData.isGravity = false;
            LZM.Player.MoveData.Velocity.y -= 1;
        }
        //player.CC.Move(deltaPosition);
        deltaPosition = parent.transform.position - prePosition;
        prePosition = parent.transform.position;
    }
}
