using System.Collections.Generic;
using NUnit.Framework;
using Office.Core;
using Office.Data;
using UnityEngine;

namespace Office.Tests.EditMode
{
    public sealed class GameSettingsServiceTests
    {
        private FakeStore store;
        private FakeDisplay display;
        private EventBus bus;

        [SetUp]
        public void SetUp()
        {
            store = new FakeStore();
            display = new FakeDisplay();
            bus = new EventBus();
        }

        private GameSettingsService Create() => new(store, display, bus);

        [Test]
        public void FreshInstall_UsesTheDefaultsAndDoesNotTouchTheScreen()
        {
            var settings = Create();

            Assert.AreEqual(GameSettings.DefaultMasterVolume, settings.Current.MasterVolume);
            Assert.AreEqual(GameSettings.DefaultMusicVolume, settings.Current.MusicVolume);
            Assert.IsFalse(settings.Current.HasResolution);
            Assert.AreEqual(0, display.Applied,
                "Nothing was ever chosen, so nothing may resize the player's window at boot.");
        }

        [Test]
        public void StoredResolution_IsReappliedAtBoot()
        {
            Create().ApplyDisplay(new Vector2Int(1280, 720), FullScreenMode.Windowed);

            display.Applied = 0;
            var reloaded = Create();

            Assert.AreEqual(1, display.Applied,
                "A resolution that survives the session but not the restart is not a setting.");
            Assert.AreEqual(new Vector2Int(1280, 720), reloaded.Current.Resolution);
            Assert.AreEqual(FullScreenMode.Windowed, reloaded.Current.DisplayMode);
        }

        [Test]
        public void ApplyDisplay_ReachesTheScreenBeforeItIsStored()
        {
            var settings = Create();

            settings.ApplyDisplay(new Vector2Int(2560, 1440), FullScreenMode.FullScreenWindow);

            Assert.AreEqual(new Vector2Int(2560, 1440), display.LastResolution);
            Assert.AreEqual(FullScreenMode.FullScreenWindow, display.LastMode);
            Assert.AreEqual(new Vector2Int(2560, 1440), settings.Current.Resolution);
        }

        [Test]
        public void ApplyDisplay_RefusesANonPositiveSize()
        {
            var settings = Create();

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            settings.ApplyDisplay(new Vector2Int(0, 1080), FullScreenMode.Windowed);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(0, display.Applied, "That would leave the game with no window.");
        }

        [Test]
        public void Volume_IsClampedAndAnnounced()
        {
            var settings = Create();

            var announced = 0;
            var last = default(GameSettings);

            bus.Subscribe<SettingsChanged>(evt =>
            {
                announced++;
                last = evt.Settings;
            });

            settings.SetMusicVolume(2f);

            Assert.AreEqual(1f, settings.Current.MusicVolume);
            Assert.AreEqual(1, announced);
            Assert.AreEqual(1f, last.MusicVolume);
        }

        [Test]
        public void UnchangedValue_IsNotAnnouncedAgain()
        {
            var settings = Create();

            var announced = 0;
            bus.Subscribe<SettingsChanged>(_ => announced++);

            settings.SetMusicVolume(0.4f);
            settings.SetMusicVolume(0.4f);

            Assert.AreEqual(1, announced);
        }

        [Test]
        public void Volume_SurvivesARestart()
        {
            Create().SetMasterVolume(0.25f);

            Assert.AreEqual(0.25f, Create().Current.MasterVolume, 0.0001f);
        }

        [Test]
        public void Resolutions_ContainTheCurrentScreenAndTheStoredOne()
        {
            Create().ApplyDisplay(new Vector2Int(3440, 1440), FullScreenMode.Windowed);

            display.CurrentResolution = new Vector2Int(1600, 900);

            var reloaded = Create();

            CollectionAssert.Contains(reloaded.Resolutions, new Vector2Int(1600, 900));
            CollectionAssert.Contains(reloaded.Resolutions, new Vector2Int(3440, 1440));
        }

        private sealed class FakeStore : ISettingsStore
        {
            private readonly Dictionary<string, float> floats = new();
            private readonly Dictionary<string, int> ints = new();

            public bool Has(string key) => floats.ContainsKey(key) || ints.ContainsKey(key);

            public float GetFloat(string key, float fallback) =>
                floats.TryGetValue(key, out var value) ? value : fallback;

            public void SetFloat(string key, float value) => floats[key] = value;

            public int GetInt(string key, int fallback) =>
                ints.TryGetValue(key, out var value) ? value : fallback;

            public void SetInt(string key, int value) => ints[key] = value;

            public void Save()
            {
            }
        }

        private sealed class FakeDisplay : IDisplayDevice
        {
            public Vector2Int CurrentResolution { get; set; } = new(1920, 1080);

            public FullScreenMode CurrentMode { get; set; } = FullScreenMode.FullScreenWindow;

            public IReadOnlyList<Vector2Int> Supported { get; set; } = new[]
            {
                new Vector2Int(1280, 720),
                new Vector2Int(1920, 1080)
            };

            public bool ChangesTakeEffect => true;

            public int Applied { get; set; }

            public Vector2Int LastResolution { get; private set; }

            public FullScreenMode LastMode { get; private set; }

            public void Apply(Vector2Int resolution, FullScreenMode mode)
            {
                Applied++;
                LastResolution = resolution;
                LastMode = mode;
                CurrentResolution = resolution;
                CurrentMode = mode;
            }
        }
    }
}
