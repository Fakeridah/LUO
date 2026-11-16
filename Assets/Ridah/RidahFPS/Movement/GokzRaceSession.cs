using System;
using System.Collections.Generic;
using UnityEngine;

namespace RidahFPS.Controllers
{
    [Serializable]
    public struct GokzRaceResult
    {
        public LZM player;
        public int placement;
        public float time;
    }

    /// <summary>
    /// Local multi-player equivalent of gokz-racing. It is deliberately
    /// independent of Steam/GlobalAPI; participants can be registered by
    /// local player objects or by a future network adapter.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Race Session")]
    public sealed class GokzRaceSession : MonoBehaviour
    {
        [SerializeField] private List<LZM> participants = new List<LZM>();
        [SerializeField] private bool raceActive;
        [SerializeField] private float raceTime;
        [SerializeField] private bool autoFinishLocalRuntime = true;

        private readonly Dictionary<LZM, float> _finishTimes = new Dictionary<LZM, float>();
        private GokzRuntime _runtime;

        public bool RaceActive => raceActive;
        public float RaceTime => raceTime;
        public event Action RaceStarted;
        public event Action<GokzRaceResult> RacerFinished;
        public event Action RaceEnded;

        private void OnEnable()
        {
            _runtime = GokzRuntime.Instance;
            if (_runtime != null)
                _runtime.RunFinished += OnLocalRunFinished;
        }

        private void OnDisable()
        {
            if (_runtime != null)
                _runtime.RunFinished -= OnLocalRunFinished;
        }

        private void Update()
        {
            if (_runtime == null && GokzRuntime.Instance != null)
            {
                _runtime = GokzRuntime.Instance;
                _runtime.RunFinished += OnLocalRunFinished;
            }
            if (raceActive)
                raceTime += Time.deltaTime;
        }

        public void Register(LZM racer)
        {
            if (racer != null && !participants.Contains(racer))
                participants.Add(racer);
        }

        public void Unregister(LZM racer)
        {
            participants.Remove(racer);
        }

        public void BeginRace()
        {
            raceTime = 0f;
            _finishTimes.Clear();
            raceActive = true;
            RaceStarted?.Invoke();
        }

        public void Finish(LZM racer)
        {
            if (!raceActive || racer == null || _finishTimes.ContainsKey(racer))
                return;

            _finishTimes.Add(racer, raceTime);
            int placement = _finishTimes.Count;
            RacerFinished?.Invoke(new GokzRaceResult
            {
                player = racer,
                placement = placement,
                time = raceTime
            });

            if (_finishTimes.Count >= Mathf.Max(1, participants.Count))
            {
                raceActive = false;
                RaceEnded?.Invoke();
            }
        }

        public void EndRace()
        {
            if (!raceActive)
                return;
            raceActive = false;
            RaceEnded?.Invoke();
        }

        private void OnLocalRunFinished(GokzRunResult result)
        {
            if (!autoFinishLocalRuntime || _runtime == null || _runtime.Player == null)
                return;
            Finish(_runtime.Player);
        }
    }
}
