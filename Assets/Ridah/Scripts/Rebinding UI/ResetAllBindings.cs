using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

using GameplayIngredients;
[ManagerDefaultPrefab("RebindManager")]
public class ResetAllBindings : Manager
{
    [SerializeField]
    private InputActionAsset inputActions;
    
    public void ResetBindings()
    {
        foreach(InputActionMap map in inputActions.actionMaps)
        {
            map.RemoveAllBindingOverrides();
        }
        PlayerPrefs.DeleteKey("rebinds");
    }
}
