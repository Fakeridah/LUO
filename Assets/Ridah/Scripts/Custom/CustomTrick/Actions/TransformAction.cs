using NaughtyAttributes;
using UnityEngine;
using RidahFPS.Controllers;
using Ridah.Weapon;
using System.Collections.Generic;
using UnityEngine.Rendering;
namespace GameplayIngredients.Actions
{
    [AddComponentMenu("Ridah/Transform Action")]
    [HelpURL(Help.URL + "Transform")]
    [Callable("Game Objects")]
    public class TransformAction : ActionBase
    {
        public enum Action
        {
            SetPosition,
            SetRotation,
            SetScale,
            SetTRS,
            SetMaterials,
            ToggleMaterial,
            ColliderTriggerToggle,
            PortalClean,
            ToggleVolumeProfile,
            SetLayer,
            ResetScale,
            InstanceOBJ,
            ChangeSkybox,
            SetTag,
            MaterialBooleanSet,
            MaterialSetRenderQueue
        }

        public Action action = Action.SetPosition;
        [NonNullCheck]
        public Transform Object;
        [ShowIf("isNotSetTRS")]
        public Vector3 trans;
        bool isSetTRS() { return (action == Action.SetTRS && isTransform()); }
        bool isNotSetTRS() { return (action != Action.SetTRS&& isTransform()); }
        [ShowIf("isTransform")]
        public bool isUseReference = false;
        [ShowIf("isUseReference")]
        public Transform reference;
        [ShowIf("isMaterial")]
        public List<Material> materials;
        [ShowIf("isPortalClean")]
        public PortalPair portalPair;
        [ShowIf("isToggleVolumeProfile")]
        public Volume playerVolume;
        [ShowIf("isToggleVolumeProfile")]
        public VolumeProfile playerVolumeProfile;
        [ShowIf("isSetLayer")]
        public string targetLayer;
        [ShowIf("isResetScale")]
        public GameObject[] scaledObjD;
        [ShowIf("isResetScale")]
        public GameObject[] scaledObjX;
        [ShowIf("isResetScale")]
        public float scaleDist=3;
        bool sb = true;
        Vector3[] transformsScaleD;
        Vector3[] transformsScaleX;
        [ShowIf("isInstanceOBJ")]
        public GameObject[] instanceObj;
        [ShowIf("isChangeSkybox")]
        public Material newSkybox;
        [ShowIf("isSetTag")]
        public string targetTag;
        [ShowIf("isUseSingleMaterial")]
        public Material targetMaterial;
        [ShowIf("isMaterialBooleanSet")]
        public string keywordName;
        [ShowIf("isMaterialBooleanSet")]
        public bool keywordState;



        public override void Execute(GameObject instigator = null)
        {
            if (Object == null)
            {
                Debug.LogWarning($"{this.gameObject.name}:{this.name} : Null Transform");
            }
            else
            {
                switch (action)
                {
                    default:
                    case Action.SetPosition:
                        Object.position = trans;
                        if (isUseReference)
                            Object.position = reference.position;
                        break;
                    case Action.SetRotation:
                        Object.rotation = Quaternion.Euler(trans);
                        if (isUseReference)
                            Object.rotation = reference.rotation;
                        break;
                    case Action.SetScale:
                        Object.localScale = trans;
                        if (isUseReference)
                            Object.localScale = reference.localScale;
                        break;
                    case Action.SetTRS:
                        Object.position = reference.position;
                        Object.rotation = reference.rotation;
                        Object.localScale = reference.localScale;
                        break;
                    case Action.SetMaterials:
                        materialsChange();
                        break;
                    case Action.ToggleMaterial:
                        materialsToggle();
                        break;
                    case Action.ColliderTriggerToggle:
                        if(Object.GetComponent<Collider>())
                        {
                            if (Object.GetComponent<Collider>().isTrigger)Object.GetComponent<Collider>().isTrigger = false;
                            else Object.GetComponent<Collider>().isTrigger = true;
                        }
                        break;
                    case Action.PortalClean:
                        if (portalPair.Portals != null) { 
                        portalPair.Portals[0].isPlaced = false;
                        portalPair.Portals[1].isPlaced = false;
                        }
                        break;
                    case Action.ToggleVolumeProfile:
                        playerVolume.profile = playerVolumeProfile;
                        break;
                    case Action.SetLayer:
                        Object.gameObject.layer = (int)Mathf.Log(LayerMask.GetMask(targetLayer), 2);
                        break;
                    case Action.ResetScale:
                        ResetScale();
                        break;
                    case Action.InstanceOBJ:
                        int randomNum = Random.Range(0, instanceObj.Length);
                        Instantiate(instanceObj[randomNum], Object.position, Object.rotation, instanceObj[randomNum].transform.parent);
                        break;
                    case Action.ChangeSkybox:
                        RenderSettings.skybox = newSkybox;
                        break;
                    case Action.SetTag:
                        Object.gameObject.tag = targetTag;
                        break;
                    case Action.MaterialBooleanSet:
                        if (targetMaterial == null) targetMaterial = Object.gameObject.GetComponent<MeshRenderer>().material;
                        LocalKeyword booleanKey =new LocalKeyword(targetMaterial.shader,keywordName);
                        targetMaterial.SetKeyword(booleanKey, keywordState);
                        break;
                    case Action.MaterialSetRenderQueue:
                        if (targetMaterial == null) targetMaterial = Object.gameObject.GetComponent<MeshRenderer>().material;
                        targetMaterial.renderQueue = 5000;
                        break;
                }
            }
        }

