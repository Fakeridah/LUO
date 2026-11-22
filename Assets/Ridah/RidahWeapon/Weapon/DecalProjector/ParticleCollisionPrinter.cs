using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ridah.Decal
{
    /**
    * The CollisionPrinter Component. Prints a projection under set conditions related to the collision of the object attached to this printer.
    */
    [ExecuteInEditMode]
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleCollisionPrinter : Printer
    {
        /**
        * Defines the orientation of the projection relative to the surface of the collision. Velocity will orient the projection as if its up is the direction the collision object is moving in. Random will orient the projection as if its up is random.
        */
        public RotationSource rotationSource;

        /**
        * Defines the percentage of particles that print projections. At 0, no particles will print, at 1, all will.
        */
        public float ratio = 1;

        private ParticleSystem partSystem;
        private float maxparticleCollisionSize;
        private List<ParticleCollisionEvent> collisionEvents;
        void Start()
        {
            //Grab Particle System
            partSystem = GetComponent<ParticleSystem>();

            if (Application.isPlaying)
            {
                //Initialize collision list
                collisionEvents = new List<ParticleCollisionEvent>();
                init();
            }
        }
        void Update()
        {
            // Log Setup Warnings
            if (partSystem.collision.enabled != true)
            {
                Debug.LogWarning("Particle system collisions must be enabled for the particle system to print decals");
            }
            else if (partSystem.collision.sendCollisionMessages != true)
            {
                Debug.LogWarning("Particle system must send collision messages for the particle system to print decals. This option can be enabled under the collisions menu.");
            }
        }

        void OnParticleCollision(GameObject other)
        {
            if (enabled && Application.isPlaying && ratio > 0)
            {
                int numCollisionEvents = partSystem.GetCollisionEvents(other, collisionEvents);

                int i = 0;
                while (i < numCollisionEvents)
                {
                    if (ratio == 1 || ratio > Random.Range(0f, 1f))
                    {
                        //Grab collision data
                        Vector3 position = collisionEvents[i].intersection;
                        Vector3 normal = collisionEvents[i].normal;
                        Transform surface = other.transform;

                        // Use the normal physics query mask so custom layers such as Player
                        // remain eligible for the follow-up surface raycast.
                        int layerMask = Physics.DefaultRaycastLayers;

                        // Collision intersections can lie on or slightly inside the collider.
                        // Start just outside the surface and cast back into it.
                        Vector3 normalDirection = normal.normalized;
                        if (normalDirection.sqrMagnitude > 0.0001f)
                        {
                            RaycastHit hit;
                            Vector3 rayOrigin = position + normalDirection * 0.1f;
                            if (Physics.Raycast(rayOrigin, -normalDirection, out hit, 0.3f, layerMask, QueryTriggerInteraction.Ignore))
                            {
                                position = hit.point;
                                normalDirection = hit.normal;
                                surface = hit.collider.transform;
                            }
                            else
                            {
                                // A valid particle collision should still print if the extra
                                // surface query misses due to contact-point precision.
                                position += normalDirection * 0.002f;
                            }

                            //Calculate our rotation
                            Vector3 rot;
                            if (rotationSource == RotationSource.Velocity && collisionEvents[i].velocity != Vector3.zero) rot = collisionEvents[i].velocity.normalized;
                            else if (rotationSource == RotationSource.Random) rot = Random.insideUnitSphere.normalized;
                            else rot = Vector3.up;

                            //Print
                            Print(position, Quaternion.LookRotation(-normalDirection, rot), surface, surface.gameObject.layer);
                        }
                    }
                    i++;
                }
            }
        }
    }
}
