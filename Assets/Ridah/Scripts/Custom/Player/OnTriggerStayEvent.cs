using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;
using UnityEngine;
namespace GameplayIngredients.Events
{
    [AddComponentMenu("Ridah/On Trigger Event")]
    [RequireComponent(typeof(Collider))]
    public class OnTriggerStayEvent : EventBase
    {
        public Callable[] onTriggerEnter;
        public Callable[] onTriggerExit;

        public bool OnlyInteractWithTag = true;
        [EnableIf("OnlyInteractWithTag")]
        public string Tag = "Player";

        public enum TriggerStayAction
        {
            None,
            AddForce,
            ResetScale
        }
        public TriggerStayAction action = TriggerStayAction.None;
        [ShowIf("isAddForce")]
        public Transform forceDir;
        [ShowIf("isAddForce")]
        public float forceMag = 0f;
        [ShowIf("isResetScale")]
        public Transform referencePos;
        [ShowIf("isResetScale")]
        public float scaleDist = 3;
        bool sb = true;
        Vector3 transformsScale;
        [ShowIf("isResetScale")]
        public string excludeTag="Player";

        private void OnTriggerStay(Collider other)
        {
            if (OnlyInteractWithTag && other.tag == Tag)
            {
                Callable.Call(onTriggerEnter, other.gameObject);
            }
            if (!OnlyInteractWithTag)
            {
                Callable.Call(onTriggerEnter, other.gameObject);
                switch (action)
                {
                    default:
                    case TriggerStayAction.None:
                        break;
                    case TriggerStayAction.AddForce:
                        if(other.GetComponent<Rigidbody>())
                        other.GetComponent<Rigidbody>().AddForce(forceDir.forward*forceMag);
                        break;
                    case TriggerStayAction.ResetScale:
                        if (!other.CompareTag("Player")&& !other.CompareTag(excludeTag))
                        ResetScale(other);
                        break;
                }
            }
        }
        private void OnTriggerEnter(Collider other)
        {
            if (!OnlyInteractWithTag)
            {
                if (!other.CompareTag("Player")) sb = true;
            }
        }
        private void ResetScale(Collider other)
        {
            float dist = Vector3.Distance(other.transform.position, referencePos.position) / scaleDist;
            if (sb)
            {
                transformsScale = other.transform.localScale;
                if (dist < .5) transformsScale *= 10;
                    sb = false;
            }
            if (dist < 1 && dist > .1 && scaleDist != 0)
            {
                other.transform.localScale = transformsScale * dist;
            }

        }
        private void OnTriggerExit(Collider other)
        {
            if (OnlyInteractWithTag && other.tag == Tag)
            {
                Callable.Call(onTriggerExit, other.gameObject);
            }
            if (!OnlyInteractWithTag)
            {
                Callable.Call(onTriggerExit, other.gameObject);
            }
        }
        bool isAddForce() { return action == TriggerStayAction.AddForce; }
        bool isResetScale() { return action == TriggerStayAction.ResetScale; }
    }
}
