using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CharacterController))]
public class PortablePlayer : MonoBehaviour
{
    private LZM player;
    private PlayerAiming playerAiming;

    private Portal inPortal;
    private Portal outPortal;
    protected new Collider collider;

    private static readonly Quaternion halfTurn = Quaternion.Euler(0.0f, 180.0f, 0.0f);

    private void Start()
    {
        player = LZM.Player;
        collider = GetComponent<CharacterController>();
        playerAiming = player.GetComponentInChildren<PlayerAiming>();
    }
    private void LateUpdate()
    {
    }

    public void SetIsInPortal(Portal inPortal, Portal outPortal, Collider wallCollider)
    {
        this.inPortal = inPortal;
        this.outPortal = outPortal;
        if(wallCollider!=null)
        Physics.IgnoreCollision(collider, wallCollider);
    }


    public void ExitPortal(Collider wallCollider)
    {
        if (wallCollider != null)
            Physics.IgnoreCollision(collider, wallCollider, false);
    }
    public virtual void Warp()
    {
        var inTransform = inPortal.transform;
        var outTransform = outPortal.transform;

        // Update position of object.
        Vector3 relativePos = inTransform.InverseTransformPoint(transform.position + player.CC.center);
        relativePos = halfTurn * relativePos;
        relativePos.z -= .16f;//for smooth portal
        player._isReset = true;
        player._resetPosition = outTransform.TransformPoint(relativePos) - player.CC.center ;

        //transform.position = outTransform.TransformPoint(relativePos);

        // Update rotation of object.
        Quaternion relativeRot = Quaternion.Inverse(inTransform.rotation) * Quaternion.Euler(playerAiming.realRotation);
        relativeRot = halfTurn * relativeRot;
        player._resetRotation = (outTransform.rotation * relativeRot).eulerAngles;
        if (player._resetRotation.z >90)//change!
            player._resetRotation.z -= 360;

        // Update velocity of rigidbody.
        Vector3 relativeVel = inTransform.InverseTransformDirection(player.MoveData.Velocity);
        relativeVel = halfTurn * relativeVel;
        player.MoveData.Velocity = outTransform.TransformDirection(relativeVel);

        ///*When GET through the portal door, If the direction of rotation is 
        ///*opposite to the direction of key press will get stuck , haven't solve it RN;

        // Swap portal references.
        var tmp = inPortal;
        inPortal = outPortal;
        outPortal = tmp;
    }
    public virtual void Warp2()
    {
        var inTransform = inPortal.transform;
        var outTransform = outPortal.transform;

        // Update position of object.
        Vector3 relativePos = inTransform.InverseTransformPoint(transform.position + player.CC.center);
        relativePos = halfTurn * relativePos;
        relativePos.z -= .36f + player.CC.height;//for smooth portal
        player._isReset = true;
        player._resetPosition = outTransform.TransformPoint(relativePos) - player.CC.center;

        //transform.position = outTransform.TransformPoint(relativePos);

        // Update rotation of object.
        Quaternion relativeRot = Quaternion.Inverse(inTransform.rotation) * Quaternion.Euler(playerAiming.realRotation);
        relativeRot = halfTurn * relativeRot;
        player._resetRotation = (outTransform.rotation * relativeRot).eulerAngles;
        if (player._resetRotation.z > 90)//change!
            player._resetRotation.z -= 360;

        // Update velocity of rigidbody.
        Vector3 relativeVel = inTransform.InverseTransformDirection(player.MoveData.Velocity);
        relativeVel = halfTurn * relativeVel;
        player.MoveData.Velocity = outTransform.TransformDirection(relativeVel);

        ///*When GET through the portal door, If the direction of rotation is 
        ///*opposite to the direction of key press will get stuck , haven't solve it RN;

        // Swap portal references.
        var tmp = inPortal;
        inPortal = outPortal;
        outPortal = tmp;
    }
    public virtual void Warp3()
    {
        var inTransform = inPortal.transform;
        var outTransform = outPortal.transform;

        // Update position of object.
        Vector3 relativePos = inTransform.InverseTransformPoint(transform.position + player.CC.center/2);
        relativePos = halfTurn * relativePos;
        relativePos.z -= .16f;//for smooth portal
        player._isReset = true;
        player._resetPosition = outTransform.TransformPoint(relativePos) - player.CC.center/2;

        //transform.position = outTransform.TransformPoint(relativePos);

        // Update rotation of object.
        Quaternion relativeRot = Quaternion.Inverse(inTransform.rotation) * Quaternion.Euler(playerAiming.realRotation);
        relativeRot = halfTurn * relativeRot;
        player._resetRotation = (outTransform.rotation * relativeRot).eulerAngles;
        if (player._resetRotation.z > 90)//change!
            player._resetRotation.z -= 360;

        // Update velocity of rigidbody.
        Vector3 relativeVel = inTransform.InverseTransformDirection(player.MoveData.Velocity);
        relativeVel = halfTurn * relativeVel;
        player.MoveData.Velocity = outTransform.TransformDirection(relativeVel);

        ///*When GET through the portal door, If the direction of rotation is 
        ///*opposite to the direction of key press will get stuck , haven't solve it RN;

        // Swap portal references.
        var tmp = inPortal;
        inPortal = outPortal;
        outPortal = tmp;
    }
    public virtual void Warp4()
    {
        var inTransform = inPortal.transform;
        var outTransform = outPortal.transform;

        // Update position of object.
        Vector3 relativePos = inTransform.InverseTransformPoint(transform.position + player.CC.center/2);
        relativePos = halfTurn * relativePos;
        relativePos.z -= .36f + player.CC.height;//for smooth portal
        player._isReset = true;
        player._resetPosition = outTransform.TransformPoint(relativePos) - player.CC.center/2;

        //transform.position = outTransform.TransformPoint(relativePos);

        // Update rotation of object.
        Quaternion relativeRot = Quaternion.Inverse(inTransform.rotation) * Quaternion.Euler(playerAiming.realRotation);
        relativeRot = halfTurn * relativeRot;
        player._resetRotation = (outTransform.rotation * relativeRot).eulerAngles;
        if (player._resetRotation.z > 90)//change!
            player._resetRotation.z -= 360;

        // Update velocity of rigidbody.
        Vector3 relativeVel = inTransform.InverseTransformDirection(player.MoveData.Velocity);
        relativeVel = halfTurn * relativeVel;
        player.MoveData.Velocity = outTransform.TransformDirection(relativeVel);

        ///*When GET through the portal door, If the direction of rotation is 
        ///*opposite to the direction of key press will get stuck , haven't solve it RN;

        // Swap portal references.
        var tmp = inPortal;
        inPortal = outPortal;
        outPortal = tmp;
    }
}
