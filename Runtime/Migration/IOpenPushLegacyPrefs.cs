namespace OpenPushSDK.Platform
{
    /// <summary>PlayerPrefs, abstracted so the migrator runs under dotnet test.</summary>
    internal interface IOpenPushLegacyPrefs
    {
        bool HasKey(string key);
        string GetString(string key);
        void SetString(string key, string value);
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        void DeleteKey(string key);
        void Save();
    }
}
