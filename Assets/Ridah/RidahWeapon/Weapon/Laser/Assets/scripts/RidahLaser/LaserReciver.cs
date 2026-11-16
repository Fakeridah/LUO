using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using GameplayIngredients;
using GameplayIngredients.Actions;
namespace GameplayIngredients.Events
{
    [AddComponentMenu("Ridah/Laser Reciver Event")]
    [HelpURL(Help.URL + "LaserReciver")]
    public class LaserReciver : EventBase
    {
        public Callable[] onReceived;
        public Callable[] noReceived;
        public Callable[] onAttacked;
        public Callable[] noAttacked;
        internal bool isEnemy = false;
        // Start is called before the first frame update
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {
            if (GetComponentInChildren<LineRenderer>()&&!isEnemy)
                Callable.Call(onReceived, gameObject);
            else Callable.Call(noReceived, gameObject);

            if (GetComponentInChildren<LineRenderer>() && isEnemy)
                Callable.Call(onAttacked, gameObject);
            else Callable.Call(noAttacked, gameObject);

            isEnemy = false;
        }
    }
}
