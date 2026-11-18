using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CursorFUCK : MonoBehaviour
{
    // Start is called before the first frame update
    private void OnEnable()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    private void OnDisable()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
}
