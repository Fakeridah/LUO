using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using GameplayIngredients;
using GameplayIngredients.Events;
using RidahFPS.Controllers;
public class LaserManager : MonoBehaviour
{
    private static LaserManager _laserManager;
    public static LaserManager laserManager { get { return _laserManager; } }
    private static readonly Quaternion halfTurn = Quaternion.Euler(0.0f, 180.0f, 0.0f);

    public struct DebugRay
    {
        public DebugRay( Vector3 startPos, Vector3 dir)
        {
            this.startPos = startPos;
            this.dir = dir;
        }
        public Vector3 startPos;
        public Vector3 dir;
    }
    [SerializeField] Transform laserstartpoint;
    [Range(0, 500)]
    [SerializeField] int maxLaserLines =100;
    [SerializeField] GameObject laserPrefab;
    [SerializeField] GameObject crossoverVFX;
    [SerializeField] GameObject sparkVFX;


    Vector3 laserdirection;
    List<GameObject> laserlines;
    List<GameObject> laserVFXs;
    private LaserPool laserPool;
    private LaserPool vfxPool;
    int index = 0;
    Vector3 lastHitPos = Vector3.zero;
    bool lastHitExist = true;
    public Callable[] onLaserCollision;
    LZM player;
    [HideInInspector] public bool isEmit=true;

    private void Awake()
    {
        _laserManager = this;
    }
    void Start()
    {
        player = LZM.Player;
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
        laserVFXs[0].transform.localScale = laserVFXs[0].transform.localScale *2f;
    }
    void Update()
    {
        laserEmit();
        isEmit = true;
        lastHitExist = true;
    }
    void laserEmit()
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
        if (isEmit)
            drCast(dr);
        if (lastHitPos != null&& lastHitExist)
        {
            laserVFXs[index + 1].SetActive(true);
            laserVFXs[index + 1].transform.position = lastHitPos;
            sparkVFX.transform.GetChild(0).GetComponent<VisualEffect>().Play();
            if (!isEmit)
            {
                laserVFXs[index + 1].transform.position = laserstartpoint.position;
                sparkVFX.transform.position = laserstartpoint.position;
                sparkVFX.transform.LookAt(sparkVFX.transform.position + laserstartpoint.forward);
            }
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
        if (Physics.Raycast(dr.startPos, dr.dir, out hit, Mathf.Infinity))
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
                if (hit.transform.GetComponent<SimplePortal>().otherPlaced && index < (maxLaserLines - 1))
                    drCast(_dr);
            }
            else if (hit.transform.CompareTag("Reflector") && index < (maxLaserLines - 1))
            {
                Vector3 temp = Vector3.Reflect(dr.dir, hit.normal);
                DebugRay _dr = new DebugRay(hit.point, temp);
                drCast(_dr);
            }
            else if ( hit.transform.GetComponent<LaserReciver>() && index < (maxLaserLines - 1))
            {
                hit.transform.GetComponent<LaserReciver>().isEnemy = true;
                Vector3 temp = Vector3.Reflect(dr.dir, hit.normal);
                DebugRay _dr = new DebugRay(hit.point, temp);
                drCast(_dr);
            }
            else if (hit.transform.CompareTag("Player") && index < (maxLaserLines - 1))
            {
                Callable.Call(onLaserCollision);
                player.ResetEnabled = true;
                player._isReset = true;
                Vector3 HLaserDir = new Vector3(dr.dir.x, 0, dr.dir.z).normalized;
                Vector3 HSpeed = new Vector3(player.MoveData.Velocity.x,0, player.MoveData.Velocity.z).normalized;             
                Vector3 repulseDir = Vector3.Cross(HLaserDir,Vector3.up).normalized* (Vector3.Cross(HLaserDir, HSpeed).y>0?1:-1);
                player.MoveData.Velocity = repulseDir * 6f;
                player._resetPosition = player.transform.position+new Vector3(repulseDir.x, 0, repulseDir.z)*.5f;
                player._resetRotation = player.GetComponentInChildren<PlayerAiming>().realRotation;
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
    /*public void drCast(DebugRay dr)
    {
        laserlines[index].SetActive(true);
        laserlines[index].GetComponent<LineRenderer>().positionCount = 2;
        laserlines[index].GetComponent<LineRenderer>().SetPosition(0, dr.startPos);
        laserVFXs[index].SetActive(true);
        laserVFXs[index].transform.position = dr.startPos;
        index++;
        RaycastHit hit;
        if (Physics.Raycast(dr.startPos, dr.dir, out hit, Mathf.Infinity))
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
                if (hit.transform.GetComponent<SimplePortal>().otherPlaced && index < (maxLaserLines - 1))
                    drCast(_dr);
            }
            else if (hit.transform.CompareTag("reflector") && index < (maxLaserLines - 1))
            {
                Vector3 temp = Vector3.Reflect(dr.dir, hit.normal);
                DebugRay _dr = new DebugRay(hit.point, temp);
                drCast(_dr);
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
        }
        index--;
    }*/
}
//this version only reflect once and need component line renderer
/*public class LaserManager : MonoBehaviour
{
    private static LaserManager _laserManager;
    public static LaserManager laserManager { get { return _laserManager; } }
    public struct laserline
    {
        public laserline(LineRenderer lr, Vector3 startPos, Vector3 dir)
        {
            this.lr = lr;
            this.startPos = startPos;
            this.dir = dir;
        }
        public LineRenderer lr;
        public Vector3 startPos;
        public Vector3 dir;
    }
    [SerializeField] Transform laserstartpoint;

    Vector3 laserdirection;
    [HideInInspector]
    public List<LaserReciver> reflector;
    List<GameObject> laserlines;
    LineRenderer lr;
    private LaserPool laserPool;
    int index = 0;

    private void Awake()
    {
        _laserManager = this;
    }
    void Start()
    {
        lr = GetComponent<LineRenderer>();
        laserdirection = laserstartpoint.forward;
    }
    void Update()
    {
        laserdirection = laserstartpoint.forward;

            foreach (var item in reflector)
            {
                if (item.GetComponent<LineRenderer>())
                    item.GetComponent<LineRenderer>().positionCount = 0;
            }
            laserline lin = new laserline(lr, laserstartpoint.position, laserdirection);
            lrCast(lin);
        
    }
    public void lrCast(laserline ll)
    {
        ll.lr.positionCount = 2;
        ll.lr.SetPosition(0, ll.startPos);
        RaycastHit hit;
        if (Physics.Raycast(ll.startPos, ll.dir, out hit, Mathf.Infinity))
        {
            if (hit.transform.CompareTag("reflector"))
            {
                Vector3 temp = Vector3.Reflect(ll.dir, hit.normal);
                laserline lin = new laserline(hit.transform.GetComponent<LineRenderer>(), hit.point, temp);
                lrCast(lin);
            }
            ll.lr.SetPosition(1, hit.point);
        }
        else
        {
            ll.lr.SetPosition(1, ll.startPos + ll.dir * 100);
        }
    }
}*/