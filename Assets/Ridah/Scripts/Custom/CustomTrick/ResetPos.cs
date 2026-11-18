using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;
public class ResetPos : MonoBehaviour
{
    private void OnEnable()
    {
        LZM.Player._resetPosition = transform.position;
         LZM.Player._resetRotation = transform.rotation.eulerAngles;
        LZM.Player._isReset = true;
    }
    private void OnDisable()
    {
        LZM.Player._isReset = false;
    }
}
