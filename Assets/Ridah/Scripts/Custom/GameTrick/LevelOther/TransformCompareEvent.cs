using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using UnityEngine;
namespace GameplayIngredients.Events
{
    [AddComponentMenu("Ridah/TransformCompare Event")]
    public class TransformCompareEvent : EventBase
    {
        public Callable[] resultTrue;
        public Callable[] resultFalse;


        public enum CompareType
        {
            Scale,
            AddForce,
            ResetScale
        }
        public CompareType action = CompareType.Scale;
        public bool callOnce = true;
        public Transform comparTrans;
        [ShowIf("isScale")]
        public Vector3 compareScale;
        private void Start()
        {
            if (comparTrans==null) comparTrans = transform;
        }
        bool callOnceTrigger = true;
        private void Update()
        {
            if (callOnceTrigger)
            {
                switch (action)
                {
                    default:
                    case CompareType.Scale:
                        if (comparTrans.localScale.magnitude > compareScale.magnitude)
                            Callable.Call(resultTrue);
                        else Callable.Call(resultFalse);
                        break;
                }
                if(callOnce)callOnceTrigger = false;   
            }
        }
        bool isScale() { return action == CompareType.Scale; }
    }
}
