using Office.Data;
using UnityEngine;

namespace Office.Core
{
    public readonly struct GameStateChanged
    {
        public readonly GameState Previous;
        public readonly GameState Current;

        public GameStateChanged(GameState previous, GameState current)
        {
            Previous = previous;
            Current = current;
        }
    }

    public readonly struct PowerStateChanged
    {
        public readonly int ZoneId;
        public readonly bool IsPowered;

        public PowerStateChanged(int zoneId, bool isPowered)
        {
            ZoneId = zoneId;
            IsPowered = isPowered;
        }
    }

    public readonly struct LocalPlayerSpawned
    {
        public readonly ulong ClientId;

        public LocalPlayerSpawned(ulong clientId) => ClientId = clientId;
    }

    public readonly struct LocalPauseChanged
    {
        public readonly bool IsPaused;

        public LocalPauseChanged(bool isPaused) => IsPaused = isPaused;
    }

    /// <summary>
    /// What the local player is currently looking at, as a line of HUD text. An empty
    /// prompt means "nothing in reach" and hides the label.
    /// </summary>
    /// <remarks>
    /// Published only when the text actually changes, not every frame — the interactor
    /// probes continuously and the bus is not a polling channel.
    /// </remarks>
    public readonly struct InteractionPromptChanged
    {
        public readonly string Prompt;

        public InteractionPromptChanged(string prompt) => Prompt = prompt ?? string.Empty;

        public bool HasPrompt => !string.IsNullOrEmpty(Prompt);
    }

    /// <summary>
    /// The local player's condition, for the HUD and for screen effects.
    /// </summary>
    /// <remarks>
    /// Carries plain numbers rather than the replicated struct so that <c>Office.Core</c>
    /// stays free of both NGO and the gameplay assembly — the bus is the seam between them,
    /// and it only works as one if nothing gameplay-shaped travels through it.
    /// GDD §14 wants health read through breathing and screen grain rather than a bar, so
    /// expect more than one subscriber to this.
    /// </remarks>
    public readonly struct LocalVitalsChanged
    {
        public readonly float Health;
        public readonly float MaxHealth;
        public readonly bool IsDowned;
        public readonly bool IsDead;

        /// <summary>Seconds left to be revived. Only meaningful while downed.</summary>
        public readonly float BleedOutRemaining;

        public LocalVitalsChanged(float health, float maxHealth, bool isDowned, bool isDead,
            float bleedOutRemaining)
        {
            Health = health;
            MaxHealth = maxHealth;
            IsDowned = isDowned;
            IsDead = isDead;
            BleedOutRemaining = bleedOutRemaining;
        }

        public float Normalised => MaxHealth <= 0f ? 0f : Mathf.Clamp01(Health / MaxHealth);
    }

    public readonly struct PlayerConnectionChanged
    {
        public readonly ulong ClientId;
        public readonly bool Connected;

        public PlayerConnectionChanged(ulong clientId, bool connected)
        {
            ClientId = clientId;
            Connected = connected;
        }
    }
}
