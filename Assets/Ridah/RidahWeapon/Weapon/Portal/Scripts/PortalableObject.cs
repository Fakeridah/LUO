using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adds portal traversal to a transform-driven object. A Rigidbody is optional:
/// when one is present its pose and velocities are transported with the object;
/// otherwise the component detects the object's transform crossing directly.
/// </summary>
public class PortalableObject : MonoBehaviour
{
    private const string PortalGhostLayerName = "Invisable";

    internal static int PortalGhostLayerMask
    {
        get
        {
            int layer = LayerMask.NameToLayer(PortalGhostLayerName);
            return layer >= 0 ? 1 << layer : 0;
        }
    }

    [SerializeField, Min(0f), Tooltip("0 keeps the transferred speed unchanged.")]
    private float maxSpeed;
    [SerializeField]
    private bool resolveTransformCollisions = true;

    private const float TransformCollisionSkin = 0.005f;
    private const float HammerUnitScale =
        1f / PortalTransformMath.HammerUnitsPerUnityUnit;
    private const int MaxTransformSlideIterations = 3;
    private const int MaxExitDepenetrationIterations = 4;
    private const int MaxPortalExitSearchDistanceHu = 100;
    private static RaycastHit[] TransformCollisionHits =
        new RaycastHit[64];
    private static Collider[] TransformOverlapHits = new Collider[64];

    private sealed class VisualPart
    {
        public Transform Source;
        public Transform Clone;
        public MeshFilter SourceMeshFilter;
        public MeshFilter CloneMeshFilter;
        public MeshRenderer SourceMeshRenderer;
        public MeshRenderer CloneMeshRenderer;
        public SkinnedMeshRenderer SourceSkinnedRenderer;
        public SkinnedMeshRenderer CloneSkinnedRenderer;
        public List<Material> SourceMeshMaterials;
        public List<Material> CachedMeshMaterials;
        public List<Material> SourceSkinnedMaterials;
        public List<Material> CachedSkinnedMaterials;
        public Transform[] CloneBones;
        public Mesh CachedSkinnedMesh;
    }

    private readonly List<VisualPart> visualParts = new List<VisualPart>();
    private readonly Dictionary<Transform, Transform> cloneTransforms =
        new Dictionary<Transform, Transform>();

    private GameObject cloneObject;
    private Rigidbody body;
    private CharacterController characterController;
    private Collider[] objectColliders = Array.Empty<Collider>();
    private Portal occupiedPortal;
    private Portal recentExitPortal;
    private Vector3 previousBoundsCenter;
    private Vector3 previousBoundsExtents;
    private Vector3 previousRootPosition;
    private Quaternion previousRootRotation = Quaternion.identity;
    private bool hasPreviousPose;

    /// <summary>
    /// Fired after a portal crossing. Transform-driven movement scripts can
    /// rotate their own velocity or cached movement direction here.
    /// </summary>
    public event Action<Portal, Portal, Quaternion> Teleported;

    public Portal OccupiedPortal => occupiedPortal;
    public Portal RecentExitPortal => recentExitPortal;

    internal static PortalableObject EnsureForItemLayer(
        Collider itemCollider,
        int itemLayer)
    {
        if (itemCollider == null || itemLayer < 0)
            return null;

        Transform itemRoot = null;
        for (Transform current = itemCollider.transform;
             current != null;
             current = current.parent)
        {
            if (current.gameObject.layer == itemLayer)
                itemRoot = current;
        }

        if (itemRoot == null)
            return null;

        Rigidbody attachedBody = itemCollider.attachedRigidbody;
        Transform componentRoot = attachedBody != null
            ? attachedBody.transform
            : itemRoot;
        PortalableObject portalable =
            componentRoot.GetComponentInParent<PortalableObject>();
        if (portalable == null)
            portalable = componentRoot.gameObject.AddComponent<PortalableObject>();

        return portalable;
    }

    protected virtual void Awake()
    {
        body = GetComponent<Rigidbody>();
        characterController = GetComponentInChildren<CharacterController>(true);
        RefreshColliders();
        CaptureCurrentPose();
    }

    private void OnEnable()
    {
        CaptureCurrentPose();
    }

    private void OnDisable()
    {
        recentExitPortal = null;
        if (occupiedPortal != null)
        {
            occupiedPortal.SetPortalableObjectOccupancy(this, false);
            occupiedPortal = null;
        }

        DestroyVisualClone();
    }

    private void OnDestroy()
    {
        if (occupiedPortal != null)
        {
            occupiedPortal.SetPortalableObjectOccupancy(this, false);
            occupiedPortal = null;
        }
        DestroyVisualClone();
    }

    private void FixedUpdate()
    {
        // Dynamic rigidbodies are advanced by Unity between fixed ticks. Check
        // each tick so a fast object cannot cross an entire portal between
        // rendered frames.
        if (body != null && !body.isKinematic)
        {
            ProcessMovement(true);
            ResolveRigidbodyPortalSpaceCollision();
        }
    }

    private void LateUpdate()
    {
        // This also catches CharacterControllers and objects moved directly
        // from Update/LateUpdate, neither of which needs a Rigidbody trigger.
        if (body == null || body.isKinematic)
            ProcessMovement();

        UpdateVisualClone();
    }

    /// <summary>
    /// Can be called by a custom movement controller immediately after it
    /// updates the object's transform. It is safe to call in addition to the
    /// automatic polling; duplicate samples have no effect.
    /// </summary>
    public virtual void Warp()
    {
        ProcessMovement();
    }

