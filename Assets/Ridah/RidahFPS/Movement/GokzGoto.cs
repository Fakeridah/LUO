using System;
using System.Collections.Generic;
using UnityEngine;

namespace RidahFPS.Controllers
{
    [Serializable]
    public struct GokzGotoTarget
    {
        public string name;
        public Transform target;
    }

    /// <summary>
    /// Local transform registry corresponding to gokz-goto. Targets are
    /// explicitly configured by the map author rather than inferred from
    /// server entity names.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Goto")]
    public sealed class GokzGoto : MonoBehaviour
    {
        [SerializeField] private LZM player;
        [SerializeField] private List<GokzGotoTarget> targets = new List<GokzGotoTarget>();

        public IReadOnlyList<GokzGotoTarget> Targets => targets;

        public bool Goto(string targetName)
        {
            ResolvePlayer();
            if (player == null)
                return false;
            for (int i = 0; i < targets.Count; i++)
            {
                if (!string.Equals(targets[i].name, targetName, StringComparison.OrdinalIgnoreCase) ||
                    targets[i].target == null)
                    continue;
                player.transform.SetPositionAndRotation(
                    targets[i].target.position,
                    targets[i].target.rotation);
                player.MoveData.Velocity = Vector3.zero;
                player.ResetGoKzMovementState();
                Physics.SyncTransforms();
                return true;
            }
            return false;
        }

        private void ResolvePlayer()
        {
            if (player == null)
                player = LZM.Player != null ? LZM.Player : FindFirstObjectByType<LZM>();
        }
    }
}
