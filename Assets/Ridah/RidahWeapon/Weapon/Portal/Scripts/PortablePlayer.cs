using UnityEngine;
using RidahFPS.Controllers;

[RequireComponent(typeof(LZM))]
public class PortablePlayer : MonoBehaviour
{
    private LZM player;
    private PlayerAiming playerAiming;
    private Camera playerCamera;
    private Portal portalEnvironment;
    private bool hasPendingPortalEyeOffset;
    private Vector3 pendingPortalEyePosition;

    public LZM Player => player != null ? player : (player = GetComponent<LZM>());
    public Portal PortalEnvironment => portalEnvironment;

    public Vector3 GetEyePosition()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
        return playerCamera != null
            ? playerCamera.transform.position
            : GetBaseEyePosition() + GetPortalEyeOffset();
    }

    /// <summary>
    /// CPortalGameMovement captures mv->GetAbsOrigin()+GetViewOffset before
    /// applying a teleport, without the render-only eye interpolation offset.
    /// </summary>
    public Vector3 GetMovementEyePosition()
    {
        return GetBaseEyePosition();
    }

    public Vector3 GetBaseEyePosition()
    {
        if (playerAiming == null)
            playerAiming = GetComponentInChildren<PlayerAiming>();
        if (playerAiming != null)
            return playerAiming.GetPortalBaseEyePosition();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
        return playerCamera != null
            ? playerCamera.transform.position
            : transform.position;
    }

    public Vector3 GetPortalEyeOffset()
    {
        if (playerAiming == null)
            playerAiming = GetComponentInChildren<PlayerAiming>();
        return playerAiming != null
            ? playerAiming.PortalEyeOffsetWorld
            : Vector3.zero;
    }

    public void QueuePortalEyeOffset(Vector3 transformedOldEyePosition)
    {
        pendingPortalEyePosition = transformedOldEyePosition;
        hasPendingPortalEyeOffset = true;
    }

    public void ApplyPendingPortalEyeOffset()
    {
        if (!hasPendingPortalEyeOffset)
            return;

        if (playerAiming == null)
            playerAiming = GetComponentInChildren<PlayerAiming>();
        if (playerAiming != null)
            playerAiming.SetPortalEyeOffset(
                pendingPortalEyePosition,
                GetBaseEyePosition());
        hasPendingPortalEyeOffset = false;
    }

    /// <summary>
    /// Tracks the portal collision environment selected by the movement
    /// crossing, as C_Portal_Player::m_hPortalEnvironment does.
    /// </summary>
    public void RecordPortalTransition(Portal exitPortal)
    {
        portalEnvironment = exitPortal;
    }

    public void SetPortalEnvironment(Portal environment)
    {
        portalEnvironment = environment;
    }

    private void Awake()
    {
        player = GetComponent<LZM>();
        playerAiming = GetComponentInChildren<PlayerAiming>();
        playerCamera = GetComponentInChildren<Camera>();
    }

    /// <summary>
    /// CPortalGameMovement transforms the player and view angles together at
    /// the end of the movement command. The visual portal and its render path
    /// stay owned by Portal; this applies only the player's exit orientation.
    /// </summary>
    public void ApplyPortalRotation(Quaternion portalRotation)
    {
        if (Player == null)
            return;

        if (playerAiming == null)
            playerAiming = Player.GetComponentInChildren<PlayerAiming>();
        if (playerAiming == null)
            return;

        Vector3 transformedViewAngles =
            (portalRotation * Quaternion.Euler(playerAiming.realRotation))
            .eulerAngles;
        playerAiming.realRotation = transformedViewAngles;

        if (playerAiming.bodyTransform != null)
        {
            playerAiming.bodyTransform.eulerAngles = Vector3.Scale(
                transformedViewAngles,
                new Vector3(0f, 1f, 0f));
        }

        playerAiming.transform.eulerAngles = transformedViewAngles;
    }
}
