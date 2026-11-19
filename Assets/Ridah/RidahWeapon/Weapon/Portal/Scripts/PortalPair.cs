using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PortalPair : MonoBehaviour
{
    public Portal[] Portals { private set; get; }

    private void Awake()
    {
        // Portal.Start disables unplaced portal children, and the pair itself
        // is also commonly activated by a gameplay event. Include inactive
        // children so the firing controller always receives both portals.
        Portals = GetComponentsInChildren<Portal>(true);

        if(Portals.Length != 2)
        {
            Debug.LogError("PortalPair children must contain exactly two Portal components in total.");
        }
    }
}
