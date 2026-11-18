using NaughtyAttributes;
using UnityEngine;
using RidahFPS.Controllers;
using Ridah.Weapon;
using HeneGames.DialogueSystem;
namespace GameplayIngredients.Actions
{
    [AddComponentMenu("Ridah/Player Action")]
    [HelpURL(Help.URL + "Players")]
    [Callable("Player")]
    public class PlayerAction : ActionBase
    {
        public enum Action
        {
            LockJump,
            LockView,
            LockShoot,
            ResetSpawnPos,
            ResetPos,
            ResetSpeed,
            ResetEnabled,
            ResetScale,
            GravityShift,
            GravityEnabled,
            Freeze,
            CleanSuperTarget,
            ResetForce,
            DialogSelect,
            SelfSpeakSelect,
            PortalTransport
        }

        public Action action = Action.LockJump;
        [ShowIf("isLockJump")]
        public bool LockJump = true;
        [ShowIf("isResetPos")]
        public Transform resetPos;
        [ShowIf("isSetSpawnPos")]
        public Transform spawnPos;
        [ShowIf("isLockView")]
        public bool LockView = false;
        [ShowIf("isLockShoot")]
        public bool LockShoot= false;
        [ShowIf("isResetSpeed")]
        public float SpeedScale = 0.0f;
        [ShowIf("isResetSpeed")]
        public float clampSpeed = 1000.0f;
        [ShowIf("isResetSpeed")]
        public Transform transForward;
        [ShowIf("isResetEnabled")]
        public bool ResetEnabled = true;
        [ShowIf("isResetScale")]
        public bool useScale = false;
        [ShowIf("useScale")]
        public float scale = 1f;
        [ShowIf("isResetScale")]
        public bool useReference = false;
        [ShowIf("isResetScale")]
        public bool weaponScale = false;
        [ShowIf("useReference")]
        public Transform scalePos;
        [ShowIf("useReference")]
        public float scaleDist=1f;
        [ShowIf("weaponScale")]
        public GameObject[] _weapons; 
        Vector3[] transformsPos;
        Vector3[] transformsScale;
        bool sb = true;
        [ShowIf("isGravityEnabled")]
        public bool GravityEnabled=false;
        [ShowIf("isFreeze")]
        public bool freeze=true;
        [ShowIf("isResetForce")]
        public MazeMoveBox mmbox;
        [ShowIf("isResetForce")]
        public float originalForce = 1000;
        [ShowIf("isResetForce")]
        public float newForce = 1000;
        [ShowIf("isResetForce")]
        public bool isToggleForce = false;
        bool toggleForce = true;
        [ShowIf("isDialogSelect")]
        public bool dialogStart = true;
        [ShowIf("isDialogStart")]
        public DialogueManager dialogueManager;
        [ShowIf("isSelfSpeakSelect")]
        public bool SelfSpeakStart = true;
        [ShowIf("isSelfSpeakStart")]
        public DialogSpeakSelf dialogSpeakSelf;
        [ShowIf("isSelfSpeakSelect")]
        public bool manual = true;
        [ShowIf("isPortalTransport")]
        public Transform portal1;
        [ShowIf("isPortalTransport")]
        public Transform portal2;



        public override void Execute(GameObject instigator = null)
        {
            if (LZM.Player == null && LZM.Player.GetComponentInChildren<PlayerAiming>() == null&& LZM.Player.GetComponentInChildren<ParticleWeapon>())
            {
                Debug.LogWarning($"{this.gameObject.name}:{this.name} : Null Player");
            }
            else
            {
                switch (action)
                {
                    default:
                    case Action.LockJump:
                        LZM.Player.jumpEnabled = !LockJump;
                        break;
                    case Action.ResetPos:
                        ResetPos();
                        break;
                    case Action.ResetSpawnPos:
                        ResetSpawn();
                        break;
                    case Action.LockView:
                        LZM.Player.GetComponentInChildren<PlayerAiming>().lockView = LockView;
                        break;
                    case Action.LockShoot:
                        foreach (var weapons in LZM.Player.weapon)
                            weapons.lockShoot = !LockShoot;
                        break;
                    case Action.ResetSpeed:
                        LZM.Player.MoveData.Velocity = transForward.forward* LZM.Player.MoveData.Velocity.magnitude * SpeedScale;
                        LZM.Player.MoveData.Velocity = Vector3.ClampMagnitude(LZM.Player.MoveData.Velocity, clampSpeed);
                        break;
                    case Action.ResetEnabled:
                        LZM.Player.ResetEnabled = ResetEnabled;
                        break;
                    case Action.ResetScale:
                        ResetScale();
                        break;
                    case Action.GravityShift:
                        LZM.Player._useGravityEffect = true;
                        LZM.Player._reverseGravity = !LZM.Player._reverseGravity;
                        //player.cc.center = new Vector3(0, player.cc.height / 2 * (player._reverseGravity ? -1 : 1), 0);
                        break;
                    case Action.GravityEnabled:
                        LZM.Player._useGravityEffect = GravityEnabled;
                        break;
                    case Action.Freeze:
                        Freeze();
                        break;
                    case Action.CleanSuperTarget:
                        Transform target = LZM.Player.GetComponentInChildren<SuperEnginal>().target;
                        if (target)
                        {
                            if (target.GetComponent<Rigidbody>() != null)
                           target.GetComponent<Rigidbody>().isKinematic = false;
                            Physics.IgnoreCollision(target.GetComponent<Collider>(), LZM.Player.GetComponent<Collider>(), false);
                            target.GetComponent<Outline>().enabled = false;
                            LZM.Player.GetComponentInChildren<SuperEnginal>().target = null;
                        }
                        break;
                    case Action.ResetForce:
                        if (toggleForce) mmbox.regressForce = newForce;
                        else mmbox.regressForce = originalForce;
                        if (isToggleForce) toggleForce = !toggleForce;
                        break;
                    case Action.DialogSelect:
                        if (dialogStart)
                        {
                            DialogueUI.instance.StartDialogue(dialogueManager);
                        }
                        else 
                        {
                            DialogueUI.instance.currentDialogueManager.StopDialogue();
                            DialogueUI.instance.currentDialogueManager.choiceIsOn = true;
                        }
                        break;
                    case Action.SelfSpeakSelect:
                        if (SelfSpeakStart)
                        {
                            SpeakSelfUI.instance.StartDialogue(dialogSpeakSelf);
                            SpeakSelfUI.instance.manual = manual;
                        }
                        else
                        {
                            SpeakSelfUI.instance.currentDialogueManager.StopDialogue();
                            SpeakSelfUI.instance.currentDialogueManager.choiceIsOn = true;
                            SpeakSelfUI.instance.manual = manual;
                        }
                        break;
                    case Action.PortalTransport:
                        ResetPortalPos();
                        break;

                }
            }
        }

