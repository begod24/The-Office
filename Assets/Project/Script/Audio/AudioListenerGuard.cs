using Office.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Office.Audio
{
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

                if (listener.isActiveAndEnabled) return true;
            }

            return false;
        }
    }
}
