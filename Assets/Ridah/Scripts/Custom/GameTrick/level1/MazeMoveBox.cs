using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;
using GameplayIngredients.Events;
public class MazeMoveBox : MonoBehaviour
{
    public float force =1f;
    public float regressForce = 1f;
    Vector3 forceDir;
    Vector2 input;
    Rigidbody rigidBox;
    bool target = false;
    private SuperEnginal superEnginal;
    // Start is called before the first frame update
    void Start()
    {
        superEnginal = GetComponent<SuperEnginal>();
    }

    // Update is called once per frame
    void Update()
    {
        if (superEnginal.target != null && superEnginal.target.gameObject.activeInHierarchy)
        { 
            if (superEnginal.target.transform.tag == superEnginal.objTags[2])
            {
                rigidBox = superEnginal.target.GetComponent<Rigidbody>();
                //RaycastHit hit;
                Vector3 ratDir = (superEnginal.target.position - Camera.main.transform.position).normalized;
                Vector3 horizontalDir = Vector3.Cross(ratDir, Vector3.up).normalized;
                input = new Vector2(-Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")).normalized;
                forceDir = (horizontalDir * input.x + Vector3.up * input.y).normalized;

                Vector3 expectPos = superEnginal.originalDistance * Camera.main.transform.forward + Camera.main.transform.position;
                Vector3 regressDir = (expectPos- superEnginal.target.position).normalized;

                rigidBox.AddForce(force * forceDir+ regressForce* regressDir);
                if (superEnginal.target.transform.GetComponent<LaserReciver>())//&& ((int)rigidBox.constraints)==94
                {
                    float mouseX = Input.GetAxisRaw("Mouse X") * Time.fixedDeltaTime * 500f;
                    float mouseY = Input.GetAxisRaw("Mouse Y") * Time.fixedDeltaTime * 100f;
                    superEnginal.target.RotateAround(superEnginal.target.transform.position, Vector3.up, -mouseX);
                    //superEnginal.target.RotateAround(superEnginal.target.transform.position, superEnginal.player.transform.right, mouseY);
                }
            }
        }
    }
}
