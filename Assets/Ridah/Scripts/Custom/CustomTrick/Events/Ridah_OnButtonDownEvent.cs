using UnityEngine;
using UnityEngine.InputSystem;
namespace GameplayIngredients.Events
{
#if !ENABLE_LEGACY_INPUT_MANAGER
    [WarnDisabledModule("Legacy Input Manager","Player Settings")]
#endif
    [AddComponentMenu("Ridah/On Button Down Event (Input System)")]
    public class RidahOnButtonDownEvent : EventBase
    {
        public string button;
        public InputActionAsset playerInput;

        public Callable[] OnButtonDown;
        public Callable[] OnButtonUp;

        private bool sustained = true;
        private bool triggerOnce = true;
#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            if (playerInput[button].ReadValue<float>() > 0 && sustained)
            { Callable.Call(OnButtonDown, gameObject); triggerOnce = true; sustained = false; }

            if (playerInput[button].ReadValue<float>() == 0 && triggerOnce)
            { Callable.Call(OnButtonUp, gameObject); triggerOnce = false; sustained = true; }
        }
#endif
    }
}


