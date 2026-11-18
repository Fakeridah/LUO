using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;
public class MedusaMove : MonoBehaviour
{
    Transform cam;
    Plane[] camerFrustum;
    Collider bounds;
    // Start is called before the first frame update
    void Start()
    {
        cam = Camera.main.transform;
        bounds = GetComponent<Collider>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 dir = (transform.position - cam.position).normalized;
        RaycastHit raycast;
        camerFrustum = GeometryUtility.CalculateFrustumPlanes(Camera.main);
        if (GeometryUtility.TestPlanesAABB(camerFrustum, bounds.bounds))
        {
            if (Physics.Linecast(cam.position, transform.position, out raycast))
            {
                if (raycast.transform.tag == "Enemy")
                {
                    LZM.Player.MoveEnabled = false;
                    LZM.Player.crouchingEnabled = false;
                    LZM.Player.jumpEnabled = false;
                    LZM.Player.MoveData.Velocity = Vector3.zero;
                }
            }
        }
        else
        {
            LZM.Player.MoveEnabled = true;
            LZM.Player.crouchingEnabled = true;
            LZM.Player.jumpEnabled = true;
        }

    }
}
