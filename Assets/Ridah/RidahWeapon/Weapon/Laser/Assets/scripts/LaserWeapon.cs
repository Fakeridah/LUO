using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using GameplayIngredients.Events;
namespace Ridah.Weapon
{
    public class LaserWeapon : WeaponController
    {
        private static readonly Quaternion halfTurn = Quaternion.Euler(0.0f, 180.0f, 0.0f);

        public struct DebugRay
        {
            public DebugRay(Vector3 startPos, Vector3 dir)
            {
                this.startPos = startPos;
                this.dir = dir;
            }
            public Vector3 startPos;
            public Vector3 dir;
        }
        [SerializeField] Transform laserstartpoint;
        [Range(0,500)]
        [SerializeField] int maxLaserLines = 100;
        [SerializeField] GameObject laserPrefab;
        [SerializeField] GameObject crossoverVFX;
        [SerializeField] GameObject sparkVFX;

        public bool isWeapon=true;
        [SerializeField] LayerMask layerMask;

        Vector3 laserdirection;
        List<GameObject> laserlines;
        List<GameObject> laserVFXs;
        private LaserPool laserPool;
        private LaserPool vfxPool;
        int index = 0;
        Vector3 lastHitPos= Vector3.zero;
        bool lastHitExist = true;
        Vector3 screenCenterPos;
        Transform cameraTrans;

        private void Awake()
        {
        }
        void Start()
        {
            cameraTrans = Camera.main.transform;
            laserdirection = laserstartpoint.forward;
            laserlines = new List<GameObject>();
            laserPool = new LaserPool();
            laserPool.laserPrefab = laserPrefab;
            laserPool.start();
            for (int i = 0; i < maxLaserLines; i++)
            {
                laserlines.Add(laserPool.laserPool.Get());
                laserlines[i].transform.SetParent(transform);
                laserlines[i].SetActive(false);
            }

            laserVFXs = new List<GameObject>();
            vfxPool = new LaserPool();
            vfxPool.laserPrefab = crossoverVFX;
            vfxPool.start();
            for (int i = 0; i < maxLaserLines; i++)
            {
                laserVFXs.Add(vfxPool.laserPool.Get());
                laserVFXs[i].transform.SetParent(transform);
                laserVFXs[i].SetActive(false);
            }
            if (isWeapon)
            {
                laserlines[0].GetComponent<LineRenderer>().startWidth = .07f;
                laserlines[0].GetComponent<LineRenderer>().endWidth = .07f;
                laserVFXs[0].transform.localScale = laserVFXs[0].transform.localScale / 5f;
            }
        }
        void Update()
        {
            if (isWeapon) weaponLaser();
            else triggerLaser();
            lastHitExist = true;
        }
        void weaponLaser()
        {
            if (Input.GetMouseButton(0) && lockShoot)
            {
                sparkVFX.transform.GetChild(0).GetComponent<VisualEffect>().Play();
                RaycastHit hit;
                if (Physics.Raycast(cameraTrans.position, cameraTrans.forward, out hit, Mathf.Infinity,layerMask))
                {
                    screenCenterPos = hit.point;
                }
                else screenCenterPos = cameraTrans.position + cameraTrans.forward * 100;
                //if (Vector3.Dot((screenCenterPos - laserstartpoint.position).normalized, cameraTrans.forward) > 0)
                    laserdirection = (screenCenterPos - laserstartpoint.position).normalized;
                //else laserdirection = laserstartpoint.forward;

                index = 0;
                foreach (var item in laserlines)
                {
                    item.SetActive(false);
                    item.transform.SetParent(transform);
                }
                foreach (var item in laserVFXs)
                {
                    item.SetActive(false);
                    item.transform.SetParent(transform);
                }
                DebugRay dr = new DebugRay(laserstartpoint.position, laserdirection);
                drCast(dr);
            }
            else
            {

                sparkVFX.transform.GetChild(0).GetComponent<VisualEffect>().Stop();
                foreach (var item in laserlines)
                {
                    item.SetActive(false);
                    item.transform.SetParent(transform);
                }
                foreach (var item in laserVFXs)
                {
                    item.SetActive(false);
                    item.transform.SetParent(transform);
                }
            }
        }
        void triggerLaser()
        {
            laserdirection = laserstartpoint.forward;

            index = 0;
            foreach (var item in laserlines)
            {
                item.SetActive(false);
                item.transform.SetParent(transform);
            }
            foreach (var item in laserVFXs)
            {
                item.SetActive(false);
                item.transform.SetParent(transform);
            }
            sparkVFX.transform.GetChild(0).GetComponent<VisualEffect>().Stop();
            DebugRay dr = new DebugRay(laserstartpoint.position, laserdirection);
            drCast(dr);
            if (lastHitPos != null && lastHitExist)
            {
                laserVFXs[index + 1].SetActive(true);
                laserVFXs[index + 1].transform.position = lastHitPos;
                sparkVFX.transform.GetChild(0).GetComponent<VisualEffect>().Play();
            }
        }
        
