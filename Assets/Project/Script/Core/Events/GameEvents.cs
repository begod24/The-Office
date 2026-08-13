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
    /// The local player opened or closed the inventory screen. Nothing pauses: the run goes
    /// on, and no other client sees anything.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="LocalPauseChanged"/> even though both free the cursor and mute
    /// this player's input, because the two are not interchangeable to everything that listens:
    /// the pause menu has to suppress its own Escape key for this one and not for its own, and
    /// a screen effect tied to being paused must not fire when a player merely looks in a bag.
    /// Publishing this as a pause would also let one of the two overlays close the other's
    /// cursor out from under it.
    /// </remarks>
    public readonly struct LocalInventoryChanged
    {
        public readonly bool IsOpen;

        public LocalInventoryChanged(bool isOpen) => IsOpen = isOpen;
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

    /// <summary>
    /// How far a scene load has got, for whatever is covering the screen while it happens.
    /// </summary>
    /// <remarks>
    /// Published by the scene loader rather than read from it, so the loading screen never
    /// holds a reference to the loader and can be built, tested and replaced on its own.
    /// <para>
    /// <b>Progress is not the same as readiness.</b> A run is playable only once every client
    /// has reported its scene loaded and the session has moved to InRun; this number stops at
    /// 1 well before that. Anything showing it has to treat the phase as the truth and this as
    /// the detail — see <c>LoadingScreen</c>.
    /// </para>
    /// </remarks>
    public readonly struct SceneLoadProgressChanged
    {
        public readonly string SceneName;

        /// <summary>0 to 1 across the load itself.</summary>
        public readonly float Progress;

        /// <summary>False on the last event of a load, once the scene is up.</summary>
        public readonly bool IsLoading;

        public SceneLoadProgressChanged(string sceneName, float progress, bool isLoading)
        {
            SceneName = sceneName ?? string.Empty;
            Progress = Mathf.Clamp01(progress);
            IsLoading = isLoading;
        }
    }

    /// <summary>
    /// A setting changed. Carries the whole picture rather than the one field that moved.
    /// </summary>
    /// <remarks>
    /// Whoever reacts to this usually reads more than the field that changed — the music
    /// wants its own volume, a future mixer wants every bus — and a delta would make each
    /// listener keep a copy of the rest to fill the gaps in. One value means no listener
    /// holds settings state of its own.
    /// </remarks>
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

    /// <summary>
    /// Something audible happened in the world, for whatever hunts by ear. GDD §8.1: fighting
    /// is loud and pulls the office in.
    /// </summary>
    /// <remarks>
    /// <b>Server-side only.</b> It is published where an action is ruled on and consumed where
    /// enemy brains live, and both of those are the server — the event never crosses the wire.
    /// What a remote client sees is the enemy's replicated behaviour changing, not the noise.
    /// Publishing it on a client is not an error the bus can catch; it is simply a noise
    /// nothing will ever hear.
    /// <para>
    /// This is a gameplay fact, not a sound. The audio layer has its own reasons to play
    /// something and does not wait for this event; a silenced weapon would still publish it.
    /// </para>
    /// </remarks>
    public readonly struct NoiseRaised
    {
        public readonly Vector3 Position;

        /// <summary>
        /// Metres this noise carries. A listener also caps it with its own hearing range —
        /// the quieter of the two decides.
        /// </summary>
        public readonly float Radius;

        /// <summary>Who made it, or <see cref="ulong.MaxValue"/> when the world did.</summary>
        public readonly ulong SourceClientId;

        public NoiseRaised(Vector3 position, float radius, ulong sourceClientId)
        {
            Position = position;
            Radius = Mathf.Max(0f, radius);
            SourceClientId = sourceClientId;
        }
    }
}
