using NaughtyAttributes;
using UnityEngine;
using System.Collections.Generic;
using RidahFPS.Controllers;
using Ridah.Weapon;

namespace GameplayIngredients.Actions
{
    [AddComponentMenu("Ridah/Pickup Action")]
    [HelpURL(Help.URL + "Pickup")]
    [Callable("Player")]
    public class PickupAction : ActionBase
    {
        public enum Action
        {
            PutIntoMenu,
            CleanAll,
            AddElement,
            SetAcitve,
            DropItem,
            SetTrigger
        }
        public Action action = Action.PutIntoMenu;
        public RingMenu ringMenu;
        [ShowIf("useElement")] 
        public RingElement element;
        [ShowIf("isDropItem")]
        public bool dropAll = false;
        [ShowIf("isPutIntoMenu")]
        public GameObject destroyOBJ;
        [ShowIf("isCleanAll")]
        public int index;
        [ShowIf("isAddElement")]
        public List<RingElement> elements;
        [ShowIf("isSetTrigger")]
        public bool trigger = true;




        public override void Execute(GameObject instigator = null)
        {
            if (ringMenu == null)
            {
                Debug.LogWarning($"{this.gameObject.name}:{this.name} : Null ");
            }
            else
            {
                switch (action)
                {
                    default:
                    case Action.PutIntoMenu:
                        instance();
                        break;
                    case Action.CleanAll:
                        cleanAll();
                        break;
                    case Action.AddElement:
                        if (elements.Count != 0) { 
                            foreach (var ele in elements) {
                                ringMenu.Elements.Add(ele);
                            }
                        }
                        break;
                    case Action.SetAcitve:
                        SetAcitve();
                        break;
                    case Action.DropItem:
                        dropItem();
                        break;
                    case Action.SetTrigger:
                        element.trigger = trigger;
                        break;
                }
            }

        }

        private void dropItem()
        {
            for (int i = 0; i < ringMenu.Elements.Count; i++)
            {
                if (ringMenu.Elements[i].name == element.name)
                {
                    ringMenu.Elements.RemoveAt(i);
                    ringMenu.initPiece();
                    if (!dropAll) break;
                    i--;
                }
            }
        }

        private void SetAcitve()
        {
            foreach (var ele in ringMenu.Elements)
            {
                ele.gameObject.SetActive(false);
            }
            if(element)
            element.gameObject.SetActive(true);
            else gameObject.SetActive(true);
        }

        private void cleanAll()
        {
           ringMenu.Elements.RemoveRange(index, ringMenu.Elements.Count-index);
        }

        private void instance()
        {
            if(element)ringMenu.Elements.Add(element);
            if(destroyOBJ) destroyOBJ.SetActive(false);
                ringMenu.initPiece();
        }


        bool isPutIntoMenu() { return action == Action.PutIntoMenu; }
        bool isCleanAll() { return action == Action.CleanAll; }
        bool isAddElement() { return action == Action.AddElement; }
        bool isSetAcitve() { return action == Action.SetAcitve || isPutIntoMenu(); }
        bool isDropItem() { return action == Action.DropItem ||  action == Action.SetAcitve; }
        bool isSetTrigger() { return action == Action.SetTrigger; }
        bool useElement() { return isPutIntoMenu() || isDropItem()|| isSetTrigger(); }

    }
}

