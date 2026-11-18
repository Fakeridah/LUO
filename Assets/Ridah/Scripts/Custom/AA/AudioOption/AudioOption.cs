using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameOptionsUtility.AA
{
    public class AudioOption : GameOption
    {
        static List<AudioSource> _audio;

        public static void AddAudio(AudioSource audio)
        {
            if (_audio == null)
                _audio = new List<AudioSource>();

            if (!_audio.Contains(audio))
                _audio.Add(audio);

            Get<AudioOption>().Apply();
        }

        public static void RemoveAudio(AudioSource audio)
        {
            if (_audio == null)
                _audio = new List<AudioSource>();

            if (_audio.Contains(audio))
                _audio.Remove(audio);
        }

        public override void Apply()
        {
            if (_audio == null)
                return;

            foreach (var audio in _audio)
            {
                audio.volume = Get<GraphicOption>().gameVolume;
                if (audio.GetComponent<ShootAudioOption>()) audio.volume = Get<GraphicOption>().shootVolume;
                if (audio.GetComponent<BGMAudioOption>()) audio.volume = Get<GraphicOption>().bgmVolume;
            }
        }
    }
}