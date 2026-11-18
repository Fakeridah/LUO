using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace RidahFPS.Controllers
{
    public class TurretController : MonoBehaviour
    {
        public enum VH { xy,zy }
        [SerializeField] private Transform turretTrans;
        [SerializeField] private Transform cameraTrans;
        [SerializeField] private VH vh = VH.xy;
        [SerializeField] private Vector3 axisCorrect;
        public bool turretPlay=false;
        LZM player;
        float turretLerp = 0f;


        [Header("Sensitivity")]
        float sensitivityMultiplier = 1f;
        float horizontalSensitivity = 1f;
        float verticalSensitivity = 1f;

        [Header("Restrictions")]
        public float minYRotation = -90f;
        public float maxYRotation = 90f;

        [Header("Dont change this")]
        public float xuanzhuanmuma = 0f;
        //The real rotation of the camera without recoil
        internal Vector3 realRotation;
        Vector3 initRot;
        Vector3 initPos;
        void Start()
        {
            player = LZM.Player;
            sensitivityMultiplier = player.gameObject.GetComponentInChildren<PlayerAiming>().sensitivityMultiplier;
            horizontalSensitivity = player.gameObject.GetComponentInChildren<PlayerAiming>().horizontalSensitivity;
            verticalSensitivity = player.gameObject.GetComponentInChildren<PlayerAiming>().verticalSensitivity;
            initRot = turretTrans.rotation.eulerAngles;
            initPos = turretTrans.position;
            realRotation = initRot;
        }
        // Update is called once per frame
        void Update()
        {
            if (!RidahSimpleCameraController.instance.noClip)
            {
                turretPlay = cameraTrans.gameObject.activeInHierarchy;
                player.gameObject.GetComponent<LZM>().enabled = !turretPlay;
                player.gameObject.GetComponentInChildren<PlayerAiming>().enabled = !turretPlay;
                player.Canvas.SetActive(!turretPlay);

                turretLerp = Mathf.Lerp(turretLerp, turretPlay ? 1 : 0, Time.deltaTime/5);
                if (turretPlay)
                {
                    float xMovement = Input.GetAxisRaw("Mouse X") * horizontalSensitivity * sensitivityMultiplier * player.MoveData.reverseGravity;
                    float yMovement = -Input.GetAxisRaw("Mouse Y") * verticalSensitivity * sensitivityMultiplier * player.MoveData.reverseGravity;

                    // Calculate real rotation from input
                    realRotation = new Vector3(Mathf.Clamp(realRotation.x + yMovement, minYRotation, maxYRotation), realRotation.y + xMovement+xuanzhuanmuma, realRotation.z);
                    //realRotation.z = Mathf.Lerp(realRotation.z, 0f, Time.deltaTime * 3f);


                    switch (vh)
                    {
                        default:
                        case VH.xy:
                            turretTrans.rotation = Quaternion.Euler(realRotation);
                            break;
                        case VH.zy:
                            turretTrans.rotation = Quaternion.Euler(new Vector3(0, realRotation.y, -realRotation.x));
                            break;
                    }
                    Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, cameraTrans.position, turretLerp);
                    Camera.main.transform.rotation = Quaternion.Lerp(Camera.main.transform.rotation, Quaternion.Euler(realRotation + axisCorrect), turretLerp);

                }
                else
                {
                    realRotation = initRot;
                    Camera.main.transform.localPosition = Vector3.Lerp(Camera.main.transform.localPosition, Vector3.zero, Time.deltaTime*10);
                    Camera.main.transform.localRotation = Quaternion.Lerp(Camera.main.transform.localRotation, Quaternion.Euler(Vector3.zero), Time.deltaTime * 10);
                    if (Camera.main.transform.localRotation.eulerAngles.magnitude<0.01) gameObject.SetActive(false);
                        turretTrans.rotation = Quaternion.Euler(initRot);
                }
            }
        }
    }
}
