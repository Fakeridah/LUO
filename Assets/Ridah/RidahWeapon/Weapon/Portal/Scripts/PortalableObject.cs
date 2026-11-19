using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class PortalableObject : MonoBehaviour
{
    [SerializeField] private float maxSpeed = 10f;

    private GameObject cloneObject;
    private int inPortalCount;
    private Portal inPortal;
    private Portal outPortal;
    private Rigidbody rigidbody;
    private Collider collider;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    protected virtual void Awake()
    {
        cloneObject = new GameObject(name + " Portal Clone");
        cloneObject.SetActive(false);

        MeshFilter meshFilter = cloneObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cloneObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = GetComponent<MeshFilter>().sharedMesh;
        meshRenderer.sharedMaterials = GetComponent<MeshRenderer>().sharedMaterials;
        cloneObject.transform.localScale = transform.localScale;

        rigidbody = GetComponent<Rigidbody>();
        collider = GetComponent<Collider>();
    }

    private void OnDestroy()
    {
        if (cloneObject != null)
            Destroy(cloneObject);
    }

    private void LateUpdate()
    {
        if (inPortal == null || outPortal == null)
            return;

        if (cloneObject.activeSelf && inPortal.isPlaced && outPortal.isPlaced)
        {
            Transform inTransform = inPortal.transform;
            Transform outTransform = outPortal.transform;

            Vector3 relativePosition = Quaternion.Inverse(inTransform.rotation) *
                (transform.position - inTransform.position);
            relativePosition = HalfTurn * relativePosition;
            cloneObject.transform.SetPositionAndRotation(
                outTransform.position + outTransform.rotation * relativePosition,
                outTransform.rotation *
                (HalfTurn * (Quaternion.Inverse(inTransform.rotation) *
                    transform.rotation)));
        }
        else
        {
            cloneObject.transform.position = new Vector3(-1000f, 1000f, -1000f);
        }
    }

    public void SetIsInPortal(Portal inPortal, Portal outPortal,
        Collider wallCollider)
    {
        this.inPortal = inPortal;
        this.outPortal = outPortal;

        if (collider != null && wallCollider != null &&
            collider.enabled && wallCollider.enabled)
            Physics.IgnoreCollision(collider, wallCollider, true);

        cloneObject.SetActive(true);
        inPortalCount++;
    }

    public void ExitPortal(Collider wallCollider)
    {
        if (collider != null && wallCollider != null &&
            collider.enabled && wallCollider.enabled)
            Physics.IgnoreCollision(collider, wallCollider, false);

        inPortalCount = Mathf.Max(0, inPortalCount - 1);
        if (inPortalCount == 0)
            cloneObject.SetActive(false);
    }

    public virtual void Warp()
    {
        if (inPortal == null || outPortal == null ||
            !inPortal.isPlaced || !outPortal.isPlaced || rigidbody == null)
            return;

        Transform inTransform = inPortal.transform;
        Transform outTransform = outPortal.transform;
        Collider sourceWall = inPortal.WallCollider;
        Collider destinationWall = outPortal.WallCollider;

        Vector3 relativePosition = Quaternion.Inverse(inTransform.rotation) *
            (transform.position - inTransform.position);
        relativePosition = HalfTurn * relativePosition;
        transform.position = outTransform.position +
            outTransform.rotation * relativePosition;

        Quaternion relativeRotation = HalfTurn *
            (Quaternion.Inverse(inTransform.rotation) * transform.rotation);
        transform.rotation = outTransform.rotation * relativeRotation;

        Vector3 relativeVelocity = HalfTurn *
            inTransform.InverseTransformDirection(rigidbody.linearVelocity);
        rigidbody.linearVelocity = Vector3.ClampMagnitude(
            outTransform.TransformDirection(relativeVelocity), maxSpeed);

        if (collider != null)
        {
            if (sourceWall != null && sourceWall.enabled && collider.enabled)
                Physics.IgnoreCollision(collider, sourceWall, false);
            if (destinationWall != null && destinationWall.enabled &&
                collider.enabled)
                Physics.IgnoreCollision(collider, destinationWall, true);
        }

        Portal previousPortal = inPortal;
        inPortal = outPortal;
        outPortal = previousPortal;
    }
}
