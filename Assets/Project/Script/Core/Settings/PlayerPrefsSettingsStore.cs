using UnityEngine;

namespace Office.Core
{
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
