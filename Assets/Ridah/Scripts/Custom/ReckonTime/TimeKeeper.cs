using UnityEngine;

namespace GameplayIngredients
{
    [AddComponentMenu("Ridah/TimeKeeper")]
    [HelpURL(Help.URL + "timers")]
    [AdvancedHierarchyIcon("Packages/net.peeweek.gameplay-ingredients/Icons/Misc/ic-timer.png")]
    public class TimeKeeper : GameplayIngredientsBehaviour
    {
        public bool StartOnEnable = false;

        public bool TimeLimited = false;

        public uint Hours = 0;
        [Range(0, 59)]
        public uint Minutes = 1;
        [Range(0, 59)]
        public uint Seconds = 30;
        [Range(0, 999)]
        public uint Milliseconds = 0;

        public uint CurrentHours { get { return (uint)Mathf.Floor(m_TTL / 3600); } }
        public uint CurrentMinutes { get { return (uint)Mathf.Floor(m_TTL / 60) % 60; } }
        public uint CurrentSeconds { get { return (uint)Mathf.Floor(m_TTL) % 60; } }
        public uint CurrentMilliseconds { get { return (uint)((m_TTL % 1.0f) * 1000); } }


        public Callable[] OnStopped;
        public Callable[] OnInterrupt;
        public Callable[] OnStart;
        private bool startBool = false;
        float m_TTL = 0.0f;

        public bool isRunning => m_TTL > 0.0f;

        public void OnEnable()
        {
            if (StartOnEnable)
                Restart();
            else
                startBool = false;
        }


        public void Restart(GameObject instigator = null)
        {
            startBool = true;
               m_TTL = 0.0f;
            Callable.Call(OnStart, instigator);
        }

        public void Update()
        {
            if (m_TTL >= 0.0f&& startBool)
            {
                m_TTL += Time.deltaTime;
                if (m_TTL >= Hours * 3600 + Minutes * 60 + Seconds + Milliseconds * 0.001f && TimeLimited)
                {
                    m_TTL = Hours * 3600 + Minutes * 60 + Seconds + Milliseconds * 0.001f;
                    Callable.Call(OnStopped);
                }
            }
        }

        public void Interrupt(GameObject instigator = null)
        {
            if (m_TTL > 0.0f)
            {
                startBool = false;
                Callable.Call(OnInterrupt, instigator);
            }
        }
        public void ClearAndStop(GameObject instigator = null)
        {
            if (m_TTL >= 0.0f)
            {
                m_TTL = 0f;
                   startBool = false;
            }
        }
    }
}
