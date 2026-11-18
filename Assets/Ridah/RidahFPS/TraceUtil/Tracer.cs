using UnityEngine;

namespace RidahFPS.TraceUtil
{
    public class Tracer
    {
        public static Trace TraceCollider(CharacterController collider, Vector3 origin, Vector3 end, int layerMask, float colliderScale = 1f)
        {
            float distanceToPoints = collider.height * 0.5f - collider.radius;
            Vector3 point1 = origin + collider.center + Vector3.up * distanceToPoints;
            Vector3 point2 = origin + collider.center - Vector3.up * distanceToPoints;
            return TraceCapsule(
                point1,
                point2,
                collider.radius,
                origin,
                end,
                collider.contactOffset,
                layerMask);
        }

        public static Trace TraceCapsule(Vector3 point1, Vector3 point2, float radius, Vector3 start, Vector3 destination, float contactOffset, int layerMask)
        {
            var result = new Trace
            {
                StartPos = start,
                EndPos = destination
            };

            float longSide = Mathf.Sqrt(contactOffset * contactOffset + contactOffset * contactOffset);
            radius *= 1f - contactOffset;
            Vector3 direction = (destination - start).normalized;
            float maxDistance = (Vector3.Distance(start, destination) + longSide) * 5f;

            if (Physics.CapsuleCast(
                point1,
                point2,
                radius,
                direction,
                out RaycastHit hit,
                maxDistance,
                layerMask,
                QueryTriggerInteraction.Ignore))
            {
                result.Fraction = hit.distance / maxDistance;
                result.HitCollider = hit.collider;
                result.HitPoint = hit.point;
                result.PlaneNormal = hit.normal;
                result.distance = hit.distance;

                Ray normalRay = new Ray(hit.point - direction * 0.001f, direction);
                if (hit.collider.Raycast(normalRay, out RaycastHit normalHit, 0.002f))
                    result.PlaneNormal = normalHit.normal;
            }
            else
            {
                result.Fraction = 1f;
            }

            return result;
        }

        public static Trace TraceBox(Vector3 start, Vector3 destination, Vector3 extents, float contactOffset, int layerMask)
        {
            var result = new Trace
            {
                StartPos = start,
                EndPos = destination
            };

            float longSide = Mathf.Sqrt(contactOffset * contactOffset + contactOffset * contactOffset);
            Vector3 direction = (destination - start).normalized;
            float maxDistance = Vector3.Distance(start, destination) + longSide;
            extents *= 1f - contactOffset;

            if (Physics.BoxCast(
                start,
                extents,
                direction,
                out RaycastHit hit,
                Quaternion.identity,
                maxDistance,
                layerMask,
                QueryTriggerInteraction.Ignore))
            {
                result.Fraction = hit.distance / maxDistance;
                result.HitCollider = hit.collider;
                result.HitPoint = hit.point;
                result.PlaneNormal = hit.normal;
                result.distance = hit.distance;

                Ray normalRay = new Ray(hit.point - direction * 0.001f, direction);
                if (hit.collider.Raycast(normalRay, out RaycastHit normalHit, 0.002f))
                    result.PlaneNormal = normalHit.normal;
            }
            else
            {
                result.Fraction = 1f;
            }

            return result;
        }
    }
}