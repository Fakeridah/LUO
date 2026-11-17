using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GameOptionsUtility
{
    public class GraphicOption : GameOption
    {
        #region Preferences
        public class Preferences
        {
            public const string prefix = GameOptions.Preferences.prefix + "Graphics.";
            public const string fullScreenMode = prefix + "FullScreenMode";
            public const string vSync = prefix + "VSync";

            public const string anisotropicFiltering = prefix + "anisotropicFiltering";
            public const string displaySpeed = prefix + "DisplaySpeed";
            public const string autoBhop = prefix + "autoBhop";
            public const string middleWheelJump = prefix + "middleWheelJump";
            public const string speedFontSize = prefix + "speedFontSize";
            public const string sensitivity = prefix + "sensitivity";
            public const string gameVolume = prefix + "gameVolume";
            public const string shootVolume = prefix + "shootVolume";
            public const string bgmVolume = prefix + "bgmVolume";

            public const string targetFrameRate = prefix + "TargetFrameRate";
            public const string resolutionWidth = prefix + "ResolutionWidth";
            public const string resolutionHeight = prefix + "ResolutionHeight";
            public const string refreshRate = prefix + "RefreshRate";
            public const string quality = prefix + "Quality";

        } 
        #endregion

        #region Parameter Settins
        public FullScreenMode fullScreenMode
        {
            get { return (FullScreenMode)PlayerPrefs.GetInt(Preferences.fullScreenMode, (int)m_DefaultFullScreenMode); }
            set { PlayerPrefs.SetInt(Preferences.fullScreenMode, (int)value); }
        }
        public bool vSync
        {
            get { return PlayerPrefs.GetInt(Preferences.vSync, m_DefaultVSync ? 1 : 0) == 1 ? true : false; }
            set { PlayerPrefs.SetInt(Preferences.vSync, value ? 1 : 0); }
        }
        public bool anisotropicFiltering
        {
            get { return PlayerPrefs.GetInt(Preferences.anisotropicFiltering, m_DefaultaAnisotropicFiltering ? 1 : 0) == 1 ? true : false; }
            set { PlayerPrefs.SetInt(Preferences.anisotropicFiltering, value ? 1 : 0); }
        }
        public bool displaySpeed
        {
            get { return PlayerPrefs.GetInt(Preferences.displaySpeed, m_DefalutDisplaySpeed ? 1 : 0) == 1 ? true : false; }
            set { PlayerPrefs.SetInt(Preferences.displaySpeed, value ? 1 : 0); }
        }
        public float speedFontSize
        {
            get { return PlayerPrefs.GetFloat(Preferences.speedFontSize, m_DefalutSpeedFontSize); }
            set { PlayerPrefs.SetFloat(Preferences.speedFontSize, value); }
        }
        public float sensitivity
        {
            get { return PlayerPrefs.GetFloat(Preferences.sensitivity, m_DefalutSensitivity); }
            set { PlayerPrefs.SetFloat(Preferences.sensitivity, value); }
        }
        public float gameVolume
        {
            get { return PlayerPrefs.GetFloat(Preferences.gameVolume, m_DefalutGameVolume); }
            set { PlayerPrefs.SetFloat(Preferences.gameVolume, value); }
        }
        public float shootVolume
        {
            get { return PlayerPrefs.GetFloat(Preferences.shootVolume, m_DefalutShootVolume); }
            set { PlayerPrefs.SetFloat(Preferences.shootVolume, value); }
        }
        public float bgmVolume
        {
            get { return PlayerPrefs.GetFloat(Preferences.bgmVolume, m_DefalutBGMVolume); }
            set { PlayerPrefs.SetFloat(Preferences.bgmVolume, value); }
        }
        public bool autoBhop
        {
            get { return PlayerPrefs.GetInt(Preferences.autoBhop, m_DefalutAutoBhop ? 1 : 0) == 1 ? true : false; }
            set { PlayerPrefs.SetInt(Preferences.autoBhop, value ? 1 : 0); }
        }
        public bool middleWheelJump
        {
            get { return PlayerPrefs.GetInt(Preferences.middleWheelJump, m_DefalutMiddleWheelJump ? 1 : 0) == 1 ? true : false; }
            set { PlayerPrefs.SetInt(Preferences.middleWheelJump, value ? 1 : 0); }
        }
        public int targetFrameRate
        {
            get { return PlayerPrefs.GetInt(Preferences.targetFrameRate, m_DefaultTargetFrameRate); }
            set { PlayerPrefs.SetInt(Preferences.targetFrameRate, value); }
        }
        public int width
        {
            get { return PlayerPrefs.GetInt(Preferences.resolutionWidth, m_DefaultNativeResolution ? Screen.width : m_DefaultWidth); }
            set { PlayerPrefs.SetInt(Preferences.resolutionWidth, value); }
        }
        public int height
        {
            get { return PlayerPrefs.GetInt(Preferences.resolutionHeight, m_DefaultNativeResolution ? Screen.height : m_DefaultHeight); }
            set { PlayerPrefs.SetInt(Preferences.resolutionHeight, value); }
        }
        public int refreshRate
        {
            get { return PlayerPrefs.GetInt(Preferences.refreshRate, m_DefaultRefreshRate); }
            set { PlayerPrefs.SetInt(Preferences.refreshRate, value); }
        }
        public int quality
        {
            get { return PlayerPrefs.GetInt(Preferences.quality, QualitySettings.GetQualityLevel()); }
            set { PlayerPrefs.SetInt(Preferences.quality, value); }
        } 
        #endregion

        #region Defaults
        [Header("Defaults")]
        [SerializeField]
        protected FullScreenMode m_DefaultFullScreenMode = FullScreenMode.FullScreenWindow;
        [SerializeField]
        protected bool m_DefaultVSync = true;
        [SerializeField]
        protected bool m_DefaultaAnisotropicFiltering = true;
        [SerializeField]
        protected bool m_DefalutDisplaySpeed = true;
        [SerializeField]
        protected float m_DefalutSpeedFontSize = .5f;
        [SerializeField]
        protected float m_DefalutSensitivity = .3f;
        [SerializeField]
        protected float m_DefalutGameVolume = 1f;
        [SerializeField]
        protected float m_DefalutShootVolume = 1f;
        [SerializeField]
        protected float m_DefalutBGMVolume = 1f;
        [SerializeField]
        protected bool m_DefalutAutoBhop = true;
        [SerializeField]
        protected bool m_DefalutMiddleWheelJump = false;
        [SerializeField]
        protected int m_DefaultTargetFrameRate = -1;
        [SerializeField]
        protected bool m_DefaultNativeResolution = true;
        [SerializeField]
        protected int m_DefaultWidth = 1280;
        [SerializeField]
        protected int m_DefaultHeight = 720;
        [SerializeField]
        protected int m_DefaultRefreshRate = 60; 
        #endregion

        public override void Apply()
        {
            Screen.SetResolution(width, height, fullScreenMode, refreshRate);
            QualitySettings.vSyncCount = vSync ? 1 : 0;
            QualitySettings.anisotropicFiltering = anisotropicFiltering?AnisotropicFiltering.Enable: AnisotropicFiltering.Disable;
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.SetQualityLevel(quality);
        }
    }
}

