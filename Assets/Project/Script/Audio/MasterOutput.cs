using Office.Core;
using Office.Data;
using UnityEngine;

namespace Office.Audio
{
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
