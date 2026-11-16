using System.Collections.Generic;
using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Local world-mark implementation of gokz-paint. It deliberately exposes
    /// a small API so an input/menu layer can decide when to paint; it does not
    /// bind arbitrary project input behind the user's back.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Paint")]
    public sealed class GokzPaint : MonoBehaviour
    {
        [SerializeField] private Camera paintCamera;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private float maxDistance = 256f;
        [SerializeField] private Transform strokeRoot;

        private readonly List<LineRenderer> _strokes = new List<LineRenderer>();

        public IReadOnlyList<LineRenderer> Strokes => _strokes;

        public bool PaintFromView()
        {
            if (paintCamera == null)
                paintCamera = Camera.main;
            if (paintCamera == null)
                return false;

            Ray ray = new Ray(paintCamera.transform.position, paintCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                return false;

            PaintPoint(hit.point + hit.normal * 0.002f);
            return true;
        }

        public void PaintPoint(Vector3 point)
        {
            GameObject strokeObject = new GameObject("GOKZPaint");
            if (strokeRoot != null)
                strokeObject.transform.SetParent(strokeRoot, true);

            LineRenderer line = strokeObject.AddComponent<LineRenderer>();
            GokzProfile profile = GokzProfile.Instance;
            line.startColor = profile != null ? profile.PaintColor : Color.white;
            line.endColor = line.startColor;
            float size = profile != null ? profile.PaintSize : 4f;
            line.startWidth = size / GokzKzTimerMovement.HammerUnitsPerUnityUnit;
            line.endWidth = line.startWidth;
            line.positionCount = 1;
            line.SetPosition(0, point);
            if (lineMaterial != null)
                line.material = lineMaterial;
            _strokes.Add(line);
        }

        public void Clear()
        {
            for (int i = 0; i < _strokes.Count; i++)
            {
                if (_strokes[i] != null)
                    Destroy(_strokes[i].gameObject);
            }
            _strokes.Clear();
        }
    }
}
