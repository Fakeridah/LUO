using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ridah.Weapon
{
    public class ParticleWeapon : WeaponController
    {
        [Header("Particle Fire")]
        public ParticleSystem blueParticles;
        public ParticleSystem orangeParticles;
        public int fireRate = 250;

        [Header("Recoil")]
        public float recoil = 2;

        [Header("Fire Audio")]
        public AudioSource fireAudio;
        public AudioClip[] Clips;
        private ParticleSystem.EmissionModule blueEmissionModule;
        private ParticleSystem.EmissionModule orangeEmissionModule;

        public void OnEnable()
        {
            if (blueParticles != null) blueEmissionModule = blueParticles.emission;
            if (orangeParticles != null) orangeEmissionModule = orangeParticles.emission;
        }

        public override void UpdateWeapon()
        {
            base.UpdateWeapon();
            if (primary && fireAudio != null&& blueParticles.gameObject.activeInHierarchy)
            {
                fireAudio.pitch = .8f;
                if (!fireAudio.isPlaying)
                {
                    if (Clips.Length > 0)
                        fireAudio.clip = Clips[Random.Range(0, Clips.Length)];
                    fireAudio.Play();
                }
            }
            if (secondary && fireAudio != null && orangeParticles.gameObject.activeInHierarchy)
            {
                if (Clips.Length > 0)
                    fireAudio.clip = Clips[Random.Range(0, Clips.Length)];
                fireAudio.pitch = 3f;
                    fireAudio.Play();
            }
            Fire();
        }
        private void Fire()
        {
            if (timeToFire == 0)
            {
                //Primary fire
                if (blueParticles != null)
                {
                    if (primary)
                    {
                        //Spawn particles
                        blueEmissionModule.rateOverTimeMultiplier = fireRate;
                    }
                    else
                    {
                        //Don't spawn particles
                        blueEmissionModule.rateOverTimeMultiplier = 0;
                    }
                }

                //Secondary fire
                if (orangeParticles != null)
                {
                    if (secondary)
                    {
                        //Spawn particles
                        orangeEmissionModule.rateOverTimeMultiplier = fireRate;
                    }
                    else
                    {
                        //Don't spawn particles
                        orangeEmissionModule.rateOverTimeMultiplier = 0;
                    }
                }
            }
        }
    }
}