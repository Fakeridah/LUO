using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SizeMatchUnlock : MonoBehaviour
{
    public GameObject target;
    public float errorSize=.2f;
    public float errorAngle = 5f;
    public float matchDist=.5f;
    void Start()
    {
        
    }
    void Update()
    {
        if (transform.localScale.x > target.transform.localScale.x- errorSize
            && transform.localScale.x < target.transform.localScale.x
            && (transform.position - target.transform.position).magnitude < matchDist)
        {
            if ((Mathf.Abs(transform.rotation.eulerAngles.x % 180) < (target.transform.rotation.eulerAngles.x+ errorAngle)
                || Mathf.Abs((transform.rotation.eulerAngles.x + errorAngle) % 180) < (target.transform.rotation.eulerAngles.x + errorAngle))
                && (Mathf.Abs(transform.rotation.eulerAngles.z % 180) < (target.transform.rotation.eulerAngles.z + errorAngle)
                || Mathf.Abs((transform.rotation.eulerAngles.z + errorAngle) % 180) < (target.transform.rotation.eulerAngles.z + errorAngle)) 
                && Mathf.Abs(transform.rotation.eulerAngles.y % 180) < (target.transform.rotation.eulerAngles.y + errorAngle)
                && Mathf.Abs(transform.rotation.eulerAngles.y % 180) > (target.transform.rotation.eulerAngles.y - errorAngle))
            {
                target.gameObject.SetActive(true);
                transform.gameObject.SetActive(false);
            }
        }
    }
}
