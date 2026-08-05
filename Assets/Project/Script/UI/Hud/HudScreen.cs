using Office.Core;
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

        public HudObjectivesPanel Objectives => objectives;

        public HudSquadPanel Squad => squad;

        public HudHotbar Hotbar => hotbar;

        private void Start()
        {
            if (ServiceLocator.TryGet(out lobby)) lobby.Changed += Refresh;

            if (objectives != null) objectives.ShowPlaceholders();

            Refresh();
        }

        private void OnDestroy()
        {
            if (lobby != null) lobby.Changed -= Refresh;
        }

        public void SetCrosshairVisible(bool visible)
        {
            if (crosshair != null) crosshair.SetActive(visible);
        }

        public void SetHealth(ulong clientId, float normalized)
        {
            if (squad != null) squad.SetHealth(clientId, normalized);
        }

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
