using UnityEngine;
using UnityEngine.UI;
namespace GameOptionsUtility
{
    [RequireComponent(typeof(Toggle))]
    public class ToggleSpeedDisplay : MonoBehaviour
    {

        private void OnEnable()
        {
            InitializeEntries();
            GetComponent<Toggle>().onValueChanged.AddListener(UpdateOptions);
        }

        private void OnDisable()
        {
            GetComponent<Toggle>().onValueChanged.RemoveListener(UpdateOptions);
        }

        public void InitializeEntries()
        {
            GetComponent<Toggle>().isOn = GameOption.Get<GraphicOption>().displaySpeed;
        }

        void UpdateOptions(bool value)
        {
            GameOption.Get<GraphicOption>().displaySpeed = value;
        }

    
    }


}
