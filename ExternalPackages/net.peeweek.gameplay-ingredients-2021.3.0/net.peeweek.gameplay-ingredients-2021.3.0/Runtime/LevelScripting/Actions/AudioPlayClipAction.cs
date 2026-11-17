using UnityEngine;
using NaughtyAttributes;

namespace GameplayIngredients.Actions
{
    [AddComponentMenu(ComponentMenu.actionsPath + "Audio Play Clip Action")]
    [Callable("Audio", "Actions/ic-action-audio.png")]
    public class AudioPlayClipAction : ActionBase
    {
        public enum Action
        {
            Normal,
            Toggle
        }
        public Action action = Action.Normal;
        [NonNullCheck]
        public AudioSource Source;
        [ShowIf("isNormal")]
        public AudioClip Clip;
        [ShowIf("isNormal")]
        public bool isLoop = false;
        [ShowIf("isNormal")]
        public bool stop = false;
        [ShowIf("isNormal")]
        public bool RandomizePitch = false;
        [ShowIf("RandomizePitch")]
        public Vector2 PitchRange = new Vector2(0,3);
        [ShowIf("isNormal")]
        public bool RandomizeVolume = false;
        [ShowIf("RandomizeVolume")]
        public Vector2 VolumeRange = new Vector2(0, 1);

        public override void Execute(GameObject instigator = null)
        {
            if (Source == null)
            {
                Debug.LogWarning($"{this.gameObject.name}:{this.name} : Null ");
            }
            else
            {
                switch (action)
                {
                    default:
                    case Action.Normal:
                        NormalAction();
                        break;
                    case Action.Toggle:
                        Toggle();
                        break;
                }
            }
        }
        void Toggle()
        {
            if (Source.isPlaying)
            {
                Source.Stop();
            }
            else
            {
                Source.Play();
            }
        }
        void NormalAction()
            {
                Source.Stop();
                Source.loop = isLoop;
                if (RandomizePitch)
                    Source.pitch = Random.Range(PitchRange.x, PitchRange.y);
                if (RandomizeVolume)
                    Source.volume = Random.Range(VolumeRange.x, VolumeRange.y);
                if (Clip != null)
                    Source.clip = Clip;

                Source.Play();            
            if (stop)
            { Source.Stop(); }
        }
        public override string GetDefaultName()
        {
            if(Clip == null)
                return $"Play Audio Source: '{Source?.name}'";
            else
                return $"Play Audio clip '{Clip.name}' on '{Source?.name}'";
        }
        bool isNormal() { return action == Action.Normal; }
        bool isToggle() { return action == Action.Toggle; }
    }
}