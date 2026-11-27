using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using GameplayIngredients.Events;
namespace Ridah.Weapon
{
    public class LaserWeapon : WeaponController
    {
        private static readonly Quaternion halfTurn = Quaternion.Euler(0.0f, 180.0f, 0.0f);

        public struct DebugRay
        {
            public DebugRay(Vector3 startPos, Vector3 dir)
            {
                this.startPos = startPos;
                this.dir = dir;
            }
            public Vector3 startPos;
            public Vector3 dir;
        }
        [SerializeField] Transform laserstartpoint;
        [Range(0,500)]
        [SerializeField] int maxLaserLines = 100;
        [SerializeField] GameObject laserPrefab;
        [SerializeField] GameObject crossoverVFX;
        [SerializeField] GameObject sparkVFX;

        public bool isWeapon=true;
        [SerializeField] LayerMask layerMask;

        Vector3 laserdirection;
        List<GameObject> laserlines;
        List<GameObject> laserVFXs;
        private VisualEffect[] impactVisualEffects;
        private readonly RaycastHit[] raycastHits = new RaycastHit[64];
        int index = 0;
        Vector3 lastHitPos= Vector3.zero;
        bool lastHitExist = true;
        Vector3 screenCenterPos;
        Transform cameraTrans;

        private void Awake()
        {
        }
        void Start()
        {
            cameraTrans = Camera.main.transform;
            laserdirection = laserstartpoint.forward;
            RemoveLegacyPrewarmedClones();
            maxLaserLines = Mathf.Clamp(maxLaserLines, 2, 500);
            laserlines = new List<GameObject>(4);
            laserVFXs = new List<GameObject>(4);

            HideVfxTemplates();

            if (sparkVFX != null)
            {
                impactVisualEffects = sparkVFX.GetComponentsInChildren<VisualEffect>(true);
                StopImpactVfx();
            }

            DisableLaserVfxShadows();
        }
        void Update()
        {
            if (isWeapon) weaponLaser();
            else triggerLaser();
            lastHitExist = true;
        }

        private void OnDisable()
        {
            StopImpactVfx();
            DestroyLaserObjects();
            HideVfxTemplates();
        }

        void weaponLaser()
        {
            if (Input.GetMouseButton(0) && lockShoot)
            {
                RaycastHit hit;
                if (RaycastIgnoringOwner(cameraTrans.position, cameraTrans.forward, out hit))
                {
                    screenCenterPos = hit.point;
                }
                else screenCenterPos = cameraTrans.position + cameraTrans.forward * 100;
                Vector3 aimOffset = screenCenterPos - laserstartpoint.position;
                if (aimOffset.sqrMagnitude > 0.0001f &&
                    Vector3.Dot(aimOffset, cameraTrans.forward) > 0f)
                {
                    laserdirection = aimOffset.normalized;
                }
                else
                {
                    // The camera's center target can be at or behind the muzzle
                    // when a surface is extremely close. Keep the beam pointing
                    // forward instead of producing a zero or backward ray.
                    laserdirection = laserstartpoint.forward;
                }

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
                DebugRay dr = new DebugRay(laserstartpoint.position, laserdirection);
                drCast(dr);
            }
            else
            {
                StopImpactVfx();
                HideVfxTemplates();
                DestroyLaserObjects();
            }
        }
        void triggerLaser()
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
            StopImpactVfx();
            DebugRay dr = new DebugRay(laserstartpoint.position, laserdirection);
            drCast(dr);
            if (lastHitPos != null && lastHitExist)
            {
                laserVFXs[index + 1].transform.position = lastHitPos;
                laserVFXs[index + 1].SetActive(true);
            }
        }
        
