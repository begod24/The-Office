using Office.Core;
using Office.Network;
using UnityEngine;

namespace Office.UI
{
    /// <summary>
    /// The in-run HUD root: objectives top-left, squad bottom-left, item bar bottom-centre.
    ///
    /// One entry point for the whole overlay on purpose. Every system that will eventually write
    /// to the HUD — health, objectives, inventory — lives on a networked object in another scene,
    /// and each of them reaching into a canvas hierarchy directly is the cross-scene coupling
    /// Technical Plan §3.3 forbids. They go through this component instead.
    ///
    /// It reads <see cref="ILobbyService"/> for the roster and nothing else. There is no health
    /// or objective system yet; until there is, the panels show placeholders so the layout can be
    /// judged in the sandbox, which is entered directly far more often than through the lobby.
    /// </summary>
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
            // No error when the service is missing: SCN_Sandbox is a scene you are meant to be
            // able to open and press Play in, without booting the network stack first.
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

        /// <summary>Health arrives here as a fraction of maximum, never as raw hit points.</summary>
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

                // "P1" rather than the roster's "EMPLOYEE 01": the squad panel is read in
                // peripheral vision while running, and a nine-character name is not.
                squad.Bind(i, slot.ClientId, $"P{i + 1}", slot.ClientId == lobby.LocalClientId);
                shown++;
            }

            squad.HideFrom(shown);
        }
    }
}
