using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

[ExecuteInEditMode]
public class Rotater : MonoBehaviour
{
    public Vector3 speed = Vector3.zero;
    void Update()
    {
            transform.Rotate(speed);
    }
}