        public void drCast(DebugRay dr)
        {
            if (!EnsureLaserObjects(index))
                return;

            laserlines[index].GetComponent<LineRenderer>().positionCount = 2;
            laserlines[index].GetComponent<LineRenderer>().SetPosition(0, dr.startPos);
            laserVFXs[index].transform.position = dr.startPos;
            laserlines[index].SetActive(true);
            laserVFXs[index].SetActive(true);
            index++;
            RaycastHit hit;
            if (RaycastIgnoringOwner(dr.startPos, dr.dir, out hit))
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
                    if (hit.transform.GetComponent<SimplePortal>().otherPlaced&& index < (maxLaserLines - 1))
                        drCast(_dr);
                }
                else if ((hit.transform.CompareTag("Reflector") || hit.transform.GetComponent<LaserReciver>()) && index < (maxLaserLines - 1))
                {
                    Vector3 temp = Vector3.Reflect(dr.dir, hit.normal);
                    DebugRay _dr = new DebugRay(hit.point, temp);
                    drCast(_dr);
                }
                else if (hit.transform.CompareTag("Enemy") && index < (maxLaserLines - 1))
                {
                    if(hit.transform.GetComponent<LaserManager>())
                    hit.transform.GetComponent<LaserManager>().isEmit = false;
                    if (EnsureLaserObjects(index))
                    {
                        laserVFXs[index].transform.position = hit.point;
                        laserVFXs[index].SetActive(true);
                    }
                    PlaceAndPlayImpactVfx(hit.point, hit.normal);
                }
                else
                {
                    if (EnsureLaserObjects(index))
                    {
                        laserVFXs[index].transform.position = hit.point;
                        laserVFXs[index].SetActive(true);
                    }
                    PlaceAndPlayImpactVfx(hit.point, hit.normal);
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
                Vector3 endPos = dr.startPos + dr.dir * 100;
                laserlines[index - 1].GetComponent<LineRenderer>().SetPosition(0, dr.startPos);
                laserlines[index - 1].GetComponent<LineRenderer>().SetPosition(1, endPos);
                if (EnsureLaserObjects(index))
                {
                    laserVFXs[index].transform.position = endPos;
                    laserVFXs[index].SetActive(true);
                }
                lastHitExist = false;
                StopImpactVfx();
            }
            index--;
        }

        private bool EnsureLaserObjects(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= maxLaserLines ||
                laserPrefab == null || crossoverVFX == null ||
                laserlines == null || laserVFXs == null)
                return false;

            while (laserlines.Count <= itemIndex)
            {
                int newIndex = laserlines.Count;
                GameObject line = Instantiate(laserPrefab);
                GameObject marker = Instantiate(crossoverVFX);

                if (line == null || marker == null)
                {
                    if (line != null) line.SetActive(false);
                    if (marker != null) marker.SetActive(false);
                    return false;
                }

                line.transform.SetParent(transform);
                marker.transform.SetParent(transform);
                line.SetActive(false);
                marker.SetActive(false);

                if (isWeapon && newIndex == 0)
                {
                    LineRenderer lineRenderer = line.GetComponent<LineRenderer>();
                    if (lineRenderer != null)
                    {
                        lineRenderer.startWidth = .07f;
                        lineRenderer.endWidth = .07f;
                    }
                    marker.transform.localScale /= 5f;
                }

                laserlines.Add(line);
                laserVFXs.Add(marker);
                DisableRendererShadows(line);
                DisableRendererShadows(marker);
            }

            return true;
        }

        private void DestroyLaserObjects()
        {
            DestroyLaserObjects(laserlines);
            DestroyLaserObjects(laserVFXs);
            laserlines?.Clear();
            laserVFXs?.Clear();
        }

        private void DestroyLaserObjects(List<GameObject> objects)
        {
            if (objects == null)
                return;

            foreach (GameObject item in objects)
            {
                if (item == null)
                    continue;

                item.SetActive(false);
                Destroy(item);
            }
        }

        private void HideVfxTemplates()
        {
            if (laserPrefab != null)
                laserPrefab.SetActive(false);
            if (crossoverVFX != null)
                crossoverVFX.SetActive(false);

            foreach (Transform child in transform)
            {
                if (child.gameObject == laserPrefab || child.gameObject == crossoverVFX ||
                    string.Equals(child.name, "crossvfx", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(child.name, "crossfx", System.StringComparison.OrdinalIgnoreCase))
                    child.gameObject.SetActive(false);
            }
        }

        private void RemoveLegacyPrewarmedClones()
        {
            string lineCloneName = laserPrefab != null ? laserPrefab.name + "(Clone)" : null;
            string markerCloneName = crossoverVFX != null ? crossoverVFX.name + "(Clone)" : null;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == lineCloneName || child.name == markerCloneName)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
        }

        private void DisableLaserVfxShadows()
        {
            DisableRendererShadows(gameObject);
        }

        private static void DisableRendererShadows(GameObject target)
        {
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != null)
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void PlaceAndPlayImpactVfx(Vector3 position, Vector3 normal)
        {
            if (sparkVFX == null)
                return;

            sparkVFX.transform.position = position;
            sparkVFX.transform.LookAt(position + normal);
            sparkVFX.SetActive(true);

            if (impactVisualEffects != null)
            {
                foreach (VisualEffect effect in impactVisualEffects)
                {
                    if (effect != null)
                        effect.Play();
                }
            }
        }

        private void StopImpactVfx()
        {
            if (impactVisualEffects != null)
            {
                foreach (VisualEffect effect in impactVisualEffects)
                {
                    if (effect != null)
                        effect.Stop();
                }
            }

            if (sparkVFX != null)
                sparkVFX.SetActive(false);
        }

        private bool RaycastIgnoringOwner(Vector3 origin, Vector3 direction, out RaycastHit closestHit)
        {
            closestHit = default;
            Transform ownerRoot = transform.root;
            int hitCount = Physics.RaycastNonAlloc(
                origin, direction, raycastHits, Mathf.Infinity, layerMask);

            float closestDistance = Mathf.Infinity;
            bool foundHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidate = raycastHits[i];
                Collider collider = candidate.collider;
                if (collider == null)
                    continue;

                Transform hitTransform = collider.transform;
                if (hitTransform == ownerRoot || hitTransform.IsChildOf(ownerRoot))
                    continue;

                if (candidate.distance < closestDistance)
                {
                    closestDistance = candidate.distance;
                    closestHit = candidate;
                    foundHit = true;
                }
            }

            return foundHit;
        }
    }
}
