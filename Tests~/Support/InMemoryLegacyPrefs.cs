using System.Collections.Generic;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    internal sealed class InMemoryLegacyPrefs : IOpenPushLegacyPrefs
    {
        public readonly Dictionary<string, string> Strings = new Dictionary<string, string>();
        public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
        public int Saves;
        public bool HasKey(string key) => Strings.ContainsKey(key) || Ints.ContainsKey(key);
        public string GetString(string key) => Strings.TryGetValue(key, out var v) ? v : null;
        public void SetString(string key, string value) => Strings[key] = value;
        public int GetInt(string key, int fallback) => Ints.TryGetValue(key, out var v) ? v : fallback;
        public void SetInt(string key, int value) => Ints[key] = value;
        public void DeleteKey(string key) { Strings.Remove(key); Ints.Remove(key); }
        public void Save() => Saves++;
    }
}