        Material mt1;
        public void Awake()
        {
            if(Object.GetComponent<MeshRenderer>())
            mt1 = Object.GetComponent<MeshRenderer>().material;
        }
        private void materialsToggle()
        {
            if (mt1&& materials[0])
            {
                if (Object.GetComponent<MeshRenderer>().material.shader != materials[0].shader)
                {
                    mt1 = Object.GetComponent<MeshRenderer>().material;
                    if (Input.GetMouseButtonDown(0))
                    {
                        Object.GetComponent<MeshRenderer>().material = materials[0];
                    }
                }
                else if (Input.GetMouseButtonDown(0))
                {
                    Object.GetComponent<MeshRenderer>().material = mt1;
                }
            }

        }

        int i = 0;
        private void materialsChange()
        { 
            if (materials.Count!=0)
            {
                if (Input.GetMouseButtonDown(0)) i++;
                if (i >= materials.Count) i = 0;
              Object.GetComponent<MeshRenderer>().material = materials[i];
            }
        }
        private void ResetScale()
        {
            if (sb)
            {
                transformsScaleD = new Vector3[scaledObjD.Length];
                for (int i = 0; i < scaledObjD.Length; i++)
                {
                    transformsScaleD[i] = scaledObjD[i].transform.localScale;
                }
                transformsScaleX = new Vector3[scaledObjX.Length];
                for (int i = 0; i < scaledObjX.Length; i++)
                {
                    transformsScaleX[i] = scaledObjX[i].transform.localScale* 10;
                }
                sb = false;
            }
            for (int i = 0; i < scaledObjD.Length; i++)
            {
                float dist = Vector3.Distance(scaledObjD[i].transform.position, Object.position) / scaleDist;
                if (dist < 1 && dist > .1 && scaleDist != 0)
                {
                    scaledObjD[i].transform.localScale = transformsScaleD[i] * dist;
                }
            }
            for (int i = 0; i < scaledObjX.Length; i++)
            {
                float dist = Vector3.Distance(scaledObjX[i].transform.position, Object.position) / scaleDist;
                if (dist < 1 && dist > .1 && scaleDist != 0)
                {
                    scaledObjX[i].transform.localScale = transformsScaleX[i] * dist;
                }
            }
        }

        bool isTransform() { return (action == Action.SetPosition|| action == Action.SetRotation || action == Action.SetScale || action == Action.SetTRS); }
        bool isMaterial(){return (action == Action.SetMaterials)|| (action == Action.ToggleMaterial); }
        bool isPortalClean() { return (action == Action.PortalClean);}
        bool isToggleVolumeProfile() { return (action == Action.ToggleVolumeProfile); }
        bool isSetLayer() { return (action == Action.SetLayer); }
        bool isSetTag() { return (action == Action.SetTag); }
        bool isResetScale() { return action == Action.ResetScale; }
        bool isInstanceOBJ() { return action == Action.InstanceOBJ; }
        bool isChangeSkybox() { return action == Action.ChangeSkybox; }
        bool isMaterialBooleanSet() { return action == Action.MaterialBooleanSet; }
        bool isUseSingleMaterial() { return action == Action.MaterialBooleanSet|| action == Action.MaterialSetRenderQueue; }
    }
}