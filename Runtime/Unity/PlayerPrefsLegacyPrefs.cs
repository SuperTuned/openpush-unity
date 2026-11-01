using OpenPushSDK.Platform;
using UnityEngine;

namespace OpenPushSDK
{
    internal sealed class PlayerPrefsLegacyPrefs : IOpenPushLegacyPrefs
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public string GetString(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }
}
