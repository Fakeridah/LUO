using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using GameplayIngredients.Rigs;

namespace GameplayIngredients
{
    [AddComponentMenu("Ridah/TimeKeeper Display Rig")]
    [HelpURL(Help.URL + "timers")]
    public class TimeKeeperDisplay : Rig
    {
        [NonNullCheck]
        public Text text;
        [NonNullCheck]
        public TextMesh textMesh;


        [NonNullCheck]
        public TimeKeeper timerKeeper;

        [InfoBox("Use the following wildcards:\n - %h : hours\n - %m : minutes\n - %s : seconds\n - %x : milliseconds", EInfoBoxType.Normal)]
        public string format = "%s:%x";

        public override UpdateMode defaultUpdateMode => UpdateMode.Update;

        public override int defaultPriority => 0;



        private void OnValidate()
        {
            UpdateText();
        }

        private void Reset()
        {
            UpdateText();
        }

        public override void UpdateRig(float deltaTime)
        {
            if (timerKeeper == null || (text == null && textMesh == null))
                return;

            UpdateText();
        }

        void UpdateText()
        {
            if(timerKeeper.CurrentMinutes>0)
             format = "%m:%s:%x";
            if (timerKeeper.CurrentHours > 0)
                format = "%h:%m:%s:%x";
            var value = format;

            uint hours = timerKeeper != null ? timerKeeper.CurrentHours : 0;
            uint minutes = timerKeeper != null ? timerKeeper.CurrentMinutes : 0;
            uint seconds = timerKeeper != null ? timerKeeper.CurrentSeconds : 0;
            uint milliseconds = timerKeeper != null ? timerKeeper.CurrentMilliseconds : 0;

            value = value.Replace("%h", hours.ToString("D2"));
            value = value.Replace("%m", minutes.ToString("D2"));
            value = value.Replace("%s", seconds.ToString("D2"));
            value = value.Replace("%x", milliseconds.ToString("D3"));

            if (text != null)
                text.text = value;

            if (textMesh != null)
                textMesh.text = value;
        }
    }
}

