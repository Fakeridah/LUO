using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Local visual equivalent of gokz-jumpbeam. It listens to the same jump
    /// stats event as the HUD and draws the takeoff-to-landing segment.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Jump Beam")]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class GokzJumpBeam : MonoBehaviour
    {
        [SerializeField] private GokzJumpStats jumpStats;
        [SerializeField] private LineRenderer line;
        [SerializeField] private float width = 0.015f;
        [SerializeField] private Color color = new Color(0.25f, 1f, 0.25f, 0.9f);

        private LZM _player;
        private Vector3 _takeoff;

        private void Awake()
        {
            if (line == null)
                line = GetComponent<LineRenderer>();
            if (jumpStats == null)
                jumpStats = GetComponent<GokzJumpStats>();
            _player = GetComponent<LZM>();
            line.positionCount = 0;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
        }

        private void OnEnable()
        {
            if (jumpStats != null)
            {
                jumpStats.JumpStarted += OnJumpStarted;
                jumpStats.JumpCompleted += OnJumpCompleted;
            }
        }

        private void OnDisable()
        {
            if (jumpStats != null)
            {
                jumpStats.JumpStarted -= OnJumpStarted;
                jumpStats.JumpCompleted -= OnJumpCompleted;
            }
        }

        private void OnJumpStarted(GokzJumpResult result)
        {
            if (_player != null)
                _takeoff = _player.transform.position;
        }

        private void OnJumpCompleted(GokzJumpResult result)
        {
            if (_player == null || line == null)
                return;

            Vector3 landing = _player.transform.position;
            line.positionCount = 2;
            line.SetPosition(0, _takeoff);
            line.SetPosition(1, landing);
        }
    }
}
