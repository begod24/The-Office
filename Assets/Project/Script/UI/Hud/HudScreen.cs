using System.Collections.Generic;
using Office.Core;
using Office.Data;
using Office.Gameplay;
using Office.Network;
using TMPro;
using UnityEngine;

namespace Office.UI
{
    /// <summary>
    /// The in-run HUD. Draws the objectives, the squad, the hotbar and the held item, and
    /// nothing else — GDD §14 keeps it minimal so that the BSOD enemy taking it away is worth
    /// something.
    /// </summary>
    /// <remarks>
    /// <b>Everything here is a view of state that already exists somewhere else.</b> The squad
    /// reads each player's replicated <see cref="Health"/>, the hotbar reads the replicated
    /// <see cref="PlayerInventory"/>, and this component keeps no copy of either. That is why
    /// it can be destroyed and rebuilt by the editor tooling between runs without losing
    /// anything.
    /// </remarks>
    public sealed class HudScreen : MonoBehaviour
    {
        private const int MaxPlayers = 4;

        // The vertical slice has exactly one objective (GDD §16). It is written here rather
        // than authored on the panel because the panel is generated, and a generated string is
        // one nobody can edit into disagreeing with the switch that completes it.
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

        // Every player's vitals this HUD is currently listening to. Held so the subscriptions
        // can be dropped again — a body despawns at the end of every run.
        private readonly List<Health> tracked = new(MaxPlayers);

        public HudObjectivesPanel Objectives => objectives;

        public HudSquadPanel Squad => squad;

        public HudHotbar Hotbar => hotbar;

        private void Start()
        {
            group = GetComponent<CanvasGroup>();

            if (ServiceLocator.TryGet(out lobby)) lobby.Changed += Refresh;

            // The pause overlay and the inventory each replace the HUD for the local player —
            // both draw the same information larger, and leaving it underneath them reads as
            // two hotbars.
            if (ServiceLocator.TryGet(out bus))
            {
                bus.Subscribe<LocalPauseChanged>(OnPauseChanged);
                bus.Subscribe<LocalInventoryChanged>(OnInventoryChanged);
                bus.Subscribe<InteractionPromptChanged>(OnPromptChanged);
                bus.Subscribe<GameStateChanged>(OnGameStateChanged);
                bus.Subscribe<PowerStateChanged>(OnPowerChanged);
            }

            ServiceLocator.TryGet(out definitions);

            // The player object outlives no run, so the HUD cannot hold a reference across
            // one. It binds whenever the local inventory appears and lets go when it goes.
            PlayerInventory.LocalChanged += BindInventory;
            BindInventory(PlayerInventory.Local);

            // Same for vitals, except that the squad needs every player's and not only the
            // local one's — see Health.SpawnedPlayerList.
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

        // ------------------------------------------------------------------- interaction

        private void OnPromptChanged(InteractionPromptChanged evt) => SetPrompt(evt.Prompt);

        private void SetPrompt(string prompt)
        {
            if (interactPrompt == null) return;

            interactPrompt.text = prompt;
            interactPrompt.enabled = !string.IsNullOrEmpty(prompt);
        }

        // ------------------------------------------------------------------------ vitals

        /// <summary>
        /// Subscribes to every spawned player's vitals and drops the ones that have gone.
        /// </summary>
        /// <remarks>
        /// This is the wiring that was missing: the panel and its setters existed, and nothing
        /// ever called them, so damage changed the numbers on the network and the HUD went on
        /// drawing full bars. Bound per instance rather than through the event bus because the
        /// bus carries only the local player's vitals — <c>LocalVitalsChanged</c> has no client
        /// id on it and cannot grow one without <c>Office.Core</c> learning what a player is.
        /// </remarks>
        private void BindVitals()
        {
            UntrackAll();

            // A seat with no body reads as OFFLINE rather than as a full bar. Between runs
            // that is the truth, and during one it is the half-second before a late joiner's
            // player object arrives.
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

        // The state arrives without saying who it belongs to, so every tracked player is
        // redrawn. There are at most four, and this only runs when someone is hurt.
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

            // Two states, two screens. The banner is a countdown a teammate can still stop;
            // the death screen is what is left when it ran out, and showing the banner for
            // both told a dead player they had time they no longer had.
            SetDownedBanner(state.IsDowned, state.BleedOutRemaining);
            SetDeathScreen(state.IsDead);
        }

        private void SetDeathScreen(bool visible)
        {
            if (deathScreen != null) deathScreen.SetActive(visible);

            if (deathLabel == null || !visible) return;

            // The artwork already carries the stop screen's own copy. The only thing added is
            // what it cannot know: that the run is still going and there is something to do.
            deathLabel.text = "[ LMB ]  or  [ A / D ]   watch a colleague";
        }

        private void SetOutcome(string message)
        {
            var visible = !string.IsNullOrEmpty(message);

            if (outcomeScreen != null) outcomeScreen.SetActive(visible);

            if (outcomeLabel != null && visible) outcomeLabel.text = message;
        }

        // ---------------------------------------------------------------------- the run

        /// <remarks>
        /// The HUD is the only thing that reads the terminal states. They exist for exactly
        /// one network tick — <c>SessionDirector</c> has to pass through one before it can
        /// write Lobby — which is enough to put a screen up and let the scene change take it
        /// away again.
        /// </remarks>
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

        // No Update here, deliberately. The server ticks the bleed-out clock into the
        // replicated state every frame (Health.Update), so the countdown arrives as ordinary
        // state changes and redrawing it locally would be a second, disagreeing clock.

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

        // A missing definition is a content bug, not a reason to blank the slot: the count
        // still draws, so the player can see they are carrying something.
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

        // ------------------------------------------------------------------- visibility

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

        // Two overlays can hide the HUD, and either can close while the other is still up.
        private void ApplyVisibility()
        {
            if (group != null) group.alpha = paused || inventoryOpen ? 0f : 1f;
        }

        public void SetCrosshairVisible(bool visible)
        {
            if (crosshair != null) crosshair.SetActive(visible);
        }

        // ------------------------------------------------------------------------ squad

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

            // The roster just replaced every row, so whatever health they were showing went
            // with it. Re-reading the vitals is what puts it back.
            BindVitals();
        }
    }
}
