using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Local equivalent of gokz-slayonend. It is disabled by the official
    /// default profile and only freezes the local player after a finished run.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Slay On End")]
    public sealed class GokzSlayOnEnd : MonoBehaviour
    {
        [SerializeField] private LZM player;
        [SerializeField] private bool slayOnEnd;

        private GokzRuntime _runtime;

        private void OnEnable()
        {
            _runtime = GokzRuntime.Instance;
            if (_runtime != null)
                _runtime.RunFinished += OnRunFinished;
        }

        private void OnDisable()
        {
            if (_runtime != null)
                _runtime.RunFinished -= OnRunFinished;
        }

        private void OnRunFinished(GokzRunResult result)
        {
            bool enabled = slayOnEnd || GokzProfile.Instance != null && GokzProfile.Instance.SlayOnEnd;
            if (!enabled)
                return;
            if (player == null)
                player = LZM.Player;
            if (player != null)
            {
                player.MoveEnabled = false;
                player.MoveData.Velocity = Vector3.zero;
            }
        }
    }
}
