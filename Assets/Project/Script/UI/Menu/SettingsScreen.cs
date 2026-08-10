using System.Collections.Generic;
using Office.Core;
using Office.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    /// <summary>
    /// The settings column, shared by the main menu and the pause menu.
    /// </summary>
    /// <remarks>
    /// Both copies are views of one <see cref="ISettingsService"/>, and neither keeps state of
    /// its own beyond the display choice it is staging. That is what makes the pause menu
    /// agree with the main menu: the two are built by the same builder into different scenes,
    /// so anything either remembered locally would be a second answer to a question the
    /// service already answers — and they would disagree the first time one of them changed
    /// something.
    /// <para>
    /// <b>Volume applies as it is dragged; display waits for Apply.</b> The player is
    /// listening to the thing the slider sets, so a round trip through a button would be a
    /// worse way to choose a level. A resolution is the opposite: the wrong one is only
    /// recoverable while the screen is still readable, so nothing may set one on the way past.
    /// </para>
    /// </remarks>
    public sealed class SettingsScreen : MonoBehaviour
    {
        // Screen.SetResolution is accepted and ignored in play mode — the Game view owns the
        // size there. Without this line the picker looks broken to the only person who ever
        // tests it in the editor.
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

        // Every time the panel opens, not once: the other copy of this screen may have moved
        // something since, and so may anything else that ever writes a setting.
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

            // Nothing stored yet means the picker starts on whatever is already on screen,
            // rather than on the first entry in the list.
            var target = current.HasResolution
                ? current.Resolution
                : new Vector2Int(Screen.width, Screen.height);

            resolutionIndex = Mathf.Max(0, ResolutionCatalogue.NearestIndex(resolutions, target));
            displayModeIndex = DisplayModes.IndexOf(current.DisplayMode);

            // Without notification: these are the values the service already holds, and
            // echoing them back through the setters would write over a change still in flight.
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

            // Nothing staged means nothing to apply. It also tells the player which of the two
            // halves of this screen has already taken effect and which has not.
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
