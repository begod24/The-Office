using Office.Core;
using Office.Gameplay;
using UnityEngine;

namespace Office.UI
{
    // Thin stamina line under the crosshair. Fades in while stamina is being spent
    // and fades back out once it refills, so it never fights for attention.
    public sealed class HudStaminaBar : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform fill;

        [SerializeField] private float visibleAlpha = 0.6f;
        [SerializeField] private float fadeSpeed = 4f;

        private IEventBus bus;
        private PlayerMovement movement;

        private void Start()
        {
            if (ServiceLocator.TryGet(out bus))
                bus.Subscribe<LocalPlayerSpawned>(OnLocalPlayerSpawned);

            if (group != null) group.alpha = 0f;

            // The player may have spawned before this HUD was loaded.
            TryBindLocalPlayer();
        }

        private void OnDestroy() => bus?.Unsubscribe<LocalPlayerSpawned>(OnLocalPlayerSpawned);

        private void Update()
        {
            if (group == null || fill == null) return;

            if (movement == null)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, 0f, fadeSpeed * Time.deltaTime);
                return;
            }

            var value = Mathf.Clamp01(movement.NormalizedStamina);

            var scale = fill.localScale;
            scale.x = value;
            fill.localScale = scale;

            var target = value >= 0.999f ? 0f : visibleAlpha;
            group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.deltaTime);
        }

        private void OnLocalPlayerSpawned(LocalPlayerSpawned evt) => TryBindLocalPlayer();

        private void TryBindLocalPlayer()
        {
            var candidates = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

            foreach (var candidate in candidates)
            {
                if (!candidate.IsOwner) continue;

                movement = candidate;
                return;
            }
        }
    }
}
