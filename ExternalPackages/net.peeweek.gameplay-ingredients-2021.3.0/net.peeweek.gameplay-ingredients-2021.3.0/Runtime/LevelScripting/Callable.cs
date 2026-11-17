using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameplayIngredients
{
    public abstract class Callable : MonoBehaviour, ICallable
    {
        [CallableName]
        public string Name;

        public void Reset()
        {
            if(Name == string.Empty || Name == null)
                Name = GetDefaultName();
        }

        public abstract void Execute(GameObject instigator = null);
        public abstract new string ToString();

        public static void Call(Callable[] calls, GameObject instigator = null)
        {
            if (calls == null)
            {
                Debug.LogError("Cannot execute callable list: Null or Missing");
                return;
            }

            foreach (var call in calls)
            {
                if (call == null)
                {
                    // Missing optional event bindings are common in migrated
                    // scenes.  Skip them without turning scene activation into
                    // a runtime error.
                    Debug.LogWarning("Skipping a null or missing Callable event binding.");
                    continue;
                }

                if (GameplayIngredientsSettings.currentSettings.verboseCalls)
                    Debug.Log($"[CALL] : {call.gameObject.scene.name} : {call.gameObject.name} :> {call.GetType().Name} ({call.Name})");

                call.Execute(instigator);
            }
        }

        public static void Call(Callable call, GameObject instigator = null)
        {
            if (call != null)
                call.Execute(instigator);
            else
                Debug.LogError("Cannot execute call: Null or Missing");
        }

        [ContextMenu("Reset Callable Name")]
        private void MenuSetDefaultName()
        {
            Name = GetDefaultName();
        }

        public virtual string GetDefaultName()
        {
            return GetType().Name;
        }
    }
}

