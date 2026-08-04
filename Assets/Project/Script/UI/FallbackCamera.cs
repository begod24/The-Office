using Office.Core;
using UnityEngine;

namespace Office.UI
{
    /// <summary>
    /// Renders the scene before a local player exists, then gets out of the way.
    ///
    /// Without it the game shows a black screen and Unity's "no cameras rendering" warning
    /// while the player is still at the connection panel. It steps aside the moment the owned
    /// player spawns so two cameras never render the same frame.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FallbackCamera : MonoBehaviour
    {
        [SerializeField] private AudioListener fallbackListener;

        private IEventBus bus;

        private void Start()
        {
            if (!ServiceLocator.TryGet(out bus)) return;

            bus.Subscribe<LocalPlayerSpawned>(OnLocalPlayerSpawned);
        }

        private void OnDestroy() => bus?.Unsubscribe<LocalPlayerSpawned>(OnLocalPlayerSpawned);

        private void OnLocalPlayerSpawned(LocalPlayerSpawned evt)
        {
            if (fallbackListener != null) fallbackListener.enabled = false;

            gameObject.SetActive(false);
        }
    }
}
