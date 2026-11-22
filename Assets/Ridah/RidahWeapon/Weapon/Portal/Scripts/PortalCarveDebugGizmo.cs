using UnityEngine;

/// <summary>
/// TEMPORARY DEBUG VISUALISATION.
/// Draws the collision mesh that Portal generates for a wall as a wireframe
/// in the Scene view, so the carved hole can be compared directly against
/// the visible wall and the portal quad. Remove together with the other
/// temporary portal diagnostics once placement and traversal are confirmed.
/// </summary>
public class PortalCarveDebugGizmo : MonoBehaviour
{
    public MeshCollider CarvedCollider;

    private void OnDrawGizmos()
    {
        if (CarvedCollider == null)
            return;

        Mesh mesh = CarvedCollider.sharedMesh;
        if (mesh == null)
            return;

        Transform t = CarvedCollider.transform;
        Gizmos.color = new Color(0.1f, 1f, 0.25f, 0.85f);
        Gizmos.DrawWireMesh(
            mesh, t.position, t.rotation, t.lossyScale);
    }
}
