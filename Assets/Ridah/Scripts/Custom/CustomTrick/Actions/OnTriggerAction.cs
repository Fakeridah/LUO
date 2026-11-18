using NaughtyAttributes;
using UnityEngine;

namespace GameplayIngredients.Actions
{
    [AddComponentMenu("Ridah/OnTrigger Action")]
    [AdvancedHierarchyIcon("Packages/net.peeweek.gameplay-ingredients/Icons/Events/ic-event-trigger.png")]
    [RequireComponent(typeof(Collider))]
    public class OnTriggerAction : MonoBehaviour
    {
        public enum Action
        {
            DestroyObj
        }

        public Action action = Action.DestroyObj;

        [ShowIf("isDestroyObj")]
        public bool OnlyInteractWithTag = true;
        [ShowIf("isDestroyObj")]
        public float deathDelayTime;
        [EnableIf("OnlyInteractWithTag")]
        public string Tag = "Player";




        public void Execute(GameObject  Object)
        {
            if (Object == null)
            {
                return;
            }
            else
            {
                switch (action)
                {
                    default:
                    case Action.DestroyObj:
                        Destroy(Object, deathDelayTime);
                        break;
                }
            }
        }
        bool isDestroyObj() { return (action == Action.DestroyObj); }

        private void OnTriggerEnter(Collider other)
        {
            if (OnlyInteractWithTag && other.tag == Tag)
            {
                Execute(other.gameObject);
            }
            if (!OnlyInteractWithTag)
            {
                Execute(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
        }
    }
    
}
