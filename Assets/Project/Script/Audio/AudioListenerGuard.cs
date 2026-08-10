using Office.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Office.Audio
{
    /// <summary>
    /// Keeps exactly one <see cref="AudioListener"/> alive: its own, whenever no scene
    /// supplies one.
    /// </summary>
    /// <remarks>
    /// Every part of the game answers "who hears this" differently. A run wants the listener
    /// at the player's ears, the sandbox has a fallback camera holding one until a body
    /// spawns, and the lobby ships no camera at all — so a track that outlives scenes cannot
    /// rely on any of them. Unity's answer to two listeners is a warning and an undefined
    /// winner; its answer to none is silence with nothing logged at all, which is the failure
    /// worth guarding against, because it looks exactly like a music bug.
    /// <para>
    /// The same reasoning as <c>UIEventSystemInstaller</c> and the one EventSystem, with one
    /// difference: this one yields instead of removing, because a positional listener on the
    /// player is right and this one is only the floor under it.
    /// </para>
    /// </remarks>
    public sealed class AudioListenerGuard : MonoBehaviour
    {
        [SerializeField] private AudioListener owned;

        private IEventBus bus;

        internal void Adopt(AudioListener listener) => owned = listener;

        private void Awake()
        {
            if (owned == null) owned = GetComponent<AudioListener>();

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;

            // A player body brings its own listener up mid-scene, long after the load that
            // created it. PlayerRig already announces that moment for the fallback camera.
            if (ServiceLocator.TryGet(out bus)) bus.Subscribe<LocalPlayerSpawned>(OnPlayerSpawned);
        }

        private void Start() => Reevaluate();

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;

            bus?.Unsubscribe<LocalPlayerSpawned>(OnPlayerSpawned);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Reevaluate();

        private void OnSceneUnloaded(Scene scene) => Reevaluate();

        private void OnPlayerSpawned(LocalPlayerSpawned evt) => Reevaluate();

        private void Reevaluate()
        {
            if (owned == null) return;

            owned.enabled = !AnyOtherListener();
        }

        private bool AnyOtherListener()
        {
            var listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var listener in listeners)
            {
                if (listener == owned) continue;

                // isActiveAndEnabled, not enabled: PlayerRig leaves a remote player's listener
                // enabled on an object it switched off, and a disabled object hears nothing.
                if (listener.isActiveAndEnabled) return true;
            }

            return false;
        }
    }
}
