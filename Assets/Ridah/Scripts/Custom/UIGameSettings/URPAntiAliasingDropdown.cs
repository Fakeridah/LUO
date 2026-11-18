using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

namespace GameOptionsUtility.URP
{
    [RequireComponent(typeof(Dropdown))]
    public class URPAntiAliasingDropdown : MonoBehaviour
    {
        public bool ApplyImmediately = true;
        private void OnEnable()
        {
            var dropdown = GetComponent<Dropdown>();
            InitializeEntries(dropdown);
            dropdown.onValueChanged.AddListener(UpdateOptions);
            UpdateOptions(dropdown.value);
        }

        private void OnDisable()
        {
            GetComponent<Dropdown>().onValueChanged.RemoveListener(UpdateOptions);
        }

        public void InitializeEntries(Dropdown dropdown)
        {
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("None"));
            dropdown.options.Add(new Dropdown.OptionData("FXAA"));
            dropdown.options.Add(new Dropdown.OptionData("SMAA"));
            dropdown.options.Add(new Dropdown.OptionData("TAA"));
            dropdown.SetValueWithoutNotify((int)GameOption.Get<URPCameraOption>().antiAliasing);
        }

        void UpdateOptions(int value)
        {
            var option = GameOption.Get<URPCameraOption>();
            option.antiAliasing = (AntialiasingMode)value;

            if (ApplyImmediately)
                option.Apply();
        }
    }


}
