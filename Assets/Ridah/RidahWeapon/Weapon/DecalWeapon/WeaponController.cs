using UnityEngine;
using System.Collections;
using UnityEngine.Audio;
namespace Ridah.Weapon
{
    //Weapon Controller
    public class WeaponController : MonoBehaviour
    {
        [HideInInspector]
        public bool lockShoot=false;

        //Input
        protected bool primary;
        protected bool secondary;
        protected bool alternate;

        //Projectile fire
        protected float timeToFire;

        void OnEnable()
        {
        }

        void Update()
        {
            //Check for Input
            primary = (Input.GetMouseButton(0) ? true : false)&& lockShoot;
            secondary = ((!Input.GetMouseButton(0) && Input.GetMouseButton(1)) ? true : false) && lockShoot;
            alternate = ((!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && Input.GetMouseButton(2)) ? true : false) && lockShoot;
        }

        public virtual void UpdateWeapon()
        {
            //Increment time since fired
            timeToFire = Mathf.Clamp(timeToFire - Time.fixedDeltaTime, 0, Mathf.Infinity);
        }
       
    }
}