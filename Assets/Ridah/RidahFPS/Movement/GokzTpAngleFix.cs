using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Unity equivalent of gokz-tpanglefix. Unity teleports are immediate,
    /// but the view controller can otherwise keep its previous local angles;
    /// this component explicitly synchronizes both transforms after a GOKZ
    /// teleport.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Teleport Angle Fix")]
    public sealed class GokzTpAngleFix : MonoBehaviour
    {
        [SerializeField] private LZM player;
        [SerializeField] private bool enabledInProfile = true;

        private GokzRuntime _runtime;

        private void OnEnable()
        {
            ResolvePlayer();
            _runtime = GokzRuntime.Instance;
            if (_runtime != null)
                _runtime.Teleported += OnTeleported;
        }

        private void OnDisable()
        {
            if (_runtime != null)
                _runtime.Teleported -= OnTeleported;
        }

        private void Update()
        {
            if (_runtime == null && GokzRuntime.Instance != null)
            {
                _runtime = GokzRuntime.Instance;
                _runtime.Teleported += OnTeleported;
            }
        }

        public void Apply(Quaternion rotation)
        {
            ResolvePlayer();
            if (!enabledInProfile || player == null)
                return;

            player.transform.rotation = rotation;
            PlayerAiming aiming = player.GetComponentInChildren<PlayerAiming>();
            if (aiming != null)
                aiming.realRotation = rotation.eulerAngles;
        }

        private void OnTeleported(GokzSaveLocation location)
        {
            Apply(location.rotation);
        }

        private void ResolvePlayer()
        {
            if (player == null)
                player = LZM.Player != null ? LZM.Player : FindFirstObjectByType<LZM>();
        }
    }
}
