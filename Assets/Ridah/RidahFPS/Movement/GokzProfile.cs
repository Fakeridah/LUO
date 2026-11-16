using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Unity/PlayerPrefs equivalent of gokz-core clientprefs plus the option
    /// stores used by HUD, jumpstats, paint, profile and quiet. The names and
    /// defaults mirror the official GOKZ option families so other local
    /// modules can share one profile instead of inventing separate toggles.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Profile")]
    public sealed class GokzProfile : MonoBehaviour
    {
        public static GokzProfile Instance { get; private set; }

        [Header("Core")]
        [SerializeField] private bool checkpointMessages;
        [SerializeField] private bool checkpointSounds = true;
        [SerializeField] private bool teleportSounds;
        [SerializeField] private bool errorSounds = true;
        [SerializeField] private bool virtualButtonIndicators;
        [SerializeField] private bool buttonThroughPlayers = true;
        [SerializeField] private int safeguard;
        [SerializeField] private bool tips = true;
        [SerializeField] private bool slayOnEnd;
        [SerializeField] private bool tpAngleFix;

        [Header("HUD")]
        [SerializeField] private bool centerPanel = true;
        [SerializeField] private bool timerText = true;
        [SerializeField] private bool speedText = true;
        [SerializeField] private bool deadStrafe = true;
        [SerializeField] private bool showKeys = true;
        [SerializeField] private bool showControls;
        [SerializeField] private bool showTimeType = true;

        [Header("Jumpstats")]
        [SerializeField] private bool jumpstatsEnabled = true;
        [SerializeField] private bool jumpstatsChatReport = true;
        [SerializeField] private bool jumpstatsConsoleReport;
        [SerializeField] private bool jumpstatsSounds = true;
        [SerializeField] private bool jumpstatsFailstatsChat;
        [SerializeField] private bool jumpstatsFailstatsConsole;
        [SerializeField] private int jumpstatsMinimumBroadcastTier = (int)GokzJumpTier.Ownage;

        [Header("Paint / Quiet")]
        [SerializeField] private Color paintColor = Color.white;
        [SerializeField, Range(1f, 32f)] private float paintSize = 4f;
        [SerializeField] private bool showPlayers = true;
        [SerializeField] private bool quietSoundscapes;
        [SerializeField] private bool quietFallDamageSound;
        [SerializeField] private bool quietAmbientSounds;
        [SerializeField, Range(0f, 1f)] private float checkpointVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float teleportVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float timerVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float errorVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float jumpstatsVolume = 1f;

        public bool CheckpointMessages => checkpointMessages;
        public bool CheckpointSounds => checkpointSounds;
        public bool TeleportSounds => teleportSounds;
        public bool ErrorSounds => errorSounds;
        public bool VirtualButtonIndicators => virtualButtonIndicators;
        public bool ButtonThroughPlayers => buttonThroughPlayers;
        public int Safeguard => safeguard;
        public bool Tips => tips;
        public bool SlayOnEnd => slayOnEnd;
        public bool TpAngleFix => tpAngleFix;
        public bool CenterPanel => centerPanel;
        public bool TimerText => timerText;
        public bool SpeedText => speedText;
        public bool DeadStrafe => deadStrafe;
        public bool ShowKeys => showKeys;
        public bool ShowControls => showControls;
        public bool ShowTimeType => showTimeType;
        public bool JumpstatsEnabled => jumpstatsEnabled;
        public bool JumpstatsChatReport => jumpstatsChatReport;
        public bool JumpstatsConsoleReport => jumpstatsConsoleReport;
        public bool JumpstatsSounds => jumpstatsSounds;
        public bool JumpstatsFailstatsChat => jumpstatsFailstatsChat;
        public bool JumpstatsFailstatsConsole => jumpstatsFailstatsConsole;
        public GokzJumpTier JumpstatsMinimumBroadcastTier =>
            (GokzJumpTier)Mathf.Clamp(jumpstatsMinimumBroadcastTier, 0, (int)GokzJumpTier.Wrecker);
        public Color PaintColor => paintColor;
        public float PaintSize => paintSize;
        public bool ShowPlayers => showPlayers;
        public bool QuietSoundscapes => quietSoundscapes;
        public bool QuietFallDamageSound => quietFallDamageSound;
        public bool QuietAmbientSounds => quietAmbientSounds;
        public float CheckpointVolume => checkpointVolume;
        public float TeleportVolume => teleportVolume;
        public float TimerVolume => timerVolume;
        public float ErrorVolume => errorVolume;
        public float JumpstatsVolume => jumpstatsVolume;

        public event Action Changed;

        private string StorageKey => "GOKZ.profile." + SceneManager.GetActiveScene().path;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Load();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SetMode(GokzMode mode)
        {
            if (GokzRuntime.Instance != null)
                GokzRuntime.Instance.SetMode(mode);
        }

        public void SetCenterPanel(bool value) { centerPanel = value; Commit(); }
        public void SetTimerText(bool value) { timerText = value; Commit(); }
        public void SetSpeedText(bool value) { speedText = value; Commit(); }
        public void SetShowKeys(bool value) { showKeys = value; Commit(); }
        public void SetJumpstatsEnabled(bool value) { jumpstatsEnabled = value; Commit(); }
        public void SetPaintColor(Color value) { paintColor = value; Commit(); }
        public void SetPaintSize(float value) { paintSize = Mathf.Clamp(value, 1f, 32f); Commit(); }
        public void SetShowPlayers(bool value) { showPlayers = value; Commit(); }

        public void Save()
        {
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void Load()
        {
            string json = PlayerPrefs.GetString(StorageKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
                JsonUtility.FromJsonOverwrite(json, this);
        }

        private void Commit()
        {
            Save();
            Changed?.Invoke();
        }
    }
}
