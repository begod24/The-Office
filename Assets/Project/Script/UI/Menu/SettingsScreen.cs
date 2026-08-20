using System.Collections.Generic;
using Office.Core;
using Office.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        private const string EditorNote = "// EDITOR IGNORES RESOLUTION — TEST IT IN A BUILD";

        [Header("Display")]
        [SerializeField] private TMP_Text displayModeValue;
        [SerializeField] private Button displayModePrevButton;
        [SerializeField] private Button displayModeNextButton;
        [SerializeField] private TMP_Text resolutionValue;
        [SerializeField] private Button resolutionPrevButton;
        [SerializeField] private Button resolutionNextButton;
        [SerializeField] private Button applyButton;

        [Header("Audio")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private TMP_Text masterValue;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private TMP_Text musicValue;

        [Header("Feedback")]
        [SerializeField] private TMP_Text noteLabel;

        private ISettingsService settings;
        private IReadOnlyList<Vector2Int> resolutions;

        private int resolutionIndex;
        private int displayModeIndex;

        private void Awake()
        {
            ServiceLocator.TryGet(out settings);

            if (displayModePrevButton != null)
                displayModePrevButton.onClick.AddListener(() => StepDisplayMode(-1));

            if (displayModeNextButton != null)
                displayModeNextButton.onClick.AddListener(() => StepDisplayMode(1));

            if (resolutionPrevButton != null)
                resolutionPrevButton.onClick.AddListener(() => StepResolution(-1));

            if (resolutionNextButton != null)
                resolutionNextButton.onClick.AddListener(() => StepResolution(1));

            if (applyButton != null) applyButton.onClick.AddListener(ApplyDisplay);

            if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        }

        private void OnEnable()
        {
            if (settings == null && !ServiceLocator.TryGet(out settings))
            {
                SetInteractable(false);
                Show("// NO SERVICES — ENTER PLAY MODE FROM SCN_BOOT");
                return;
            }

            SetInteractable(true);

            resolutions = settings.Resolutions;
            SyncFromSettings();
        }

        private void SyncFromSettings()
        {
            var current = settings.Current;

            var target = current.HasResolution
                ? current.Resolution
                : new Vector2Int(Screen.width, Screen.height);

            resolutionIndex = Mathf.Max(0, ResolutionCatalogue.NearestIndex(resolutions, target));
            displayModeIndex = DisplayModes.IndexOf(current.DisplayMode);

            if (masterSlider != null) masterSlider.SetValueWithoutNotify(current.MasterVolume);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(current.MusicVolume);

            Refresh();
            Show(settings.DisplayChangesTakeEffect ? string.Empty : EditorNote);
        }

        private void StepResolution(int direction)
        {
            if (resolutions == null || resolutions.Count == 0) return;

            resolutionIndex = (resolutionIndex + direction + resolutions.Count) % resolutions.Count;
            Refresh();
        }

        private void StepDisplayMode(int direction)
        {
            var count = DisplayModes.Ordered.Length;

            displayModeIndex = (displayModeIndex + direction + count) % count;
            Refresh();
        }

        private void ApplyDisplay()
        {
            if (settings == null || resolutions == null || resolutions.Count == 0) return;

            var chosen = resolutions[resolutionIndex];
            var mode = DisplayModes.Ordered[displayModeIndex];

            settings.ApplyDisplay(chosen, mode);

            Show(settings.DisplayChangesTakeEffect
                ? $"// APPLIED {chosen.x} x {chosen.y} {DisplayModes.Label(mode).ToUpperInvariant()}"
                : EditorNote);

            Refresh();
        }

        private void OnMasterChanged(float value)
        {
            settings?.SetMasterVolume(value);
            RefreshVolumeLabels();
        }

        private void OnMusicChanged(float value)
        {
            settings?.SetMusicVolume(value);
            RefreshVolumeLabels();
        }

        private void Refresh()
        {
            if (resolutionValue != null && resolutions != null && resolutions.Count > 0)
            {
                var chosen = resolutions[resolutionIndex];
                resolutionValue.text = $"{chosen.x} x {chosen.y}";
            }

            if (displayModeValue != null)
                displayModeValue.text = DisplayModes.Label(DisplayModes.Ordered[displayModeIndex]);

            RefreshVolumeLabels();

            if (applyButton != null) applyButton.interactable = HasPendingDisplayChange();
        }

        private bool HasPendingDisplayChange()
        {
            if (settings == null || resolutions == null || resolutions.Count == 0) return false;

            var current = settings.Current;
            if (!current.HasResolution) return true;

            return current.Resolution != resolutions[resolutionIndex] ||
                   current.DisplayMode != DisplayModes.Ordered[displayModeIndex];
        }

        private void RefreshVolumeLabels()
        {
            if (masterValue != null && masterSlider != null)
                masterValue.text = $"{VolumeCurve.ToPercent(masterSlider.value)}%";

            if (musicValue != null && musicSlider != null)
                musicValue.text = $"{VolumeCurve.ToPercent(musicSlider.value)}%";
        }

        private void SetInteractable(bool value)
        {
            if (displayModePrevButton != null) displayModePrevButton.interactable = value;
            if (displayModeNextButton != null) displayModeNextButton.interactable = value;
            if (resolutionPrevButton != null) resolutionPrevButton.interactable = value;
            if (resolutionNextButton != null) resolutionNextButton.interactable = value;
            if (applyButton != null) applyButton.interactable = value;
            if (masterSlider != null) masterSlider.interactable = value;
            if (musicSlider != null) musicSlider.interactable = value;
        }

        private void Show(string note)
        {
            if (noteLabel != null) noteLabel.text = note;
        }
    }
}
