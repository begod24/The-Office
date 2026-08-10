namespace Office.Core
{
    /// <summary>
    /// Where settings survive between sessions.
    /// </summary>
    /// <remarks>
    /// The seam exists so the settings service can be exercised without touching the real
    /// machine: an EditMode test that wrote through <c>PlayerPrefs</c> would change the editor
    /// it runs in, and the next run would start from whatever the last test left behind.
    /// </remarks>
    public interface ISettingsStore
    {
        bool Has(string key);

        float GetFloat(string key, float fallback);

        void SetFloat(string key, float value);

        int GetInt(string key, int fallback);

        void SetInt(string key, int value);

        /// <summary>Flush to disk. Called once per change, not per key.</summary>
        void Save();
    }
}
