using UnityEngine;
using RidahFPS.Controllers;

[RequireComponent(typeof(LZM))]
public class PortablePlayer : MonoBehaviour
{
    private LZM player;
    private PlayerAiming playerAiming;

    public LZM Player => player != null ? player : (player = GetComponent<LZM>());

    public Vector3 GetEyePosition()
    {
        if (playerAiming == null)
            playerAiming = GetComponentInChildren<PlayerAiming>();
        return playerAiming != null
            ? playerAiming.transform.position
            : transform.position;
    }

    public void ApplyPortalEyeOffset(Vector3 transformedOldEyePosition)
    {
        if (playerAiming == null)
            playerAiming = GetComponentInChildren<PlayerAiming>();
        if (playerAiming != null)
            playerAiming.SetPortalEyeOffset(
                transformedOldEyePosition,
                playerAiming.transform.position);
    }

    private void Awake()
    {
        player = GetComponent<LZM>();
        playerAiming = GetComponentInChildren<PlayerAiming>();
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
