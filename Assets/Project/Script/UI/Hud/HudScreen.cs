using Office.Core;
using Office.Data;
using Office.Network;
using UnityEngine;

namespace Office.UI
{
    public sealed class HudScreen : MonoBehaviour
    {
        private const int MaxPlayers = 4;

        [SerializeField] private HudObjectivesPanel objectives;
        [SerializeField] private HudSquadPanel squad;
        [SerializeField] private HudHotbar hotbar;
        [SerializeField] private GameObject crosshair;

        [Tooltip("Fills the panels with dummy rows when no session is running.")]
        [SerializeField] private bool showPlaceholdersWhenOffline = true;

        private ILobbyService lobby;
        private IEventBus bus;
        private CanvasGroup group;

        public HudObjectivesPanel Objectives => objectives;

        public HudSquadPanel Squad => squad;

        public HudHotbar Hotbar => hotbar;

        private void Start()
        {
            group = GetComponent<CanvasGroup>();

            if (ServiceLocator.TryGet(out lobby)) lobby.Changed += Refresh;

            // The pause overlay replaces the HUD for the local player.
            if (ServiceLocator.TryGet(out bus)) bus.Subscribe<LocalPauseChanged>(OnPauseChanged);

            if (objectives != null) objectives.ShowPlaceholders();

            Refresh();
        }

        private void OnDestroy()
        {
            if (lobby != null) lobby.Changed -= Refresh;
            bus?.Unsubscribe<LocalPauseChanged>(OnPauseChanged);
        }

        private void OnPauseChanged(LocalPauseChanged evt)
        {
            if (group != null) group.alpha = evt.IsPaused ? 0f : 1f;
        }

        public void SetCrosshairVisible(bool visible)
        {
            if (crosshair != null) crosshair.SetActive(visible);
        }

        public void SetHealth(ulong clientId, float normalized)
        {
            if (squad != null) squad.SetHealth(clientId, normalized);
        }

        // Health systems report raw points; players cap at MaxPlayerHealth (100).
        public void SetHealthPoints(ulong clientId, float healthPoints) =>
            SetHealth(clientId, healthPoints / GameplayConstants.MaxPlayerHealth);

        public void SetDowned(ulong clientId, bool downed)
        {
            if (squad != null) squad.SetDowned(clientId, downed);
        }

        private void Refresh()
        {
            if (squad == null) return;

            if (lobby == null || !lobby.IsAvailable || lobby.PlayerCount == 0)
            {
                squad.ShowPlaceholders(showPlaceholdersWhenOffline ? MaxPlayers : 0);
                return;
            }

            var shown = 0;

            for (var i = 0; i < MaxPlayers && i < squad.Capacity; i++)
            {
                if (!lobby.TryGetSlot(i, out var slot)) break;

                squad.Bind(i, slot.ClientId, $"P{i + 1}", slot.ClientId == lobby.LocalClientId);
                shown++;
            }

            squad.HideFrom(shown);
        }
    }
}
