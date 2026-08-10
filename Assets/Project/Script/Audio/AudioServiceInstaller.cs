using Office.Core;
using UnityEngine;

namespace Office.Audio
{
    /// <summary>
    /// Owns the music source, the master output and the fallback listener for the whole
    /// application.
    /// </summary>
    /// <remarks>
    /// The track used to be a GameObject inside <c>SCN_MainMenu</c>, which made it restart on
    /// every visit to the menu and left the lobby silent: a scene is the wrong owner for
    /// something meant to outlive scenes. The composition root never unloads, so this is the
    /// same move <c>UIEventSystemInstaller</c> makes for the one EventSystem, and for the same
    /// reason.
    /// <para>
    /// Registers no service. Nothing asks the audio layer for anything yet — it listens to
    /// settings and to the active scene, which is all a background track needs. When stingers
    /// and SFX arrive they get an <c>IAudioService</c> registered here.
    /// </para>
    /// </remarks>
    public sealed class AudioServiceInstaller : ServiceInstaller
    {
        private const string ObjectName = "[Audio]";

        [Tooltip("Looping background track for every screen outside a level.")]
        [SerializeField] private AudioClip menuTrack;

        [Tooltip("Seconds to fade the track in and out around a level.")]
        [Range(0f, 8f)]
        [SerializeField] private float fadeSeconds = 1.25f;

        // After the UI installer, before the session one: nothing here depends on either, and
        // the screen is what the player is waiting for.
        public override int Order => 15;

        private GameObject owned;

        public override void Install()
        {
            if (menuTrack == null)
                Debug.LogWarning("[Audio] No track assigned — the game runs silent. Re-run " +
                                 "'Office/Setup/Build Boot Scene' to wire it.");

            owned = new GameObject(ObjectName);

            // Built inactive so every Awake below runs after Configure rather than during
            // AddComponent, which is where a component created at runtime would otherwise
            // read fields nobody has filled in yet.
            owned.SetActive(false);

            var source = owned.AddComponent<AudioSource>();
            source.playOnAwake = false;

            var listener = owned.AddComponent<AudioListener>();

            // Off until the guard finds no scene supplying one. Enabled here it would be the
            // second listener in every scene that has a camera.
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