        private void ResetSpawn()
        {
            if (spawnPos != null)
            {
                LZM.Player._startPosition = spawnPos.position;
                LZM.Player._startRotation = spawnPos.rotation;
            }
            else
            {
                LZM.Player._startPosition = transform.position;
                LZM.Player._startRotation = transform.rotation;
            }
        }
        private void ResetPos()
        {
            LZM.Player._isReset = true;
            if (resetPos != null)
            {
                LZM.Player._resetPosition = resetPos.position;
                LZM.Player._resetRotation = resetPos.rotation.eulerAngles;
            }
        }
        private void ResetPortalPos()
        {
            if (Vector3.Distance(LZM.Player.transform.position, portal2.position) > .5)
                LZM.Player._isReset = true;
            if (portal1 != null&& portal2 != null)
            {
                LZM.Player._resetPosition = portal2.position+ LZM.Player.transform.position-portal1.position;
                LZM.Player._resetRotation = LZM.Player.GetComponentInChildren<PlayerAiming>().realRotation+ portal2.rotation.eulerAngles - portal1.rotation.eulerAngles;
            }
        }
        private void ResetScale()
        {
            if (sb)
            {
                transformsPos = new Vector3[_weapons.Length];
                transformsScale = new Vector3[_weapons.Length];
                for (int i = 0; i < _weapons.Length; i++)
                {
                    transformsPos[i] = _weapons[i].transform.localPosition;
                    transformsScale[i] = _weapons[i].transform.localScale;
                }
                sb = false;
            }
            if (useScale)
                LZM.Player.scale = scale;
            float dist = Vector3.Distance(LZM.Player.transform.position,scalePos.position)/ scaleDist;
            if (useReference && dist < 1 && dist > .1 && scaleDist!=0 && Mathf.Abs(dist - LZM.Player.scale)<.05)
            {
                LZM.Player._isChangeScale = true;
                LZM.Player.scale = dist;
                for(int i =0;i< _weapons.Length;i++)
                {
                    _weapons[i].transform.localPosition = transformsPos[i] *dist;
                    _weapons[i].transform.localScale = transformsScale[i] * dist;
                }
            }
        }
        private void Freeze()
        {
            if (freeze)
            {
                LZM.Player.MoveEnabled = false;
                LZM.Player.MoveData.crouchEnabled = false;
                LZM.Player.jumpEnabled = false;
            }
            else
            {
                LZM.Player.MoveEnabled = true;
                LZM.Player.MoveData.crouchEnabled = true;
                LZM.Player.jumpEnabled = true;
            }
        }
        bool isSetSpawnPos() { return action == Action.ResetSpawnPos; }
        bool isResetPos() { return action == Action.ResetPos; }
        bool isLockView() { return action == Action.LockView; }
        bool isLockShoot() { return action == Action.LockShoot; }
        bool isLockJump() { return action == Action.LockJump; }
        bool isResetSpeed() { return action == Action.ResetSpeed; }
        bool isResetEnabled() { return action == Action.ResetEnabled; }
        bool isResetScale() { return action == Action.ResetScale; }
        bool isShiftGravity() { return action == Action.GravityShift; }
        bool isGravityEnabled() { return action == Action.GravityEnabled; }
        bool isFreeze() { return action == Action.Freeze; }
        bool isResetForce() { return action == Action.ResetForce; }
        bool isDialogSelect() { return action == Action.DialogSelect; }
        bool isDialogStart() { return action == Action.DialogSelect&& SelfSpeakStart; }
        bool isSelfSpeakSelect() { return action == Action.SelfSpeakSelect; }
        bool isSelfSpeakStart() { return action == Action.SelfSpeakSelect && SelfSpeakStart; }
        bool isPortalTransport() { return action == Action.PortalTransport; }

    }
}

