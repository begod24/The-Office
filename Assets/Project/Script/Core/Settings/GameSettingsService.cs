using System;
using System.Collections.Generic;
using Office.Data;
using UnityEngine;

namespace Office.Core
{
    public sealed class GameSettingsService : ISettingsService
    {
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

        public GameSettingsService(ISettingsStore store, IDisplayDevice display,
            IEventBus bus = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.display = display ?? throw new ArgumentNullException(nameof(display));
            this.bus = bus;

            Current = Load();

            resolutions = ResolutionCatalogue.Build(display.Supported,
                display.CurrentResolution, Current.Resolution);

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

            var width = store.GetInt(WidthKey, 0);
            var height = store.GetInt(HeightKey, 0);

            var mode = store.Has(ModeKey)
                ? (FullScreenMode)store.GetInt(ModeKey, (int)display.CurrentMode)
                : display.CurrentMode;

            return new GameSettings(new Vector2Int(width, height), mode, master, music);
        }

        private void Commit(GameSettings next)
        {
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
