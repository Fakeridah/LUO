using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserReflector : MonoBehaviour
{
    Vector3 position;
    Vector3 laserdirection;
    LineRenderer lr;

    public bool LaserHit;

    GameObject tempreflector;

    public bool laseropen;
    public int hitCount = 0;

    void Start()
    {
        laseropen = false;
        hitCount = 0;
        lr = GetComponent<LineRenderer>();
    }


    void Update()
    {
        if (laseropen)
        {
            if (LaserHit)
            {
                lr.positionCount = 2;
                lr.SetPosition(0, position);

                RaycastHit hit;

                if (Physics.Raycast(position, laserdirection, out hit, Mathf.Infinity))
                {
                    if (hit.transform.CompareTag("reflector"))
                    {
                        if (hit.transform.gameObject.GetComponent<LaserReflector>().hitCount<2)
                        {
                            tempreflector = hit.transform.gameObject;
                            tempreflector.GetComponent<LaserReflector>().LaserHit = true;
                            Vector3 temp = Vector3.Reflect(laserdirection, hit.normal); // vector3.reflect yansima için kullanilir yansimanin yerini gosterir
                            hit.collider.GetComponent<LaserReflector>().openray(hit.point, temp);
                        }
                    }
                    lr.SetPosition(1, hit.point);

                }
                else
                {
                    if (tempreflector)
                    {
                        tempreflector.GetComponent<LaserReflector>().closray();
                    }

                    lr.SetPosition(1, laserdirection * 100);
                }

                LaserHit = false;
            }
            else
            {
                lr.positionCount = 0;
            }

        }
        else
        {
            lr.positionCount = 0;
        }
            
    }

    public void openray(Vector3 pos, Vector3 dir)
    {
        laseropen = true;
        position = pos;
        laserdirection = dir;
    }
    public void closray()
    {
        laseropen = false;
        tempreflector = null;
    }
}
