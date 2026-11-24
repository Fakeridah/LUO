using UnityEngine;
using UnityEngine.InputSystem;
using Ridah.Weapon;
using RidahFPS.Controllers;

public class PortalPlacement : WeaponController
{
    [SerializeField]
    private PortalPair portals;

    [SerializeField]
    private LayerMask raycastMask;

    [SerializeField]
    private Crosshair crosshair;

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private GameObject portalGun;

    private PlayerInput playerInput;
    private InputAction portalPrimaryAction;
    private InputAction portalSecondaryAction;

    private void Update()
    {
        if (portals == null || portals.Portals == null ||
            portals.Portals.Length < 2)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        CachePortalInputActions();
        bool firePrimary =
            (portalPrimaryAction != null &&
                portalPrimaryAction.WasPressedThisFrame()) ||
            (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame) ||
            (Gamepad.current != null &&
                Gamepad.current.buttonSouth.wasPressedThisFrame);
        bool fireSecondary =
            (portalSecondaryAction != null &&
                portalSecondaryAction.WasPressedThisFrame()) ||
            (Mouse.current != null &&
                Mouse.current.rightButton.wasPressedThisFrame) ||
            (Gamepad.current != null &&
                Gamepad.current.buttonEast.wasPressedThisFrame);

        bool pairActive = portals.isActiveAndEnabled;
        bool gunActive = portalGun == null || portalGun.activeInHierarchy;

        if (firePrimary)
            FirePortalFromInput(0, pairActive, gunActive);
        else if (fireSecondary)
            FirePortalFromInput(1, pairActive, gunActive);

        if (portals.Portals[0] != null && portals.Portals[0].Bullet != null)
            portals.Portals[0].Bullet.SetActive(!portals.Portals[0].isPlaced);
        if (portals.Portals[1] != null && portals.Portals[1].Bullet != null)
            portals.Portals[1].Bullet.SetActive(!portals.Portals[1].isPlaced);
    }

    private void CachePortalInputActions()
    {
        if (portalPrimaryAction != null && portalSecondaryAction != null)
            return;

        if (playerInput == null && mainCamera != null)
            playerInput = mainCamera.GetComponentInParent<PlayerInput>();
        if (playerInput == null && LZM.Player != null)
            playerInput = LZM.Player.GetComponent<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
            return;

        InputActionMap portalActionMap =
            playerInput.actions.FindActionMap("FirstRoom", false);
        if (portalActionMap == null)
            return;

        if (portalPrimaryAction == null)
            portalPrimaryAction = portalActionMap.FindAction(
                "PortalPrimary", false);
        if (portalSecondaryAction == null)
            portalSecondaryAction = portalActionMap.FindAction(
                "PortalSecondary", false);
    }

    /// <summary>
    /// Every gate between the click and PlacePortal is silent on its own, so a
    /// swallowed click is indistinguishable from a rejected one. Name the gate
    /// that stopped it instead.
    /// </summary>
    private void FirePortalFromInput(int portalID, bool pairActive,
        bool gunActive)
    {
        Portal portal = portals.Portals[portalID];
        if (!pairActive)
        {
            LogIgnored(portalID, "portal pair is inactive");
            return;
        }
        if (!gunActive)
        {
            LogIgnored(portalID, "portal gun is inactive");
            return;
        }
        if (portal == null)
        {
            LogIgnored(portalID, "no portal at this index");
            return;
        }
        if (!portal.placeEabled)
        {
            LogIgnored(portalID,
                "player is inside this portal's trigger");
            return;
        }
        if (!lockShoot)
        {
            LogIgnored(portalID, "weapon cooldown");
            return;
        }

        if (portal.vfx != null)
            portal.vfx.localScale = 0.01f * Vector3.one;
        FirePortal(portalID, mainCamera.transform.position,
            mainCamera.transform.forward, 250f);
    }

    private static void LogIgnored(int portalID, string reason)
    {
        Debug.Log("[PortalPlace] click " + portalID + " ignored: " + reason);
    }

    private void FirePortal(int portalID, Vector3 position, Vector3 direction,
        float distance)
    {
        Portal portal = portals.Portals[portalID];
        if (portal == null)
            return;

        int queryMask = raycastMask.value | portal.PlacementMask.value;
        RaycastHit hit = default;
        bool hitFound;
        // Probe the authored wall, not the carved collision. A portal hole is
        // real geometry, so a shot aimed at an existing portal would pass
        // straight through it and the click would be dropped - that is what
        // made re-placing a portal take several tries.
        Portal.BeginPlacementProbe();
        try
        {
            hitFound = Physics.Raycast(position, direction, out hit, distance,
                queryMask, QueryTriggerInteraction.Ignore);
        }
        finally
        {
            Portal.EndPlacementProbe();
        }
        if (!hitFound)
        {
            Debug.Log("[PortalPlace] click " + portalID +
                " missed: nothing within " + distance.ToString("F0") +
                " UU along the view ray");
            return;
        }

        int hitLayerMask = 1 << hit.collider.gameObject.layer;
        if ((portal.PlacementMask.value & hitLayerMask) == 0)
        {
            Debug.Log("[PortalPlace] click " + portalID +
                " ignored: " + hit.collider.name + " is on layer '" +
                LayerMask.LayerToName(hit.collider.gameObject.layer) +
                "', which the placement mask excludes");
            return;
        }

        Vector3 portalForward = -hit.normal;
        Vector3 portalRight = Vector3.ProjectOnPlane(
            mainCamera.transform.right, hit.normal);
        if (portalRight.sqrMagnitude < 0.0001f)
            portalRight = Vector3.ProjectOnPlane(mainCamera.transform.up,
                hit.normal);
        if (portalRight.sqrMagnitude < 0.0001f)
        {
            LogIgnored(portalID,
                "the view direction is parallel to the surface normal");
            return;
        }

        portalRight.Normalize();
        Vector3 portalUp = Vector3.Cross(portalForward, portalRight).normalized;
        Quaternion portalRotation = Quaternion.LookRotation(
            portalForward, portalUp);

        portal.PlacePortal(hit.collider, hit.point, portalRotation);
        if (crosshair != null)
            crosshair.SetPortalPlaced(portalID, portal.isPlaced);
    }

}
