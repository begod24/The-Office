using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Office.UI
{
    // Resolution picker shared by the main menu and the pause menu. The arrows only
    // select; the choice takes effect on Apply. Unity persists the last used
    // resolution on its own.
    public sealed class SettingsScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text resolutionValue;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button applyButton;

        private readonly List<Vector2Int> options = new();
        private int index;
        private bool initialised;

        private void OnEnable()
        {
            EnsureInitialised();
            SyncIndexToCurrent();
            RefreshLabel();
        }

        private void EnsureInitialised()
        {
            if (initialised) return;
            initialised = true;

            BuildOptions();

            if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
            if (applyButton != null) applyButton.onClick.AddListener(Apply);
        }

        private void BuildOptions()
        {
            options.Clear();

            foreach (var resolution in Screen.resolutions)
                AddOption(new Vector2Int(resolution.width, resolution.height));

            // The editor (and some platforms) report no display modes.
            if (options.Count == 0)
            {
                AddOption(new Vector2Int(1280, 720));
                AddOption(new Vector2Int(1366, 768));
                AddOption(new Vector2Int(1600, 900));
                AddOption(new Vector2Int(1920, 1080));
                AddOption(new Vector2Int(2560, 1440));
                AddOption(new Vector2Int(3840, 2160));
            }

            AddOption(new Vector2Int(Screen.width, Screen.height));

            options.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        }

        private void AddOption(Vector2Int size)
        {
            if (size.x <= 0 || size.y <= 0) return;
            if (!options.Contains(size)) options.Add(size);
        }

        private void SyncIndexToCurrent()
        {
            var current = new Vector2Int(Screen.width, Screen.height);
            var found = options.IndexOf(current);
            if (found >= 0) index = found;
        }

        private void Step(int direction)
        {
            if (options.Count == 0) return;

            index = (index + direction + options.Count) % options.Count;
            RefreshLabel();
        }

        private void Apply()
        {
            if (options.Count == 0) return;

            var chosen = options[index];
            Screen.SetResolution(chosen.x, chosen.y, Screen.fullScreenMode);
        }

        private void RefreshLabel()
        {
            if (resolutionValue == null || options.Count == 0) return;

            var chosen = options[index];
            resolutionValue.text = $"{chosen.x} x {chosen.y}";
        }
    }
}
