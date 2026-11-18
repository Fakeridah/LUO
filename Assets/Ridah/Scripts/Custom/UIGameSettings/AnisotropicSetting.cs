using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameOptionsUtility
{
    [RequireComponent(typeof(Toggle))]
    public class AnisotropicSetting : MonoBehaviour
    {
        public Dropdown targetFrameRateDropdown;

        private void OnEnable()
        {
            InitializeEntries();
            GetComponent<Toggle>().onValueChanged.AddListener(UpdateOptions);
            UpdateTargetFramerate(GameOption.Get<GraphicOption>().anisotropicFiltering);
        }

        private void OnDisable()
        {
            GetComponent<Toggle>().onValueChanged.RemoveListener(UpdateOptions);
        }

        public void InitializeEntries()
        {
            GetComponent<Toggle>().isOn = GameOption.Get<GraphicOption>().anisotropicFiltering;
        }

        void UpdateOptions(bool value)
        {
            GameOption.Get<GraphicOption>().anisotropicFiltering = value;
            UpdateTargetFramerate(value);
        }

        void UpdateTargetFramerate(bool value)
        {
            if (targetFrameRateDropdown != null)
            {
                targetFrameRateDropdown.interactable = !value;
                targetFrameRateDropdown.captionText.CrossFadeAlpha(value ? 0.1f : 1.0f, targetFrameRateDropdown.colors.fadeDuration, true);
            }

        }
    }

}

