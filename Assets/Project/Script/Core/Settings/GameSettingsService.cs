using System;
using System.Collections.Generic;
using Office.Data;
using UnityEngine;

namespace Office.Core
{
    /// <summary>
    /// Loads the stored settings at boot, applies the display, and writes every change back.
    /// </summary>
    /// <remarks>
    /// <b>What this class does not do is play anything.</b> It owns state, persistence and the
    /// screen; turning a stored number into sound belongs to <c>Office.Audio</c>, which is the
    /// assembly that owns the source it would be set on. Core deciding how loud a track is
    /// would be the same mistake as Core knowing what a track is.
    /// <para>
    /// Volumes apply as they are dragged and are stored on every change; the display waits for
    /// Apply. That split is not a UI decision — a resolution the monitor cannot show is only
    /// recoverable while the screen is still readable, so nothing may set one on the way past.
    /// </para>
    /// </remarks>
    public sealed class GameSettingsService : ISettingsService
    {
        // Namespaced so that a future binding or profile key cannot collide with these, and
        // so a corrupt entry is identifiable in the player prefs by name.
        private const string MasterKey = "office.audio.master";
        private const string MusicKey = "office.audio.music";
        private const string WidthKey = "office.display.width";
        private const string HeightKey = "office.display.height";
        private const string ModeKey = "office.display.mode";

        private readonly ISettingsStore store;
        private readonly IDisplayDevice display;
        private readonly IEventBus bus;

        private readonly List<Vector2Int> resolutions;

        public GameSettings Current { get; private set; }

        public IReadOnlyList<Vector2Int> Resolutions => resolutions;

        public bool DisplayChangesTakeEffect => display.ChangesTakeEffect;

        /// <param name="bus">
        /// Optional. Without one the service still works and simply announces nothing, which
        /// is what an EditMode test wants.
        /// </param>
        public GameSettingsService(ISettingsStore store, IDisplayDevice display,
            IEventBus bus = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.display = display ?? throw new ArgumentNullException(nameof(display));
            this.bus = bus;

            Current = Load();

            resolutions = ResolutionCatalogue.Build(display.Supported,
                display.CurrentResolution, Current.Resolution);

            // Unity persists the last window size on its own, but that is the size the window
            // happened to end at — including one an earlier build set. Reapplying what the
            // player chose makes the picker and the window the same fact from the first frame.
            if (Current.HasResolution) display.Apply(Current.Resolution, Current.DisplayMode);
        }

        public void SetMasterVolume(float slider) => Commit(Current.WithMasterVolume(slider));

        public void SetMusicVolume(float slider) => Commit(Current.WithMusicVolume(slider));

        public void ApplyDisplay(Vector2Int resolution, FullScreenMode mode)
        {
            if (resolution.x <= 0 || resolution.y <= 0)
            {
                Debug.LogError($"[Settings] Refusing to apply a {resolution.x}x{resolution.y} " +
                               "resolution — that would leave the game with no window.");
                return;
            }

            display.Apply(resolution, mode);
            Commit(Current.WithResolution(resolution).WithDisplayMode(mode));
        }

        private GameSettings Load()
        {
            var master = store.GetFloat(MasterKey, GameSettings.DefaultMasterVolume);
            var music = store.GetFloat(MusicKey, GameSettings.DefaultMusicVolume);

            // Zero means "never applied one", which is not the same as a resolution of zero:
            // the screen then starts on whatever is already on the display.
            var width = store.GetInt(WidthKey, 0);
            var height = store.GetInt(HeightKey, 0);

            var mode = store.Has(ModeKey)
                ? (FullScreenMode)store.GetInt(ModeKey, (int)display.CurrentMode)
                : display.CurrentMode;

            return new GameSettings(new Vector2Int(width, height), mode, master, music);
        }

        private void Commit(GameSettings next)
        {
            // A slider reports every frame it is dragged through, and most of those frames
            // carry the value it already has.
            if (next == Current) return;

            Current = next;
            Persist(next);

            bus?.Publish(new SettingsChanged(next));
        }

        private void Persist(GameSettings settings)
        {
            store.SetFloat(MasterKey, settings.MasterVolume);
            store.SetFloat(MusicKey, settings.MusicVolume);
            store.SetInt(WidthKey, settings.Width);
            store.SetInt(HeightKey, settings.Height);
            store.SetInt(ModeKey, (int)settings.DisplayMode);
            store.Save();
        }
    }
}
