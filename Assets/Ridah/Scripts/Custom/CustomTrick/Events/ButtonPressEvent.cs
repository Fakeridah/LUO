using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameplayIngredients;

namespace GameplayIngredients.Events
{
    [AddComponentMenu("Ridah/Transform Event")]
    public class ButtonPressEvent : EventBase
    {
        public Callable[] buttonDownAction;
        public Callable[] buttonUpAction;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
        internal void ButtonDownAction()
        {
            Callable.Call(buttonDownAction, gameObject);
        }
        internal void ButtonUpAction()
        {
            Callable.Call(buttonUpAction, gameObject);
        }

    }
}