using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;
using Ridah.Weapon;
using UnityEngine.VFX;
public class MagnetGun : WeaponController
{
    [SerializeField] private VisualEffect magnetAttract;
    [SerializeField] private VisualEffect magnetRepulse;
    [SerializeField] private float Force = 20f;
    [SerializeField] private float Permeability = 0.05f;
    [SerializeField] private float MaxForce = 1000f;
    [SerializeField] private Transform magnetPos;
    [SerializeField] private CharacterController cc;
    List<Magnet> magnets = new List<Magnet>();
    LZM player;
    // Start is called before the first frame update
    void Start()
    {
        magnetAttract?.SendEvent("MagnetStop");
        magnetRepulse?.SendEvent("MagnetStop1");
        player = LZM.Player;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0)&&lockShoot&& !Input.GetMouseButtonDown(1))
        {
            magnetAttract?.SendEvent("MagnetPlay");
        }
        if (Input.GetMouseButtonUp(0) && lockShoot && !Input.GetMouseButtonUp(1))
        {
            magnetAttract?.SendEvent("MagnetStop");
        }
        if (!Input.GetMouseButtonDown(0) && lockShoot && Input.GetMouseButtonDown(1))
        {
            magnetRepulse?.SendEvent("MagnetPlay1");
        }
        if (!Input.GetMouseButtonUp(0) && lockShoot && Input.GetMouseButtonUp(1))
        {
            magnetRepulse?.SendEvent("MagnetStop1");
        }
    }
    void FixedUpdate()
    {
        if (Input.GetMouseButton(0) && lockShoot&& !Input.GetMouseButton(1))
        {
            MagnetRepulse();
        }
        if (Input.GetMouseButton(1) && lockShoot&& !Input.GetMouseButton(0))
        {
            MagnetAttract();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInChildren<Magnet>() != null)
            magnets.Add(other.GetComponentInChildren<Magnet>());
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInChildren<Magnet>()!=null)
            magnets.Remove(other.GetComponentInChildren<Magnet>());
    }
    void MagnetAttract()
    {
        var magCount = magnets.Count;
        var accF2 = Vector3.zero;
        for (int i = 0; i < magCount; i++)
        {
            var m1 = magnets[i];
            if (m1.RigidBody == null)
                continue;

            var rb1 = m1.RigidBody;
            var f = CalculateGilbertForce(m1);
            var magnetForce = m1.MagnetForce * Force;
            var accF1 = f * magnetForce;
            accF2 -= f * magnetForce;
            if (accF1.magnitude > MaxForce)
            {
                accF1 = accF1.normalized * MaxForce;
            }
            rb1.AddForceAtPosition(accF1, m1.transform.position);
        }
        if (accF2.magnitude < 2000)//change!!!
        {
            player.MoveData.Velocity += accF2 / 20 * Time.deltaTime;//change!!!
        }
    }
    void MagnetRepulse()
    {
        var magCount = magnets.Count;
        var accF2 = Vector3.zero;
        for (int i = 0; i < magCount; i++)
        {
            var m1 = magnets[i];
            if (m1.RigidBody == null)
                continue;

            var rb1 = m1.RigidBody;
            var f = CalculateGilbertForce(m1);
            var magnetForce = m1.MagnetForce * Force;
            var accF1 = -f * magnetForce;
            accF2 += f * magnetForce;
            if (accF1.magnitude > MaxForce)
            {
                accF1 = accF1.normalized * MaxForce;
            }
            rb1.AddForceAtPosition(accF1, m1.transform.position);
        }
        if (accF2.magnitude < 2000)//change!!!
        {
            player.MoveData.Velocity += accF2 / 20 * Time.deltaTime;//change!!!
        }
    }
    Vector3 CalculateGilbertForce(Magnet magnet1)
    {
        var m1 = magnet1.transform.position;
        var m2 = magnetPos.position;
        var r = m2 - m1;
        var dist = r.magnitude;
        var part0 = Permeability * magnet1.MagnetForce * Force;
        var part1 = 4 * Mathf.PI * dist;

        var f = (part0 / part1);

        //if (magnet1.MagneticPole == magnet2.MagneticPole)
        //    f = -f;
        //
        //if (magnet1.MagneticPole == Magnet.Pole.Metal && magnet2.MagneticPole == Magnet.Pole.Metal)
        //    f = 0;

        return f * r.normalized;
    }
}
