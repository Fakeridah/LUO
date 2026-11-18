using UnityEngine;
using NaughtyAttributes;


namespace GameplayIngredients.Actions
{
    public enum AnimationParameterType { Play, Rewind,Stop,Toggle,Rewind2 };
    [AddComponentMenu("Ridah/Set Animation Parameter Action")]
    [Callable("Game", "Actions/ic-generic-action.png")]
    public class AnimationAciton : ActionBase
    {
        public Animation animation;
        public string animationClipName;
        [OnValueChanged("OnParameterTypeChanged")]
        public AnimationParameterType parameterType = AnimationParameterType.Play;

        [ShowIf("isPlay")]
        public bool interrupt=false;
        [ShowIf("isPlayorRewind")]
        public bool playOnce = true;
        bool isPlayed = false;
        bool toggleInitState =true;

        public override void Execute(GameObject instigator = null)
        {
            if (animation == null)
                return;

            switch (parameterType)
            {
                case AnimationParameterType.Play:
                    if(!string.IsNullOrEmpty(animationClipName))
                    animation[animationClipName].speed = 1;
                    if (animation.isPlaying&& interrupt) animation[animationClipName].time = 0;
                    if(!isPlayed|| !playOnce)
                    animation.Play();
                    isPlayed = true;
                    break;
                case AnimationParameterType.Rewind:
                    if (!string.IsNullOrEmpty(animationClipName))
                    {
                        if (animation[animationClipName].time > animation[animationClipName].length)
                            animation[animationClipName].time = animation[animationClipName].length;
                        animation[animationClipName].speed = -1;
                        if(!animation.isPlaying)
                        animation[animationClipName].time = animation.GetClip(animationClipName).length;
                    }
                    if (!isPlayed || !playOnce)
                        animation.Play();
                    isPlayed = true;
                    break;
                case AnimationParameterType.Stop:
                    if (!string.IsNullOrEmpty(animationClipName))
                    {
                        animation[animationClipName].speed=0;
                    }
                    animation.Stop();
                    break;
                case AnimationParameterType.Toggle:
                    animation.Play();
                    if (!string.IsNullOrEmpty(animationClipName))
                    {
                        if(animation[animationClipName].time < 0)
                        animation[animationClipName].time = 0;
                        if (animation[animationClipName].time > animation[animationClipName].length)
                            animation[animationClipName].time = animation[animationClipName].length;
                        animation[animationClipName].speed = -animation[animationClipName].speed;
                    }
                    break;
                case AnimationParameterType.Rewind2:
                    if (!string.IsNullOrEmpty(animationClipName))
                    {
                        if (animation[animationClipName].time > animation[animationClipName].length)
                            animation[animationClipName].time = animation[animationClipName].length;
                        animation[animationClipName].speed = -1;
                    }
                    if (!isPlayed)
                        animation.Play();
                    isPlayed = true;
                    break;
            }       
        }
        private void Start()
        {
            if (string.IsNullOrEmpty(animationClipName))
                animationClipName = animation.clip.name;
            if (isToggle())
            animation[animationClipName].speed = -1;
        }
        bool isPlay() { return parameterType == AnimationParameterType.Play; }
        bool isRewind() { return parameterType == AnimationParameterType.Rewind; }
        bool isPlayorRewind() { return parameterType == AnimationParameterType.Rewind|| parameterType == AnimationParameterType.Play; }
        bool isToggle() { return parameterType == AnimationParameterType.Toggle; }
        bool isRewind2() { return parameterType == AnimationParameterType.Rewind2; }
    }
}