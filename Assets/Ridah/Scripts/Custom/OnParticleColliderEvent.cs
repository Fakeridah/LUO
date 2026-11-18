using NaughtyAttributes;
using UnityEngine;using System.Collections;
using System.Collections.Generic;

namespace GameplayIngredients.Events
{
    [AddComponentMenu("Ridah/On Particle Collider Event")]
    [RequireComponent(typeof(Collider))]
    public class OnParticleColliderEvent : EventBase
    {
        public Callable[] onCollisionEnter;

        void OnParticleCollision(GameObject other)
        { 
            Callable.Call(onCollisionEnter, other);
          
        }
    }
}
