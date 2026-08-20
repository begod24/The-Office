using Office.Core;
using Office.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Office.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class MusicDirector : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip track;

        [Tooltip("Seconds to cross the whole fade, in either direction.")]
        [SerializeField] private float fadeSeconds = 1.25f;

        private ISettingsService settings;
        private IEventBus bus;

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
                var step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;

                level = Mathf.MoveTowards(level, target, step);
                source.volume = level * gain;
            }

            if (level > 0f) Resume();
            else if (source.isPlaying) source.Pause();
        }

        private void Resume()
        {
            if (source.isPlaying) return;

            source.UnPause();
            if (!source.isPlaying) source.Play();
        }
    }
}
