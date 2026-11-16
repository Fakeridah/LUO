using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Pool;

namespace Ridah.Decal
{
    [AddComponentMenu("Ridah/need to do more things here in the future")]
    public class DecalContorller : MonoBehaviour
    {
        private ObjectPool<DecalContorller> decalPool;
        //public FadeType type;
        //public FadeWrapMode wrapMode;
        //public AnimationCurve fade = AnimationCurve.EaseInOut(0, 1, 1, 0);
        //public float fadeLength = 1;

        public DecalProjector[] decals = new DecalProjector[1];
        private float timeElapsed = 1;
        private float timeElapsed2 = 1;
        public float depth = 3.0f;//change!
        private void Awake()
        {
        }
        private void OnEnable()
        {
            StartCoroutine(OneSecond());
            StartCoroutine(FiveSeconds());
        }
        // Start is called before the first frame update
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {

        }
        private void SetScale(DecalProjector Projection, Vector3 Scale)
        {
            Projection.size = new Vector3(Scale.x,Scale.y, depth);
        }
        private void SetAlpha(DecalProjector Projection, float rate)
        {
            Projection.fadeFactor = rate;
        }
        private IEnumerator FiveSeconds()
        {
            timeElapsed = 5;
            while (timeElapsed>0)
            {
                timeElapsed -= Time.deltaTime;
                //Apply fade
                SetAlpha(decals[0], timeElapsed);
                yield return null;
            }
            decalPool.Release(this);
        }
        private IEnumerator OneSecond()
        {
            timeElapsed2 = 0;
            while (timeElapsed2 < .2)
            {
                timeElapsed2 += Time.deltaTime;
                //Apply fade
                SetScale(decals[0], Vector3.one* timeElapsed2*3*5);
                yield return null;
            }
        }
        public void SetPool(ObjectPool<DecalContorller> pool)
        {
            decalPool = pool;
        }
    }

    public enum FadeType { Alpha, Scale, Both };
    public enum FadeWrapMode { Once, Clamp, Loop, PingPong };
}