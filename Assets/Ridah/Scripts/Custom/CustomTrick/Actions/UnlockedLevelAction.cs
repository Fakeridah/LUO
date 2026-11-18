using UnityEngine;
using NaughtyAttributes;
using UnityEngine.UI;
namespace GameplayIngredients.Actions
{
    [AddComponentMenu("Ridah/Level Data")]
    [Callable("Data")]
    public class UnlockedLevelAction : ActionBase
    {
        public enum Action
        {
            Passed,
            IsPassed,
            ToggleLevelImg
        }
        public Action action = Action.Passed;
        [NonNullCheck]
        public UnlockedLevel levels;
        public string levelName;
        [ShowIf("isIsPassed")]
        public Callable[] pass;
        [ShowIf("isIsPassed")]
        public Callable[] unpass;
        [ShowIf("isToggleLevelImg")]
        public GameObject image;
        [ShowIf("isToggleLevelImg")]
        public Sprite defaultSprite;
        [ShowIf("isToggleLevelImg")]
        public Sprite passSprite;

        public override void Execute(GameObject instigator = null)
        {
            if (levels == null)
            {
                Debug.LogWarning($"{this.gameObject.name}:{this.name} : Null ");
            }
            else
            {
                switch (action)
                {
                    default:
                    case Action.Passed:
                        if(!levels.Level.Contains(levelName))
                        levels.Level.Add(levelName);
                        break;
                    case Action.IsPassed:
                        if (levels.Level.Contains(levelName)) Callable.Call(pass, instigator);
                        else Callable.Call(unpass, instigator);
                        break;
                    case Action.ToggleLevelImg:
                        Toggle();
                        break;
                }
            }
            void Toggle()
            {
                if (!levels.Level.Contains(levelName)&&image)
                    image.GetComponent<Image>().sprite = defaultSprite;
                else if(image) image.GetComponent<Image>().sprite = passSprite;
            }

        }

        bool isPassed() { return action == Action.Passed; }
        bool isIsPassed() { return action == Action.IsPassed; }
        bool isToggleLevelImg() { return action == Action.ToggleLevelImg; }
    }
}