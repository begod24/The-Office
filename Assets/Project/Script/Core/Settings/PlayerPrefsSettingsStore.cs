using UnityEngine;

namespace Office.Core
{
    /// <summary>
    /// The shipping store: Unity's per-user preferences.
    /// </summary>
    /// <remarks>
    /// Enough for a handful of numbers, and it needs no file format, no schema and no
    /// migration. When settings grow past that — key bindings, per-player profiles — this is
    /// the one class that changes.
    /// </remarks>
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public bool Has(string key) => PlayerPrefs.HasKey(key);

        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);

        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);

        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public void Save() => PlayerPrefs.Save();
    }
}