    /// <summary>
    /// Refreshes portal occupancy after a collider trigger callback. Polling
    /// remains authoritative, so trigger callbacks and a Rigidbody are not
    /// required for the object to traverse a portal.
    /// </summary>
    public void RefreshPortalState()
    {
        UpdatePortalOccupancy(GetObjectBounds());
    }

    public void ClearPortalOccupancy(Portal portal)
    {
        if (occupiedPortal != portal)
            return;

        occupiedPortal = null;
        DestroyVisualClone();
    }

    private void ProcessMovement(bool physicsStep = false)
    {
        if (body == null)
            body = GetComponent<Rigidbody>();
        if (characterController == null)
            characterController = GetComponentInChildren<CharacterController>(true);
        if (objectColliders.Length == 0)
            RefreshColliders();

        Vector3 currentPosition = body != null ? body.position : transform.position;
        Quaternion currentRotation = body != null ? body.rotation : transform.rotation;
        Bounds bounds = GetObjectBounds();
        Portal previousPortalEnvironment = occupiedPortal;
        UpdatePortalOccupancy(bounds);
        Portal portalEnvironment = occupiedPortal != null
            ? occupiedPortal
            : previousPortalEnvironment;

        if (!hasPreviousPose)
        {
            previousBoundsCenter = bounds.center;
            previousBoundsExtents = bounds.extents;
            previousRootPosition = currentPosition;
            previousRootRotation = currentRotation;
            hasPreviousPose = true;
            return;
        }

        if (recentExitPortal != null)
        {
            Vector3 exitNormal =
                Portal.GetPortalPlaneNormal(recentExitPortal);
            float exitDistance = Vector3.Dot(
                bounds.center - recentExitPortal.transform.position, exitNormal);
            float normalExtent = Mathf.Abs(exitNormal.x) * bounds.extents.x +
                Mathf.Abs(exitNormal.y) * bounds.extents.y +
                Mathf.Abs(exitNormal.z) * bounds.extents.z;
            if (exitDistance >
                Portal.PortalableObjectExitClearance + normalExtent)
                recentExitPortal = null;
        }

        Vector3 halfExtents = Vector3.Max(previousBoundsExtents, bounds.extents);
        bool controllerHandlesCollision = characterController != null &&
            characterController.enabled;
        Vector3 positionBeforeCollisionResolution = currentPosition;
        if ((body == null || body.isKinematic) &&
            !controllerHandlesCollision &&
            resolveTransformCollisions && objectColliders.Length > 0)
        {
            ResolveTransformCollisions(
                previousBoundsCenter,
                bounds.center,
                halfExtents,
                ref currentPosition);
            if (currentPosition != transform.position)
            {
                if (body != null)
                    body.position = currentPosition;
                else
                    transform.position = currentPosition;
                Physics.SyncTransforms();
                bounds = GetObjectBounds();
            }
        }
        else if ((body == null || body.isKinematic) &&
                 controllerHandlesCollision)
        {
            ResolvePortalSpaceCollisions(
                previousBoundsCenter,
                bounds.center,
                halfExtents,
                ref currentPosition);
            if (currentPosition != positionBeforeCollisionResolution)
            {
                transform.position = currentPosition;
                Physics.SyncTransforms();
                bounds = GetObjectBounds();
            }
        }

        Vector3 transformedPosition = currentPosition;
        Quaternion transformedRotation = currentRotation;
        Portal entryPortal;
        Portal exitPortal;
        Quaternion portalRotation;
        float crossingFraction;
        bool crossed = Portal.TryHandlePortalableObjectMovement(
            previousBoundsCenter,
            bounds.center,
            halfExtents,
            portalEnvironment,
            recentExitPortal,
            ref transformedPosition,
            ref transformedRotation,
            out entryPortal,
            out exitPortal,
            out portalRotation,
            out crossingFraction);

        if (crossed)
        {
            Vector3 sourceLinearVelocity = body != null
                ? body.linearVelocity
                : Vector3.zero;
            Vector3 sourceAngularVelocity = body != null
                ? body.angularVelocity
                : Vector3.zero;

            if (body != null)
            {
                body.position = transformedPosition;
                body.rotation = transformedRotation;
                Vector3 transformedVelocity = body.linearVelocity;
                if (physicsStep && !body.isKinematic && body.useGravity)
                {
                    Vector3 postCrossingGravity = Physics.gravity *
                        Time.fixedDeltaTime * (1f - crossingFraction);
                    transformedVelocity -= postCrossingGravity;
                    transformedVelocity = portalRotation * transformedVelocity;
                    transformedVelocity += postCrossingGravity * 1.008f;
                }
                else
                {
                    transformedVelocity =
                        portalRotation * transformedVelocity;
                }

                body.linearVelocity = maxSpeed > 0f
                    ? Vector3.ClampMagnitude(transformedVelocity, maxSpeed)
                    : transformedVelocity;
                body.angularVelocity = portalRotation * body.angularVelocity;

                Vector3 exitForward = Portal.GetPortalPlaneNormal(exitPortal);
                if (exitForward.sqrMagnitude > 0.000001f)
                {
                    exitForward.Normalize();
                    Vector3 exitCenter = PortalTransformMath.TransformPoint(
                        entryPortal, exitPortal, bounds.center);
                    float minimumExitSpeed =
                        Portal.GetSourceMinimumPortalableExitSpeed(
                            exitPortal,
                            exitCenter,
                            Portal.TransformAabbHalfExtents(
                                bounds.extents, portalRotation),
                            Physics.gravity.magnitude);
                    float forwardSpeed = Vector3.Dot(
                        body.linearVelocity, exitForward);
                    if (forwardSpeed < minimumExitSpeed)
                    {
                        body.linearVelocity += exitForward *
                            (minimumExitSpeed - forwardSpeed);
                    }
                }
            }
            else
            {
                transform.SetPositionAndRotation(
                    transformedPosition, transformedRotation);
            }

            // Keep the root pose in the destination world before any exit
            // recovery. The old transform-only depenetration path used the
            // pre-teleport currentPosition here, which could write the source
            // pose back onto an object embedded at the exit.
            currentPosition = transformedPosition;
            Physics.SyncTransforms();

            bool shouldRecoverExit = objectColliders.Length > 0 &&
                (resolveTransformCollisions ||
                 (body != null && !body.isKinematic));
            bool pushedForward = false;
            bool exitWasBlocked = false;
            if (shouldRecoverExit)
            {
                Bounds exitBounds = GetObjectBounds();
                Vector3 recoveredPosition = currentPosition;
                pushedForward = TryPushPortalableObjectOutOfExit(
                    exitPortal,
                    currentPosition,
                    exitBounds,
                    ref recoveredPosition,
                    out exitWasBlocked);
                if (pushedForward)
                {
                    currentPosition = recoveredPosition;
                    if (body != null)
                        body.position = currentPosition;
                    else
                        transform.position = currentPosition;
                    Physics.SyncTransforms();
                }
            }

            if (exitWasBlocked && !pushedForward)
            {
                // The exit remains solid after the forward search. Reject the
                // crossing and restore the last source-side pose rather than
                // leaving a Rigidbody or transform-driven prop embedded at
                // the destination.
                currentPosition = previousRootPosition;
                currentRotation = previousRootRotation;
                if (body != null)
                {
                    body.position = currentPosition;
                    body.rotation = currentRotation;
                    body.linearVelocity = sourceLinearVelocity;
                    body.angularVelocity = sourceAngularVelocity;

                    if (!body.isKinematic)
                    {
                        Vector3 entryNormal = entryPortal != null
                            ? Portal.GetPortalPlaneNormal(entryPortal)
                            : Vector3.zero;
                        if (entryNormal.sqrMagnitude > 0.000001f)
                        {
                            entryNormal.Normalize();
                            float inwardSpeed = Vector3.Dot(
                                body.linearVelocity, entryNormal);
                            if (inwardSpeed < 0f)
                                body.linearVelocity -=
                                    entryNormal * inwardSpeed;
                        }
                    }
                }
                else
                {
                    transform.SetPositionAndRotation(
                        currentPosition, currentRotation);
                }

                Physics.SyncTransforms();
                currentPosition = FindSafePortalEntryRollbackPosition(
                    entryPortal, currentPosition);
                if (body != null)
                    body.position = currentPosition;
                else
                    transform.position = currentPosition;
                Physics.SyncTransforms();
                bounds = GetObjectBounds();
                UpdatePortalOccupancy(bounds);
            }
            else
            {
                recentExitPortal = exitPortal;
                OnPortalTeleported(entryPortal, exitPortal, portalRotation);
                Teleported?.Invoke(entryPortal, exitPortal, portalRotation);
                bounds = GetObjectBounds();
                UpdatePortalOccupancy(bounds);

                // Positional recovery can leave a dynamic prop just clear of
                // a nearby collider. Clip its velocity before the next
                // physics step, like the player's slide move, instead of
                // transferring an impulse to that collider.
                if (pushedForward && body != null && !body.isKinematic)
                    ClipPortalExitVelocityAgainstNearbyColliders();
            }
        }

        previousBoundsCenter = bounds.center;
        previousBoundsExtents = bounds.extents;
        previousRootPosition = body != null ? body.position : transform.position;
        previousRootRotation = body != null ? body.rotation : transform.rotation;
        hasPreviousPose = true;
    }

