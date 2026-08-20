using System.Collections.Generic;
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

        private const string PowerObjective = "RESTORE POWER";

        [SerializeField] private HudObjectivesPanel objectives;
        [SerializeField] private HudSquadPanel squad;
        [SerializeField] private HudHotbar hotbar;
        [SerializeField] private HudHeldItem heldItem;
        [SerializeField] private GameObject crosshair;

        [Tooltip("Line under the crosshair naming what the player is looking at.")]
        [SerializeField] private TMP_Text interactPrompt;

        [Tooltip("Covers the screen while the local player is down. Unmissable on purpose — " +
                 "being downed is the one state a player must never have to look for.")]
        [SerializeField] private GameObject downedBanner;

        [SerializeField] private TMP_Text downedLabel;

        [Tooltip("Fills the screen when the local player dies. GDD §15 — death is not a " +
                 "return to the menu, so the run has to say so without ending it.")]
        [SerializeField] private GameObject deathScreen;

        [SerializeField] private TMP_Text deathLabel;

        [Tooltip("Fills the screen when the shift ends, either way. It is up for the one tick " +
                 "the terminal state exists before the session returns everyone to the lobby.")]
        [SerializeField] private GameObject outcomeScreen;

        [SerializeField] private TMP_Text outcomeLabel;

        [Tooltip("Fills the panels with dummy rows when no session is running.")]
        [SerializeField] private bool showPlaceholdersWhenOffline = true;

        private ILobbyService lobby;
        private IEventBus bus;
        private CanvasGroup group;

        private bool paused;
        private bool inventoryOpen;

        private DefinitionRegistry definitions;
        private PlayerInventory inventory;

        private readonly List<Health> tracked = new(MaxPlayers);

        public HudObjectivesPanel Objectives => objectives;

        public HudSquadPanel Squad => squad;

        public HudHotbar Hotbar => hotbar;

        private void Start()
        {
            group = GetComponent<CanvasGroup>();

            if (ServiceLocator.TryGet(out lobby)) lobby.Changed += Refresh;

            if (ServiceLocator.TryGet(out bus))
            {
                bus.Subscribe<LocalPauseChanged>(OnPauseChanged);
                bus.Subscribe<LocalInventoryChanged>(OnInventoryChanged);
                bus.Subscribe<InteractionPromptChanged>(OnPromptChanged);
                bus.Subscribe<GameStateChanged>(OnGameStateChanged);
                bus.Subscribe<PowerStateChanged>(OnPowerChanged);
            }

            ServiceLocator.TryGet(out definitions);

            PlayerInventory.LocalChanged += BindInventory;
            BindInventory(PlayerInventory.Local);

            Health.SpawnedPlayersChanged += BindVitals;
            BindVitals();

            SetPrompt(string.Empty);
            SetDownedBanner(false, 0f);
            SetDeathScreen(false);
            SetOutcome(null);

            if (objectives != null) objectives.ShowPlaceholders();
            if (heldItem != null) heldItem.Clear();

            Refresh();
        }

        private void OnDestroy()
        {
            if (lobby != null) lobby.Changed -= Refresh;

            bus?.Unsubscribe<LocalPauseChanged>(OnPauseChanged);
            bus?.Unsubscribe<LocalInventoryChanged>(OnInventoryChanged);
            bus?.Unsubscribe<InteractionPromptChanged>(OnPromptChanged);
            bus?.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            bus?.Unsubscribe<PowerStateChanged>(OnPowerChanged);

            PlayerInventory.LocalChanged -= BindInventory;
            BindInventory(null);

            Health.SpawnedPlayersChanged -= BindVitals;
            UntrackAll();
        }

        private void OnPromptChanged(InteractionPromptChanged evt) => SetPrompt(evt.Prompt);

        private void SetPrompt(string prompt)
        {
            if (interactPrompt == null) return;

            interactPrompt.text = prompt;
            interactPrompt.enabled = !string.IsNullOrEmpty(prompt);
        }

        private void BindVitals()
        {
            UntrackAll();

            if (squad != null) squad.SetAllOffline();

            var players = Health.SpawnedPlayerList;

            for (var i = 0; i < players.Count; i++)
            {
                var health = players[i];
                if (health == null) continue;

                health.Changed += OnVitalsChanged;
                tracked.Add(health);

                Draw(health);
            }
        }

        private void UntrackAll()
        {
            foreach (var health in tracked)
                if (health != null)
                    health.Changed -= OnVitalsChanged;

            tracked.Clear();
        }

        private void OnVitalsChanged(VitalsState state)
        {
            foreach (var health in tracked)
                if (health != null)
                    Draw(health);
        }

        private void Draw(Health health)
        {
            if (!health.IsSpawned) return;

            var owner = health.OwnerClientId;
            var state = health.State;

            if (squad != null)
            {
                if (state.IsDead) squad.SetDead(owner);
                else if (state.IsDowned) squad.SetDowned(owner, state.BleedOutRemaining);
                else squad.SetHealth(owner, health.Normalised);
            }

            if (!ReferenceEquals(health, Health.Local)) return;

            SetDownedBanner(state.IsDowned, state.BleedOutRemaining);
            SetDeathScreen(state.IsDead);
        }

        private void SetDeathScreen(bool visible)
        {
            if (deathScreen != null) deathScreen.SetActive(visible);

            if (deathLabel == null || !visible) return;

            deathLabel.text = "[ LMB ]  or  [ A / D ]   watch a colleague";
        }

        private void SetOutcome(string message)
        {
            var visible = !string.IsNullOrEmpty(message);

            if (outcomeScreen != null) outcomeScreen.SetActive(visible);

            if (outcomeLabel != null && visible) outcomeLabel.text = message;
        }

        private void OnGameStateChanged(GameStateChanged evt)
        {
            switch (evt.Current)
            {
                case GameState.InRun:
                    if (objectives != null)
                    {
                        objectives.Set(0, PowerObjective, HudObjectiveState.Active);
                        objectives.HideFrom(1);
                    }

                    SetOutcome(null);
                    break;

                case GameState.RunComplete:
                    SetOutcome("SHIFT COMPLETE");
                    break;

                case GameState.RunFailed:
                    SetOutcome("SHIFT LOST");
                    break;

                case GameState.Lobby:
                    if (objectives != null) objectives.ShowPlaceholders();

                    SetOutcome(null);
                    SetDeathScreen(false);
                    SetDownedBanner(false, 0f);
                    break;
            }
        }

        private void OnPowerChanged(PowerStateChanged evt)
        {
            if (!evt.IsPowered || objectives == null) return;

            objectives.Set(0, PowerObjective, HudObjectiveState.Complete);
        }

        private void SetDownedBanner(bool visible, float bleedOutRemaining)
        {
            if (downedBanner != null) downedBanner.SetActive(visible);

            if (downedLabel == null || !visible) return;

            downedLabel.text = bleedOutRemaining > 0f
                ? $"DOWNED   {Mathf.CeilToInt(bleedOutRemaining)}"
                : "DOWNED";
        }

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
            if (inventory == null || !inventory.IsSpawned)
            {
                if (hotbar != null) hotbar.ClearAll();
                if (heldItem != null) heldItem.Clear();
                return;
            }

            if (hotbar != null)
            {
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

                    hotbar.SetItem(i, IconFor(stack.DefinitionId), stack.Count);
                }

                hotbar.SetSelected(inventory.SelectedIndex);
            }

            RefreshHeldItem();
        }

        private void RefreshHeldItem()
        {
            if (heldItem == null) return;

            var index = inventory.SelectedIndex;

            if (index < 0 || index >= inventory.Capacity)
            {
                heldItem.Clear();
                return;
            }

            var stack = inventory[index];

            if (stack.IsEmpty)
            {
                heldItem.Clear();
                return;
            }

            heldItem.Show(Resolve(stack.DefinitionId), stack);
        }

        private Sprite IconFor(int definitionId)
        {
            var definition = Resolve(definitionId);
            return definition != null ? definition.Icon : null;
        }

        private ItemDefinition Resolve(int definitionId) =>
            definitions != null &&
            definitions.TryGet<ItemDefinition>(definitionId, out var definition)
                ? definition
                : null;

        private void OnPauseChanged(LocalPauseChanged evt)
        {
            paused = evt.IsPaused;
            ApplyVisibility();
        }

        private void OnInventoryChanged(LocalInventoryChanged evt)
        {
            inventoryOpen = evt.IsOpen;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (group != null) group.alpha = paused || inventoryOpen ? 0f : 1f;
        }

        public void SetCrosshairVisible(bool visible)
        {
            if (crosshair != null) crosshair.SetActive(visible);
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

                squad.Bind(i, slot.ClientId, $"P{i + 1}", slot.DisplayName.ToString(),
                    slot.ClientId == lobby.LocalClientId);
                shown++;
            }

            squad.HideFrom(shown);

            BindVitals();
        }
    }
}
