using UnityEngine;

namespace Office.Audio
{
    /// <summary>
    /// Looping background track for the menu screens. Fades up from silence so the
    /// music does not slam in at full volume the moment the scene loads.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class MenuMusic : MonoBehaviour
    {
        [SerializeField] private AudioSource source;

        [Tooltip("Volume the track settles at once the fade finishes.")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.5f;

        [Tooltip("Seconds spent fading from silence up to the target volume.")]
        [SerializeField] private float fadeInSeconds = 2f;

        private float elapsed;

        private void Reset()
        {
            source = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();

            source.loop = true;

            // 2D — a menu has no world to place the track in.
            source.spatialBlend = 0f;
            source.volume = fadeInSeconds > 0f ? 0f : volume;
        }

        private void Update()
        {
            if (fadeInSeconds <= 0f || elapsed >= fadeInSeconds) return;

            // Unscaled: the menu is free to sit at timeScale 0.
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Clamp01(elapsed / fadeInSeconds) * volume;
        }
    }
}
