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

    public readonly struct LocalInventoryChanged
    {
        public readonly bool IsOpen;

        public LocalInventoryChanged(bool isOpen) => IsOpen = isOpen;
    }

    public readonly struct InteractionPromptChanged
    {
        public readonly string Prompt;

        public InteractionPromptChanged(string prompt) => Prompt = prompt ?? string.Empty;

        public bool HasPrompt => !string.IsNullOrEmpty(Prompt);
    }

    public readonly struct LocalVitalsChanged
    {
        public readonly float Health;
        public readonly float MaxHealth;
        public readonly bool IsDowned;
        public readonly bool IsDead;

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

    public readonly struct SceneLoadProgressChanged
    {
        public readonly string SceneName;

        public readonly float Progress;

        public readonly bool IsLoading;

        public SceneLoadProgressChanged(string sceneName, float progress, bool isLoading)
        {
            SceneName = sceneName ?? string.Empty;
            Progress = Mathf.Clamp01(progress);
            IsLoading = isLoading;
        }
    }

    public readonly struct SettingsChanged
    {
        public readonly GameSettings Settings;

        public SettingsChanged(GameSettings settings) => Settings = settings;
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

    public readonly struct NoiseRaised
    {
        public readonly Vector3 Position;

        public readonly float Radius;

        public readonly ulong SourceClientId;

        public NoiseRaised(Vector3 position, float radius, ulong sourceClientId)
        {
            Position = position;
            Radius = Mathf.Max(0f, radius);
            SourceClientId = sourceClientId;
        }
    }
}
