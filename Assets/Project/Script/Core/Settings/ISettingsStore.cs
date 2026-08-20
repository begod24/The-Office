namespace Office.Core
{
    public interface ISettingsStore
    {
        bool Has(string key);

        float GetFloat(string key, float fallback);

        void SetFloat(string key, float value);

        int GetInt(string key, int fallback);

        void SetInt(string key, int value);

        void Save();
    }
}
