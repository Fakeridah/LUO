using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Ridah.Weapon
{
    public class ParticleBullet : WeaponController
    {
        [Header("Particle Bullet")]
        public ParticleSystem bullet;
        public int fireRate = 250;

        [Header("Recoil")]
        public float recoil = 2;

        [Header("Fire Audio")]
        public AudioSource fireAudio;
        public AudioClip[] Clips;
        private ParticleSystem.EmissionModule bulletEmissionModule;

        private ParticleSystem partSystem;
        private float maxparticleCollisionSize;
        private List<ParticleCollisionEvent> collisionEvents;
        public void OnEnable()
        {
            if (bullet != null) bulletEmissionModule = bullet.emission;
        }

        void Start()
        {
            //Grab Particle System
            partSystem = GetComponent<ParticleSystem>();

            if (Application.isPlaying)
            {
                //Initialize collision list
                collisionEvents = new List<ParticleCollisionEvent>();
            }
        }
        public override void UpdateWeapon()
        {
            base.UpdateWeapon();

            if (primary && fireAudio != null)
            {
                fireAudio.pitch = .8f;
                if (!fireAudio.isPlaying)
                {
                    if (Clips.Length > 0)
                        fireAudio.clip = Clips[Random.Range(0, Clips.Length)];
                    fireAudio.Play();
                }
            }
            Fire();
        }
        private void Fire()
        {
                //Primary fire
                if (bullet != null)
                {
                    if (primary)
                    {
                        //Spawn particles
                        bulletEmissionModule.rateOverTimeMultiplier = fireRate;
                    }
                    else
                    {
                        //Don't spawn particles
                        bulletEmissionModule.rateOverTimeMultiplier = 0;
                    }
                }
        }

        // Update is called once per frame
        private void OnParticleCollision(GameObject other)
        {

        }
    }
}
