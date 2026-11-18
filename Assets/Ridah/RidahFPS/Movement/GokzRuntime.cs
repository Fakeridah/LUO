using System;
using System.Collections.Generic;
using UnityEngine;

namespace RidahFPS.Controllers
{
    public enum GokzMode
    {
        // Keep the same numeric order as gokz-core's Mode enum. This matters
        // when a map/profile stores a mode as an integer.
        Vanilla = 0,
        SimpleKz = 1,
        KzTimer = 2
    }

    public enum GokzRunState
    {
        Stopped,
        Running,
        Paused,
        Finished
    }

    [Serializable]
    public struct GokzSaveLocation
    {
        public bool valid;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public int course;
        public int checkpoint;
    }

    [Serializable]
    public struct GokzCheckpoint
    {
        public int index;
        public GokzSaveLocation location;
    }

    /// <summary>
    /// Unity runtime equivalent of the portable parts of gokz-core:
    /// mode selection, map run state, timer, course/checkpoint events and
    /// saveloc. SourceMod-only networking, Steam and database calls are not
    /// silently faked here; their local Unity equivalents use these events.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Runtime")]
    public sealed class GokzRuntime : MonoBehaviour
    {
        public static GokzRuntime Instance { get; private set; }

        [Header("GOKZ mode")]
        [SerializeField] private GokzMode mode = GokzMode.KzTimer;
        [SerializeField] private LZM player;
        [SerializeField] private bool autoStartOnStartZone = true;
        [SerializeField] private bool startTimerOnSpawn = false;

        [Header("Run state")]
        [SerializeField] private GokzRunState runState = GokzRunState.Stopped;
        [SerializeField] private float runTime;
        [SerializeField] private int currentCourse;
        [SerializeField] private int currentCheckpoint;
        [SerializeField] private int teleportCount;
        [SerializeField] private bool runValid = true;
        [SerializeField] private GokzSaveLocation saveLocation;
        [SerializeField] private List<GokzCheckpoint> checkpoints = new List<GokzCheckpoint>();

        public GokzMode Mode => mode;
        public LZM Player => player;
        public GokzRunState RunState => runState;
        public float RunTime => runTime;
        public int CurrentCourse => currentCourse;
        public int CurrentCheckpoint => currentCheckpoint;
        public int TeleportCount => teleportCount;
        public bool RunValid => runValid;
        public GokzSaveLocation SaveLocation => saveLocation;
        public IReadOnlyList<GokzCheckpoint> Checkpoints => checkpoints;
        public bool TimerRunning => runState == GokzRunState.Running;

        public event Action<GokzMode> ModeChanged;
        public event Action RunStarted;
        public event Action RunPaused;
        public event Action RunResumed;
        public event Action RunStopped;
        public event Action<GokzRunResult> RunFinished;
        public event Action<int, int> CheckpointReached;
        public event Action<GokzCheckpoint> CheckpointMade;
        public event Action<GokzSaveLocation> Teleported;
        public event Action<GokzSaveLocation> SaveLocationChanged;

        private bool _moveEnabledBeforePause;
        private GokzSaveLocation _undoLocation;
        private bool _canUndoTeleport;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResolvePlayer();
            if (startTimerOnSpawn)
                StartRun();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void FixedUpdate()
        {
            ResolvePlayer();
            if (runState == GokzRunState.Running)
                runTime += SourceMovementCore.TickInterval;
        }

        public void SetMode(GokzMode newMode)
        {
            if (mode == newMode)
                return;

            mode = newMode;
            ModeChanged?.Invoke(mode);
        }

        public void StartRun()
        {
            StartRun(currentCourse);
        }

        public void StartRun(int course)
        {
            currentCourse = Mathf.Max(0, course);
            runTime = 0f;
            currentCheckpoint = 0;
            teleportCount = 0;
            runValid = true;
            checkpoints.Clear();
            _canUndoTeleport = false;
            runState = GokzRunState.Running;
            RunStarted?.Invoke();
        }

        public void PauseRun()
        {
            if (runState != GokzRunState.Running || player != null && player.MoveData.antiPause)
                return;

            _moveEnabledBeforePause = player == null || player.MoveEnabled;
            if (player != null)
                player.MoveEnabled = false;
            runState = GokzRunState.Paused;
            RunPaused?.Invoke();
        }

        public void ResumeRun()
        {
            if (runState != GokzRunState.Paused)
                return;

            if (player != null)
                player.MoveEnabled = _moveEnabledBeforePause;
            runState = GokzRunState.Running;
            RunResumed?.Invoke();
        }