        public void drCast(DebugRay dr)
        {
            laserlines[index].SetActive(true);
            laserlines[index].GetComponent<LineRenderer>().positionCount = 2;
            laserlines[index].GetComponent<LineRenderer>().SetPosition(0, dr.startPos);
            laserVFXs[index].SetActive(true);
            laserVFXs[index].transform.position = dr.startPos;
            index++;
            RaycastHit hit;
            if (Physics.Raycast(dr.startPos, dr.dir, out hit, Mathf.Infinity, layerMask))
            {
                //Debug.DrawLine(dr.startPos, hit.point, Color.white);
                if (hit.transform.CompareTag("Portal") && hit.transform.GetComponent<SimplePortal>())
                {
                    hit.point += dr.dir * .3f;
                    var inTransform = hit.transform;
                    Transform outTransform = hit.transform.GetComponent<SimplePortal>().otherPortal.transform;
                    Vector3 outPos = outTransform.TransformPoint(halfTurn * inTransform.InverseTransformPoint(hit.point));
                    Vector3 outDir = outTransform.TransformDirection(halfTurn * inTransform.InverseTransformDirection(dr.dir));
                    DebugRay _dr = new DebugRay(outPos, outDir);
                    if (hit.transform.GetComponent<SimplePortal>().otherPlaced&& index < (maxLaserLines - 1))
                        drCast(_dr);
                }
                else if ((hit.transform.CompareTag("Reflector") || hit.transform.GetComponent<LaserReciver>()) && index < (maxLaserLines - 1))
                {
                    Vector3 temp = Vector3.Reflect(dr.dir, hit.normal);
                    DebugRay _dr = new DebugRay(hit.point, temp);
                    drCast(_dr);
                }
                else if (hit.transform.CompareTag("Enemy") && index < (maxLaserLines - 1))
                {
                    if(hit.transform.GetComponent<LaserManager>())
                    hit.transform.GetComponent<LaserManager>().isEmit = false;
                    laserVFXs[index].SetActive(true);
                    laserVFXs[index].transform.position = hit.point;
                    sparkVFX.transform.position = hit.point;
                    sparkVFX.transform.LookAt(sparkVFX.transform.position + hit.normal);
                }
                else
                {
                    laserVFXs[index].SetActive(true);
                    laserVFXs[index].transform.position = hit.point;
                    sparkVFX.transform.position = hit.point;
                    sparkVFX.transform.LookAt(sparkVFX.transform.position + hit.normal);
                }
                if (index - 1 >= 0)
                {
                    laserlines[index - 1].transform.SetParent(hit.transform);
                    laserlines[index - 1].GetComponent<LineRenderer>().SetPosition(1, hit.point);
                    lastHitPos = hit.point;
                }
            }
            else
            {
                //Debug.DrawLine(dr.startPos, dr.startPos + dr.dir * 100, Color.white);
                laserlines[index - 1].GetComponent<LineRenderer>().SetPosition(0, dr.startPos);
                laserlines[index - 1].GetComponent<LineRenderer>().SetPosition(1, dr.startPos + dr.dir * 100);
                lastHitExist = false;
                sparkVFX.transform.GetChild(0).GetComponent<VisualEffect>().Stop();
            }
            index--;
        }
    }
}