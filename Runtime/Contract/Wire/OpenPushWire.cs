using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>
    /// Tolerant readers over OpenPushJson for the payloads both native bridges emit. Missing keys,
    /// JSON null and the wrong type all read as null/false; nothing here throws on native input.
    /// </summary>
    internal static class OpenPushWire
    {
        private static readonly IReadOnlyDictionary<string, string> Empty =
            new Dictionary<string, string>(StringComparer.Ordinal);

        internal static Dictionary<string, object> Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return OpenPushJson.TryParse(json, out var value) ? value as Dictionary<string, object> : null; }
            catch (Exception) { return null; }
        }

        internal static string Str(Dictionary<string, object> o, string key)
        {
            if (o == null || !o.TryGetValue(key, out var v) || v == null) return null;
            return v is string s ? s : Scalar(v);
        }

        internal static bool Bool(Dictionary<string, object> o, string key)
        {
            if (o == null || !o.TryGetValue(key, out var v) || v == null) return false;
            switch (v)
            {
                case bool b: return b;
                case double d: return d != 0;
                case string s: return s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
                default: return false;
            }
        }

        internal static int? Int(Dictionary<string, object> o, string key)
        {
            if (o == null || !o.TryGetValue(key, out var v) || v == null) return null;
            if (v is double d) return (int)d;
            if (v is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
            return null;
        }

        internal static long Long(Dictionary<string, object> o, string key)
        {
            if (o == null || !o.TryGetValue(key, out var v) || v == null) return 0;
            if (v is double d) return (long)d;
            return v is string s && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : 0;
        }

        internal static Dictionary<string, object> Obj(Dictionary<string, object> o, string key)
            => o != null && o.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        internal static List<object> Arr(Dictionary<string, object> o, string key)
            => o != null && o.TryGetValue(key, out var v) ? v as List<object> : null;

        /// <summary>
        /// A data map as the public API types it: string to string. Numbers and booleans become their
        /// invariant text, nested objects and arrays become their JSON, nulls are dropped.
        /// </summary>
        internal static IReadOnlyDictionary<string, string> StringMap(Dictionary<string, object> o, string key)
        {
            var source = Obj(o, key);
            if (source == null) return Empty;
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in source)
            {
                if (pair.Key == null || pair.Value == null) continue;
                map[pair.Key] = pair.Value is string s ? s
                    : pair.Value is Dictionary<string, object> || pair.Value is List<object> ? ToJson(pair.Value)
                    : Scalar(pair.Value);
            }
            return map;
        }

        internal static string StringMapToJson(IReadOnlyDictionary<string, string> map)
        {
            var entries = new Dictionary<string, object>(StringComparer.Ordinal);
            if (map != null) foreach (var pair in map) if (pair.Key != null) entries[pair.Key] = pair.Value;
            return OpenPushJson.WriteValueMap(entries);
        }

        internal static string ToJson(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return "\"" + OpenPushJson.Escape(s) + "\"";
                case bool b: return b ? "true" : "false";
                case IEnumerable<KeyValuePair<string, object>> map: return OpenPushJson.WriteValueMap(map);
                case IEnumerable<KeyValuePair<string, string>> strings:
                    var copy = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (var pair in strings) if (pair.Key != null) copy[pair.Key] = pair.Value;
                    return OpenPushJson.WriteValueMap(copy);
                case IEnumerable list:
                    var sb = new StringBuilder("[");
                    var first = true;
                    foreach (var item in list) { if (!first) sb.Append(','); first = false; sb.Append(ToJson(item)); }
                    return sb.Append(']').ToString();
                default: return Scalar(value);
            }
        }

        private static string Scalar(object v)
        {
            switch (v)
            {
                case bool b: return b ? "true" : "false";
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                default: return Convert.ToString(v, CultureInfo.InvariantCulture);
            }
        }
    }
}
