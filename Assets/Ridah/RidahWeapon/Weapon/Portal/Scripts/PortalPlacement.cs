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

        if (firePrimary && pairActive && gunActive &&
            portals.Portals[0] != null && portals.Portals[0].placeEabled &&
            lockShoot)
        {
            if (portals.Portals[0].vfx != null)
                portals.Portals[0].vfx.localScale = 0.01f * Vector3.one;
            FirePortal(0, mainCamera.transform.position,
                mainCamera.transform.forward, 250f);
        }
        else if (fireSecondary && pairActive && gunActive &&
            portals.Portals[1] != null && portals.Portals[1].placeEabled &&
            lockShoot)
        {
            if (portals.Portals[1].vfx != null)
                portals.Portals[1].vfx.localScale = 0.01f * Vector3.one;
            FirePortal(1, mainCamera.transform.position,
                mainCamera.transform.forward, 250f);
        }

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

    private void FirePortal(int portalID, Vector3 position, Vector3 direction,
        float distance)
    {
        RaycastHit hit;
        if (!Physics.Raycast(position, direction, out hit, distance,
            raycastMask, QueryTriggerInteraction.Ignore))
            return;

        int wallLayer = LayerMask.NameToLayer("Wall");
        if (wallLayer < 0 || hit.collider.gameObject.layer != wallLayer)
            return;

        Portal portal = portals.Portals[portalID];
        Vector3 portalForward = -hit.normal;
        Vector3 portalRight = Vector3.ProjectOnPlane(
            mainCamera.transform.right, hit.normal);
        if (portalRight.sqrMagnitude < 0.0001f)
            portalRight = Vector3.ProjectOnPlane(mainCamera.transform.up,
                hit.normal);
        if (portalRight.sqrMagnitude < 0.0001f)
            return;

        portalRight.Normalize();
        Vector3 portalUp = Vector3.Cross(portalForward, portalRight).normalized;
        Quaternion portalRotation = Quaternion.LookRotation(
            portalForward, portalUp);

        portal.PlacePortal(hit.collider, hit.point, portalRotation);
        if (crosshair != null)
            crosshair.SetPortalPlaced(portalID, portal.isPlaced);
    }

}
