using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;

[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    public bool activeStart = false;
    public bool isPlaced = false;

    [field: SerializeField]
    public GameObject Bullet { get; private set; }
    public Transform vfx;

    [field: SerializeField]
    public Portal otherPortal { get; private set; }

    [SerializeField]
    private Renderer outlineRenderer;

    [field:SerializeField]
    public Color portalColour { get; private set; }

    [SerializeField]
    private LayerMask placementMask;

    [SerializeField]
    private Transform testTransform;

    private List<PortalableObject> portalObjects = new List<PortalableObject>();
    // PortablePlayer is a scene component; it is assigned when a player enters the portal.
    private PortablePlayer portablePlayer;

    private Collider wallCollider;

    public Renderer Renderer { get; private set; }
    private new Collider collider;
    private bool playerTrigger = false;

    private void Awake()
    {
        collider = GetComponent<Collider>();
        Renderer = GetComponent<Renderer>();
    }

    private void Start()
    {
        if(outlineRenderer!=null)
        outlineRenderer.material.SetColor("_OutlineColour", portalColour);
        gameObject.SetActive(activeStart);
        Time.fixedDeltaTime = 1f / 128;
    }
    private void Update()
    {
        Renderer.enabled = otherPortal.isPlaced&&(vfx.localScale.x > 1.2)&&(otherPortal.vfx.localScale.x > 1.2);
        for (int i = 0; i < portalObjects.Count; ++i)
        {
            Vector3 objPos = transform.InverseTransformPoint(portalObjects[i].transform.position);

            if (objPos.z > 0.0f)
            {
                portalObjects[i].Warp();
            }
        }
        vfx.localScale = Mathf.Lerp(vfx.localScale.x, 1.25f, Time.deltaTime * 10) * Vector3.one;
    }
    private void FixedUpdate()
    {
        //FixIntersects();
        if (playerTrigger)
        {
            float this_Angle = Vector3.Angle(Vector3.up, -transform.forward);
            float other_Angle = Vector3.Angle(Vector3.up, -otherPortal.transform.forward);
            if ((this_Angle > 40 && other_Angle > 100)|| (this_Angle < 40 && other_Angle > 140))
            {
                Vector3 playerPos = transform.InverseTransformPoint(portablePlayer.transform.position + portablePlayer.gameObject.GetComponent<CharacterController>().center);
                if (playerPos.z > -0.38f)
                {
                    portablePlayer.Warp2();
                    playerTrigger = false;
                }
            }
            /*else if (this_Angle<40&&other_Angle > 100)
            {
                Vector3 playerPos = transform.InverseTransformPoint(portablePlayer.transform.position + portablePlayer.gameObject.GetComponent<CharacterController>().center/2);
                if (playerPos.z > -0.38f)
                {
                    portablePlayer.Warp4();
                    playerTrigger = false;
                }
            }*/
            else if(this_Angle < 40&& other_Angle < 100)
            {
                Vector3 playerPos = transform.InverseTransformPoint(portablePlayer.transform.position + portablePlayer.gameObject.GetComponent<CharacterController>().center/2);
                if (playerPos.z > -0.08f)
                {
                    portablePlayer.Warp3();
                    playerTrigger = false;
                }
            }
            else 
            {
                Vector3 playerPos = transform.InverseTransformPoint(portablePlayer.transform.position + portablePlayer.gameObject.GetComponent<CharacterController>().center);
                if (playerPos.z > -0.08f)//!!
                {
                    portablePlayer.Warp();
                    playerTrigger = false;//!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                }
            }
        }
    }

    public Portal GetOtherPortal()
    {
        return otherPortal;
    }

    public Color GetColour()
    {
        return portalColour;
    }
    public void SetMaskID(int id)
    {
        Renderer.material.SetInt("_MaskID", id);
    }
    /*public void SetColour(Color colour)
    {
        material.SetColor("_Colour", colour);
        outlineRenderer.material.SetColor("_OutlineColour", colour);
    }
   
    public void SetTexture(RenderTexture tex)
    {
        material.mainTexture = tex;
    }

    public bool IsRendererVisible()
    {
        return renderer.isVisible;
    }*/
    public bool placeEabled=true;//prevent IgnoreCollision with wall forever
    private void OnTriggerEnter(Collider other)
    {
        var obj = other.GetComponent<PortalableObject>();
        if (otherPortal.isPlaced)
        {
            placeEabled = false;
            if (obj != null)
            {
                portalObjects.Add(obj);
                obj.SetIsInPortal(this, otherPortal, wallCollider);
            }
            if (other.tag == "Player")
            {
                if (Vector3.Angle(Vector3.up, -transform.forward) < 40)
                {
                    if (other.gameObject.GetComponent<LZM>().MoveData.crouching)
                    {
                        portablePlayer = other.GetComponent<PortablePlayer>();
                        playerTrigger = true;
                        portablePlayer.SetIsInPortal(this, otherPortal, wallCollider);
                    }
                }
                else if (Vector3.Angle(Vector3.up, -transform.forward) < 95)
                {
                    portablePlayer = other.GetComponent<PortablePlayer>();
                    playerTrigger = true;
                    portablePlayer.SetIsInPortal(this, otherPortal, wallCollider);
                }
                else wallCollider = null;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var obj = other.GetComponent<PortalableObject>();
        if (otherPortal.isPlaced)
        {
            placeEabled = true;
            if (portalObjects.Contains(obj))
            {
                portalObjects.Remove(obj);
                obj.ExitPortal(wallCollider);
            }
            if (other.tag == "Player")
            {
                portablePlayer.ExitPortal(wallCollider);
                playerTrigger = false;
            }
        }
    }

    public bool PlacePortal(Collider wallCollider, Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
        transform.position -= transform.forward * 0.001f;

        FixOverhangs( wallCollider);
        FixIntersects();

        if(CheckOverlap())
        {
            this.wallCollider = wallCollider;
            transform.position = testTransform.position;
            transform.rotation = testTransform.rotation;
            gameObject.SetActive(true);
            isPlaced = true;
            return true;

        }
        return false;
    }

    // Ensure the portal cannot extend past the edge of a surface.
    private void FixOverhangs(Collider wallCollider)
    {
        var testPoints = new List<Vector3>
        {
            new Vector3(1.1f,  0.0f, 0.1f),
            new Vector3( -1.1f,  0.0f, 0.1f),
            new Vector3( 0.0f, -1.6f, 0.1f),
            new Vector3( 0.0f,  1.6f, 0.1f)
        };

        /*var testDirs = new List<Vector3>
        {
             Vector3.right,
            -Vector3.right,
             Vector3.up,
            -Vector3.up
        };*/

        for(int i = 0; i < 4; ++i)
        {
            //RaycastHit hit;
            Vector3 raycastPos = testTransform.TransformPoint(testPoints[i]);
            //Vector3 raycastDir = testTransform.TransformDirection(testDirs[i]);

            if(!Physics.CheckSphere(raycastPos, 0.05f))
            {
                gameObject.SetActive(false);
                isPlaced = false;
            }
            /*else if (Physics.Raycast(raycastPos, raycastDir, out hit, 2.1f, placementMask))
            {
                var offset = hit.point - raycastPos;
                testTransform.Translate(offset, Space.World);
            }*/
        }
    }

    // Ensure the portal cannot intersect a section of wall.
    private void FixIntersects()
    {
        var testDirs = new List<Vector3>
        {
             Vector3.right,
            -Vector3.right,
             Vector3.up,
            -Vector3.up
        };

        var testDists = new List<float> { 1.1f, 1.1f, 1.6f, 1.6f };

        for (int i = 0; i < 4; ++i)
        {
            RaycastHit hit;
            Vector3 raycastPos = testTransform.TransformPoint(0.0f, 0.0f, -0.1f);
            Vector3 raycastDir = testTransform.TransformDirection(testDirs[i]);

            if (Physics.Raycast(raycastPos, raycastDir, out hit, testDists[i]))
            {
                var offset = (hit.point - raycastPos);
                var newOffset = -raycastDir * (testDists[i] - offset.magnitude);
                testTransform.Translate(newOffset, Space.World);
            }
        }
    }

    // Once positioning has taken place, ensure the portal isn't intersecting anything.
    private bool CheckOverlap()
    {
        var checkExtents = new Vector3(0.8f, 1.6f, 0.05f);
        var checkPositions = new Vector3[]
            { testTransform.position + testTransform.TransformVector(new Vector3(.0f,.0f,-.1f)),

            testTransform.position + testTransform.TransformVector(new Vector3(-1.0f,-2.0f,-.1f)),
            testTransform.position + testTransform.TransformVector(new Vector3(-1.0f,2.0f,-.1f)),
            testTransform.position + testTransform.TransformVector(new Vector3(1.0f,-2.0f,-.1f)),
            testTransform.position + testTransform.TransformVector(new Vector3(1.0f,2.0f,-.1f)),

            testTransform.TransformVector(new Vector3(.0f,.0f,.2f)),

            };

        var intersections = Physics.OverlapBox(checkPositions[0], checkExtents, testTransform.rotation);

        if (intersections.Length > 1)
        {
            return false;
        }
        else if (intersections.Length == 1)
        { if (intersections[0] != collider) return false;  }

        bool isOverLapping = true;

        for(int i = 1; i < checkPositions.Length - 1; i++)
        {
            isOverLapping &= Physics.Linecast(checkPositions[i],
                checkPositions[i]+checkPositions[checkPositions.Length-1],placementMask);
        }
        return isOverLapping;
    }

    public void RemovePortal()
    {
        gameObject.SetActive(false);
        isPlaced = false;
    }

}
