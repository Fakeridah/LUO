using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameOptionsUtility.AA
{
    [RequireComponent(typeof(AudioSource))]
    public class BGMAudioOption : MonoBehaviour
    {
        private void OnEnable()
        {
            AudioOption.AddAudio(GetComponent<AudioSource>());
        }

        private void OnDisable()
        {
            AudioOption.RemoveAudio(GetComponent<AudioSource>());
        }
    }
}