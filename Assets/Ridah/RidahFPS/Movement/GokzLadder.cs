using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Marks a collider as a Source-style ladder for GOKZ movement. A tag is
    /// also accepted by the solver for existing map content, so adding this
    /// component is optional when the project already uses the Ladder tag.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Ladder")]
    [RequireComponent(typeof(Collider))]
    public sealed class GokzLadder : MonoBehaviour
    {
        [SerializeField] private bool climbable = true;
        public bool Climbable => climbable;
    }
}
