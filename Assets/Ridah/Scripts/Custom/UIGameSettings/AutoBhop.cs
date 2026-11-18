using UnityEngine;
using UnityEngine.UI;
namespace GameOptionsUtility
{
    [RequireComponent(typeof(Toggle))]
    public class AutoBhop : MonoBehaviour
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
            GetComponent<Toggle>().isOn = GameOption.Get<GraphicOption>().autoBhop;
        }

        void UpdateOptions(bool value)
        {
            GameOption.Get<GraphicOption>().autoBhop = value;
        }

    }


}
