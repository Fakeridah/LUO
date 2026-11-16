using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RidahFPS.Controllers
{
    [Serializable]
    public struct GokzReplayFrame
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public bool grounded;
        public bool crouched;
        public int jumpCount;
    }

    [Serializable]
    public sealed class GokzReplayClip
    {
        public int tickRate = GokzKzTimerMovement.TickRate;
        public List<GokzReplayFrame> frames = new List<GokzReplayFrame>();
    }

    /// <summary>
    /// Local deterministic frame recorder/playback counterpart to
    /// gokz-replays. Playback is opt-in and never replaces the live movement
    /// controller during normal play.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Replay Recorder")]
    public sealed class GokzReplayRecorder : MonoBehaviour
    {
        [SerializeField] private LZM player;
        [SerializeField] private bool recordOnStart;

        public bool Recording { get; private set; }
        public bool Playing { get; private set; }
        public GokzReplayClip CurrentClip { get; private set; }

        private int _playbackIndex;
        private bool _previousMoveEnabled;

        private void Awake()
        {
            ResolvePlayer();
            if (recordOnStart)
                StartRecording();
        }

        private void FixedUpdate()
        {
            ResolvePlayer();
            if (player == null)
                return;

            if (Recording)
            {
                CurrentClip.frames.Add(new GokzReplayFrame
                {
                    time = CurrentClip.frames.Count * GokzKzTimerMovement.TickInterval,
                    position = player.transform.position,
                    rotation = player.transform.rotation,
                    velocity = player.MoveData.Velocity,
                    grounded = player.GokzGrounded,
                    crouched = player.GokzCrouched,
                    jumpCount = player.GokzJumpCount
                });
            }

            if (Playing)
                AdvancePlayback();
        }

        public void StartRecording()
        {
            ResolvePlayer();
            CurrentClip = new GokzReplayClip();
            Recording = player != null;
            Playing = false;
        }

        public GokzReplayClip StopRecording()
        {
            Recording = false;
            return CurrentClip;
        }

        public string ExportJson()
        {
            return CurrentClip == null ? string.Empty : JsonUtility.ToJson(CurrentClip);
        }

        public bool SaveCurrentClip(string fileName = "gokz-replay.json")
        {
            string json = ExportJson();
            if (string.IsNullOrEmpty(json))
                return false;
            string path = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllText(path, json);
            return true;
        }

        public bool LoadClip(string fileName = "gokz-replay.json")
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(path))
                return false;
            string json = File.ReadAllText(path);
            return StartPlayback(json);
        }

        public bool StartPlayback(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            GokzReplayClip clip = JsonUtility.FromJson<GokzReplayClip>(json);
            return StartPlayback(clip);
        }

        public bool StartPlayback(GokzReplayClip clip)
        {
            ResolvePlayer();
            if (player == null || clip == null || clip.frames == null || clip.frames.Count == 0)
                return false;

            CurrentClip = clip;
            _playbackIndex = 0;
            Recording = false;
            Playing = true;
            _previousMoveEnabled = player.MoveEnabled;
            player.MoveEnabled = false;
            return true;
        }

        public void StopPlayback()
        {
            Playing = false;
            if (player != null)
                player.MoveEnabled = _previousMoveEnabled;
        }

        private void AdvancePlayback()
        {
            if (_playbackIndex >= CurrentClip.frames.Count)
            {
                StopPlayback();
                return;
            }

            GokzReplayFrame frame = CurrentClip.frames[_playbackIndex++];
            player.transform.SetPositionAndRotation(frame.position, frame.rotation);
            player.MoveData.Velocity = frame.velocity;
        }

        private void ResolvePlayer()
        {
            if (player != null)
                return;
            player = LZM.Player;
            if (player == null)
                player = FindFirstObjectByType<LZM>();
        }
    }
}
