using Office.Core;
using Office.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Office.Audio
{
    /// <summary>
    /// The one music track: audible wherever the player is in front of the game, faded out
    /// wherever they are inside it.
    /// </summary>
    /// <remarks>
    /// Which scenes those are is <see cref="SceneNames.IsFrontEnd"/>'s answer, never a list
    /// kept here — see that method for why the question is asked from the front-end side.
    /// The track lives on the boot object rather than in the menu scene, which is what makes
    /// it survive the menu→lobby swap instead of restarting on every visit and leaving the
    /// lobby silent.
    /// <para>
    /// <b>Fading is driven by the active scene, not by the game state.</b> The phase moves to
    /// Generating while the loading screen is still up and the player is still looking at the
    /// front end; the scene going active is the moment the office actually appears, which is
    /// the moment the music has to be gone by.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(AudioSource))]
    public sealed class MusicDirector : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip track;

        [Tooltip("Seconds to cross the whole fade, in either direction.")]
        [SerializeField] private float fadeSeconds = 1.25f;

        private ISettingsService settings;
        private IEventBus bus;

        /// <summary>Where the fade currently is, 0..1. Multiplied by the player's volume.</summary>
        private float level;

        private float target;
        private float gain = 1f;

        internal void Configure(AudioSource playFrom, AudioClip clip, float fade)
        {
            source = playFrom;
            track = clip;
            fadeSeconds = Mathf.Max(0f, fade);
        }

        private void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();

            source.clip = track;
            source.loop = true;

            // 2D — a menu has no world to place the track in.
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.volume = 0f;

            if (ServiceLocator.TryGet(out settings))
                gain = VolumeCurve.ToGain(settings.Current.MusicVolume);

            if (ServiceLocator.TryGet(out bus)) bus.Subscribe<SettingsChanged>(OnSettingsChanged);

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void Start()
        {
            // Boot is the active scene until the first additive load lands, and it is front
            // end, so the track fades up under the menu appearing rather than after it.
            Evaluate(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            bus?.Unsubscribe<SettingsChanged>(OnSettingsChanged);
        }

        private void OnActiveSceneChanged(Scene _, Scene next) => Evaluate(next.name);

        private void Evaluate(string sceneName) =>
            target = SceneNames.IsFrontEnd(sceneName) ? 1f : 0f;

        private void OnSettingsChanged(SettingsChanged evt)
        {
            gain = VolumeCurve.ToGain(evt.Settings.MusicVolume);
            source.volume = level * gain;
        }

        private void Update()
        {
            if (track == null) return;

            if (!Mathf.Approximately(level, target))
            {
                // Unscaled: a menu is free to sit at timeScale 0.
                var step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;

                level = Mathf.MoveTowards(level, target, step);
                source.volume = level * gain;
            }

            // Paused rather than stopped, so coming back from a run picks the track up where
            // it was instead of restarting it every time the player leaves the office.
            if (level > 0f) Resume();
            else if (source.isPlaying) source.Pause();
        }

        private void Resume()
        {
            if (source.isPlaying) return;

            // UnPause does nothing to a source that has never played, which is the state on
            // the first frame — hence both calls rather than a flag remembering which is due.
            source.UnPause();
            if (!source.isPlaying) source.Play();
        }
    }
}
