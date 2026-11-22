using UnityEngine;

/// <summary>
/// The single Unity-side equivalent of Portal Revolution's portal-pair
/// transform. Source stores a row-major affine matrix; this helper expresses
/// the same entry-local 180-degree turn and exit-world transform for gameplay
/// and rendering.
/// </summary>
public static class PortalTransformMath
{
    public const float HammerUnitsPerUnityUnit = 52.4934383202f;
    public const float SourceClipOffsetHammerUnits = 2f;
    public const float SourceCameraDistanceEpsilonHammerUnits = 1f;
    public const float SourceFrustumClipEpsilonHammerUnits = 0.01f;

    private static readonly Quaternion HalfTurn =
        Quaternion.Euler(0f, 180f, 0f);

    public static Quaternion GetEntryToExitRotation(
        Portal entry, Portal exit)
    {
        return exit.transform.rotation * HalfTurn *
            Quaternion.Inverse(entry.transform.rotation);
    }

    /// <summary>
    /// Returns the column-vector Unity matrix corresponding to Source's
    /// destination-adjusted frame multiplied by the source inverse frame.
    /// Portal scale is intentionally excluded, matching the Source matrix.
    /// </summary>
    public static Matrix4x4 GetEntryToExitMatrix(Portal entry, Portal exit)
    {
        return Matrix4x4.TRS(
                exit.transform.position, exit.transform.rotation, Vector3.one) *
            Matrix4x4.Rotate(HalfTurn) *
            Matrix4x4.TRS(
                entry.transform.position, entry.transform.rotation,
                Vector3.one).inverse;
    }

    public static Vector3 TransformPoint(
        Portal entry, Portal exit, Vector3 point)
    {
        return TransformPoint(
            entry, exit, point, GetEntryToExitRotation(entry, exit));
    }

    public static Vector3 TransformPoint(
        Portal entry, Portal exit, Vector3 point,
        Quaternion entryToExitRotation)
    {
        return exit.transform.position + entryToExitRotation *
            (point - entry.transform.position);
    }

    public static Vector3 TransformVector(
        Portal entry, Portal exit, Vector3 vector)
    {
        return GetEntryToExitRotation(entry, exit) * vector;
    }

    public static Quaternion TransformRotation(
        Portal entry, Portal exit, Quaternion rotation)
    {
        return GetEntryToExitRotation(entry, exit) * rotation;
    }
}
