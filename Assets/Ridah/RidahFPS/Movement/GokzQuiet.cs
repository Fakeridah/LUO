using UnityEngine;

namespace RidahFPS.Controllers
{
    /// <summary>
    /// Unity equivalent of the portable parts of gokz-quiet: hide other
    /// player renderers and mute scene audio categories through profile flags.
    /// </summary>
    [AddComponentMenu("Ridah/GOKZ Quiet")]
    public sealed class GokzQuiet : MonoBehaviour
    {
        [SerializeField] private bool applyEveryFrame;

        private void Start()
        {
            Apply();
        }

        private void Update()
        {
            if (applyEveryFrame)
                Apply();
        }

        public void Apply()
        {
            GokzProfile profile = GokzProfile.Instance;
            if (profile == null)
                return;

            LZM localPlayer = LZM.Player;
            LZM[] players = FindObjectsOfType<LZM>();
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == localPlayer)
                    continue;
                Renderer[] renderers = players[i].GetComponentsInChildren<Renderer>(true);
                for (int j = 0; j < renderers.Length; j++)
                    renderers[j].enabled = profile.ShowPlayers;
            }

            AudioSource[] sources = FindObjectsOfType<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null || source.transform.IsChildOf(transform))
                    continue;

                string objectName = source.gameObject.name.ToLowerInvariant();
                bool ambient = objectName.Contains("ambient") || objectName.Contains("soundscape");
                if (ambient && (profile.QuietAmbientSounds || profile.QuietSoundscapes))
                    source.mute = true;
            }
        }
    }
}
