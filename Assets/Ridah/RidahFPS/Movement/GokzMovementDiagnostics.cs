using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Standalone equivalent of the useful local part of gokz-anticheat:
    /// detect impossible movement transitions without pretending to replace
    /// SourceMod's server-side trust boundary. It is disabled by default.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Movement Diagnostics")]
    public sealed class GokzMovementDiagnostics : MonoBehaviour
    {
        [SerializeField] private LZM player;
        [SerializeField] private bool enabledInGame;
        [SerializeField] private bool invalidateRunOnViolation;
        [SerializeField] private float toleranceHu = 8f;

        public int Violations { get; private set; }
        public event System.Action<float> ViolationDetected;

        private Vector3 _previousPosition;

        private void OnEnable()
        {
            ResolvePlayer();
            if (player != null)
                _previousPosition = player.transform.position;
        }

        private void FixedUpdate()
        {
            if (!enabledInGame)
                return;
            ResolvePlayer();
            if (player == null)
                return;

            float displacementHu = Vector3.Distance(
                player.transform.position,
                _previousPosition) * SourceMovementCore.HammerUnitsPerUnityUnit;
            float maxAllowedHu = GokzModeSettings.For(
                GokzRuntime.Instance != null ? GokzRuntime.Instance.Mode : GokzMode.KzTimer).maxVelocity /
                SourceMovementCore.TickRate + toleranceHu;
            if (displacementHu > maxAllowedHu && GokzRuntime.Instance != null)
            {
                Violations++;
                ViolationDetected?.Invoke(displacementHu);
                if (invalidateRunOnViolation)
                    GokzRuntime.Instance.InvalidateRun();
            }
            _previousPosition = player.transform.position;
        }

        private void ResolvePlayer()
        {
            if (player == null)
                player = LZM.Player != null ? LZM.Player : FindFirstObjectByType<LZM>();
        }
    }
}
