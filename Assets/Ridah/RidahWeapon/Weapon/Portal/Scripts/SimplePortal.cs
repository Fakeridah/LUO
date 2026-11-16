using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimplePortal : MonoBehaviour
{
    [field: SerializeField]
    public SimplePortal otherPortal { get; private set; }
    public bool otherPlaced = false;
    private void Update()
    {
        otherPlaced = otherPortal.isActiveAndEnabled;
    }
}
