using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Unity equivalent of the information part of gokz-hud.
    ///
    /// The SourceMod HUD writes a centre panel and optional bottom speed and
    /// timer text. Unity does not have CSGO hint-text messages, so this uses
    /// ordinary Canvas Text elements while keeping the same information and
    /// colour rules.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ HUD")]
    public sealed class GokzHud : MonoBehaviour
    {
        public static GokzHud Active { get; private set; }

        [Header("Target")]
        [SerializeField] private LZM player;

        [Header("GOKZ HUD options")]
        [SerializeField] private bool showCenterPanel = true;
        [SerializeField] private bool showBottomSpeed = false;
        [SerializeField] private bool showTopTimer = true;
        [SerializeField] private bool showKeys = false;
        [SerializeField] private bool showStoppedTimer = true;
        [SerializeField] private bool preciseTimer = false;
        [SerializeField] private bool showDeadStrafeColor = false;

        [Header("Existing or generated UI")]
        [SerializeField] private Text speedText;
        [SerializeField] private Text infoPanelText;
        [SerializeField] private Text timerText;

        private GameplayIngredients.TimeKeeper _timeKeeper;
        private GokzRuntime _gokzRuntime;
        private GokzProfile _profile;
        private float _nextLookupTime;
        private int _lastJumpCount = -1;
        private bool _showDuckString;
        private bool _lastJumpWasPerfect;

        private static readonly Color White = Color.white;
        private static readonly Color PerfGreen = new Color(0.25f, 1f, 0.25f, 1f);
        private static readonly Color DeadStrafeRed = new Color(1f, 0.125f, 0.125f, 1f);

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Destroy(this);
                return;
            }

            Active = this;
            EnsurePlayer();
            EnsureUi();
        }

        private void OnDestroy()
        {
            if (Active == this)
                Active = null;
        }

        private void Update()
        {
            EnsurePlayer();
            if (player == null)
                return;

            if (_gokzRuntime == null)
                _gokzRuntime = GokzRuntime.Instance;
            if (_profile == null)
                _profile = GokzProfile.Instance;

            if (_timeKeeper == null && Time.unscaledTime >= _nextLookupTime)
            {
                _nextLookupTime = Time.unscaledTime + 1f;
                _timeKeeper = FindFirstObjectByType<GameplayIngredients.TimeKeeper>();
            }

            if (_lastJumpCount != player.GokzJumpCount)
            {
                _lastJumpCount = player.GokzJumpCount;
                _lastJumpWasPerfect = player.GokzLastJumpWasPerfect;
                _showDuckString = player.GokzJumpCount > 0 && player.GokzLastJumpWasDuck;
            }

            UpdateHudText();
        }

        private void EnsurePlayer()
        {
            if (player != null)
                return;

            player = LZM.Player;
            if (player == null)
                player = FindFirstObjectByType<LZM>();
        }

        private void EnsureUi()
        {
            if (speedText == null)
            {
                Transform existing = transform.Find("SpeedText");
                if (existing != null)
                    speedText = existing.GetComponent<Text>();
            }

            if (infoPanelText == null)
                infoPanelText = CreateText("GOKZInfoPanel", 26, TextAnchor.MiddleCenter);
            if (timerText == null)
                timerText = CreateText("GOKZTimerText", 32, TextAnchor.MiddleCenter);

            ConfigureText(infoPanelText, 26, TextAnchor.MiddleCenter);
            ConfigureText(timerText, 32, TextAnchor.MiddleCenter);
            ConfigureText(speedText, 45, TextAnchor.MiddleCenter);

            SetLayout(infoPanelText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -95f), new Vector2(800f, 220f));
            SetLayout(timerText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(800f, 80f));
            SetLayout(speedText, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 125f), new Vector2(650f, 140f));
        }

        private Text CreateText(string objectName, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(transform, false);

            Text text = textObject.AddComponent<Text>();
            text.font = speedText != null && speedText.font != null
                ? speedText.font
                : Resources.GetBuiltinResource<Font>("Arial.ttf");
            ConfigureText(text, fontSize, alignment);
            return text;
        }

        private static void ConfigureText(Text text, int fontSize, TextAnchor alignment)
        {
            if (text == null)
                return;

            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = White;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        private static void SetLayout(
            Text text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            if (text == null)
                return;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private void UpdateHudText()
        {
            bool grounded = player.GokzGrounded || player.MoveData.grounded;
            float speed = GetHorizontalSpeedHu();
            Color speedColor = GetSpeedColor(grounded);
            string speedString = FormatSpeed(speed, speedColor);
            Color takeoffColor = _lastJumpWasPerfect ? PerfGreen : White;
            string takeoffString = FormatTakeoff(player.GokzLastTakeoffSpeed, takeoffColor);
            string timerString = FormatTimer();

            if (infoPanelText != null)
            {
                bool showPanel = _profile == null ? showCenterPanel : _profile.CenterPanel;
                bool showKeysNow = _profile == null ? showKeys : _profile.ShowKeys;
                infoPanelText.gameObject.SetActive(showPanel);
                if (showPanel)
                {
                    string info = timerString + "\nSpeed: " + speedString + " u/s";
                    if (!grounded)
                        info += "\n(" + takeoffString + ")" + (_showDuckString ? " <color=#71EEB8>C</color>" : string.Empty);
                    if (showKeysNow)
                        info += "\n" + FormatKeys();
                    infoPanelText.text = info;
                }
            }

            if (speedText != null)
            {
                bool showSpeed = _profile == null ? showBottomSpeed : _profile.SpeedText;
                speedText.gameObject.SetActive(showSpeed);
                if (showSpeed)
                {
                    speedText.color = speedColor;
                    speedText.text = grounded
                        ? speed.ToString("0", CultureInfo.InvariantCulture)
                        : speed.ToString("0", CultureInfo.InvariantCulture) + "\n(" +
                          player.GokzLastTakeoffSpeed.ToString("0", CultureInfo.InvariantCulture) + ")";
                }
            }

            if (timerText != null)
            {
                bool timerRunning = IsTimerRunning();
                bool showTimer = _profile == null ? showTopTimer : _profile.TimerText;
                timerText.gameObject.SetActive(showTimer && timerRunning);
                if (timerRunning)
                    timerText.text = FormatTimerValue();
            }
        }

        private float GetHorizontalSpeedHu()
        {
            Vector3 up = player.MoveData.reverseGravity < 0f ? Vector3.down : Vector3.up;
            Vector3 velocity = Vector3.ProjectOnPlane(player.MoveData.Velocity, up);
            return velocity.magnitude *
                   GokzKzTimerMovement.HammerUnitsPerUnityUnit;
        }

        private Color GetSpeedColor(bool grounded)
        {
            if (grounded || player.MoveData.underwater)
                return White;

            Vector3 up = player.MoveData.reverseGravity < 0f ? Vector3.down : Vector3.up;
            float verticalSpeedHu = Vector3.Dot(player.MoveData.Velocity, up) *
                GokzKzTimerMovement.HammerUnitsPerUnityUnit;
            bool showDeadStrafe = _profile == null ? showDeadStrafeColor : _profile.DeadStrafe;
            if (showDeadStrafe && verticalSpeedHu > 0f && verticalSpeedHu < 140f)
                return DeadStrafeRed;
            if (_lastJumpWasPerfect)
                return PerfGreen;
            return White;
        }

        private static string FormatSpeed(float value, Color color)
        {
            return Colorize(value.ToString("0", CultureInfo.InvariantCulture), color);
        }

        private static string FormatTakeoff(float value, Color color)
        {
            return Colorize(value.ToString("0", CultureInfo.InvariantCulture), color);
        }

        private string FormatTimer()
        {
            if (_gokzRuntime != null)
                return _gokzRuntime.TimerRunning
                    ? "Time: " + FormatTimerValue()
                    : (showStoppedTimer ? "Time: Stopped" : string.Empty);

            if (_timeKeeper == null || !_timeKeeper.isRunning)
                return showStoppedTimer ? "Time: Stopped" : string.Empty;
            return "Time: " + FormatTimerValue();
        }

        private string FormatTimerValue()
        {
            if (_gokzRuntime != null)
                return FormatSeconds(_gokzRuntime.RunTime);

            if (_timeKeeper == null)
                return "00:00.00";

            uint hours = _timeKeeper.CurrentHours;
            uint minutes = _timeKeeper.CurrentMinutes;
            uint seconds = _timeKeeper.CurrentSeconds;
            uint milliseconds = _timeKeeper.CurrentMilliseconds;
            if (hours > 0)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:00}:{1:00}:{2:00}.{3:000}",
                    hours, minutes, seconds, milliseconds);
            }

            if (preciseTimer)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:00}:{1:00}.{2:000}",
                    minutes, seconds, milliseconds);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}.{2:00}",
                minutes, seconds, milliseconds / 10);
        }

        private bool IsTimerRunning()
        {
            return _gokzRuntime != null
                ? _gokzRuntime.TimerRunning
                : _timeKeeper != null && _timeKeeper.isRunning;
        }

        private string FormatSeconds(float secondsValue)
        {
            secondsValue = Mathf.Max(0f, secondsValue);
            uint totalMilliseconds = (uint)(secondsValue * 1000f);
            uint milliseconds = totalMilliseconds % 1000;
            uint totalSeconds = totalMilliseconds / 1000;
            uint seconds = totalSeconds % 60;
            uint minutes = (totalSeconds / 60) % 60;
            uint hours = totalSeconds / 3600;

            if (hours > 0)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:00}:{1:00}:{2:00}.{3:000}",
                    hours, minutes, seconds, milliseconds);
            }

            return preciseTimer
                ? string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:000}", minutes, seconds, milliseconds)
                : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:00}", minutes, seconds, milliseconds / 10);
        }

        private string FormatKeys()
        {
            string forward = player.MoveData.moveForward ? "W" : "_";
            string left = player.MoveData.moveLeft ? "A" : "_";
            string back = player.MoveData.moveBack ? "S" : "_";
            string right = player.MoveData.moveRight ? "D" : "_";
            string crouch = player.MoveData.crouching ? "C" : "_";
            string jump = player.MoveData.wishJump || player.MoveData.jumpHeld ? "J" : "_";
            return "Keys: " + left + " " + forward + " " + back + " " + right + " " + crouch + " " + jump;
        }

        private static string Colorize(string value, Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + value + "</color>";
        }
    }
}