        public void StopRun()
        {
            if (runState == GokzRunState.Stopped)
                return;

            if (player != null)
                player.MoveEnabled = true;
            runState = GokzRunState.Stopped;
            RunStopped?.Invoke();
        }

        public void ResetRun()
        {
            runTime = 0f;
            currentCheckpoint = 0;
            teleportCount = 0;
            runValid = true;
            runState = GokzRunState.Stopped;
            saveLocation = default;
            checkpoints.Clear();
            _canUndoTeleport = false;
            if (player != null)
                player.MoveEnabled = true;
        }

        public void SaveCurrentLocation()
        {
            ResolvePlayer();
            if (player == null)
                return;

            saveLocation = new GokzSaveLocation
            {
                valid = true,
                position = player.transform.position,
                rotation = player.transform.rotation,
                velocity = player.MoveData.Velocity,
                course = currentCourse,
                checkpoint = currentCheckpoint
            };
            SaveLocationChanged?.Invoke(saveLocation);
        }

        public bool TeleportToSavedLocation()
        {
            ResolvePlayer();
            if (player == null || !saveLocation.valid)
                return false;

            TeleportPlayer(saveLocation);
            currentCourse = saveLocation.course;
            currentCheckpoint = saveLocation.checkpoint;
            teleportCount++;
            runValid = false;
            Teleported?.Invoke(saveLocation);
            return true;
        }

        public bool MakeCheckpoint()
        {
            ResolvePlayer();
            if (player == null || runState != GokzRunState.Running || player.MoveData.antiCheckpoint)
                return false;

            GokzCheckpoint checkpoint = new GokzCheckpoint
            {
                index = checkpoints.Count + 1,
                location = CaptureLocation(checkpoints.Count + 1)
            };
            checkpoints.Add(checkpoint);
            currentCheckpoint = checkpoint.index;
            CheckpointMade?.Invoke(checkpoint);
            CheckpointReached?.Invoke(currentCourse, currentCheckpoint);
            return true;
        }

        public bool TeleportToCheckpoint(int checkpoint = -1)
        {
            ResolvePlayer();
            if (player == null || checkpoints.Count == 0)
                return false;

            int target = checkpoint < 0 ? currentCheckpoint : checkpoint;
            target = Mathf.Clamp(target, 1, checkpoints.Count);
            GokzCheckpoint saved = checkpoints[target - 1];
            return TeleportToLocation(saved.location, target);
        }

        public bool PreviousCheckpoint()
        {
            return TeleportToCheckpoint(Mathf.Max(1, currentCheckpoint - 1));
        }

        public bool NextCheckpoint()
        {
            return TeleportToCheckpoint(Mathf.Min(checkpoints.Count, currentCheckpoint + 1));
        }

        public bool UndoTeleport()
        {
            if (!_canUndoTeleport || player == null)
                return false;

            GokzSaveLocation current = CaptureLocation(currentCheckpoint);
            TeleportPlayer(_undoLocation);
            _undoLocation = current;
            _canUndoTeleport = false;
            return true;
        }

        public void InvalidateRun()
        {
            runValid = false;
        }

        internal void HandleZone(GokzZone zone, LZM enteringPlayer)
        {
            if (player == null)
                player = enteringPlayer;
            if (enteringPlayer != player)
                return;

            currentCourse = zone.Course;
            switch (zone.ZoneType)
            {
                case GokzZoneType.Start:
                case GokzZoneType.StartButton:
                    if (autoStartOnStartZone)
                        StartRun(zone.Course);
                    break;
                case GokzZoneType.End:
                case GokzZoneType.EndButton:
                    if (runState == GokzRunState.Running || runState == GokzRunState.Paused)
                        FinishRun();
                    break;
                case GokzZoneType.Stop:
                    StopRun();
                    break;
                case GokzZoneType.Checkpoint:
                    if (zone.Checkpoint > currentCheckpoint)
                        currentCheckpoint = zone.Checkpoint;
                    CheckpointReached?.Invoke(currentCourse, currentCheckpoint);
                    break;
                case GokzZoneType.Reset:
                    ResetRun();
                    break;
                case GokzZoneType.Teleport:
                    BeginZoneTeleport(zone, enteringPlayer);
                    break;
                case GokzZoneType.AntiBhop:
                    enteringPlayer.MoveData.antiBhop = true;
                    break;
                case GokzZoneType.AntiCheckpoint:
                    enteringPlayer.MoveData.antiCheckpoint = true;
                    break;
                case GokzZoneType.AntiPause:
                    enteringPlayer.MoveData.antiPause = true;
                    break;
                case GokzZoneType.AntiJumpstats:
                    enteringPlayer.MoveData.antiJumpstats = true;
                    break;
                case GokzZoneType.BhopReset:
                    enteringPlayer.MoveData.Velocity = Vector3.ProjectOnPlane(
                        enteringPlayer.MoveData.Velocity,
                        enteringPlayer.MoveData.reverseGravity < 0f ? Vector3.down : Vector3.up);
                    break;
            }
        }