    /// <summary>
    /// Source's portal simulator sweeps a touching prop through the linked
    /// world before the prop's centre crosses the portal plane. Apply that
    /// remote contact to the real Rigidbody in entry-space so its normal
    /// physics step slides or stops at the same contact instead of tunnelling
    /// through an obstacle beside the exit portal.
    /// </summary>
    private void ResolveRigidbodyPortalSpaceCollision()
    {
        if (body == null || body.isKinematic || !body.detectCollisions)
            return;

        float step = Time.fixedDeltaTime;
        if (step <= 0f)
            return;

        Bounds bounds = GetObjectBounds();
        Vector3 gravityDisplacement = body.useGravity
            ? Physics.gravity * (0.5f * step * step)
            : Vector3.zero;
        Vector3 predictedDelta = body.linearVelocity * step +
            gravityDisplacement;
        RaycastHit remoteHit;
        if (!Portal.TryGetPortalableObjectSpaceCollision(
                bounds.center,
                bounds.extents,
                predictedDelta,
                GetTransformCollisionMask(),
                body.transform,
                recentExitPortal,
                out remoteHit) ||
            remoteHit.collider == null ||
            remoteHit.distance >= predictedDelta.magnitude ||
            Vector3.Dot(predictedDelta, remoteHit.normal) >= 0f)
            return;

        Vector3 direction = predictedDelta.normalized;
        float beforeContact = Mathf.Max(
            0f, remoteHit.distance - TransformCollisionSkin);
        Vector3 stepToContact = direction * beforeContact;
        Vector3 remaining = predictedDelta - stepToContact;
        Vector3 slideRemainder = Vector3.ProjectOnPlane(
            remaining, remoteHit.normal);
        Vector3 resolvedDisplacement = stepToContact + slideRemainder;

        // Rigidbody integrates gravity after FixedUpdate. Subtract its
        // expected half-step displacement here so the ensuing integration
        // lands at the swept contact and slides through the remaining time.
        body.linearVelocity =
            (resolvedDisplacement - gravityDisplacement) / step;
    }

