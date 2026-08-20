using NUnit.Framework;
using Office.Data;
using UnityEngine;

namespace Office.Tests.EditMode
{
    public sealed class AudioSettingsTests
    {
        [Test]
        public void Curve_KeepsTheEnds()
        {
            Assert.AreEqual(0f, VolumeCurve.ToGain(0f), "Silence has to be silent.");
            Assert.AreEqual(1f, VolumeCurve.ToGain(1f), "Full has to be unattenuated.");
        }

        [Test]
        public void Curve_IsQuieterThanTheSliderPosition()
        {
            Assert.Less(VolumeCurve.ToGain(0.5f), 0.5f);
        }

        [Test]
        public void Curve_RoundTripsAPosition()
        {
            Assert.AreEqual(0.35f, VolumeCurve.ToSlider(VolumeCurve.ToGain(0.35f)), 0.0001f);
        }

        [Test]
        public void Curve_ClampsWhatItIsGiven()
        {
            Assert.AreEqual(1f, VolumeCurve.ToGain(4f));
            Assert.AreEqual(0f, VolumeCurve.ToGain(-1f));
            Assert.AreEqual(100, VolumeCurve.ToPercent(1.4f));
        }

        [Test]
        public void Settings_ClampVolumes()
        {
            var settings = new GameSettings(new Vector2Int(1920, 1080),
                FullScreenMode.Windowed, 3f, -2f);

            Assert.AreEqual(1f, settings.MasterVolume);
            Assert.AreEqual(0f, settings.MusicVolume);
        }

        [Test]
        public void Settings_TreatAZeroResolutionAsNothingStored()
        {
            var settings = new GameSettings(Vector2Int.zero, FullScreenMode.Windowed, 1f, 1f);

            Assert.IsFalse(settings.HasResolution);
        }

        [Test]
        public void DisplayModes_MapEveryModeOntoAnOfferedEntry()
        {
            foreach (FullScreenMode mode in System.Enum.GetValues(typeof(FullScreenMode)))
            {
                var index = DisplayModes.IndexOf(mode);

                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, DisplayModes.Ordered.Length,
                    $"{mode} has nowhere to land, so the picker would open on a wrong entry.");
            }
        }

        [Test]
        public void DisplayModes_ExclusiveFullscreenReadsAsFullscreen()
        {
            Assert.AreEqual(DisplayModes.Label(FullScreenMode.FullScreenWindow),
                DisplayModes.Label(FullScreenMode.ExclusiveFullScreen));
        }
    }
}
