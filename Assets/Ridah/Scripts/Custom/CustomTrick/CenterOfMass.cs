using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CenterOfMass : MonoBehaviour
{
    [SerializeField] private Vector3 centerOfMass;
    [SerializeField] private bool Awake;
    protected Rigidbody rb;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        rb.centerOfMass = centerOfMass;
        rb.WakeUp();
        Awake = !rb.IsSleeping();
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position + transform.rotation * centerOfMass, .1f);
    }
}
