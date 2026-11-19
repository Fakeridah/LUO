using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    [SerializeField]
    private PortalPair portalPair;

    [SerializeField]
    private Image inPortalImg;

    [SerializeField]
    private Image outPortalImg;

    private void Start()
    {
        if (portalPair == null || portalPair.Portals == null ||
            portalPair.Portals.Length < 2 || inPortalImg == null ||
            outPortalImg == null)
            return;

        var portals = portalPair.Portals;

        inPortalImg.color = portals[0].GetColour();
        outPortalImg.color = portals[1].GetColour();

        inPortalImg.gameObject.SetActive(false);
        outPortalImg.gameObject.SetActive(false);
    }

    public void SetPortalPlaced(int portalID, bool isPlaced)
    {
        if (portalID < 0 || portalID > 1)
            return;

        if(portalID == 0)
        {
            if (inPortalImg != null)
                inPortalImg.gameObject.SetActive(isPlaced);
        }
        else
        {
            if (outPortalImg != null)
                outPortalImg.gameObject.SetActive(isPlaced);
        }
    }
}
