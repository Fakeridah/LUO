using UnityEngine;
using UnityEngine.UI;
namespace GameOptionsUtility.AA
{
    [RequireComponent(typeof(Slider))]
    public class GameVolumeSlider : MonoBehaviour
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
            GetComponent<Slider>().value = GameOption.Get<GraphicOption>().gameVolume;
        }

        void UpdateOptions(float value)
        {
            GameOption.Get<GraphicOption>().gameVolume = value;
            var option = GameOption.Get<AudioOption>();
            option.Apply();
        }
    }


}
