using System;
using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Local event/message counterpart of gokz-chat. Unity UI consumers can
    /// subscribe to Message without depending on a particular Canvas.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Chat")]
    public sealed class GokzChat : MonoBehaviour
    {
        public static GokzChat Active { get; private set; }

        [SerializeField] private string prefix = "[GOKZ] ";
        public event Action<string> Message;

        private GokzRuntime _runtime;

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Destroy(this);
                return;
            }
            Active = this;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            if (_runtime != null)
            {
                _runtime.RunStarted -= OnRunStarted;
                _runtime.RunFinished -= OnRunFinished;
                _runtime.CheckpointReached -= OnCheckpoint;
            }
        }

        private void OnDestroy()
        {
            if (Active == this)
                Active = null;
        }

        public void Publish(string text)
        {
            string message = prefix + (text ?? string.Empty);
            Debug.Log(message);
            Message?.Invoke(message);
        }

        private void Subscribe()
        {
            _runtime = GokzRuntime.Instance;
            if (_runtime == null)
                return;
            _runtime.RunStarted += OnRunStarted;
            _runtime.RunFinished += OnRunFinished;
            _runtime.CheckpointReached += OnCheckpoint;
        }

        private void Update()
        {
            if (_runtime == null && GokzRuntime.Instance != null)
                Subscribe();
        }

        private void OnRunStarted()
        {
            if (GokzProfile.Instance == null || GokzProfile.Instance.CheckpointMessages)
                Publish("Timer started");
        }

        private void OnCheckpoint(int course, int checkpoint)
        {
            if (GokzProfile.Instance == null || GokzProfile.Instance.CheckpointMessages)
                Publish("Checkpoint " + checkpoint);
        }

        private void OnRunFinished(GokzRunResult result)
        {
            Publish("Finished " + FormatTime(result.time));
        }

        private static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float remaining = seconds - minutes * 60f;
            return string.Format("{0:00}:{1:00.00}", minutes, remaining);
        }
    }
}
