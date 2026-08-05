using Office.Core;
using UnityEngine;

namespace Office.UI
{
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
