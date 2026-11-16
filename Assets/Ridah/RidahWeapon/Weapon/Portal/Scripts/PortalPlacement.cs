using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;
using Ridah.Weapon;
public class PortalPlacement : WeaponController
{
    [SerializeField]
    private PortalPair portals;

    [SerializeField]
    private LayerMask raycastMask;

    [SerializeField]
    private string[] layerMask;

    [SerializeField]
    private Crosshair crosshair;

    [SerializeField]
    private Camera mainCamera;

    [SerializeField]
    private GameObject portalGun;

    private void Awake()
    {
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire1")&& portals.isActiveAndEnabled&& portals.Portals[0].placeEabled&& lockShoot&& portalGun.activeInHierarchy)
        {
            portals.Portals[0].vfx.localScale = 0.01f*Vector3.one;
            //portals.Portals[0].Renderer.enabled = false;
            FirePortal(0, mainCamera.transform.position, mainCamera.transform.forward, 250.0f);
        }
        else if (Input.GetButtonDown("Fire2") && portals.isActiveAndEnabled && portals.Portals[1].placeEabled&& lockShoot && portalGun.activeInHierarchy)
        {
            portals.Portals[1].vfx.localScale = 0.01f * Vector3.one;
            //portals.Portals[1].Renderer.enabled = false;
            FirePortal(1, mainCamera.transform.position, mainCamera.transform.forward, 250.0f);
        }
        //change!!!!!!!
        if (portals.Portals[0].Bullet) portals.Portals[0].Bullet.SetActive(!portals.Portals[0].isPlaced);
        if (portals.Portals[1].Bullet)portals.Portals[1].Bullet.SetActive(!portals.Portals[1].isPlaced);
    }

    private void FirePortal(int portalID, Vector3 pos, Vector3 dir, float distance)
    {
        RaycastHit hit;
        Physics.Raycast(pos, dir, out hit, distance, raycastMask);
        //GameObject bullet = Instantiate(portals.Portals[portalID].Bullet, portals.Portals[portalID].Bullet.transform);
        if (hit.collider != null)
        {
            //change!!!!!!!!
            if (1<<hit.transform.gameObject.layer == LayerMask.GetMask(layerMask[0]))
            {/*if (hit.collider.tag == "Portal")
            {
                var inPortal = hit.collider.GetComponent<Portal>();
                var outPortal = inPortal.GetOtherPortal();

                // Update position of raycast origin with small offset.
                Vector3 relativePos = inPortal.transform.InverseTransformPoint(hit.point + dir);
                relativePos = Quaternion.Euler(0.0f, 180.0f, 0.0f) * relativePos;
                pos = outPortal.transform.TransformPoint(relativePos);

                // Update direction of raycast.
                Vector3 relativeDir = inPortal.transform.InverseTransformDirection(dir);
                relativeDir = Quaternion.Euler(0.0f, 180.0f, 0.0f) * relativeDir;
                dir = outPortal.transform.TransformDirection(relativeDir);

                distance -= Vector3.Distance(pos, hit.point);

                FirePortal(portalID, pos, dir, distance);

                if (inPortal != portals.Portals[portalID])
                return;
            }*/

                var cameraRotation = mainCamera.transform.rotation;

                var portalRight = cameraRotation * Vector3.right;

                if (Mathf.Abs(portalRight.x) >= Mathf.Abs(portalRight.z))
                {
                    portalRight = (portalRight.x >= 0) ? Vector3.right : -Vector3.right;
                }
                else
                {
                    portalRight = (portalRight.z >= 0) ? Vector3.forward : -Vector3.forward;
                }

                var portalForward = -hit.normal;
                var portalUp = -Vector3.Cross(portalRight, portalForward);

                var portalRotation = Quaternion.LookRotation(portalForward, portalUp);

                portals.Portals[portalID].transform.SetParent(hit.transform);
                portals.Portals[portalID].PlacePortal(hit.collider, hit.point, portalRotation);
                crosshair.SetPortalPlaced(portalID, portals.Portals[portalID].isPlaced);
            }
            else
            {
                portals.Portals[portalID].gameObject.SetActive(false);
                portals.Portals[portalID].isPlaced = false;
                crosshair.SetPortalPlaced(portalID, portals.Portals[portalID].isPlaced);
            }
        }
    }
}
