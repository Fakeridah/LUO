using System.Collections.Generic;
using UnityEngine;
using RidahFPS.Controllers;

[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    private static readonly List<Portal> ActivePortals = new List<Portal>();
    private static readonly Quaternion PortalHalfTurn =
        Quaternion.Euler(0f, 180f, 0f);

    // Only cut the host wall while the Source hull is crossing the portal
    // plane. The movement sweep now teleports in the same command.
    private const float PlayerCutoutDepth = 0.05f;
    private const float PlayerCutoutMargin = 0.35f;
    private const float PlayerCutoutTolerance = 0.02f;
    private const float HammerUnitsPerUnityUnit = 52.4934383202f;

    public bool activeStart = false;
    public bool isPlaced = false;

    [field: SerializeField]
    public GameObject Bullet { get; private set; }

    public Transform vfx;

    [field: SerializeField]
    public Portal otherPortal { get; private set; }

    [SerializeField]
    private Renderer outlineRenderer;

    [field: SerializeField]
    public Color portalColour { get; private set; }

    [SerializeField]
    private LayerMask placementMask;

    [SerializeField]
    private Transform testTransform;

    private readonly List<PortalableObject> portalObjects =
        new List<PortalableObject>();
    private readonly HashSet<Collider> playerTriggerColliders =
        new HashSet<Collider>();
    private Collider wallCollider;
    private Collider portalCollider;
    private Transform attachmentTransform;
    private Vector3 attachmentLocalPosition;
    private Quaternion attachmentLocalRotation;
    private Vector3 initialWorldScale;
    private static RaycastHit[] portalMovementHits = new RaycastHit[32];

    public Renderer Renderer { get; private set; }
    public Collider WallCollider => wallCollider;
    public LayerMask PlacementMask => placementMask;

    // Kept for compatibility with the existing weapon and prefab wiring.
    public bool placeEabled = true;

    private void Awake()
    {
        portalCollider = GetComponent<Collider>();
        Renderer = GetComponent<Renderer>();
        initialWorldScale = transform.lossyScale;

        // The portal surface/frame is visual in Source. Keep the root trigger
        // for portal occupancy, but do not let the decorative child meshes
        // become a physical lip in the player's movement hull.
        Collider[] childColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childColliders.Length; ++i)
        {
            Collider childCollider = childColliders[i];
            if (childCollider != portalCollider && !childCollider.isTrigger)
                childCollider.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (!ActivePortals.Contains(this))
            ActivePortals.Add(this);
    }

    private void OnDisable()
    {
        ActivePortals.Remove(this);
        playerTriggerColliders.Clear();
    }

    private void Start()
    {
        if (outlineRenderer != null)
            outlineRenderer.material.SetColor("_OutlineColour", portalColour);

        gameObject.SetActive(activeStart);
    }

    private void Update()
    {
        UpdateAttachmentPose();

        bool otherPlaced = otherPortal != null && otherPortal.isPlaced;
        if (Renderer != null)
        {
            Renderer.enabled = otherPlaced &&
                (vfx == null || vfx.localScale.x > 1.2f) &&
                (otherPortal.vfx == null || otherPortal.vfx.localScale.x > 1.2f);
        }

        for (int i = portalObjects.Count - 1; i >= 0; --i)
        {
            PortalableObject portalObject = portalObjects[i];
            if (portalObject == null)
            {
                portalObjects.RemoveAt(i);
                continue;
            }

            Vector3 objectPosition = transform.InverseTransformPoint(
                portalObject.transform.position);
            if (otherPlaced && objectPosition.z > 0f)
                portalObject.Warp();
        }

        if (vfx != null)
            vfx.localScale = Mathf.Lerp(vfx.localScale.x, 1.25f,
                Time.deltaTime * 10f) * Vector3.one;
    }

    private void FixedUpdate()
    {
        UpdateAttachmentPose();
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
        if (Renderer != null)
            Renderer.material.SetInt("_MaskID", id);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (otherPortal == null || !otherPortal.isPlaced)
            return;

        PortalableObject portalObject =
            other.GetComponentInParent<PortalableObject>();
        if (portalObject != null && !portalObjects.Contains(portalObject))
        {
            portalObjects.Add(portalObject);
            placeEabled = false;
            portalObject.SetIsInPortal(this, otherPortal, wallCollider);
        }

        PortablePlayer player = other.GetComponentInParent<PortablePlayer>();
        if (player != null)
        {
            playerTriggerColliders.Add(other);
            placeEabled = false;
            return;
        }

        placeEabled = portalObjects.Count == 0 &&
            playerTriggerColliders.Count == 0;
    }

    private void OnTriggerExit(Collider other)
    {
        PortalableObject portalObject =
            other.GetComponentInParent<PortalableObject>();
        if (portalObject != null && portalObjects.Remove(portalObject))
        {
            portalObject.ExitPortal(wallCollider);
        }

        playerTriggerColliders.Remove(other);

        placeEabled = portalObjects.Count == 0 &&
            playerTriggerColliders.Count == 0;
    }

    public bool PlacePortal(Collider hitWall, Vector3 position,
        Quaternion rotation)
    {
        if (hitWall == null)
            return false;

        Transform previousParent = transform.parent;
        Vector3 previousPosition = transform.position;
        Quaternion previousRotation = transform.rotation;
        Vector3 previousLocalScale = transform.localScale;
        Collider previousWall = wallCollider;
        Transform previousAttachmentTransform = attachmentTransform;
        Vector3 previousAttachmentLocalPosition = attachmentLocalPosition;
        Quaternion previousAttachmentLocalRotation = attachmentLocalRotation;
        bool previousPlaced = isPlaced;
        bool previousActive = gameObject.activeSelf;
        bool previousPlaceEnabled = placeEabled;

        // Keep the portal geometry at its authored scale. Parenting it to a
        // scaled floor/wall makes a rotated child inherit non-uniform scale.
        transform.SetParent(null, true);
        transform.localScale = initialWorldScale;
        attachmentTransform = null;
        wallCollider = hitWall;
        transform.position = position - rotation * Vector3.forward * 0.001f;
        transform.rotation = rotation;

        if (testTransform == null)
            testTransform = transform;

        if (!FixOverhangs() || !FixIntersects() || !CheckOverlap())
        {
            wallCollider = previousWall;
            transform.SetParent(previousParent, true);
            transform.SetPositionAndRotation(previousPosition, previousRotation);
            transform.localScale = previousLocalScale;
            attachmentTransform = previousAttachmentTransform;
            attachmentLocalPosition = previousAttachmentLocalPosition;
            attachmentLocalRotation = previousAttachmentLocalRotation;
            isPlaced = previousPlaced;
            placeEabled = previousPlaceEnabled;
            if (!previousPlaced)
                gameObject.SetActive(false);
            else if (!gameObject.activeSelf)
                gameObject.SetActive(previousActive);
            return false;
        }

        if (previousPlaced)
            ClearPortalOccupants(previousWall);

        transform.position = testTransform.position;
        transform.rotation = testTransform.rotation;
        attachmentTransform = hitWall.transform;
        attachmentLocalPosition = attachmentTransform.InverseTransformPoint(
            transform.position);
        attachmentLocalRotation = Quaternion.Inverse(
            attachmentTransform.rotation) * transform.rotation;
        gameObject.SetActive(true);
        isPlaced = true;
        placeEabled = true;
        return true;
    }

    private bool FixOverhangs()
    {
        Vector3 halfSize = GetPortalHalfSize();
        float x = Mathf.Max(0.05f, halfSize.x - 0.05f);
        float y = Mathf.Max(0.05f, halfSize.y - 0.05f);
        Vector3[] testPoints =
        {
            new Vector3(x, 0f, 0f),
            new Vector3(-x, 0f, 0f),
            new Vector3(0f, -y, 0f),
            new Vector3(0f, y, 0f)
        };

        for (int i = 0; i < testPoints.Length; ++i)
        {
            // Probe from just outside the surface through the exact collider
            // that was hit. A sphere centered 0.1 local units inside a thin
            // floor mesh can sit entirely below its zero-thickness face.
            Vector3 point = testTransform.TransformPoint(testPoints[i]) -
                testTransform.forward * 0.05f;
            Ray surfaceProbe = new Ray(point, testTransform.forward);
            if (wallCollider == null ||
                !wallCollider.Raycast(surfaceProbe, out _, 0.1f))
                return false;
        }

        return true;
    }

    private bool FixIntersects()
    {
        Vector3[] testDirections =
        {
            Vector3.right,
            -Vector3.right,
            Vector3.up,
            -Vector3.up
        };
        Vector3 halfSize = GetPortalHalfSizeWorld();
        float[] testDistances = { halfSize.x, halfSize.x, halfSize.y, halfSize.y };

        for (int i = 0; i < testDirections.Length; ++i)
        {
            RaycastHit hit;
            Vector3 raycastPosition = testTransform.TransformPoint(
                new Vector3(0f, 0f, -0.1f));
            Vector3 raycastDirection = testTransform.TransformDirection(
                testDirections[i]);

            if (Physics.Raycast(raycastPosition, raycastDirection, out hit,
                testDistances[i], placementMask,
                QueryTriggerInteraction.Ignore))
            {
                Vector3 offset = hit.point - raycastPosition;
                Vector3 correction = -raycastDirection *
                    (testDistances[i] - offset.magnitude);
                testTransform.Translate(correction, Space.World);
            }
        }

        return true;
    }

    private bool CheckOverlap()
    {
        Vector3 halfSize = GetPortalHalfSizeWorld();
        Vector3 checkExtents = new Vector3(halfSize.x, halfSize.y, 0.05f);
        Vector3 portalFront = testTransform.position -
            testTransform.forward * 0.1f;
        Vector3[] checkPositions =
        {
            portalFront,
            portalFront - testTransform.right * halfSize.x -
                testTransform.up * halfSize.y,
            portalFront - testTransform.right * halfSize.x +
                testTransform.up * halfSize.y,
            portalFront + testTransform.right * halfSize.x -
                testTransform.up * halfSize.y,
            portalFront + testTransform.right * halfSize.x +
                testTransform.up * halfSize.y
        };

        Collider[] intersections = Physics.OverlapBox(
            checkPositions[0], checkExtents, testTransform.rotation,
            placementMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < intersections.Length; ++i)
        {
            if (intersections[i] != portalCollider &&
                intersections[i] != wallCollider &&
                (wallCollider == null ||
                 !intersections[i].transform.IsChildOf(wallCollider.transform)))
                return false;
        }

        Vector3 wallProbe = testTransform.forward * 0.2f;
        for (int i = 1; i < checkPositions.Length; ++i)
        {
            if (!Physics.Linecast(checkPositions[i],
                checkPositions[i] + wallProbe, placementMask,
                QueryTriggerInteraction.Ignore))
                return false;
        }

        return true;
    }

    public void RemovePortal()
    {
        ClearPortalOccupants(wallCollider);
        wallCollider = null;
        attachmentTransform = null;
        isPlaced = false;
        placeEabled = true;
        gameObject.SetActive(false);
    }

    private void ClearPortalOccupants(Collider ignoredWall)
    {
        for (int i = 0; i < portalObjects.Count; ++i)
        {
            if (portalObjects[i] != null)
                portalObjects[i].ExitPortal(ignoredWall);
        }
        portalObjects.Clear();
        playerTriggerColliders.Clear();

    }

    /// <summary>
    /// GOKZ uses manual BoxCast/OverlapBox queries, which do not observe
    /// Physics.IgnoreCollision. Portal-owned frame meshes are visual only;
    /// the host wall is cut only where the swept hull passes through the
    /// portal plane.
    /// </summary>
    public static bool ShouldIgnoreMovementCollision(
        Collider candidate,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation)
    {
        return ShouldIgnoreMovementCollision(
            candidate,
            hullCenter,
            hullHalfExtents,
            hullRotation,
            Vector3.zero);
    }

    public static bool ShouldIgnoreMovementCollision(
        Collider candidate,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation,
        Vector3 hullSweepDelta)
    {
        return ShouldIgnorePortalWallCollision(
            candidate,
            hullCenter,
            hullHalfExtents,
            hullRotation,
            hullSweepDelta,
            false);
    }

    public static bool ShouldIgnoreMovementCollision(
        Collider candidate,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation,
        Vector3 hullSweepDelta,
        Vector3 duckHullHalfExtents,
        Vector3 movementUp)
    {
        return ShouldIgnorePortalWallCollision(
            candidate,
            hullCenter,
            hullHalfExtents,
            hullRotation,
            hullSweepDelta,
            false,
            duckHullHalfExtents,
            movementUp);
    }

    public static bool ShouldIgnoreMovementCollision(
        RaycastHit hit,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation,
        Vector3 hullSweepDelta)
    {
        return ShouldIgnoreMovementCollision(
            hit,
            hullCenter,
            hullHalfExtents,
            hullRotation,
            hullSweepDelta,
            Vector3.zero,
            Vector3.up);
    }

    public static bool ShouldIgnoreMovementCollision(
        RaycastHit hit,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation,
        Vector3 hullSweepDelta,
        Vector3 duckHullHalfExtents,
        Vector3 movementUp)
    {
        Collider candidate = hit.collider;
        if (candidate == null)
            return false;
        if (IsPortalOwnedCollider(candidate))
            return true;

        for (int i = 0; i < ActivePortals.Count; ++i)
        {
            Portal portal = ActivePortals[i];
            if (!HasActivePair(portal) ||
                (candidate != portal.wallCollider &&
                 !candidate.transform.IsChildOf(portal.wallCollider.transform)))
                continue;

            Vector3 apertureHalfExtents = hullHalfExtents;
            if (duckHullHalfExtents.sqrMagnitude > 0.000001f &&
                ShouldForcePortalCrouch(portal, movementUp))
                apertureHalfExtents = duckHullHalfExtents;

            if (FitsPortalWallPassage(
                    portal,
                    hullCenter,
                    apertureHalfExtents,
                    hullRotation,
                    hullSweepDelta,
                    false))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Stuck recovery treats portal-wall penetration as recoverable while the
    /// queried hull overlaps the aperture, including when the query is for a
    /// prospective movement position rather than the live player Transform.
    /// </summary>
    public static bool ShouldIgnoreStuckRecoveryCollision(
        Collider candidate,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation)
    {
        return ShouldIgnorePortalWallCollision(
            candidate,
            hullCenter,
            hullHalfExtents,
            hullRotation,
            Vector3.zero,
            true);
    }

    private static bool ShouldIgnorePortalWallCollision(
        Collider candidate,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation,
        Vector3 hullSweepDelta,
        bool allowApertureEdgeOverlap)
    {
        return ShouldIgnorePortalWallCollision(
            candidate,
            hullCenter,
            hullHalfExtents,
            hullRotation,
            hullSweepDelta,
            allowApertureEdgeOverlap,
            Vector3.zero,
            Vector3.up);
    }

    private static bool ShouldIgnorePortalWallCollision(
        Collider candidate,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation,
        Vector3 hullSweepDelta,
        bool allowApertureEdgeOverlap,
        Vector3 duckHullHalfExtents,
        Vector3 movementUp)
    {
        if (candidate == null)
            return false;
        if (IsPortalOwnedCollider(candidate))
            return true;

        for (int i = 0; i < ActivePortals.Count; ++i)
        {
            Portal portal = ActivePortals[i];
            if (!HasActivePair(portal) ||
                (candidate != portal.wallCollider &&
                 !candidate.transform.IsChildOf(portal.wallCollider.transform)))
                continue;

            Vector3 apertureHalfExtents = hullHalfExtents;
            if (duckHullHalfExtents.sqrMagnitude > 0.000001f &&
                ShouldForcePortalCrouch(portal, movementUp))
                apertureHalfExtents = duckHullHalfExtents;

            if (FitsPortalWallPassage(
                portal,
                hullCenter,
                apertureHalfExtents,
                hullRotation,
                hullSweepDelta,
                allowApertureEdgeOverlap))
                return true;
        }

        return false;
    }

    public static bool IsPairedPortalWallCollider(Collider candidate)
    {
        if (candidate == null)
            return false;

        for (int i = 0; i < ActivePortals.Count; ++i)
        {
            Portal portal = ActivePortals[i];
            if (!HasActivePair(portal))
                continue;

            if (candidate == portal.wallCollider ||
                candidate.transform.IsChildOf(portal.wallCollider.transform))
                return true;
        }

        return false;
    }

    public static bool IsPairedPortalWallContact(
        RaycastHit hit,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation,
        Vector3 hullSweepDelta)
    {
        Collider candidate = hit.collider;
        if (candidate == null)
            return false;

        for (int i = 0; i < ActivePortals.Count; ++i)
        {
            Portal portal = ActivePortals[i];
            if (!HasActivePair(portal) ||
                (candidate != portal.wallCollider &&
                 !candidate.transform.IsChildOf(portal.wallCollider.transform)))
                continue;

            if (FitsPortalWallPassage(
                    portal,
                    hullCenter,
                    hullHalfExtents,
                    hullRotation,
                    hullSweepDelta,
                    false))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Mirrors CPortalGameMovement::HandlePortalling: detect the hull center
    /// crossing a linked portal during this movement command, then transform
    /// the already-moved center and velocity in the same command.
    /// </summary>
    public static bool TryHandlePlayerMovement(
        PortablePlayer player,
        Vector3 previousRootPosition,
        ref Vector3 currentRootPosition,
        Vector3 hullHalfExtents,
        Vector3 duckHullHalfExtents,
        Vector3 movementUp,
        ref Vector3 velocity,
        bool wasGrounded,
        float gravityAcceleration,
        float frameTime,
        out Quaternion portalRotation,
        out bool forceDuck,
        out Portal exitPortal,
        out Vector3 transformedOldEyePosition)
    {
        portalRotation = Quaternion.identity;
        forceDuck = false;
        exitPortal = null;
        transformedOldEyePosition = Vector3.zero;
        if (player == null || movementUp.sqrMagnitude < 0.5f)
            return false;

        movementUp.Normalize();
        Vector3 centerOffset = movementUp * hullHalfExtents.y;
        Vector3 previousCenter = previousRootPosition + centerOffset;
        Vector3 currentCenter = currentRootPosition + centerOffset;
        float nearestCrossing = float.PositiveInfinity;
        Portal entryPortal = null;
        float crossingFraction = 0f;

        for (int i = 0; i < ActivePortals.Count; ++i)
        {
            Portal candidate = ActivePortals[i];
            if (!HasActivePair(candidate))
                continue;

            // PlacePortal intentionally stores transform.forward pointing
            // into the host wall (-hit.normal). The traversable/front side
            // is therefore the opposite direction.
            Vector3 normal = GetPortalPlaneNormal(candidate);
            float startDistance = Vector3.Dot(
                previousCenter - candidate.transform.position, normal);
            float endDistance = Vector3.Dot(
                currentCenter - candidate.transform.position, normal);
            float distanceTravelled = startDistance - endDistance;
            float normalExtent = ProjectHalfExtent(
                normal,
                Vector3.right,
                Vector3.up,
                Vector3.forward,
                hullHalfExtents);
            if (startDistance < -normalExtent - PlayerCutoutTolerance ||
                endDistance >= -0.000001f ||
                distanceTravelled <= 0.000001f)
                continue;

            // Normally the center crosses this plane in the current move.
            // If a prior blocked move left the hull slightly inside the
            // wall, Source's portal-environment state still recognizes the
            // transition; recover that equivalent case while the hull
            // overlaps the plane and continues inward.
            bool recoveringFromWallOverlap = startDistance < 0f;
            float fraction = recoveringFromWallOverlap
                ? 0f
                : startDistance / distanceTravelled;
            if (fraction < 0f || fraction > 1f ||
                fraction >= nearestCrossing)
                continue;

            Vector3 centerAtPlane = recoveringFromWallOverlap
                ? currentCenter - normal * endDistance
                : Vector3.Lerp(previousCenter, currentCenter, fraction);
            if (!FitsPortalOpening(candidate, centerAtPlane, hullHalfExtents,
                Quaternion.identity))
                continue;

            Portal destination = candidate.otherPortal;
            Quaternion rotation = GetPortalRotation(candidate, destination);
            bool transitionNeedsDuck = ShouldForcePortalCrouch(
                candidate, movementUp);
            Vector3 exitHalfExtents = transitionNeedsDuck
                ? duckHullHalfExtents
                : hullHalfExtents;
            Vector3 destinationCenter = MapPortalPoint(
                candidate, destination, centerAtPlane);
            if (!FitsPortalOpening(destination, destinationCenter,
                exitHalfExtents, Quaternion.identity))
                continue;

            nearestCrossing = fraction;
            entryPortal = candidate;
            exitPortal = destination;
            portalRotation = rotation;
            forceDuck = transitionNeedsDuck;
            crossingFraction = fraction;
        }

        if (entryPortal == null || exitPortal == null)
            return false;

        Vector3 transformedCenter = MapPortalPoint(
            entryPortal, exitPortal, currentCenter);
        transformedOldEyePosition = MapPortalPoint(
            entryPortal, exitPortal, player.GetEyePosition());
        Vector3 exitCenterOffset = forceDuck
            ? movementUp * duckHullHalfExtents.y
            : centerOffset;
        currentRootPosition = transformedCenter - exitCenterOffset;

        // HandlePortalling removes the part of this command's gravity that
        // belongs after the crossing, rotates the pre-exit velocity, then
        // reapplies gravity in the destination world.
        Vector3 postPortalGravityVelocity = wasGrounded
            ? Vector3.zero
            : -movementUp * gravityAcceleration * frameTime *
                (1f - crossingFraction);
        velocity -= postPortalGravityVelocity;
        velocity = portalRotation * velocity;
        velocity += postPortalGravityVelocity * 1.008f;
        player.ApplyPortalRotation(portalRotation);
        return true;
    }

    /// <summary>
    /// Source traces the movement hull through the entry portal and into the
    /// linked portal's remote world. Return the remote hit in entry-space so
    /// the normal Source slide solver can resolve it as an ordinary collision.
    /// </summary>
    public static bool TryGetPortalSpaceCollision(
        Vector3 center,
        Vector3 hullHalfExtents,
        Vector3 duckHullHalfExtents,
        Quaternion hullRotation,
        Vector3 movementUp,
        Vector3 sweepDelta,
        int layerMask,
        Transform movingRoot,
        out RaycastHit nearest,
        out bool contactedTransitionRamp)
    {
        nearest = default;
        contactedTransitionRamp = false;
        float localDistance = sweepDelta.magnitude;
        if (localDistance <= 0.000001f)
            return false;

        float nearestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < ActivePortals.Count; ++i)
        {
            Portal entry = ActivePortals[i];
            if (!HasActivePair(entry))
                continue;

            Vector3 normal = GetPortalPlaneNormal(entry);
            float startDistance = Vector3.Dot(
                center - entry.transform.position, normal);
            float deltaDistance = Vector3.Dot(sweepDelta, normal);
            float normalExtent = ProjectHalfExtent(
                normal,
                hullRotation * Vector3.right,
                hullRotation * Vector3.up,
                hullRotation * Vector3.forward,
                hullHalfExtents);

            // The sweep must approach the portal from its front and reach the
            // portal plane with some part of the hull during this command.
            if (deltaDistance >= -0.000001f ||
                startDistance < -normalExtent - PlayerCutoutTolerance ||
                startDistance + deltaDistance > normalExtent)
                continue;

            float planeFraction = Mathf.Clamp01(
                startDistance / -deltaDistance);
            Vector3 centerAtPlane = center + sweepDelta * planeFraction;
            if (!IntersectsPortalOpening(
                    entry, centerAtPlane, hullHalfExtents, hullRotation,
                    PlayerCutoutTolerance))
                continue;

            Portal exit = entry.otherPortal;
            Quaternion portalRotation = GetPortalRotation(entry, exit);
            bool transitionNeedsDuck = ShouldForcePortalCrouch(
                entry, movementUp);
            Vector3 remoteHalfExtents = transitionNeedsDuck
                ? duckHullHalfExtents
                : hullHalfExtents;
            Vector3 remoteCenterAtPlane = MapPortalPoint(
                entry, exit, centerAtPlane);
            if (!IntersectsPortalOpening(
                    exit, remoteCenterAtPlane, remoteHalfExtents, hullRotation,
                    PlayerCutoutTolerance))
                continue;

            float remoteNormalExtent = ProjectHalfExtent(
                GetPortalPlaneNormal(exit),
                hullRotation * Vector3.right,
                hullRotation * Vector3.up,
                hullRotation * Vector3.forward,
                remoteHalfExtents);
            float extentDifference = remoteNormalExtent - normalExtent;
            Vector3 remoteShift = GetPortalPlaneNormal(exit) * extentDifference;
            Vector3 remoteStart = MapPortalPoint(entry, exit, center) +
                remoteShift;
            Vector3 remoteDelta = portalRotation * sweepDelta;
            float remoteDistance = remoteDelta.magnitude;
            if (remoteDistance <= 0.000001f)
                continue;

            bool rampContact = TryGetTransitionRampContact(
                exit,
                remoteStart,
                remoteDelta,
                remoteHalfExtents,
                out float rampFraction);

            int hitCount;
            while (true)
            {
                hitCount = Physics.BoxCastNonAlloc(
                    remoteStart,
                    remoteHalfExtents,
                    remoteDelta / remoteDistance,
                    portalMovementHits,
                    Quaternion.identity,
                    remoteDistance,
                    layerMask,
                    QueryTriggerInteraction.Ignore);
                if (hitCount < portalMovementHits.Length)
                    break;

                System.Array.Resize(
                    ref portalMovementHits,
                    portalMovementHits.Length * 2);
            }

            Vector3 remoteEnd = remoteStart + remoteDelta;
            for (int hitIndex = 0; hitIndex < hitCount; ++hitIndex)
            {
                RaycastHit hit = portalMovementHits[hitIndex];
                Collider collider = hit.collider;
                if (collider == null ||
                    IsPortalOwnedCollider(collider) ||
                    collider.attachedRigidbody != null ||
                    (movingRoot != null &&
                     collider.transform.IsChildOf(movingRoot)) ||
                    IsPortalWallAtRemoteOpening(
                        hit, exit, remoteStart, remoteHalfExtents,
                        hullRotation, remoteEnd))
                    continue;

                float distance = hit.distance *
                    (localDistance / remoteDistance);
                if (distance >= nearestDistance)
                    continue;

                hit.distance = distance;
                hit.normal = Quaternion.Inverse(portalRotation) * hit.normal;
                hit.point = MapPortalPoint(
                    exit,
                    entry,
                    hit.point - remoteShift);
                nearest = hit;
                nearestDistance = distance;
                contactedTransitionRamp = rampContact &&
                    rampFraction <= planeFraction + 0.0001f;
                found = true;
            }
        }

        return found;
    }

    private static bool TryGetTransitionRampContact(
        Portal portal,
        Vector3 remoteStart,
        Vector3 remoteDelta,
        Vector3 movingHalfExtents,
        out float contactFraction)
    {
        contactFraction = 0f;
        if (portal == null)
            return false;

        // CPortalSimulator builds pAABBAngleTransformCollideable from the
        // lower portal-hole carve. Its six planes form a box: 1024 HU wide,
        // from 0.65 HU below the opening to -1024 HU, and 1/32..64 HU behind
        // the portal plane. The box is transformed through the linked portal
        // before Source traces the remote player hull against it.
        const float innerCarveHu = (0.1f + 1f / 16f) * 4f;
        const float farExtentHu = 1024f;
        const float backExtentHu = 64f;
        const float nearPlaneHu = 1f / 32f;
        float unitScale = 1f / HammerUnitsPerUnityUnit;
        float bottom = -portal.GetPortalHalfSizeWorld().y -
            innerCarveHu * unitScale;
        float farBottom = -farExtentHu * unitScale;
        float back = backExtentHu * unitScale;
        float nearPlane = nearPlaneHu * unitScale;
        if (bottom <= farBottom)
            return false;

        Transform portalTransform = portal.transform;
        Vector3 rampCenter = portalTransform.position +
            portalTransform.up * ((bottom + farBottom) * 0.5f) +
            portalTransform.forward * ((back + nearPlane) * 0.5f);
        Vector3 rampHalfExtents = new Vector3(
            farExtentHu * unitScale,
            (bottom - farBottom) * 0.5f,
            (back - nearPlane) * 0.5f);
        return SweptAabbIntersectsOrientedBox(
            remoteStart,
            remoteDelta,
            movingHalfExtents,
            rampCenter,
            rampHalfExtents,
            portalTransform.rotation,
            out contactFraction);
    }

    private static bool SweptAabbIntersectsOrientedBox(
        Vector3 start,
        Vector3 delta,
        Vector3 movingHalfExtents,
        Vector3 boxCenter,
        Vector3 boxHalfExtents,
        Quaternion boxRotation,
        out float contactFraction)
    {
        contactFraction = 0f;
        float entry = 0f;
        float exit = 1f;
        Vector3 boxRight = boxRotation * Vector3.right;
        Vector3 boxUp = boxRotation * Vector3.up;
        Vector3 boxForward = boxRotation * Vector3.forward;

        if (!IntersectSweptBoxesOnAxis(
                Vector3.right, start, delta, movingHalfExtents,
                boxCenter, boxHalfExtents, boxRight, boxUp, boxForward,
                ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.up, start, delta, movingHalfExtents,
                boxCenter, boxHalfExtents, boxRight, boxUp, boxForward,
                ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.forward, start, delta, movingHalfExtents,
                boxCenter, boxHalfExtents, boxRight, boxUp, boxForward,
                ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                boxRight, start, delta, movingHalfExtents,
                boxCenter, boxHalfExtents, boxRight, boxUp, boxForward,
                ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                boxUp, start, delta, movingHalfExtents,
                boxCenter, boxHalfExtents, boxRight, boxUp, boxForward,
                ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                boxForward, start, delta, movingHalfExtents,
                boxCenter, boxHalfExtents, boxRight, boxUp, boxForward,
                ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.Cross(Vector3.right, boxRight), start, delta,
                movingHalfExtents, boxCenter, boxHalfExtents,
                boxRight, boxUp, boxForward, ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.Cross(Vector3.right, boxUp), start, delta,
                movingHalfExtents, boxCenter, boxHalfExtents,
                boxRight, boxUp, boxForward, ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.Cross(Vector3.right, boxForward), start, delta,
                movingHalfExtents, boxCenter, boxHalfExtents,
                boxRight, boxUp, boxForward, ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.Cross(Vector3.up, boxRight), start, delta,
                movingHalfExtents, boxCenter, boxHalfExtents,
                boxRight, boxUp, boxForward, ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.Cross(Vector3.up, boxUp), start, delta,
                movingHalfExtents, boxCenter, boxHalfExtents,
                boxRight, boxUp, boxForward, ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.Cross(Vector3.up, boxForward), start, delta,
                movingHalfExtents, boxCenter, boxHalfExtents,
                boxRight, boxUp, boxForward, ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.Cross(Vector3.forward, boxRight), start, delta,
                movingHalfExtents, boxCenter, boxHalfExtents,
                boxRight, boxUp, boxForward, ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.Cross(Vector3.forward, boxUp), start, delta,
                movingHalfExtents, boxCenter, boxHalfExtents,
                boxRight, boxUp, boxForward, ref entry, ref exit) ||
            !IntersectSweptBoxesOnAxis(
                Vector3.Cross(Vector3.forward, boxForward), start, delta,
                movingHalfExtents, boxCenter, boxHalfExtents,
                boxRight, boxUp, boxForward, ref entry, ref exit))
            return false;

        if (exit < 0f || entry > 1f)
            return false;
        contactFraction = Mathf.Clamp01(entry);
        return true;
    }

    private static bool IntersectSweptBoxesOnAxis(
        Vector3 axis,
        Vector3 start,
        Vector3 delta,
        Vector3 movingHalfExtents,
        Vector3 boxCenter,
        Vector3 boxHalfExtents,
        Vector3 boxRight,
        Vector3 boxUp,
        Vector3 boxForward,
        ref float entry,
        ref float exit)
    {
        float axisLengthSquared = axis.sqrMagnitude;
        if (axisLengthSquared < 0.000001f)
            return true;
        axis /= Mathf.Sqrt(axisLengthSquared);

        float movingRadius =
            Mathf.Abs(axis.x) * movingHalfExtents.x +
            Mathf.Abs(axis.y) * movingHalfExtents.y +
            Mathf.Abs(axis.z) * movingHalfExtents.z;
        float boxRadius =
            Mathf.Abs(Vector3.Dot(axis, boxRight)) * boxHalfExtents.x +
            Mathf.Abs(Vector3.Dot(axis, boxUp)) * boxHalfExtents.y +
            Mathf.Abs(Vector3.Dot(axis, boxForward)) * boxHalfExtents.z;
        float radius = movingRadius + boxRadius;
        float origin = Vector3.Dot(start - boxCenter, axis);
        float travel = Vector3.Dot(delta, axis);

        if (Mathf.Abs(travel) < 0.000001f)
            return origin >= -radius && origin <= radius;

        float first = (-radius - origin) / travel;
        float second = (radius - origin) / travel;
        if (first > second)
        {
            float swap = first;
            first = second;
            second = swap;
        }

        entry = Mathf.Max(entry, first);
        exit = Mathf.Min(exit, second);
        return entry <= exit;
    }

    private static bool HasActivePair(Portal portal)
    {
        if (portal == null || !portal.isPlaced ||
            portal.otherPortal == null || !portal.otherPortal.isPlaced ||
            portal.otherPortal.otherPortal != portal ||
            portal.wallCollider == null ||
            portal.otherPortal.wallCollider == null ||
            portal.portalCollider == null ||
            portal.otherPortal.portalCollider == null)
            return false;

        Vector3 halfSize = portal.GetPortalHalfSizeWorld();
        Vector3 otherHalfSize = portal.otherPortal.GetPortalHalfSizeWorld();
        return Mathf.Abs(halfSize.x - otherHalfSize.x) <=
                PlayerCutoutTolerance &&
            Mathf.Abs(halfSize.y - otherHalfSize.y) <=
                PlayerCutoutTolerance;
    }

    private static Quaternion GetPortalRotation(Portal entry, Portal exit)
    {
        return exit.transform.rotation * PortalHalfTurn *
            Quaternion.Inverse(entry.transform.rotation);
    }

    public static Vector3 GetPortalPlaneNormal(Portal portal)
    {
        // PortalPlacement orients the portal's local +Z axis into the wall.
        // Source portal planes use the outward-facing normal.
        return -portal.transform.forward;
    }

    private static bool ShouldForcePortalCrouch(
        Portal entry,
        Vector3 movementUp)
    {
        if (entry == null || entry.otherPortal == null ||
            movementUp.sqrMagnitude < 0.5f)
            return false;

        Vector3 up = movementUp.normalized;
        Vector3 transformedUp = GetPortalRotation(entry, entry.otherPortal) * up;
        return Mathf.Abs(Vector3.Dot(transformedUp, up)) < 0.8660254f;
    }

    private static Vector3 MapPortalPoint(
        Portal entry,
        Portal exit,
        Vector3 point)
    {
        Quaternion entryToExit = GetPortalRotation(entry, exit);
        return exit.transform.position + entryToExit *
            (point - entry.transform.position);
    }

    private static bool FitsPortalOpening(
        Portal portal,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation)
    {
        if (portal == null || portal.portalCollider == null)
            return false;

        Vector3 halfSize = portal.GetPortalHalfSizeWorld();
        Vector3 hullRight = hullRotation * Vector3.right;
        Vector3 hullUp = hullRotation * Vector3.up;
        Vector3 hullForward = hullRotation * Vector3.forward;
        float halfWidth = ProjectHalfExtent(
            portal.transform.right,
            hullRight,
            hullUp,
            hullForward,
            hullHalfExtents);
        float halfHeight = ProjectHalfExtent(
            portal.transform.up,
            hullRight,
            hullUp,
            hullForward,
            hullHalfExtents);
        Vector3 offset = hullCenter - portal.transform.position;
        return Mathf.Abs(Vector3.Dot(offset, portal.transform.right)) +
                halfWidth <= halfSize.x + PlayerCutoutTolerance &&
            Mathf.Abs(Vector3.Dot(offset, portal.transform.up)) +
                halfHeight <= halfSize.y + PlayerCutoutTolerance;
    }

    private static bool IntersectsPortalOpening(
        Portal portal,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation,
        float margin)
    {
        if (portal == null || portal.portalCollider == null)
            return false;

        Vector3 halfSize = portal.GetPortalHalfSizeWorld();
        Vector3 hullRight = hullRotation * Vector3.right;
        Vector3 hullUp = hullRotation * Vector3.up;
        Vector3 hullForward = hullRotation * Vector3.forward;
        float halfWidth = ProjectHalfExtent(
            portal.transform.right,
            hullRight,
            hullUp,
            hullForward,
            hullHalfExtents);
        float halfHeight = ProjectHalfExtent(
            portal.transform.up,
            hullRight,
            hullUp,
            hullForward,
            hullHalfExtents);
        Vector3 offset = hullCenter - portal.transform.position;
        return Mathf.Abs(Vector3.Dot(offset, portal.transform.right)) <=
                halfSize.x + halfWidth + margin &&
            Mathf.Abs(Vector3.Dot(offset, portal.transform.up)) <=
                halfSize.y + halfHeight + margin;
    }

    private static bool IsPortalWallAtRemoteOpening(
        RaycastHit hit,
        Portal exit,
        Vector3 remoteStart,
        Vector3 hullHalfExtents,
        Quaternion hullRotation,
        Vector3 remoteEnd)
    {
        Collider candidate = hit.collider;
        if (candidate == null || exit == null || exit.wallCollider == null ||
            (candidate != exit.wallCollider &&
             !candidate.transform.IsChildOf(exit.wallCollider.transform)))
            return false;

        return FitsPortalWallPassage(
                exit,
                remoteStart,
                hullHalfExtents,
                hullRotation,
                remoteEnd - remoteStart,
                false,
                true);
    }

    private static bool FitsPortalWallPassage(
        Portal portal,
        Vector3 hullCenter,
        Vector3 hullHalfExtents,
        Quaternion hullRotation,
        Vector3 hullSweepDelta,
        bool allowApertureEdgeOverlap,
        bool requireWholeHull = false)
    {
        Vector3 planeNormal = GetPortalPlaneNormal(portal);
        float startDistance = Vector3.Dot(
            hullCenter - portal.transform.position, planeNormal);
        float sweepDistance = Vector3.Dot(hullSweepDelta, planeNormal);
        float endDistance = startDistance + sweepDistance;

        float fraction;
        if (Mathf.Abs(sweepDistance) > 0.000001f &&
            startDistance * endDistance <= 0f)
        {
            fraction = Mathf.Clamp01(-startDistance / sweepDistance);
        }
        else
        {
            // A BoxCast reaches the wall when the hull edge touches it, often
            // before the hull center reaches the portal plane. Test the point
            // in this sweep closest to the plane so that an angled approach
            // does not acquire a one-frame wall lip.
            fraction = Mathf.Abs(endDistance) < Mathf.Abs(startDistance)
                ? 1f
                : 0f;
        }

        Vector3 centerAtPlane = hullCenter + hullSweepDelta * fraction;
        float distanceAtPlane = Vector3.Dot(
            centerAtPlane - portal.transform.position, planeNormal);
        centerAtPlane -= planeNormal * distanceAtPlane;

        float hullNormalExtent = ProjectHalfExtent(
            planeNormal,
            hullRotation * Vector3.right,
            hullRotation * Vector3.up,
            hullRotation * Vector3.forward,
            hullHalfExtents);
        if (Mathf.Abs(distanceAtPlane) >
            PlayerCutoutDepth + hullNormalExtent + PlayerCutoutTolerance)
            return false;

        if (requireWholeHull)
            return FitsPortalOpening(
                portal, centerAtPlane, hullHalfExtents, hullRotation);

        return IntersectsPortalOpening(
            portal,
            centerAtPlane,
            hullHalfExtents,
            hullRotation,
            allowApertureEdgeOverlap
                ? PlayerCutoutMargin
                : PlayerCutoutTolerance);
    }

    private static bool IsPortalOwnedCollider(Collider candidate)
    {
        return candidate != null &&
            candidate.GetComponentInParent<Portal>() != null;
    }

    private static float ProjectHalfExtent(
        Vector3 axis,
        Vector3 hullRight,
        Vector3 hullUp,
        Vector3 hullForward,
        Vector3 hullHalfExtents)
    {
        return Mathf.Abs(Vector3.Dot(axis, hullRight)) * hullHalfExtents.x +
            Mathf.Abs(Vector3.Dot(axis, hullUp)) * hullHalfExtents.y +
            Mathf.Abs(Vector3.Dot(axis, hullForward)) * hullHalfExtents.z;
    }

    private Vector3 GetPortalHalfSize()
    {
        BoxCollider box = portalCollider as BoxCollider;
        if (box != null)
            return box.size * 0.5f;

        Bounds bounds = portalCollider != null
            ? portalCollider.bounds
            : new Bounds(transform.position, new Vector3(2f, 4f, 0.6f));
        return new Vector3(
            Vector3.Dot(Abs(transform.right), bounds.extents),
            Vector3.Dot(Abs(transform.up), bounds.extents),
            Vector3.Dot(Abs(transform.forward), bounds.extents));
    }

    private Vector3 GetPortalHalfSizeWorld()
    {
        BoxCollider box = portalCollider as BoxCollider;
        if (box != null)
        {
            Vector3 localHalf = box.size * 0.5f;
            Vector3 scale = Abs(transform.lossyScale);
            return Vector3.Scale(localHalf, scale);
        }

        Bounds bounds = portalCollider != null
            ? portalCollider.bounds
            : new Bounds(transform.position, new Vector3(2f, 4f, 0.6f));
        return new Vector3(
            Vector3.Dot(Abs(transform.right), bounds.extents),
            Vector3.Dot(Abs(transform.up), bounds.extents),
            Vector3.Dot(Abs(transform.forward), bounds.extents));
    }

    private void UpdateAttachmentPose()
    {
        if (attachmentTransform == null)
            return;

        transform.SetPositionAndRotation(
            attachmentTransform.TransformPoint(attachmentLocalPosition),
            attachmentTransform.rotation * attachmentLocalRotation);
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }
}
