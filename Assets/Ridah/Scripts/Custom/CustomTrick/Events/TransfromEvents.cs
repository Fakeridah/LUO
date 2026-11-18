using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using GameplayIngredients;
using GameplayIngredients.Actions;

namespace GameplayIngredients.Events
{
    [AddComponentMenu("Ridah/Transform Event")]
    public class TransfromEvent : MonoBehaviour
    {
        public Callable[] IsActive;
        public Callable[] NotActive;
        public List<GameObject> gameObjects;
        bool isActive = false;
        bool isOnce = true;
        bool notOnce = true;
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            foreach (var item in gameObjects)
            {
                isActive = item.activeInHierarchy;
                if (!isActive)
                    break;
            }
            if (isActive && isOnce)
            {
                Callable.Call(IsActive, gameObject);
                isOnce = false;
                notOnce = true;
            }

            else if(!isActive&&notOnce) { Callable.Call(NotActive, gameObject); isOnce = true; notOnce = false; }
        }
    }
}
