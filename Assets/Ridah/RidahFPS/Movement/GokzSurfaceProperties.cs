using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Source-style surface friction for a Unity collider. Unity's
    /// PhysicMaterial friction values are not Source surface properties, so
    /// movement surfaces opt in explicitly instead of silently guessing from
    /// a renderer or PhysicMaterial.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Surface Properties")]
    public sealed class GokzSurfaceProperties : MonoBehaviour
    {
        [Min(0f)]
        public float sourceFriction = 1f;

        [Min(0f)]
        public float sourceMaxSpeedFactor = 1f;
    }
}
