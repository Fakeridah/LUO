using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Magnet : MonoBehaviour
{
    public enum Pole
    {
        North,
        South,
        Metal
    }

    public float MagnetForce;
    public Pole MagneticPole;
    public Rigidbody RigidBody;
    public float WorkDist=100;
    public float Permeability = 1;
    internal float workDist = 100;

    // Use this for initialization
    void Start ()
    {
        workDist = WorkDist == 0 ? 100 : WorkDist;
    }
	
	// Update is called once per frame
	void Update ()
    {
		
	}

    void OnDrawGizmos()
    {

    }
}
