using UnityEngine;
using NaughtyAttributes;


namespace GameplayIngredients.Actions
{
    public enum SetParticleAction { Emission };

    [AddComponentMenu("Ridah/Set Particle Parameter Action")]
    [Callable("Visual Effects", "Misc/ic-vfx.png")]
    public class ParticleAction : ActionBase
    {
        public SetParticleAction particleAction = SetParticleAction.Emission;
        [NonNullCheck]
        public ParticleSystem particle;
        [ShowIf("isEmission")]
        public bool emissionEnable;
        [ShowIf("isEmission")]
        public float emissionRate=0;


        public override void Execute(GameObject instigator = null)
        {
            if (particle == null)
                return;

            ParticleSystem.EmissionModule emission = particle.emission;
            switch (particleAction)
            {
                case SetParticleAction.Emission:
                    emission.enabled = emissionEnable;
                    if(emissionRate!=0)
                    emission.rateOverTimeMultiplier = emissionRate;
                    break;
            }
        }
        bool isEmission() { return particleAction == SetParticleAction.Emission; }
    }
}
