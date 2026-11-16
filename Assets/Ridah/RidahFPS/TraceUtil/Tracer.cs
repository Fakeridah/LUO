using UnityEngine;

namespace RidahFPS.TraceUtil
{
    public class Tracer
    {
        public static Trace TraceCollider(CharacterController collider, Vector3 origin, Vector3 end, int layerMask, float colliderScale = 1f)
        {

            // Capsule collider trace
            var capc = collider;

            Vector3 point1, point2;
            Controllers.SurfPhysics.GetCapsulePoints(capc, origin, out point1, out point2);

            return TraceCapsule(point1, point2, capc.radius, origin, end, capc.contactOffset, layerMask);

            //throw new System.NotImplementedException ("Trace missing for collider: " + collider.GetType ());

        }

        public static Trace TraceCapsule(Vector3 point1, Vector3 point2, float radius, Vector3 start, Vector3 destination, float contactOffset, int layerMask)
        {
            var result = new Trace()
            {
                StartPos = start,
                EndPos = destination
            };

            var longSide = Mathf.Sqrt(contactOffset * contactOffset + contactOffset * contactOffset);
            radius *= (1f - contactOffset);
            var direction = (destination - start).normalized;
            var maxDistance = (Vector3.Distance(start, destination) + longSide)*5;

            RaycastHit hit;
            if (Physics.CapsuleCast(
                point1: point1,
                point2: point2,
                radius: radius,
                direction: direction,
                hitInfo: out hit,
                maxDistance: maxDistance,
                layerMask: layerMask,   
                queryTriggerInteraction: QueryTriggerInteraction.Ignore))
            {
                result.Fraction = hit.distance / maxDistance;
                result.HitCollider = hit.collider;
                result.HitPoint = hit.point;
                result.PlaneNormal = hit.normal;
                result.distance = hit.distance;

                RaycastHit normalHit;
                Ray normalRay = new Ray(hit.point - direction * 0.001f, direction);
                if (hit.collider.Raycast(normalRay, out normalHit, 0.002f)){
                    result.PlaneNormal = normalHit.normal;
                }
            }
            else result.Fraction = 1;

            return result;
        }

        public static Trace TraceBox(Vector3 start, Vector3 destination, Vector3 extents, float contactOffset, int layerMask)
        {
            var result = new Trace()
            {
                StartPos = start,
                EndPos = destination
            };

            var longSide = Mathf.Sqrt(contactOffset * contactOffset + contactOffset * contactOffset);
            var direction = (destination - start).normalized;
            var maxDistance = Vector3.Distance(start, destination) + longSide;
            extents *= (1f - contactOffset);

            RaycastHit hit;
            if (Physics.BoxCast(center: start,
                halfExtents: extents,
                direction: direction,
                orientation: Quaternion.identity,
                maxDistance: maxDistance,
                hitInfo: out hit,
                layerMask: layerMask,
                queryTriggerInteraction: QueryTriggerInteraction.Ignore))
            {
                result.Fraction = hit.distance / maxDistance;
                result.HitCollider = hit.collider;
                result.HitPoint = hit.point;
                result.PlaneNormal = hit.normal;
                result.distance = hit.distance;

                RaycastHit normalHit;
                Ray normalRay = new Ray(hit.point - direction * 0.001f, direction);
                if (hit.collider.Raycast(normalRay, out normalHit, 0.002f))
                {

                    result.PlaneNormal = normalHit.normal;

                }
            }
            else
            {
                result.Fraction = 1;
            }

            return result;
        }
    }
}