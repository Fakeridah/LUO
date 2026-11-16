using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Local counterpart of gokz-tips. Tips are opt-in and use the same chat
    /// bus as the rest of the Unity port.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Tips")]
    public sealed class GokzTips : MonoBehaviour
    {
        [SerializeField] private string[] tips =
        {
            "Mouse-wheel up can be bound to jump for bhop timing.",
            "KZTimer prestrafe rewards valid A/D turns up to 276 HU/s.",
            "SaveLoc stores position, view angles and velocity."
        };
        [SerializeField] private float interval = 45f;

        private float _nextTip;
        private int _index;

        private void Start()
        {
            _nextTip = Time.unscaledTime + interval;
        }

        private void Update()
        {
            if (GokzProfile.Instance != null && !GokzProfile.Instance.Tips)
                return;
            if (tips == null || tips.Length == 0 || Time.unscaledTime < _nextTip)
                return;

            _nextTip = Time.unscaledTime + Mathf.Max(1f, interval);
            if (GokzChat.Active != null)
                GokzChat.Active.Publish(tips[_index++ % tips.Length]);
        }
    }
}