        internal void HandleZoneExit(GokzZone zone, LZM exitingPlayer)
        {
            if (exitingPlayer == null || player != exitingPlayer)
                return;

            switch (zone.ZoneType)
            {
                case GokzZoneType.AntiBhop:
                    exitingPlayer.MoveData.antiBhop = false;
                    break;
                case GokzZoneType.AntiCheckpoint:
                    exitingPlayer.MoveData.antiCheckpoint = false;
                    break;
                case GokzZoneType.AntiPause:
                    exitingPlayer.MoveData.antiPause = false;
                    break;
                case GokzZoneType.AntiJumpstats:
                    exitingPlayer.MoveData.antiJumpstats = false;
                    break;
            }
        }

        private void ResolvePlayer()
        {
            if (player != null)
                return;

            player = LZM.Player;
            if (player == null)
                player = FindFirstObjectByType<LZM>();
        }

        private GokzSaveLocation CaptureLocation(int checkpoint)
        {
            return new GokzSaveLocation
            {
                valid = true,
                position = player.transform.position,
                rotation = player.transform.rotation,
                velocity = player.MoveData.Velocity,
                course = currentCourse,
                checkpoint = checkpoint
            };
        }

        private bool TeleportToLocation(GokzSaveLocation location, int checkpoint)
        {
            if (!location.valid)
                return false;

            _undoLocation = CaptureLocation(currentCheckpoint);
            _canUndoTeleport = true;
            TeleportPlayer(location);
            currentCourse = location.course;
            currentCheckpoint = checkpoint;
            teleportCount++;
            runValid = false;
            Teleported?.Invoke(location);
            return true;
        }

        private void TeleportPlayer(GokzSaveLocation location)
        {
            player.transform.SetPositionAndRotation(location.position, location.rotation);
            player.MoveData.Velocity = location.velocity;
            player.MoveData.Origin = location.position;
            player.ResetGoKzMovementState();
            Physics.SyncTransforms();
        }

        private void BeginZoneTeleport(GokzZone zone, LZM enteringPlayer)
        {
            if (zone.TeleportDestination == null || enteringPlayer != player)
                return;

            if (zone.TeleportDelay <= 0f)
            {
                TeleportFromZone(zone);
                return;
            }

            StartCoroutine(DelayedZoneTeleport(zone, enteringPlayer, zone.TeleportDelay));
        }

        private System.Collections.IEnumerator DelayedZoneTeleport(
            GokzZone zone,
            LZM enteringPlayer,
            float delay)
        {
            yield return new WaitForSeconds(delay);
            if (player == enteringPlayer)
                TeleportFromZone(zone);
        }

        private void TeleportFromZone(GokzZone zone)
        {
            Transform destination = zone.TeleportDestination;
            if (destination == null || player == null)
                return;

            Vector3 position = zone.RelativeDestination
                ? player.transform.TransformPoint(destination.localPosition)
                : destination.position;
            Quaternion rotation = zone.UseDestinationAngles
                ? destination.rotation
                : player.transform.rotation;
            Vector3 velocity = zone.ResetVelocity
                ? Vector3.zero
                : player.MoveData.Velocity;
            GokzSaveLocation location = new GokzSaveLocation
            {
                valid = true,
                position = position,
                rotation = rotation,
                velocity = velocity,
                course = currentCourse,
                checkpoint = currentCheckpoint
            };
            TeleportToLocation(location, currentCheckpoint);
        }

        private void FinishRun()
        {
            runState = GokzRunState.Finished;
            if (player != null)
                player.MoveEnabled = true;
            RunFinished?.Invoke(new GokzRunResult
            {
                course = currentCourse,
                time = runTime,
                checkpoint = currentCheckpoint,
                teleportsUsed = teleportCount,
                valid = runValid,
                mode = mode
            });
        }
    }

    [Serializable]
    public struct GokzRunResult
    {
        public int course;
        public int checkpoint;
        public float time;
        public int teleportsUsed;
        public bool valid;
        public GokzMode mode;
    }
}
