using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameOptionsUtility.URP
{
    public class URPCameraOption : GameOption
    {
        public class Preferences
        {
            public const string prefix = GameOptions.Preferences.prefix + "URP.";
            public const string antialiasing = prefix + "AntiAliasing";
        }

        static List<UniversalAdditionalCameraData> s_Cameras;

        public AntialiasingMode antiAliasing
        {
            get { return (AntialiasingMode)PlayerPrefs.GetInt(Preferences.antialiasing, (int)AntialiasingMode.TemporalAntiAliasing); }
            set { PlayerPrefs.SetInt(Preferences.antialiasing, (int)value); }
        }


        public static void AddCamera(UniversalAdditionalCameraData camera)
        {
            if (s_Cameras == null)
                s_Cameras = new List<UniversalAdditionalCameraData>();

            if (!s_Cameras.Contains(camera))
                s_Cameras.Add(camera);

            Get<URPCameraOption>().Apply();
        }

        public static void RemoveCamera(UniversalAdditionalCameraData camera)
        {
            if (s_Cameras == null)
                s_Cameras = new List<UniversalAdditionalCameraData>();

            if (s_Cameras.Contains(camera))
                s_Cameras.Remove(camera);
        }

        public override void Apply()
        {
            if (s_Cameras == null)
                return;

            foreach (var camera in s_Cameras)
            {
                camera.antialiasing = antiAliasing;
            }
        }
    }
}