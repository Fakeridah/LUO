using UnityEngine;
using GameplayIngredients;
using System.Collections.Generic;

namespace GameplayIngredients.Events
{
    [AddComponentMenu("Ridah/LevelTrick/On Animation Event")]
    public class SphereFractureAction : EventBase
    {
        public Animator animator;
        public Callable[] animationEndAction;
        public List<Vector3> randomPos;
        public float angle=0;
        bool played=false;
        int count;
        // Start is called before the first frame update
        void Start()
        {
            count = randomPos.Count;
            if (count > 0)
            {
                for(int i = 0; i < count; i++)
                {
                    randomPos[i] = Quaternion.AngleAxis(angle,Vector3.up)* randomPos[i];
                    randomPos[i] *= transform.lossyScale.x;
                    randomPos[i] += transform.position;
                }
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Main")) played = true;
            if (played)
            {
                if (animator.GetCurrentAnimatorStateInfo(0).IsName("Default"))
                {
                    played = false;
                    animator.SetBool("Fracture", false);
                    if(count>0)
                    this.transform.position = randomPos[getNonRepeatIndex()];
                    transform.Rotate(Vector3.up, Random.Range(30, 340));
                    if (transform.GetComponent<Collider>())
                    transform.GetComponent<Collider>().isTrigger = false;
                    Callable.Call(animationEndAction, gameObject);
                }
            }
        }
        int getNonRepeatIndex()
        {
            int index = (int)Random.Range(0, count - Mathf.Epsilon);
            foreach (var item in randomPos)
            {
                if (item == transform.position)
                {
                    if(index == randomPos.IndexOf(item))
                    index = getNonRepeatIndex();
                }
            }
            return index;
        }
    }
}