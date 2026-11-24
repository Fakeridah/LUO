using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : PortalableObject
{
    private CameraMove cameraMove;

    protected override void Awake()
    {
        base.Awake();

        cameraMove = GetComponent<CameraMove>();
    }

    public override void Warp()
    {
        base.Warp();
    }

    protected override void OnPortalTeleported(
        Portal entryPortal,
        Portal exitPortal,
        Quaternion portalRotation)
    {
        if (cameraMove != null)
            cameraMove.ResetTargetRotation();
    }
}
