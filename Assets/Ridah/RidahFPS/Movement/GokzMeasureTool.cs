using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Local equivalent of gokz-measure. Coordinates are reported in Hammer
    /// units, matching the movement and HUD conversion.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Measure")]
    public sealed class GokzMeasureTool : MonoBehaviour
    {
        [SerializeField] private bool hasFirstPoint;
        [SerializeField] private Vector3 firstPoint;
        [SerializeField] private Vector3 secondPoint;

        public bool HasFirstPoint => hasFirstPoint;
        public Vector3 FirstPoint => firstPoint;
        public Vector3 SecondPoint => secondPoint;
        public float DistanceHu => Vector3.Distance(firstPoint, secondPoint) *
            GokzKzTimerMovement.HammerUnitsPerUnityUnit;

        public void SetFirstPoint(Vector3 point)
        {
            firstPoint = point;
            secondPoint = point;
            hasFirstPoint = true;
        }

        public void SetSecondPoint(Vector3 point)
        {
            if (!hasFirstPoint)
                SetFirstPoint(point);
            secondPoint = point;
        }

        public void SetFirstPointFromPlayer()
        {
            if (LZM.Player != null)
                SetFirstPoint(LZM.Player.transform.position);
        }

        public void SetSecondPointFromPlayer()
        {
            if (LZM.Player != null)
                SetSecondPoint(LZM.Player.transform.position);
        }

        public void Clear()
        {
            hasFirstPoint = false;
            firstPoint = Vector3.zero;
            secondPoint = Vector3.zero;
        }
    }
}
