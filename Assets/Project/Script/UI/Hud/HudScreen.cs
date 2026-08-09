using Office.Core;
using Office.Data;
using Office.Gameplay;
using Office.Network;
using TMPro;
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

        [Tooltip("Line under the crosshair naming what the player is looking at.")]
        [SerializeField] private TMP_Text interactPrompt;

        [Tooltip("Fills the panels with dummy rows when no session is running.")]
        [SerializeField] private bool showPlaceholdersWhenOffline = true;

        private ILobbyService lobby;
        private IEventBus bus;
        private CanvasGroup group;

        private DefinitionRegistry definitions;
        private PlayerInventory inventory;

        public HudObjectivesPanel Objectives => objectives;

        public HudSquadPanel Squad => squad;

        public HudHotbar Hotbar => hotbar;

        private void Start()
        {
            group = GetComponent<CanvasGroup>();

            if (ServiceLocator.TryGet(out lobby)) lobby.Changed += Refresh;

            // The pause overlay replaces the HUD for the local player.
            if (ServiceLocator.TryGet(out bus))
            {
                bus.Subscribe<LocalPauseChanged>(OnPauseChanged);
                bus.Subscribe<InteractionPromptChanged>(OnPromptChanged);
            }

            ServiceLocator.TryGet(out definitions);

            // The player object outlives no run, so the HUD cannot hold a reference across
            // one. It binds whenever the local inventory appears and lets go when it goes.
            PlayerInventory.LocalChanged += BindInventory;
            BindInventory(PlayerInventory.Local);

            SetPrompt(string.Empty);

            if (objectives != null) objectives.ShowPlaceholders();

            Refresh();
        }

        private void OnDestroy()
        {
            if (lobby != null) lobby.Changed -= Refresh;

            bus?.Unsubscribe<LocalPauseChanged>(OnPauseChanged);
            bus?.Unsubscribe<InteractionPromptChanged>(OnPromptChanged);

            PlayerInventory.LocalChanged -= BindInventory;
            BindInventory(null);
        }

        // ------------------------------------------------------------------- interaction

        private void OnPromptChanged(InteractionPromptChanged evt) => SetPrompt(evt.Prompt);

        private void SetPrompt(string prompt)
        {
            if (interactPrompt == null) return;

            interactPrompt.text = prompt;
            interactPrompt.enabled = !string.IsNullOrEmpty(prompt);
        }

        // --------------------------------------------------------------------- inventory

        private void BindInventory(PlayerInventory next)
        {
            if (ReferenceEquals(inventory, next)) return;

            if (inventory != null) inventory.Changed -= RefreshHotbar;

            inventory = next;

            if (inventory != null) inventory.Changed += RefreshHotbar;

            RefreshHotbar();
        }

        private void RefreshHotbar()
        {
            if (hotbar == null) return;

            if (inventory == null || !inventory.IsSpawned)
            {
                hotbar.ClearAll();
                return;
            }

            for (var i = 0; i < hotbar.Count; i++)
            {
                if (i >= inventory.Capacity)
                {
                    hotbar.ClearSlot(i);
                    continue;
                }

                var stack = inventory[i];

                if (stack.IsEmpty)
                {
                    hotbar.ClearSlot(i);
                    continue;
                }

                // A missing definition is a content bug, not a reason to blank the slot:
                // draw the count so the player can still see they are carrying something.
                var icon = definitions != null &&
                           definitions.TryGet<ItemDefinition>(stack.DefinitionId, out var definition)
                    ? definition.Icon
                    : null;

                hotbar.SetItem(i, icon, stack.Count);
            }

            hotbar.SetSelected(inventory.SelectedIndex);
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
