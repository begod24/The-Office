using Office.Core;
using UnityEngine;

namespace Office.Audio
{
    public sealed class AudioServiceInstaller : ServiceInstaller
    {
        private const string ObjectName = "[Audio]";

        [Tooltip("Looping background track for every screen outside a level.")]
        [SerializeField] private AudioClip menuTrack;

        [Tooltip("Seconds to fade the track in and out around a level.")]
        [Range(0f, 8f)]
        [SerializeField] private float fadeSeconds = 1.25f;

        public override int Order => 15;

        private GameObject owned;

        public override void Install()
        {
            if (menuTrack == null)
                Debug.LogWarning("[Audio] No track assigned — the game runs silent. Re-run " +
                                 "'Office/Setup/Build Boot Scene' to wire it.");

            owned = new GameObject(ObjectName);

            owned.SetActive(false);

            var source = owned.AddComponent<AudioSource>();
            source.playOnAwake = false;

            var listener = owned.AddComponent<AudioListener>();

            listener.enabled = false;

            owned.AddComponent<AudioListenerGuard>().Adopt(listener);
            owned.AddComponent<MasterOutput>();
            owned.AddComponent<MusicDirector>().Configure(source, menuTrack, fadeSeconds);

            DontDestroyOnLoad(owned);
            owned.SetActive(true);
        }

        public override void Uninstall()
        {
            if (owned != null) Destroy(owned);
            owned = null;
        }
    }
}