    protected virtual void OnPortalTeleported(
        Portal entryPortal,
        Portal exitPortal,
        Quaternion portalRotation)
    {
    }

    private void CaptureCurrentPose()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();
        if (objectColliders.Length == 0)
            RefreshColliders();

        Bounds bounds = GetObjectBounds();
        previousBoundsCenter = bounds.center;
        previousBoundsExtents = bounds.extents;
        previousRootPosition = body != null ? body.position : transform.position;
        previousRootRotation = body != null ? body.rotation : transform.rotation;
        hasPreviousPose = true;
    }

    private void RefreshColliders()
    {
        Collider[] discovered = GetComponentsInChildren<Collider>(true);
        List<Collider> physical = new List<Collider>(discovered.Length);
        for (int i = 0; i < discovered.Length; ++i)
        {
            if (discovered[i] != null && !discovered[i].isTrigger)
                physical.Add(discovered[i]);
        }

        objectColliders = physical.ToArray();
    }

    private void ResolveTransformCollisions(
        Vector3 startCenter,
        Vector3 desiredCenter,
        Vector3 halfExtents,
        ref Vector3 rootPosition)
    {
        Vector3 remaining = desiredCenter - startCenter;
        if (remaining.sqrMagnitude <= 0.0000001f)
            return;

        int layerMask = GetTransformCollisionMask();
        Vector3 resolvedCenter = startCenter;
        for (int iteration = 0;
             iteration < MaxTransformSlideIterations &&
             remaining.sqrMagnitude > 0.0000001f;
             ++iteration)
        {
            float distance = remaining.magnitude;
            Vector3 direction = remaining / distance;
            int hitCount;
            while (true)
            {
                hitCount = Physics.BoxCastNonAlloc(
                    resolvedCenter,
                    halfExtents,
                    direction,
                    TransformCollisionHits,
                    Quaternion.identity,
                    distance,
                    layerMask,
                    QueryTriggerInteraction.Ignore);
                if (hitCount < TransformCollisionHits.Length)
                    break;

                // A full non-alloc buffer may omit the nearest hit. Grow and
                // repeat so crowded scenes cannot change the collision result.
                System.Array.Resize(
                    ref TransformCollisionHits,
                    TransformCollisionHits.Length * 2);
            }

            bool foundBlocker = false;
            RaycastHit nearest = default;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; ++i)
            {
                RaycastHit hit = TransformCollisionHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null || IsOwnCollider(hitCollider))
                    continue;
                if (Portal.ShouldIgnoreMovementCollision(
                    hit,
                    resolvedCenter,
                    halfExtents,
                    Quaternion.identity,
                    remaining))
                    continue;

                if (hit.distance >= nearestDistance)
                    continue;

                nearest = hit;
                nearestDistance = hit.distance;
                foundBlocker = true;
            }

            RaycastHit remoteHit;
            if (Portal.TryGetPortalableObjectSpaceCollision(
                    resolvedCenter,
                    halfExtents,
                    remaining,
                    layerMask,
                    transform,
                    recentExitPortal,
                    out remoteHit) &&
                remoteHit.distance < nearestDistance)
            {
                nearest = remoteHit;
                nearestDistance = remoteHit.distance;
                foundBlocker = true;
            }

            if (!foundBlocker)
            {
                resolvedCenter += remaining;
                remaining = Vector3.zero;
                break;
            }

            float stepDistance = Mathf.Max(
                0f, nearest.distance - TransformCollisionSkin);
            Vector3 step = direction * stepDistance;
            resolvedCenter += step;
            Vector3 leftover = remaining - step;
            Vector3 slide = nearest.normal.sqrMagnitude > 0.000001f
                ? Vector3.ProjectOnPlane(leftover, nearest.normal)
                : Vector3.zero;

            if ((slide - leftover).sqrMagnitude <= 0.0000001f)
            {
                remaining = Vector3.zero;
                break;
            }

            remaining = slide;
        }

        rootPosition += resolvedCenter - desiredCenter;
    }

    private void ResolvePortalSpaceCollisions(
        Vector3 startCenter,
        Vector3 desiredCenter,
        Vector3 halfExtents,
        ref Vector3 rootPosition)
    {
        Vector3 remaining = desiredCenter - startCenter;
        float distance = remaining.magnitude;
        if (distance <= 0.000001f)
            return;

        RaycastHit remoteHit;
        if (!Portal.TryGetPortalableObjectSpaceCollision(
                startCenter,
                halfExtents,
                remaining,
                GetTransformCollisionMask(),
                transform,
                recentExitPortal,
                out remoteHit) ||
            remoteHit.collider == null ||
            remoteHit.distance >= distance)
            return;

        Vector3 direction = remaining / distance;
        float stepDistance = Mathf.Max(
            0f, remoteHit.distance - TransformCollisionSkin);
        Vector3 step = direction * stepDistance;
        Vector3 leftover = remaining - step;
        Vector3 slide = Vector3.ProjectOnPlane(leftover, remoteHit.normal);
        Vector3 resolvedCenter = startCenter + step + slide;
        rootPosition += resolvedCenter - desiredCenter;
    }

    private void ResolveExitPenetration(ref Vector3 rootPosition)
    {
        if (objectColliders.Length == 0)
            return;

        int layerMask = GetTransformCollisionMask();
        for (int iteration = 0;
             iteration < MaxExitDepenetrationIterations;
             ++iteration)
        {
            bool moved = false;
            for (int objectIndex = 0;
                 objectIndex < objectColliders.Length;
                 ++objectIndex)
            {
                Collider objectCollider = objectColliders[objectIndex];
                if (objectCollider == null || objectCollider.isTrigger ||
                    !objectCollider.enabled ||
                    !objectCollider.gameObject.activeInHierarchy)
                    continue;

                Bounds bounds = objectCollider.bounds;
                int overlapCount;
                while (true)
                {
                    overlapCount = Physics.OverlapBoxNonAlloc(
                        bounds.center,
                        bounds.extents,
                        TransformOverlapHits,
                        Quaternion.identity,
                        layerMask,
                        QueryTriggerInteraction.Ignore);
                    if (overlapCount < TransformOverlapHits.Length)
                        break;

                    System.Array.Resize(
                        ref TransformOverlapHits,
                        TransformOverlapHits.Length * 2);
                }

                for (int overlapIndex = 0;
                     overlapIndex < overlapCount;
                     ++overlapIndex)
                {
                    Collider other = TransformOverlapHits[overlapIndex];
                    if (other == null || other == objectCollider ||
                        IsOwnCollider(other) ||
                        Portal.IsPortalOwnedCollider(other))
                        continue;

                    Vector3 direction;
                    float distance;
                    if (!Physics.ComputePenetration(
                            objectCollider,
                            objectCollider.transform.position,
                            objectCollider.transform.rotation,
                            other,
                            other.transform.position,
                            other.transform.rotation,
                            out direction,
                            out distance) ||
                        distance <= 0f)
                        continue;

                    Vector3 correction = direction *
                        (distance + TransformCollisionSkin);
                    rootPosition += correction;
                    if (body != null)
                        body.position = rootPosition;
                    else
                        transform.position = rootPosition;
                    Physics.SyncTransforms();
                    moved = true;
                    break;
                }

                if (moved)
                    break;
            }

            if (!moved)
                break;
        }
    }

    /// <summary>
    /// Mirrors the player's exit recovery: if the mapped object hull overlaps
    /// blocking geometry, probe forward in one-Hammer-unit increments up to
    /// 100 HU and place it at the first clear point.
    /// </summary>
    private bool TryPushPortalableObjectOutOfExit(
        Portal exitPortal,
        Vector3 currentRootPosition,
        Bounds currentBounds,
        ref Vector3 recoveredRootPosition,
        out bool exitWasBlocked)
    {
        exitWasBlocked = false;
        if (exitPortal == null || objectColliders.Length == 0)
            return false;

        Vector3 exitNormal = Portal.GetPortalPlaneNormal(exitPortal);
        if (exitNormal.sqrMagnitude < 0.000001f)
            return false;
        exitNormal.Normalize();

        int layerMask = GetTransformCollisionMask();
        if (IsPortalableObjectExitPoseClear(
                currentRootPosition,
                currentRootPosition,
                currentBounds,
                layerMask))
            return false;
        exitWasBlocked = true;

        for (int distanceHu = 1;
             distanceHu <= MaxPortalExitSearchDistanceHu;
             ++distanceHu)
        {
            Vector3 candidateRootPosition = currentRootPosition +
                exitNormal * (distanceHu * HammerUnitScale);
            Vector3 candidateDelta =
                candidateRootPosition - currentRootPosition;
            Vector3 candidateCenter = currentBounds.center + candidateDelta;
            if (Vector3.Dot(
                    candidateCenter - exitPortal.transform.position,
                    exitNormal) < 0f)
                continue;

            if (!IsPortalableObjectExitPoseClear(
                    candidateRootPosition,
                    currentRootPosition,
                    currentBounds,
                    layerMask))
                continue;

            recoveredRootPosition = candidateRootPosition;
            return true;
        }

        return false;
    }

    private void ClipPortalExitVelocityAgainstNearbyColliders()
    {
        if (body == null || body.isKinematic || objectColliders.Length == 0)
            return;

        Vector3 velocity = body.linearVelocity;
        if (velocity.sqrMagnitude <= 0.000001f)
            return;

        Bounds bounds = GetObjectBounds();
        int layerMask = GetTransformCollisionMask();
        float castSkin = Mathf.Max(
            TransformCollisionSkin,
            Physics.defaultContactOffset);

        for (int planeCount = 0; planeCount < 4; ++planeCount)
        {
            float speed = velocity.magnitude;
            if (speed <= 0.0001f)
                break;

            Vector3 direction = velocity / speed;
            Vector3 sweepDelta = velocity * Time.fixedDeltaTime;
            float distance = sweepDelta.magnitude + castSkin;
            int hitCount;
            while (true)
            {
                hitCount = Physics.BoxCastNonAlloc(
                    bounds.center,
                    bounds.extents + Vector3.one * castSkin,
                    direction,
                    TransformCollisionHits,
                    Quaternion.identity,
                    distance,
                    layerMask,
                    QueryTriggerInteraction.Ignore);
                if (hitCount < TransformCollisionHits.Length)
                    break;

                System.Array.Resize(
                    ref TransformCollisionHits,
                    TransformCollisionHits.Length * 2);
            }

            RaycastHit nearest = default;
            float nearestDistance = float.PositiveInfinity;
            for (int hitIndex = 0; hitIndex < hitCount; ++hitIndex)
            {
                RaycastHit hit = TransformCollisionHits[hitIndex];
                Collider other = hit.collider;
                if (other == null || IsOwnCollider(other) ||
                    Portal.IsPortalOwnedCollider(other) ||
                    !ObjectCollidesWithLayer(other.gameObject.layer) ||
                    Portal.ShouldIgnoreMovementCollision(
                        hit,
                        bounds.center,
                        bounds.extents,
                        Quaternion.identity,
                        sweepDelta) ||
                    hit.distance >= nearestDistance)
                    continue;

                nearest = hit;
                nearestDistance = hit.distance;
            }

            if (nearest.collider == null)
                break;

            Vector3 normal = nearest.normal.normalized;
            float inwardSpeed = Vector3.Dot(velocity, normal);
            if (inwardSpeed >= -0.0001f)
                break;

            velocity -= normal * inwardSpeed;
        }

        body.linearVelocity = velocity;
    }

    private bool ObjectCollidesWithLayer(int otherLayer)
    {
        for (int colliderIndex = 0;
             colliderIndex < objectColliders.Length;
             ++colliderIndex)
        {
            Collider objectCollider = objectColliders[colliderIndex];
            if (objectCollider == null || objectCollider.isTrigger ||
                !objectCollider.enabled ||
                !objectCollider.gameObject.activeInHierarchy)
                continue;

            if (!Physics.GetIgnoreLayerCollision(
                    objectCollider.gameObject.layer, otherLayer))
                return true;
        }

        return false;
    }

    private Vector3 FindSafePortalEntryRollbackPosition(
        Portal entryPortal,
        Vector3 sourceRootPosition)
    {
        if (entryPortal == null)
            return sourceRootPosition;

        Vector3 entryNormal = Portal.GetPortalPlaneNormal(entryPortal);
        if (entryNormal.sqrMagnitude < 0.000001f)
            return sourceRootPosition;
        entryNormal.Normalize();

        Bounds sourceBounds = GetObjectBounds();
        float centerDistance = Vector3.Dot(
            sourceBounds.center - entryPortal.transform.position,
            entryNormal);
        float boundsExtentAlongNormal =
            Mathf.Abs(entryNormal.x) * sourceBounds.extents.x +
            Mathf.Abs(entryNormal.y) * sourceBounds.extents.y +
            Mathf.Abs(entryNormal.z) * sourceBounds.extents.z;
        float requiredBackoff = Mathf.Max(
            0f,
            boundsExtentAlongNormal + TransformCollisionSkin - centerDistance);
        int firstDistanceHu = Mathf.CeilToInt(
            requiredBackoff / HammerUnitScale);
        int layerMask = GetTransformCollisionMask();

        // Keep the complete object bounds on the source side and select a
        // collision-free rollback point before releasing the blocked move.
        for (int distanceHu = firstDistanceHu;
             distanceHu <= firstDistanceHu + MaxPortalExitSearchDistanceHu;
             ++distanceHu)
        {
            Vector3 candidateRootPosition = sourceRootPosition +
                entryNormal * (distanceHu * HammerUnitScale);
            if (IsPortalableObjectExitPoseClear(
                    candidateRootPosition,
                    sourceRootPosition,
                    sourceBounds,
                    layerMask))
                return candidateRootPosition;
        }

        return sourceRootPosition;
    }

    private bool IsPortalableObjectExitPoseClear(
        Vector3 candidateRootPosition,
        Vector3 currentRootPosition,
        Bounds currentBounds,
        int layerMask)
    {
        Vector3 rootDelta = candidateRootPosition - currentRootPosition;
        Bounds candidateBounds = currentBounds;
        candidateBounds.center += rootDelta;
        Vector3 queryHalfExtents = candidateBounds.extents +
            Vector3.one * TransformCollisionSkin;

        int overlapCount;
        while (true)
        {
            overlapCount = Physics.OverlapBoxNonAlloc(
                candidateBounds.center,
                queryHalfExtents,
                TransformOverlapHits,
                Quaternion.identity,
                layerMask,
                QueryTriggerInteraction.Ignore);
            if (overlapCount < TransformOverlapHits.Length)
                break;

            // A full buffer can hide the collider that actually blocks the
            // exit. Grow and repeat only in crowded overlaps.
            System.Array.Resize(
                ref TransformOverlapHits,
                TransformOverlapHits.Length * 2);
        }

        for (int overlapIndex = 0;
             overlapIndex < overlapCount;
             ++overlapIndex)
        {
            Collider other = TransformOverlapHits[overlapIndex];
            if (other == null || IsOwnCollider(other) ||
                Portal.IsPortalOwnedCollider(other))
                continue;

            if (Portal.ShouldIgnoreMovementCollision(
                    other,
                    candidateBounds.center,
                    candidateBounds.extents,
                    Quaternion.identity))
                continue;

            int otherLayer = other.gameObject.layer;
            for (int objectIndex = 0;
                 objectIndex < objectColliders.Length;
                 ++objectIndex)
            {
                Collider objectCollider = objectColliders[objectIndex];
                if (objectCollider == null || objectCollider.isTrigger ||
                    !objectCollider.enabled ||
                    !objectCollider.gameObject.activeInHierarchy ||
                    Physics.GetIgnoreLayerCollision(
                        objectCollider.gameObject.layer, otherLayer))
                    continue;

                Vector3 colliderPosition =
                    objectCollider.transform.position + rootDelta;
                if (Physics.ComputePenetration(
                        objectCollider,
                        colliderPosition,
                        objectCollider.transform.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out _,
                        out float penetration) &&
                    penetration > TransformCollisionSkin)
                    return false;
            }
        }

        return true;
    }

    private bool IsOwnCollider(Collider candidate)
    {
        Transform candidateTransform = candidate.transform;
        return candidateTransform == transform ||
            candidateTransform.IsChildOf(transform);
    }

    private int GetTransformCollisionMask()
    {
        int layerMask = 0;
        bool foundCollider = false;
        for (int colliderIndex = 0;
             colliderIndex < objectColliders.Length;
             ++colliderIndex)
        {
            Collider objectCollider = objectColliders[colliderIndex];
            if (objectCollider == null || objectCollider.isTrigger ||
                !objectCollider.enabled ||
                !objectCollider.gameObject.activeInHierarchy)
                continue;

            foundCollider = true;
            int sourceLayer = objectCollider.gameObject.layer;
            int colliderMask = Physics.DefaultRaycastLayers;
            for (int layer = 0; layer < 32; ++layer)
            {
                if (Physics.GetIgnoreLayerCollision(sourceLayer, layer))
                    colliderMask &= ~(1 << layer);
            }

            // A compound, transform-driven object can have child colliders
            // on different layers. Include the layers that any of its
            // colliders is allowed to touch.
            layerMask |= colliderMask;
        }

        if (foundCollider)
            return layerMask;

        int rootMask = Physics.DefaultRaycastLayers;
        for (int layer = 0; layer < 32; ++layer)
        {
            if (Physics.GetIgnoreLayerCollision(gameObject.layer, layer))
                rootMask &= ~(1 << layer);
        }

        return rootMask;
    }

    private Bounds GetObjectBounds()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < objectColliders.Length; ++i)
        {
            Collider objectCollider = objectColliders[i];
            if (objectCollider == null || objectCollider.isTrigger ||
                !objectCollider.enabled ||
                !objectCollider.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = objectCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(objectCollider.bounds);
            }
        }

        // Collider-free visual objects can still be moved through a portal as
        // points; the collider is only needed for physical collision response.
        return bounds;
    }

    private void UpdatePortalOccupancy(Bounds bounds)
    {
        Portal nextPortal = Portal.FindPortalableObjectOccupancy(bounds);
        if (nextPortal == occupiedPortal)
            return;

        if (occupiedPortal != null)
            occupiedPortal.SetPortalableObjectOccupancy(this, false);
        DestroyVisualClone();

        occupiedPortal = nextPortal;
        if (occupiedPortal != null)
        {
            occupiedPortal.SetPortalableObjectOccupancy(this, true);
            BuildVisualClone();
        }
    }

    private void BuildVisualClone()
    {
        int portalGhostLayer = LayerMask.NameToLayer(PortalGhostLayerName);
        if (portalGhostLayer < 0)
        {
            Debug.LogError(
                "PortalableObject requires the '" + PortalGhostLayerName +
                "' layer so its render proxy stays out of the player camera.",
                this);
            return;
        }

        cloneObject = new GameObject(name + " Portal Clone");
        cloneObject.transform.localScale = transform.lossyScale;
        cloneObject.SetActive(false);
        cloneObject.layer = portalGhostLayer;
        cloneTransforms.Clear();
        visualParts.Clear();

        Transform[] sourceTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < sourceTransforms.Length; ++i)
        {
            Transform source = sourceTransforms[i];
            Transform clone;
            if (source == transform)
            {
                clone = cloneObject.transform;
            }
            else
            {
                GameObject child = new GameObject(source.name + " Portal Clone");
                clone = child.transform;
                clone.SetParent(cloneTransforms[source.parent], false);
                clone.localPosition = source.localPosition;
                clone.localRotation = source.localRotation;
                clone.localScale = source.localScale;
                clone.gameObject.SetActive(source.gameObject.activeSelf);
            }

            clone.gameObject.layer = portalGhostLayer;
            cloneTransforms[source] = clone;
            CopyVisualComponents(source, clone);
        }

        for (int i = 0; i < visualParts.Count; ++i)
            CacheCloneBones(visualParts[i]);
    }

    private void DestroyVisualClone()
    {
        if (cloneObject != null)
        {
            cloneObject.SetActive(false);
            Destroy(cloneObject);
            cloneObject = null;
        }

        cloneTransforms.Clear();
        visualParts.Clear();
    }

    private void CopyVisualComponents(Transform source, Transform clone)
    {
        MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
        SkinnedMeshRenderer sourceSkinned =
            source.GetComponent<SkinnedMeshRenderer>();
        if (sourceFilter == null && sourceRenderer == null &&
            sourceSkinned == null)
            return;

        VisualPart part = new VisualPart
        {
            Source = source,
            Clone = clone,
            SourceMeshFilter = sourceFilter,
            SourceMeshRenderer = sourceRenderer,
            SourceSkinnedRenderer = sourceSkinned
        };

        if (sourceFilter != null)
        {
            part.CloneMeshFilter = clone.gameObject.AddComponent<MeshFilter>();
            part.CloneMeshFilter.sharedMesh = sourceFilter.sharedMesh;
        }
        if (sourceRenderer != null)
        {
            part.CloneMeshRenderer = clone.gameObject.AddComponent<MeshRenderer>();
            part.SourceMeshMaterials = new List<Material>(4);
            part.CachedMeshMaterials = new List<Material>(4);
            CopyRendererSettings(
                sourceRenderer,
                part.CloneMeshRenderer,
                part.SourceMeshMaterials,
                part.CachedMeshMaterials);
        }
        if (sourceSkinned != null)
        {
            part.CloneSkinnedRenderer =
                clone.gameObject.AddComponent<SkinnedMeshRenderer>();
            part.CloneSkinnedRenderer.sharedMesh = sourceSkinned.sharedMesh;
            part.CloneSkinnedRenderer.localBounds = sourceSkinned.localBounds;
            part.CloneSkinnedRenderer.quality = sourceSkinned.quality;
            part.CloneSkinnedRenderer.updateWhenOffscreen =
                sourceSkinned.updateWhenOffscreen;
            part.CloneSkinnedRenderer.skinnedMotionVectors =
                sourceSkinned.skinnedMotionVectors;
            part.SourceSkinnedMaterials = new List<Material>(4);
            part.CachedSkinnedMaterials = new List<Material>(4);
            CopyRendererSettings(
                sourceSkinned,
                part.CloneSkinnedRenderer,
                part.SourceSkinnedMaterials,
                part.CachedSkinnedMaterials);
        }

        visualParts.Add(part);
    }

    private static void CopyRendererSettings(
        Renderer source,
        Renderer clone,
        List<Material> sourceMaterials,
        List<Material> cachedMaterials)
    {
        sourceMaterials.Clear();
        source.GetSharedMaterials(sourceMaterials);
        bool materialsChanged =
            sourceMaterials.Count != cachedMaterials.Count;
        for (int i = 0; !materialsChanged && i < sourceMaterials.Count; ++i)
            materialsChanged = sourceMaterials[i] != cachedMaterials[i];

        if (materialsChanged)
        {
            clone.sharedMaterials = sourceMaterials.ToArray();
            cachedMaterials.Clear();
            cachedMaterials.AddRange(sourceMaterials);
        }

        clone.enabled = source.enabled;
        clone.shadowCastingMode = source.shadowCastingMode;
        clone.receiveShadows = source.receiveShadows;
        clone.lightProbeUsage = source.lightProbeUsage;
        clone.reflectionProbeUsage = source.reflectionProbeUsage;
        clone.motionVectorGenerationMode = source.motionVectorGenerationMode;
        clone.renderingLayerMask = source.renderingLayerMask;
        clone.sortingLayerID = source.sortingLayerID;
        clone.sortingOrder = source.sortingOrder;
        clone.lightmapIndex = source.lightmapIndex;
        clone.realtimeLightmapIndex = source.realtimeLightmapIndex;
        clone.lightmapScaleOffset = source.lightmapScaleOffset;
    }

    private void CacheCloneBones(VisualPart part)
    {
        SkinnedMeshRenderer source = part.SourceSkinnedRenderer;
        SkinnedMeshRenderer clone = part.CloneSkinnedRenderer;
        if (source == null || clone == null)
            return;

        Transform[] sourceBones = source.bones;
        Transform[] cloneBones = new Transform[sourceBones.Length];
        for (int i = 0; i < sourceBones.Length; ++i)
            cloneBones[i] = MapCloneBone(sourceBones[i]);

        part.CloneBones = cloneBones;
        part.CachedSkinnedMesh = source.sharedMesh;
        clone.sharedMesh = source.sharedMesh;
        clone.rootBone = MapCloneBone(source.rootBone);
        clone.bones = cloneBones;
    }

    private void UpdateVisualClone()
    {
        if (cloneObject == null)
            return;

        if (occupiedPortal == null ||
            !Portal.IsPortalPairActive(occupiedPortal))
        {
            cloneObject.SetActive(false);
            return;
        }

        Portal.TransformPortalPose(
            occupiedPortal,
            occupiedPortal.GetOtherPortal(),
            transform.position,
            transform.rotation,
            out Vector3 clonePosition,
            out Quaternion cloneRotation);
        cloneObject.transform.SetPositionAndRotation(clonePosition, cloneRotation);
        cloneObject.transform.localScale = transform.lossyScale;

        foreach (KeyValuePair<Transform, Transform> transformPair in
            cloneTransforms)
        {
            Transform source = transformPair.Key;
            Transform clone = transformPair.Value;
            if (source == null || clone == null || source == transform)
                continue;

            clone.localPosition = source.localPosition;
            clone.localRotation = source.localRotation;
            clone.localScale = source.localScale;
            if (clone.gameObject.activeSelf != source.gameObject.activeSelf)
                clone.gameObject.SetActive(source.gameObject.activeSelf);
        }

        for (int i = 0; i < visualParts.Count; ++i)
        {
            VisualPart part = visualParts[i];
            if (part.SourceMeshFilter != null && part.CloneMeshFilter != null)
                part.CloneMeshFilter.sharedMesh = part.SourceMeshFilter.sharedMesh;
            if (part.SourceMeshRenderer != null && part.CloneMeshRenderer != null)
            {
                CopyRendererSettings(
                    part.SourceMeshRenderer,
                    part.CloneMeshRenderer,
                    part.SourceMeshMaterials,
                    part.CachedMeshMaterials);
            }
            if (part.SourceSkinnedRenderer != null &&
                part.CloneSkinnedRenderer != null)
            {
                SkinnedMeshRenderer source = part.SourceSkinnedRenderer;
                SkinnedMeshRenderer clone = part.CloneSkinnedRenderer;
                if (part.CachedSkinnedMesh != source.sharedMesh)
                    CacheCloneBones(part);
                clone.localBounds = source.localBounds;
                clone.enabled = source.enabled;
                Transform cloneRootBone = MapCloneBone(source.rootBone);
                if (clone.rootBone != cloneRootBone)
                    clone.rootBone = cloneRootBone;
                CopyRendererSettings(
                    source,
                    clone,
                    part.SourceSkinnedMaterials,
                    part.CachedSkinnedMaterials);
            }
        }

        cloneObject.SetActive(true);
    }

    private Transform MapCloneBone(Transform sourceBone)
    {
        if (sourceBone == null)
            return null;
        Transform cloneBone;
        return cloneTransforms.TryGetValue(sourceBone, out cloneBone)
            ? cloneBone
            : sourceBone;
    }
}
