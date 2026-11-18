using UnityEngine;
using UnityEngine.UI;
namespace GameOptionsUtility
{
    [RequireComponent(typeof(Slider))]
    public class SpeedFontSize : MonoBehaviour
    {
        private void OnEnable()
        {
            InitializeEntries();
            GetComponent<Slider>().onValueChanged.AddListener(UpdateOptions);
        }

        private void OnDisable()
        {
            GetComponent<Slider>().onValueChanged.RemoveListener(UpdateOptions);
        }

        public void InitializeEntries()
        {
            GetComponent<Slider>().value = GameOption.Get<GraphicOption>().speedFontSize;
        }

        void UpdateOptions(float value)
        {
            GameOption.Get<GraphicOption>().speedFontSize = value;
        }
    }


}
