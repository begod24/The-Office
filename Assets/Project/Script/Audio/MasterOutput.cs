using Office.Core;
using Office.Data;
using UnityEngine;

namespace Office.Audio
{
    /// <summary>
    /// The one place the master slider becomes engine gain.
    /// </summary>
    /// <remarks>
    /// <c>AudioListener.volume</c> is global and outlives scene loads, so this only has to
    /// write it when it changes. Something still has to own that write, and it is not the
    /// settings service: Core stores what the player chose, and this assembly decides how a
    /// stored number becomes sound — the same split that keeps the resolution next to
    /// <c>Screen</c> and the track next to its source.
    /// <para>
    /// When a mixer arrives this class points at an exposed parameter instead, and nothing
    /// else in the project has to know.
    /// </para>
    /// </remarks>
    public sealed class MasterOutput : MonoBehaviour
    {
        private IEventBus bus;

        private void Awake()
        {
            if (ServiceLocator.TryGet(out ISettingsService settings))
                Apply(settings.Current.MasterVolume);

            if (ServiceLocator.TryGet(out bus)) bus.Subscribe<SettingsChanged>(OnSettingsChanged);
        }

        private void OnDestroy() => bus?.Unsubscribe<SettingsChanged>(OnSettingsChanged);

        private void OnSettingsChanged(SettingsChanged evt) => Apply(evt.Settings.MasterVolume);

        private static void Apply(float slider) => AudioListener.volume = VolumeCurve.ToGain(slider);
    }
}
