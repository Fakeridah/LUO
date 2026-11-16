using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;
public class SuperEnginal : MonoBehaviour
{
    public static SuperEnginal instance { get; private set; }

    public Camera cameraFPS;
    [SerializeField]
    internal GameObject player;
    public LayerMask raycastMask;
    public LayerMask collisionMask;
    public string[] targetMask =new string[] { "Item" };
    [Tooltip("0--pickup with scale;"+ "1--pickup no scale; "+
        "2--pickup with force; "+ "3--pickup normally")]
    public string[] objTags = new string[] { "Item", "Collider" , "Enemy", "Untagged","Reflector" };
    private int enabledLayer;

    [Range(10,1000)]
    public float maxPickUpDistance;
    [Range(50,1000)]
    public float maxScaleDistance;
    [Range(8,256)]
    public int sample;
    public bool enableLerp;
    [Range(0,5)]
    public float lerpTime;
    [Range(0, 100)]
    public float normalPickDistance=5;

    [HideInInspector]
    public Transform target;
    [HideInInspector]
    public bool Recycle;
    internal float originalDistance;
    Vector3 originalSize;
    Vector3 originalPos;
    Vector3 originalBoundSize;
    float originalYRotation;
    private LZM pcs;
    private PlayerAiming pa;
    private float initialSens;
    private Vector3 startDirectionOffset;
    private float lerpStart;
    private bool isLerping;
    private int originalLayer; 
    private Quaternion rotOffset;
    const float pi = 3.141592653589793238f;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        pcs = player.GetComponent<LZM>();
        pa = player.GetComponentInChildren<PlayerAiming>();
        initialSens = pa.sensitivityMultiplier;
        enabledLayer = (int)Mathf.Log(collisionMask, 2);
    }
    void Update() {HandleInput();}
    void LateUpdate() {ResizeTarget();}
 
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0)&&!Input.GetMouseButton(1)) {
            if (target == null) {
                RaycastHit hit;
                if (Physics.Raycast(cameraFPS.transform.position, cameraFPS.transform.forward, out hit, maxScaleDistance, raycastMask))
                {
                    if (1 << hit.transform.gameObject.layer == LayerMask.GetMask(targetMask[0]))
                    {
                        if ((hit.transform.tag == objTags[3]|| hit.transform.tag == objTags[4]) && Vector3.Distance(cameraFPS.transform.position, hit.point) > normalPickDistance)
                        {
                            return;
                        }
                        lerpStart = Time.time;
                        target = hit.transform;
                        startDirectionOffset = (target.position - cameraFPS.transform.position).normalized - cameraFPS.transform.forward;
                        if (target.GetComponent<Rigidbody>() != null && target.tag != objTags[2])
                        {
                            Physics.IgnoreCollision(target.GetComponent<Collider>(), player.GetComponent<Collider>(), true);
                            //target.GetComponent<Collider>().isTrigger = true;
                            target.GetComponent<Rigidbody>().isKinematic = true; 
                        }
                        originalDistance = Vector3.Distance(cameraFPS.transform.position, target.position);
                        originalSize = target.localScale;
                        originalPos = target.position;
                        var bound = target.GetComponent<Collider>().bounds.size;
                        originalBoundSize = new Vector3(bound.x, bound.z, bound.y);
                        originalYRotation = target.localEulerAngles.y - cameraFPS.transform.localEulerAngles.y;
                        // rotOffset = Quaternion.inverse(Quaternion.Inverse(transform.rotation) * transform.rotation);
                        originalLayer = target.gameObject.layer;
                        //target.gameObject.layer = enabledLayer;
                        isLerping = enableLerp;
                        
                    }
                }
            } else if(target.tag != objTags[2])
            {
                if (target.GetComponent<Rigidbody>() != null)
                    target.GetComponent<Rigidbody>().isKinematic = false;
                Physics.IgnoreCollision(target.GetComponent<Collider>(), player.GetComponent<Collider>(), false);
                //target.GetComponent<Collider>().isTrigger = false;
                //target.gameObject.layer = originalLayer;
                
                    Collider[] colliders = target.GetComponentsInChildren<Collider>();
                    if (colliders != null)
                    {
                        foreach (var item in colliders)
                        {
                            item.enabled = true;
                        }
                    }
                target = null;
            }
        }
        if (target!= null && (Input.GetMouseButtonUp(0))) {
            if(target.tag == objTags[2]|| target.tag == "Clicker")
            { if (target.GetComponent<Outline>() != null)target.GetComponent<Outline>().enabled = false;
                target.gameObject.layer = originalLayer; target = null; }  
        }
        if (Input.GetMouseButton(1) && target != null && target.tag != objTags[2]) {RotateObj();} else if (target != null) { pa.lockView = false;}
        if (Input.GetMouseButtonUp(1) && target != null && target.tag != objTags[2]) {originalYRotation = target.localEulerAngles.y - cameraFPS.transform.localEulerAngles.y;}
    }

    private float OutSine(float x) {
        if (x > 1f) {return 1f;}
        return Mathf.Sin(0.5f * pi * x);
    }

    private void ResizeTarget()
    {
        //change!!!!!!
        if (target == null||(target.tag!=objTags[0]&& target.tag != objTags[1] && target.tag != objTags[3] && target.tag != objTags[4])) {return;}

        Vector3 targetPos = BoxCast(cameraFPS.transform.forward);
        if (target.tag == objTags[3]|| target.tag == objTags[4])
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>();
            if (colliders != null)
            {
                foreach(var item in colliders)
                {
                    if(item != target.GetComponent<Collider>())
                    item.enabled = false;
                }
            }
            //targetPos = BoxCastSafe(cameraFPS.transform.forward);
            target.position = cameraFPS.transform.position+ cameraFPS.transform.forward*originalDistance;
            if (Vector3.Distance(cameraFPS.transform.position, targetPos) < originalDistance)
            { target.position = targetPos; }
            return;
        }
        if (isLerping) { //lerp object center to mouse tap position
            float lerpPercentage = (Time.time - lerpStart) / lerpTime;
            if (lerpPercentage > 1) {lerpPercentage = 1;isLerping = false;}
            Vector3 startPos = BoxCast(cameraFPS.transform.forward + startDirectionOffset);
            float progress = OutSine(lerpPercentage);
            target.position = Vector3.Lerp(startPos, targetPos, progress);
        }
        else
        {
            target.position = targetPos;
        }
        if (target.tag == objTags[0])
            target.localScale = CalcScale(target.position, originalSize);
        //else target.localScale = originalSize;
        // target.rotation = transform.rotation * rotOffset;
        // if (!Input.GetMouseButton(1))
        // target.localEulerAngles = new Vector3(target.localEulerAngles.x, originalYRotation + transform.localEulerAngles.y, target.localEulerAngles.z);
    }

    private Vector3 CalcScale(Vector3 tarPos, Vector3 originalSize) {
        float currentDistance = Vector3.Distance(cameraFPS.transform.position, tarPos);
        float scale = currentDistance / originalDistance;
        return scale * originalSize;
    }

    private void RotateObj() {
        pa.lockView = true;
        float mouseX = Input.GetAxisRaw("Mouse X") * Time.fixedDeltaTime  * 100f;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.fixedDeltaTime  * 100f;
        target.RotateAround(target.transform.position,Vector3.up,-mouseX);
        target.RotateAround(target.transform.position, player.transform.right, mouseY);
    }
    private float dist = 2f;
    private Vector3 BoxCast(Vector3 Direction)
    {
        Vector3 boxSize, boxCenter;
        boxCenter = Direction * 0.001f + cameraFPS.transform.position;
        Collider[] collider = new Collider[0];
        while (collider.Length == 0)
        {
            boxCenter += Direction * (1f / sample);
            boxSize = CalcScale(boxCenter, originalBoundSize) * 0.5f;
            collider = Physics.OverlapBox(boxCenter, boxSize, target.rotation, collisionMask);
            if (Vector3.Distance(cameraFPS.transform.position, boxCenter) > maxScaleDistance)
            {
                return cameraFPS.transform.position+ cameraFPS.transform.forward*dist;
            }
        }

        while (collider.Length != 0) {
            boxCenter -= Direction * (1f / sample);
            dist = Vector3.Distance( boxCenter, cameraFPS.transform.position);
            boxSize = CalcScale(boxCenter, originalBoundSize) * 0.5f;
            collider = Physics.OverlapBox(boxCenter, boxSize, target.rotation, collisionMask);
        }
        return boxCenter;
    }

    private Vector3 BoxCastSafe(Vector3 Direction)
    {
        Vector3 boxSize, boxCenter;
        boxCenter = Direction * 0.5f + cameraFPS.transform.position;
        Collider[] collider = new Collider[0];
        while (collider.Length == 0)
        {
            boxCenter += Direction * (1f / sample);
            boxSize = CalcScale(boxCenter, originalBoundSize) * 0.5f;
            collider = Physics.OverlapBox(boxCenter, boxSize, target.rotation);
            if (Vector3.Distance(cameraFPS.transform.position, boxCenter) > maxScaleDistance)
            {
                return cameraFPS.transform.position + cameraFPS.transform.forward * dist;
            }
        }

        while (collider.Length != 0)
        {
            boxCenter -= Direction * (1f / sample);
            dist = Vector3.Distance(boxCenter, cameraFPS.transform.position);
            boxSize = CalcScale(boxCenter, originalBoundSize) * 0.5f;
            collider = Physics.OverlapBox(boxCenter, boxSize, target.rotation);
        }
        return boxCenter;
    }

    private void SuperPowerGrab()
    {

    }
}
