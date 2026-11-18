using NaughtyAttributes;
using UnityEngine;
using RidahFPS.Controllers;
using Ridah.Weapon;

namespace GameplayIngredients.Actions
{
    [AddComponentMenu("Ridah/Ridah Trick Action")]
    [HelpURL(Help.URL + "Players")]
    [Callable("Player")]
    public class RidahTrickAction : ActionBase
    {
        public enum Action
        {
            ProjectorMatch,
            LockView
        }

        public Action action = Action.ProjectorMatch;
        [ShowIf("isProjectorMatch")]
        public GameObject pic;
        [ShowIf("isProjectorMatch")]
        public Transform projector;
        [ShowIf("isProjectorMatch")]
        public Callable[] _matchAction;




        public override void Execute(GameObject instigator = null)
        {
                switch (action)
                {
                    default:
                    case Action.LockView:
                        matchAction();
                        break;

                }
        }
        private void Update()
        {
            if(action == Action.ProjectorMatch&&LZM.Player&& pic&& projector)
            {
                matchAction();
            }
        }
        void matchAction()
        {
            Transform trans = Camera.main.transform;
            if (pic.activeInHierarchy&&Vector3.Distance(projector.position, trans.position) < 0.2&&
               Quaternion.Angle( trans.rotation, projector.rotation)<1)
            {
                Callable.Call(_matchAction);
            }
        }
        bool isProjectorMatch() { return action == Action.ProjectorMatch; }

    }
}

