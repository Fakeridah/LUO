using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;
using GameplayIngredients;
using GameplayIngredients.Events;
public class RaycastInteract : EventBase
{
    public float pickupDist = 10f;
    public Callable[] eventsTrue;
    public Callable[] eventsFalse;
    public bool trigger=false;

    void LateUpdate()
    {
        if (trigger)
        {
            Callable.Call(eventsTrue, gameObject);
            trigger = false;
        }
        else
        {
            Callable.Call(eventsFalse, gameObject);
        }
    }
}
