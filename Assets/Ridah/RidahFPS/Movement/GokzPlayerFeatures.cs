using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// The single player component for GOKZ jump statistics, paint, measure,
    /// local records, replay, chat, tips, quiet mode, teleport view sync,
    /// racing, end-of-run handling, and local movement diagnostics.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Ridah/GOKZ Player Features")]
    public sealed class GokzPlayerFeatures : MonoBehaviour
    {
        public static GokzPlayerFeatures Instance { get; private set; }

        [Header("Player and runtime")]
        [SerializeField] private LZM player;
        [SerializeField] private GokzRuntime runtime;

        [Header("Jump stats")]
        [SerializeField] private bool jumpStatsEnabled = true;
        [Header("Paint")]
        [SerializeField] private bool paintEnabled = true;
        [SerializeField, Min(0.02f)] private float paintInterval = 0.1f;
        [SerializeField, Min(1)] private int maximumPaintMarks = 512;
        [SerializeField] private Camera paintCamera;
        [Header("Measure")]
        [SerializeField] private bool hasFirstPoint;
        [SerializeField] private Vector3 firstPoint;
        [SerializeField] private Vector3 secondPoint;

        [Header("Local records")]
        [SerializeField] private int maximumRecords = 100;
        [SerializeField] private List<GokzRecord> records = new List<GokzRecord>();

        [Header("Replay")]
        [SerializeField] private bool recordOnStart;
        [SerializeField] private GokzReplayClip currentClip;

        [Header("Chat and tips")]
        [SerializeField] private string chatPrefix = "[GOKZ] ";
        [SerializeField] private string[] tips =
        {
            "Mouse-wheel up can be bound to jump for bhop timing.",
            "KZTimer prestrafe rewards valid A/D turns up to 276 HU/s.",
            "SaveLoc stores position, view angles and velocity."
        };
        [SerializeField] private float tipInterval = 45f;

        [Header("Quiet and teleport view")]
        [SerializeField] private bool quietApplyEveryFrame;
        [SerializeField] private bool teleportAngleFixEnabled = true;

        [Header("Race")]
        [SerializeField] private List<LZM> raceParticipants = new List<LZM>();
        [SerializeField] private bool raceActive;
        [SerializeField] private float raceTime;
        [SerializeField] private bool autoFinishLocalRuntime = true;

        [Header("End of run")]
        [SerializeField] private bool slayOnEnd;

        [Header("Movement diagnostics")]
        [SerializeField] private bool diagnosticsEnabled;
        [SerializeField] private bool invalidateRunOnViolation;
        [SerializeField] private float diagnosticToleranceHu = 8f;

        public GokzJumpResult LastJump { get; private set; }
        public bool Recording { get; private set; }
        public bool Playing { get; private set; }
        public GokzReplayClip CurrentClip => currentClip;
        public IReadOnlyList<GokzRecord> Records => records;
        public bool RaceActive => raceActive;
        public float RaceTime => raceTime;
        public int Violations { get; private set; }
        public bool HasFirstPoint => hasFirstPoint;
        public Vector3 FirstPoint => firstPoint;
        public Vector3 SecondPoint => secondPoint;
        public float DistanceHu => Vector3.Distance(firstPoint, secondPoint) *
            SourceMovementCore.HammerUnitsPerUnityUnit;
        public string LastCommandResult { get; private set; }

        public event Action<GokzJumpResult> JumpCompleted;
        public event Action<GokzJumpResult> JumpStarted;
        public event Action<GokzRecord> RecordAdded;
        public event Action<string> Message;
        public event Action RaceStarted;
        public event Action<GokzRaceResult> RacerFinished;
        public event Action RaceEnded;
        public event Action<float> ViolationDetected;
        public event Action<string> CommandCompleted;

        private int _lastJumpCount;
        private bool _wasGrounded;
        private bool _hasActiveJump;
        private int _jumpTicks;
        private int _syncTicks;
        private int _strafes;
        private int _strafeDirection;
        private readonly List<GokzStrafeResult> _activeStrafes = new List<GokzStrafeResult>();
        private float _previousSpeedHu;
        private float _maxSpeedHu;
        private Vector3 _takeoffPosition;
        private float _takeoffSpeed;
        private bool _takeoffPerfect;
        private bool _takeoffDucked;
        private int _playbackIndex;
        private bool _previousMoveEnabled;
        private float _nextTip;
        private int _tipIndex;
        private readonly Dictionary<LZM, float> _finishTimes = new Dictionary<LZM, float>();
        private Vector3 _previousPosition;
        private PlayerInput _paintPlayerInput;
        private LZM _paintInputPlayer;
        private InputAction _paintHoldAction;
        private bool _paintCommandHeld;
        private bool _paintActive;
        private float _nextPaintSample;
        private bool _hasLastPaintPosition;
        private Vector3 _lastPaintPosition;
        private readonly Dictionary<int, Material> _paintMaterialsByColor = new Dictionary<int, Material>();
        private readonly Queue<GameObject> _paintMarks = new Queue<GameObject>();
        private static readonly string[] PaintColorNames =
        {
            "red", "white", "black", "blue", "brown", "green", "yellow", "purple"
        };
        private static readonly Color[] PaintColors =
        {
            new Color32(255, 0, 0, 255), new Color32(255, 255, 255, 255),
            new Color32(0, 0, 0, 255), new Color32(0, 0, 255, 255),
            new Color32(139, 69, 19, 255), new Color32(0, 128, 0, 255),
            new Color32(255, 255, 0, 255), new Color32(128, 0, 128, 255)
        };
        private static readonly float[] PaintSizesHu =
        {
            4f * 0.05f / 0.13f, 4f, 4f * 0.4f / 0.13f
        };
        private string StorageKey => "GOKZ.records." + SceneManager.GetActiveScene().path;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResolveReferences();
            LoadRecords();
            _nextTip = Time.unscaledTime + tipInterval;
            if (player != null)
            {
                _previousPosition = player.transform.position;
                _lastJumpCount = player.GokzJumpCount;
                _wasGrounded = player.GokzGrounded || player.MoveData.grounded;
                _previousSpeedHu = GetHorizontalSpeedHu(player.MoveData.Velocity, player.MoveData.reverseGravity);
            }
            if (recordOnStart)
                StartRecording();
        }

        private void OnEnable() => SubscribeRuntime();

        private void Start() => ApplyQuietSettings();

        private void OnDisable()
        {
            UnsubscribeRuntime();
            _paintCommandHeld = false;
            _paintActive = false;
            if (Playing && player != null)
                player.MoveEnabled = _previousMoveEnabled;
        }

        private void OnDestroy()
        {
            UnsubscribeRuntime();
            ClearPaintMarks();
            foreach (Material material in _paintMaterialsByColor.Values)
                if (material != null)
                    Destroy(material);
            _paintMaterialsByColor.Clear();
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            ResolveReferences();
            SubscribeRuntime();
            PollPaintControls();
            if (raceActive)
                raceTime += Time.deltaTime;

            if (GokzProfile.Instance == null || GokzProfile.Instance.Tips)
            {
                if (tips != null && tips.Length > 0 && Time.unscaledTime >= _nextTip)
                {
                    _nextTip = Time.unscaledTime + Mathf.Max(1f, tipInterval);
                    Publish(tips[_tipIndex++ % tips.Length]);
                }
            }

            if (quietApplyEveryFrame)
                ApplyQuietSettings();
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            TickReplay();
            TickMovementDiagnostics();
        }

        public void ResolveReferences()
        {
            if (player == null)
                player = LZM.Player != null ? LZM.Player : FindFirstObjectByType<LZM>();
            if (runtime == null)
                runtime = GokzRuntime.Instance != null ? GokzRuntime.Instance : FindFirstObjectByType<GokzRuntime>();

            if (_paintInputPlayer != player)
            {
                _paintInputPlayer = player;
                _paintPlayerInput = player != null ? player.GetComponent<PlayerInput>() : null;
                InputActionAsset actions = _paintPlayerInput != null ? _paintPlayerInput.actions : null;
                _paintHoldAction = actions != null ? actions.FindAction("PaintHold", false) : null;
            }

            if (paintCamera == null && player != null)
                paintCamera = player.GetComponentInChildren<Camera>(true);
        }

        /// <summary>Execute a local GOKZ command from a console or UI.</summary>
        public bool ExecuteCommand(string commandLine)
        {
            ResolveReferences();
            if (string.IsNullOrWhiteSpace(commandLine))
                return CommandFail("empty command");

            string[] args = commandLine.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string command = args[0].ToLowerInvariant().TrimStart('/');
            if (command == "+paint" || command == "-paint" || command == "sm_paint" ||
                command == "sm_paintoptions" || command == "paint")
                return ExecutePaintCommand(command, args);

            if (runtime == null)
                return CommandFail("GOKZ runtime unavailable");

            switch (command)
            {
                case "start": runtime.StartRun(); return CommandSucceed("timer started");
                case "stop": runtime.StopRun(); return CommandSucceed("timer stopped");
                case "pause": runtime.PauseRun(); return CommandSucceed("timer paused");
                case "resume": runtime.ResumeRun(); return CommandSucceed("timer resumed");
                case "cp":
                case "checkpoint": return runtime.MakeCheckpoint()
                    ? CommandSucceed("checkpoint saved") : CommandFail("checkpoint unavailable");
                case "tp":
                case "teleport": return runtime.TeleportToCheckpoint()
                    ? CommandSucceed("teleported to checkpoint") : CommandFail("checkpoint unavailable");
                case "prev": return runtime.PreviousCheckpoint()
                    ? CommandSucceed("teleported to previous checkpoint") : CommandFail("no previous checkpoint");
                case "next": return runtime.NextCheckpoint()
                    ? CommandSucceed("teleported to next checkpoint") : CommandFail("no next checkpoint");
                case "undo": return runtime.UndoTeleport()
                    ? CommandSucceed("teleport undone") : CommandFail("nothing to undo");
                case "saveloc": runtime.SaveCurrentLocation(); return CommandSucceed("location saved");
                case "loadloc": return runtime.TeleportToSavedLocation()
                    ? CommandSucceed("location loaded") : CommandFail("no saved location");
                case "measure1": SetFirstPointFromPlayer(); return CommandSucceed("measure point 1 set");
                case "measure2": SetSecondPointFromPlayer(); return CommandSucceed("measure point 2 set");
                case "measure": return CommandSucceed(DistanceHu.ToString("0.00") + " HU");
                case "mode": return ExecuteModeCommand(args.Length > 1 ? args[1] : string.Empty);
                case "replay": return ExecuteReplayCommand(args);
                default: return CommandFail("unknown command: " + command);
            }
        }

        private bool ExecutePaintCommand(string command, string[] args)
        {
            switch (command)
            {
                case "+paint":
                    _paintCommandHeld = true;
                    return CommandSucceed("painting started");
                case "-paint":
                    _paintCommandHeld = false;
                    return CommandSucceed("painting stopped");
                case "sm_paint":
                    return PaintFromView()
                        ? CommandSucceed("paint placed")
                        : CommandFail("paint missed: no paintable surface in view");
                case "sm_paintoptions":
                    return CommandSucceed(GetPaintOptionsHelp());
                case "paint":
                    if (args.Length == 1)
                        return PaintFromView()
                            ? CommandSucceed("paint placed")
                            : CommandFail("paint missed: no paintable surface in view");
                    switch (args[1].ToLowerInvariant())
                    {
                        case "options":
                            return CommandSucceed(GetPaintOptionsHelp());
                        case "color":
                            if (args.Length < 3)
                                return CommandFail("paint color red|white|black|blue|brown|green|yellow|purple");
                            return TrySetPaintColor(args[2])
                                ? CommandSucceed("paint color: " + GetPaintColorName())
                                : CommandFail("unknown paint color: " + args[2]);
                        case "size":
                            if (args.Length < 3)
                                return CommandFail("paint size small|medium|big");
                            return TrySetPaintSize(args[2])
                                ? CommandSucceed("paint size: " + GetPaintSizeName())
                                : CommandFail("unknown paint size: " + args[2]);
                        case "clear":
                            ClearPaintMarks();
                            return CommandSucceed("paint marks cleared");
                        default:
                            return CommandFail("paint [options|color <name>|size <name>|clear]");
                    }
                default:
                    return CommandFail("unknown paint command");
            }
        }

        private void PollPaintControls()
        {
            if (!paintEnabled)
            {
                _paintActive = false;
                return;
            }

            Keyboard keyboard = Keyboard.current;
            bool hold = IsActionPressed(_paintHoldAction) || _paintCommandHeld;

            // Keep P usable if this prefab is used with a different PlayerInput asset.
            if ((_paintHoldAction == null || !_paintHoldAction.enabled) && keyboard != null)
                hold |= keyboard.pKey.isPressed;

            bool shouldPaint = hold || _paintCommandHeld;
            if (shouldPaint && !_paintActive)
                _nextPaintSample = 0f;
            _paintActive = shouldPaint;
            if (_paintActive && Time.unscaledTime >= _nextPaintSample)
            {
                PaintFromView();
                _nextPaintSample = Time.unscaledTime + Mathf.Max(0.02f, paintInterval);
            }
        }

        private static bool IsActionPressed(InputAction action)
        {
            return action != null && action.enabled && action.IsPressed();
        }

        /// <summary>Places one client-local GOKZ paint mark at the viewed surface.</summary>
        public bool PaintFromView()
        {
            if (!paintEnabled)
                return false;

            ResolveReferences();
            if (paintCamera == null)
                paintCamera = Camera.main;
            if (paintCamera == null)
                return false;

            Ray ray = new Ray(paintCamera.transform.position, paintCamera.transform.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            int nearest = -1;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hits[i].collider == null || player != null &&
                    (hitTransform == player.transform || hitTransform.IsChildOf(player.transform)))
                    continue;
                if (hits[i].distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = hits[i].distance;
                }
            }
            if (nearest < 0)
                return false;

            Vector3 point = hits[nearest].point;
            Vector3 normal = hits[nearest].normal.normalized;
            float unitsPerMeter = SourceMovementCore.HammerUnitsPerUnityUnit;
            if (_hasLastPaintPosition &&
                (point - _lastPaintPosition).sqrMagnitude * unitsPerMeter * unitsPerMeter < 1f)
                return false;

            if (!CreatePaintMark(point, normal))
                return false;

            _lastPaintPosition = point;
            _hasLastPaintPosition = true;
            return true;
        }

        public void ClearPaintMarks()
        {
            while (_paintMarks.Count > 0)
            {
                GameObject mark = _paintMarks.Dequeue();
                if (mark != null)
                    Destroy(mark);
            }
            _hasLastPaintPosition = false;
        }

        private bool CreatePaintMark(Vector3 point, Vector3 normal)
        {
            GokzProfile profile = GokzProfile.Instance;
            int colorIndex = GetClosestPaintColor(profile != null ? profile.PaintColor : PaintColors[0]);
            Material material = GetPaintMaterial(colorIndex);
            if (material == null)
                return false;

            GameObject mark = new GameObject("GOKZPaintDecal");
            Vector3 surfaceUp = paintCamera != null
                ? Vector3.ProjectOnPlane(paintCamera.transform.up, normal)
                : Vector3.ProjectOnPlane(Vector3.up, normal);
            if (surfaceUp.sqrMagnitude < 0.000001f)
                surfaceUp = Vector3.ProjectOnPlane(Vector3.right, normal);
            mark.transform.SetPositionAndRotation(
                point + normal * 0.002f,
                Quaternion.LookRotation(-normal, surfaceUp.normalized));

            float diameter = (profile != null ? profile.PaintSize : 4f) /
                SourceMovementCore.HammerUnitsPerUnityUnit;
            DecalProjector projector = mark.AddComponent<DecalProjector>();
            projector.material = material;
            projector.size = new Vector3(Mathf.Max(0.0001f, diameter),
                Mathf.Max(0.0001f, diameter), 0.02f);
            projector.pivot = Vector3.zero;
            projector.fadeFactor = 1f;

            _paintMarks.Enqueue(mark);
            int limit = Mathf.Max(1, maximumPaintMarks);
            while (_paintMarks.Count > limit)
            {
                GameObject oldest = _paintMarks.Dequeue();
                if (oldest != null)
                    Destroy(oldest);
            }
            return true;
        }

        private Material GetPaintMaterial(int colorIndex)
        {
            colorIndex = Mathf.Clamp(colorIndex, 0, PaintColors.Length - 1);
            if (_paintMaterialsByColor.TryGetValue(colorIndex, out Material cached) && cached != null)
                return cached;

            Material decalTemplate = Resources.Load<Material>("GokzPaintDecal");
            if (decalTemplate == null)
            {
                Debug.LogError("GOKZ URP decal material is missing from Resources.", this);
                return null;
            }

            Material material = new Material(decalTemplate)
            {
                name = "GOKZ Paint " + PaintColorNames[colorIndex]
            };
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", PaintColors[colorIndex]);
            _paintMaterialsByColor[colorIndex] = material;
            return material;
        }

        private bool TrySetPaintColor(string name)
        {
            if (GokzProfile.Instance == null)
                return false;
            for (int i = 0; i < PaintColorNames.Length; i++)
            {
                if (!string.Equals(name, PaintColorNames[i], StringComparison.OrdinalIgnoreCase))
                    continue;
                GokzProfile.Instance.SetPaintColor(PaintColors[i]);
                return true;
            }
            return false;
        }

        private bool TrySetPaintSize(string name)
        {
            if (GokzProfile.Instance == null)
                return false;
            string normalized = name.ToLowerInvariant();
            int size = normalized == "small" ? 0 : normalized == "medium" || normalized == "med" ? 1 :
                normalized == "big" || normalized == "large" ? 2 : -1;
            if (size < 0)
                return false;
            GokzProfile.Instance.SetPaintSize(PaintSizesHu[size]);
            return true;
        }

        private string GetPaintOptionsHelp()
        {
            return "Paint: hold P to paint; default color red, size medium. Current " +
                GetPaintColorName() + " / " + GetPaintSizeName();
        }

        private string GetPaintColorName()
        {
            Color color = GokzProfile.Instance != null ? GokzProfile.Instance.PaintColor : PaintColors[0];
            return PaintColorNames[GetClosestPaintColor(color)];
        }

        private string GetPaintSizeName()
        {
            float size = GokzProfile.Instance != null ? GokzProfile.Instance.PaintSize : PaintSizesHu[1];
            string[] names = { "small", "medium", "big" };
            return names[GetClosestPaintSize(size)];
        }

        private static int GetClosestPaintColor(Color color)
        {
            int closest = 0;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < PaintColors.Length; i++)
            {
                Color candidate = PaintColors[i];
                Vector4 delta = new Vector4(color.r - candidate.r, color.g - candidate.g,
                    color.b - candidate.b, color.a - candidate.a);
                float distance = delta.sqrMagnitude;
                if (distance < closestDistance)
                {
                    closest = i;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        private static int GetClosestPaintSize(float size)
        {
            int closest = 0;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < PaintSizesHu.Length; i++)
            {
                float distance = Mathf.Abs(size - PaintSizesHu[i]);
                if (distance < closestDistance)
                {
                    closest = i;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        private bool ExecuteModeCommand(string modeName)
        {
            if (string.Equals(modeName, "kztimer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(modeName, "kzt", StringComparison.OrdinalIgnoreCase))
                runtime.SetMode(GokzMode.KzTimer);
            else if (string.Equals(modeName, "simplekz", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(modeName, "skz", StringComparison.OrdinalIgnoreCase))
                runtime.SetMode(GokzMode.SimpleKz);
            else if (string.Equals(modeName, "vanilla", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(modeName, "vnl", StringComparison.OrdinalIgnoreCase))
                runtime.SetMode(GokzMode.Vanilla);
            else
                return CommandFail("mode must be vanilla, simplekz or kztimer");
            return CommandSucceed("mode changed to " + runtime.Mode);
        }

        private bool ExecuteReplayCommand(string[] args)
        {
            if (args.Length < 2)
                return CommandFail("replay start|stop|save");
            switch (args[1].ToLowerInvariant())
            {
                case "start": StartRecording(); return CommandSucceed("replay recording started");
                case "stop": StopRecording(); return CommandSucceed("replay recording stopped");
                case "save": return SaveCurrentReplay()
                    ? CommandSucceed("replay saved") : CommandFail("no replay to save");
                default: return CommandFail("replay start|stop|save");
            }
        }

        private bool CommandSucceed(string result)
        {
            LastCommandResult = result;
            CommandCompleted?.Invoke(result);
            return true;
        }

        private bool CommandFail(string result)
        {
            LastCommandResult = result;
            CommandCompleted?.Invoke(result);
            return false;
        }

        private void SubscribeRuntime()
        {
            if (runtime == null)
                runtime = GokzRuntime.Instance;
            if (runtime == null || _subscribedRuntime == runtime)
                return;
            UnsubscribeRuntime();
            _subscribedRuntime = runtime;
            runtime.RunStarted += OnRunStarted;
            runtime.RunFinished += OnRunFinished;
            runtime.CheckpointReached += OnCheckpoint;
            runtime.Teleported += OnTeleported;
        }

        private GokzRuntime _subscribedRuntime;

        private void UnsubscribeRuntime()
        {
            if (_subscribedRuntime == null)
                return;
            _subscribedRuntime.RunStarted -= OnRunStarted;
            _subscribedRuntime.RunFinished -= OnRunFinished;
            _subscribedRuntime.CheckpointReached -= OnCheckpoint;
            _subscribedRuntime.Teleported -= OnTeleported;
            _subscribedRuntime = null;
        }

        // Jump statistics
        public void RecordMovementTick(Vector3 positionBefore, Vector3 velocityBefore, float viewYawDelta)
        {
            if (!jumpStatsEnabled || GokzProfile.Instance != null && !GokzProfile.Instance.JumpstatsEnabled ||
                player == null || player.MoveData.antiJumpstats)
                return;

            float speedBeforeHu = GetHorizontalSpeedHu(velocityBefore, player.MoveData.reverseGravity);
            int jumpCount = player.GokzJumpCount;
            bool jumpStartedThisTick = false;
            if (jumpCount != _lastJumpCount)
            {
                jumpStartedThisTick = true;
                _lastJumpCount = jumpCount;
                _takeoffPosition = positionBefore;
                _takeoffSpeed = player.GokzLastTakeoffSpeed;
                _takeoffPerfect = player.GokzLastJumpWasPerfect;
                _takeoffDucked = player.GokzLastJumpWasDuck;
                _jumpTicks = 0;
                _syncTicks = 0;
                _strafes = 0;
                _strafeDirection = 0;
                _activeStrafes.Clear();
                _previousSpeedHu = speedBeforeHu;
                _maxSpeedHu = speedBeforeHu;
                _hasActiveJump = true;
                JumpStarted?.Invoke(new GokzJumpResult
                {
                    number = jumpCount,
                    type = jumpCount > 1 ? GokzJumpType.Bhop : GokzJumpType.LongJump,
                    takeoffSpeed = _takeoffSpeed,
                    perfect = _takeoffPerfect,
                    ducked = _takeoffDucked
                });
            }

            bool grounded = player.GokzGrounded || player.MoveData.grounded;
            float currentSpeedHu = GetHorizontalSpeedHu(player.MoveData.Velocity, player.MoveData.reverseGravity);
            // GOKZ tracks the takeoff tick and calls Update once more on landing.
            // _wasGrounded still describes the previous movement tick here.
            if (_hasActiveJump && (jumpStartedThisTick || !_wasGrounded))
            {
                _jumpTicks++;
                float speedDelta = currentSpeedHu - _previousSpeedHu;
                _maxSpeedHu = Mathf.Max(_maxSpeedHu, currentSpeedHu);
                if (speedDelta > 0f)
                    _syncTicks++;

                int turn = viewYawDelta < -0.001f ? -1 : viewYawDelta > 0.001f ? 1 : 0;
                if (turn != 0 && turn != _strafeDirection)
                {
                    _strafeDirection = turn;
                    _strafes++;
                    _activeStrafes.Add(new GokzStrafeResult { direction = turn });
                }

                // GOKZ keeps assigning ticks to the current strafe until the
                // player turns the other way, including dead-air ticks.
                if (_strafes > 0 && _activeStrafes.Count > 0)
                {
                    int index = _activeStrafes.Count - 1;
                    GokzStrafeResult strafe = _activeStrafes[index];
                    strafe.ticks++;
                    if (speedDelta > 0.000001f)
                    {
                        strafe.gainTicks++;
                        strafe.gain += speedDelta;
                    }
                    else if (speedDelta < -0.000001f)
                    {
                        strafe.loss -= speedDelta;
                    }
                    strafe.sync = 100f * strafe.gainTicks / strafe.ticks;
                    _activeStrafes[index] = strafe;
                }
            }

            if (_hasActiveJump && !_wasGrounded && grounded)
            {
                Vector3 offset = player.transform.position - _takeoffPosition;
                Vector3 up = player.MoveData.reverseGravity < 0f ? Vector3.down : Vector3.up;
                Vector3 horizontalOffset = Vector3.ProjectOnPlane(offset, up);
                float landingSpeed = currentSpeedHu;
                float verticalSpeed = Mathf.Abs(Vector3.Dot(player.MoveData.Velocity, up)) *
                    SourceMovementCore.HammerUnitsPerUnityUnit;
                GokzJumpType type = _lastJumpCount > 1 ? GokzJumpType.Bhop : GokzJumpType.LongJump;
                // GOKZ jumpstats adds the 32 HU player hull width to normal jump distance.
                float distance = horizontalOffset.magnitude * SourceMovementCore.HammerUnitsPerUnityUnit + 32f;
                LastJump = new GokzJumpResult
                {
                    number = _lastJumpCount,
                    type = type,
                    tier = GetJumpTier(type, distance),
                    takeoffSpeed = _takeoffSpeed,
                    maxSpeed = _maxSpeedHu,
                    landingSpeed = landingSpeed,
                    airtime = _jumpTicks * SourceMovementCore.TickInterval,
                    distance = distance,
                    strafes = _strafes,
                    sync = _jumpTicks > 0 ? 100f * _syncTicks / _jumpTicks : 0f,
                    strafeStats = _activeStrafes.ToArray(),
                    perfect = _takeoffPerfect,
                    ducked = _takeoffDucked,
                    deadstrafed = verticalSpeed > 0f && verticalSpeed < 140f
                };
                JumpCompleted?.Invoke(LastJump);
                _hasActiveJump = false;
            }

            _previousSpeedHu = currentSpeedHu;
            _wasGrounded = grounded;
        }

        private static float GetHorizontalSpeedHu(Vector3 velocity, float gravitySign)
        {
            Vector3 up = gravitySign < 0f ? Vector3.down : Vector3.up;
            return Vector3.ProjectOnPlane(velocity, up).magnitude * SourceMovementCore.HammerUnitsPerUnityUnit;
        }

        private GokzJumpTier GetJumpTier(GokzJumpType type, float distance)
        {
            GokzMode mode = runtime != null ? runtime.Mode : GokzMode.KzTimer;
            float[] tiers = type == GokzJumpType.LongJump
                ? mode == GokzMode.Vanilla
                    ? new[] { 210f, 235f, 240f, 245f, 248f, 250f }
                    : new[] { 215f, 265f, 270f, 275f, 280f, 282f }
                : mode == GokzMode.Vanilla
                    ? new[] { 150f, 230f, 233f, 235f, 238f, 240f }
                    : mode == GokzMode.SimpleKz
                        ? new[] { 215f, 265f, 270f, 275f, 280f, 282f }
                        : new[] { 215f, 340f, 345f, 350f, 355f, 357f };
            if (distance >= tiers[5]) return GokzJumpTier.Wrecker;
            if (distance >= tiers[4]) return GokzJumpTier.Ownage;
            if (distance >= tiers[3]) return GokzJumpTier.Godlike;
            if (distance >= tiers[2]) return GokzJumpTier.Perfect;
            if (distance >= tiers[1]) return GokzJumpTier.Impressive;
            if (distance >= tiers[0]) return GokzJumpTier.Meh;
            return GokzJumpTier.None;
        }

        // Measure
        public void SetFirstPoint(Vector3 point) { firstPoint = point; secondPoint = point; hasFirstPoint = true; }
        public void SetSecondPoint(Vector3 point) { if (!hasFirstPoint) SetFirstPoint(point); secondPoint = point; }
        public void SetFirstPointFromPlayer() { if (player != null) SetFirstPoint(player.transform.position); }
        public void SetSecondPointFromPlayer() { if (player != null) SetSecondPoint(player.transform.position); }
        public void ClearMeasure() { hasFirstPoint = false; firstPoint = Vector3.zero; secondPoint = Vector3.zero; }

        // Local records
        public bool TryGetPersonalBest(int course, out GokzRecord best) => TryGetPersonalBest(course, false, out best);
        public bool TryGetPersonalBest(int course, bool validOnly, out GokzRecord best)
        {
            best = default;
            bool found = false;
            foreach (GokzRecord record in records)
            {
                if (record.course != course || validOnly && !record.valid || found && record.time >= best.time) continue;
                best = record; found = true;
            }
            return found;
        }
        public int GetRank(GokzRecord record)
        {
            int rank = 1;
            foreach (GokzRecord item in records) if (item.course == record.course && item.time < record.time) rank++;
            return rank;
        }
        public void ClearRecords() { records.Clear(); PlayerPrefs.DeleteKey(StorageKey); PlayerPrefs.Save(); }
        private void OnRunFinished(GokzRunResult result)
        {
            GokzRecord record = new GokzRecord
            {
                map = SceneManager.GetActiveScene().name,
                course = result.course,
                time = result.time,
                teleportsUsed = result.teleportsUsed,
                valid = result.valid,
                mode = result.mode,
                timestamp = DateTime.UtcNow.ToString("O")
            };
            records.Add(record);
            records.Sort((a, b) => a.time.CompareTo(b.time));
            int max = Mathf.Max(1, maximumRecords);
            if (records.Count > max) records.RemoveRange(max, records.Count - max);
            SaveRecords();
            RecordAdded?.Invoke(record);
            if (autoFinishLocalRuntime && player != null) FinishRacer(player);
            bool shouldSlay = slayOnEnd || GokzProfile.Instance != null && GokzProfile.Instance.SlayOnEnd;
            if (shouldSlay && player != null) { player.MoveEnabled = false; player.MoveData.Velocity = Vector3.zero; }
        }
        private void LoadRecords()
        {
            string json = PlayerPrefs.GetString(StorageKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;
            GokzRecordList loaded = JsonUtility.FromJson<GokzRecordList>(json);
            if (loaded != null && loaded.records != null) records = loaded.records;
        }
        private void SaveRecords()
        {
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(new GokzRecordList { records = records }));
            PlayerPrefs.Save();
        }

        // Replay
        public void StartRecording() { ResolveReferences(); currentClip = new GokzReplayClip(); Recording = player != null; Playing = false; }
        public GokzReplayClip StopRecording() { Recording = false; return currentClip; }
        public string ExportReplayJson() => currentClip == null ? string.Empty : JsonUtility.ToJson(currentClip);
        public bool SaveCurrentReplay(string fileName = "gokz-replay.json")
        {
            string json = ExportReplayJson(); if (string.IsNullOrEmpty(json)) return false;
            File.WriteAllText(Path.Combine(Application.persistentDataPath, fileName), json); return true;
        }
        public bool LoadReplay(string fileName = "gokz-replay.json")
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            return File.Exists(path) && StartReplay(File.ReadAllText(path));
        }
        public bool StartReplay(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;
            return StartReplay(JsonUtility.FromJson<GokzReplayClip>(json));
        }
        public bool StartReplay(GokzReplayClip clip)
        {
            ResolveReferences();
            if (player == null || clip == null || clip.frames == null || clip.frames.Count == 0) return false;
            currentClip = clip; _playbackIndex = 0; Recording = false; Playing = true;
            _previousMoveEnabled = player.MoveEnabled; player.MoveEnabled = false; return true;
        }
        public void StopReplay()
        {
            Playing = false;
            if (player != null) player.MoveEnabled = _previousMoveEnabled;
        }
        private void TickReplay()
        {
            if (player == null) return;
            if (Recording)
                currentClip.frames.Add(new GokzReplayFrame
                {
                    time = currentClip.frames.Count * SourceMovementCore.TickInterval,
                    position = player.transform.position,
                    rotation = player.transform.rotation,
                    velocity = player.MoveData.Velocity,
                    grounded = player.GokzGrounded,
                    crouched = player.GokzCrouched,
                    jumpCount = player.GokzJumpCount
                });
            if (!Playing) return;
            if (_playbackIndex >= currentClip.frames.Count) { StopReplay(); return; }
            GokzReplayFrame frame = currentClip.frames[_playbackIndex++];
            player.transform.SetPositionAndRotation(frame.position, frame.rotation);
            player.MoveData.Velocity = frame.velocity;
        }

        // Chat and tips
        public void Publish(string text)
        {
            string message = chatPrefix + (text ?? string.Empty);
            Debug.Log(message); Message?.Invoke(message);
        }
        private void OnRunStarted()
        {
            if (GokzProfile.Instance == null || GokzProfile.Instance.CheckpointMessages) Publish("Timer started");
        }
        private void OnCheckpoint(int course, int checkpoint)
        {
            if (GokzProfile.Instance == null || GokzProfile.Instance.CheckpointMessages) Publish("Checkpoint " + checkpoint);
        }

        // Quiet settings
        public void ApplyQuietSettings()
        {
            GokzProfile p = GokzProfile.Instance; if (p == null) return;
            LZM[] players = FindObjectsOfType<LZM>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == player) continue;
                foreach (Renderer renderer in players[i].GetComponentsInChildren<Renderer>(true)) renderer.enabled = p.ShowPlayers;
            }
            foreach (AudioSource source in FindObjectsOfType<AudioSource>())
            {
                if (source == null || source.transform.IsChildOf(transform)) continue;
                string objectName = source.gameObject.name.ToLowerInvariant();
                if ((objectName.Contains("ambient") || objectName.Contains("soundscape")) &&
                    (p.QuietAmbientSounds || p.QuietSoundscapes)) source.mute = true;
            }
        }

        // Teleport angle synchronization
        public void ApplyTeleportRotation(Quaternion rotation)
        {
            ResolveReferences();
            if (!teleportAngleFixEnabled || player == null) return;
            player.transform.rotation = rotation;
            PlayerAiming aiming = player.GetComponentInChildren<PlayerAiming>();
            if (aiming != null) aiming.realRotation = rotation.eulerAngles;
        }
        private void OnTeleported(GokzSaveLocation location) => ApplyTeleportRotation(location.rotation);

        // Race session
        public void RegisterRacer(LZM racer) { if (racer != null && !raceParticipants.Contains(racer)) raceParticipants.Add(racer); }
        public void UnregisterRacer(LZM racer) => raceParticipants.Remove(racer);
        public void BeginRace() { raceTime = 0f; _finishTimes.Clear(); raceActive = true; RaceStarted?.Invoke(); }
        public void FinishRacer(LZM racer)
        {
            if (!raceActive || racer == null || _finishTimes.ContainsKey(racer)) return;
            _finishTimes.Add(racer, raceTime);
            RacerFinished?.Invoke(new GokzRaceResult { player = racer, placement = _finishTimes.Count, time = raceTime });
            if (_finishTimes.Count >= Mathf.Max(1, raceParticipants.Count)) { raceActive = false; RaceEnded?.Invoke(); }
        }
        public void EndRace() { if (!raceActive) return; raceActive = false; RaceEnded?.Invoke(); }

        // Local movement diagnostics
        private void TickMovementDiagnostics()
        {
            if (!diagnosticsEnabled || player == null) return;
            float movedHu = Vector3.Distance(player.transform.position, _previousPosition) *
                SourceMovementCore.HammerUnitsPerUnityUnit;
            float maxHu = GokzModeSettings.For(runtime != null ? runtime.Mode : GokzMode.KzTimer).maxVelocity /
                SourceMovementCore.TickRate + diagnosticToleranceHu;
            if (movedHu > maxHu && runtime != null)
            {
                Violations++;
                ViolationDetected?.Invoke(movedHu);
                if (invalidateRunOnViolation) runtime.InvalidateRun();
            }
            _previousPosition = player.transform.position;
        }
    }
}
