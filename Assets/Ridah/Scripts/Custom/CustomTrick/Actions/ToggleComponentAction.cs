using NaughtyAttributes;
using UnityEngine;

namespace GameplayIngredients.Actions
{
    [AddComponentMenu("Ridah/Toggle Component Action")]
    [Callable("Game Objects", "Actions/ic-action-list.png")]
    public class ToggleComponentAction : ActionBase
    {

        [ReorderableList]
        public ComponentToggle[] Targets;

        public override void Execute(GameObject instigator = null)
        {
            foreach (var target in Targets)
            {
                if (target.gameObject == null)
                {
                    Debug.LogWarning($"({gameObject.name}) > ToggleBehaviourAction ({this.Name}) Target is null, ignoring", this.gameObject);
                }
                else
                {
                    switch (target.State)
                    {
                        case ComponentToggle.ComponentToggleState.Disable:
                            switch (target.Type)
                            {
                                case ComponentToggle.ComponentType.Collider:
                                    target.gameObject.GetComponent<Collider>().enabled = false;
                                    break;
                                case ComponentToggle.ComponentType.Renderer:
                                    target.gameObject.GetComponent<Renderer>().enabled= false;
                                    break;
                                case ComponentToggle.ComponentType.Animation:
                                    target.gameObject.GetComponent<Animation>().enabled = false;
                                    break;
                            }
                            break;
                        case ComponentToggle.ComponentToggleState.Enable:
                            switch (target.Type)
                            {
                                case ComponentToggle.ComponentType.Collider:
                                    target.gameObject.GetComponent<Collider>().enabled = true;
                                    break;
                                case ComponentToggle.ComponentType.Renderer:
                                    target.gameObject.GetComponent<Renderer>().enabled = true;
                                    break;
                                case ComponentToggle.ComponentType.Animation:
                                    target.gameObject.GetComponent<Animation>().enabled = true;
                                    break;
                            }
                            break;
                        case ComponentToggle.ComponentToggleState.Toggle:
                            switch (target.Type)
                            {
                                case ComponentToggle.ComponentType.Collider:
                                    target.gameObject.GetComponent<Collider>().enabled =
                                        !target.gameObject.GetComponent<Collider>().enabled;
                                    break;
                                case ComponentToggle.ComponentType.Renderer:
                                    target.gameObject.GetComponent<Renderer>().enabled =
                                        !target.gameObject.GetComponent<Renderer>().enabled;
                                    break;
                                case ComponentToggle.ComponentType.Animation:
                                    target.gameObject.GetComponent<Animation>().enabled =
                                        !target.gameObject.GetComponent<Animation>().enabled;
                                    break;
                            }
                            break;
                    }
                }
            }
        }

        public override string GetDefaultName()
        {
            return $"Toggle Behaviours";
        }

        [System.Serializable]
        public struct ComponentToggle
        {
            [System.Serializable]
            public enum ComponentToggleState
            {
                Disable = 0,
                Enable = 1,
                Toggle = 2
            }
            public enum ComponentType
            {
                Collider = 0,
                Renderer = 1,
                Animation = 2
            }
            public GameObject gameObject; 
            public ComponentToggleState State;
            public ComponentType Type;
        }
    }
}
