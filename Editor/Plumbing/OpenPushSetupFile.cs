using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenPushSDK.Core;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// ProjectSettings/OpenPushSetup.json, schema <c>openpush-setup/1</c>. The iOS post-build step records
    /// what it found in the real export under <c>found</c>, so the next editor session knows the existing
    /// extension's names. <c>choices</c> mirrors the answers given in the Setup window or the build dialog
    /// (the settings asset holds the values a build uses). Rules shared with the native setup tools: unknown
    /// fields are kept on rewrite, <c>found</c> is always regenerated, <c>choices</c> only changes with an
    /// explicit answer. A file that does not parse is never overwritten.
    /// </summary>
    internal sealed class OpenPushSetupFile
    {
        internal const string DefaultPath = "ProjectSettings/OpenPushSetup.json";
        internal const string Schema = "openpush-setup/1";

        private readonly Dictionary<string, object> _root;

        /// <summary>False when the file exists but is not a JSON object; it is then left alone.</summary>
        public readonly bool Writable;

        private OpenPushSetupFile(Dictionary<string, object> root, bool writable)
        {
            _root = root;
            Writable = writable;
        }

        internal static OpenPushSetupFile Read(string path)
        {
            if (!File.Exists(path)) return Empty();
            return Parse(File.ReadAllText(path));
        }

        internal static OpenPushSetupFile Parse(string json)
        {
            if (OpenPushJson.TryParse(json, out var parsed) && parsed is Dictionary<string, object> root)
                return new OpenPushSetupFile(root, true);
            return new OpenPushSetupFile(new Dictionary<string, object>(StringComparer.Ordinal), false);
        }

        internal static OpenPushSetupFile Empty() => new OpenPushSetupFile(new Dictionary<string, object>(StringComparer.Ordinal), true);

        internal IReadOnlyList<OpenPushSetupFinding> Found
            => _root.TryGetValue("found", out var found) && found is List<object> list
                ? list.Select(OpenPushSetupFinding.FromJson).Where(f => f != null).ToList()
                : new List<OpenPushSetupFinding>();

        /// <summary>Replaces <c>found</c> wholesale; every other field is kept.</summary>
        internal void SetFound(IEnumerable<OpenPushSetupFinding> findings)
        {
            EnsureHeader();
            _root["found"] = findings.Select(f => (object)f.ToJson()).ToList();
        }

        internal Dictionary<string, object> Choice(string key)
            => _root.TryGetValue("choices", out var choices) && choices is Dictionary<string, object> map
               && map.TryGetValue(key, out var choice) ? choice as Dictionary<string, object> : null;

        internal bool HasChoice(string key) => Choice(key) != null;

        /// <summary>Records an explicit answer; the other choices are kept.</summary>
        internal void SetChoice(string key, Dictionary<string, object> value)
        {
            EnsureHeader();
            if (!(_root.TryGetValue("choices", out var choices) && choices is Dictionary<string, object> map))
                _root["choices"] = map = new Dictionary<string, object>(StringComparer.Ordinal);
            map[key] = value;
        }

        internal void RemoveChoice(string key)
        {
            if (_root.TryGetValue("choices", out var choices) && choices is Dictionary<string, object> map) map.Remove(key);
        }

        /// <summary>The checklist as Report saw it (a field of this package's own).</summary>
        internal void SetChecks(IEnumerable<KeyValuePair<string, (bool Passed, string Detail)>> rows)
        {
            EnsureHeader();
            _root["checks"] = rows.Select(r => (object)new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["title"] = r.Key, ["passed"] = r.Value.Passed, ["detail"] = r.Value.Detail,
            }).ToList();
        }

        /// <summary>A top-level boolean of this package's own (an unknown field to the other tools).</summary>
        internal bool Flag(string key) => _root.TryGetValue(key, out var value) && value is bool b && b;

        internal void SetFlag(string key, bool value)
        {
            EnsureHeader();
            _root[key] = value;
        }

        private void EnsureHeader()
        {
            if (!_root.ContainsKey("schema")) _root["schema"] = Schema;
            if (!_root.ContainsKey("platform")) _root["platform"] = "unity";
        }

        internal string ToJson() => Pretty(OpenPushJson.WriteValueMap(_root)) + "\n";

        /// <summary>
        /// Writes the file only when its text would change, so an unchanged export leaves the committed file
        /// (and its timestamp) alone. False when nothing was written.
        /// </summary>
        internal bool WriteIfChanged(string path)
        {
            if (!Writable) return false;
            if (File.Exists(path) && File.ReadAllText(path) == ToJson()) return false;
            return Write(path);
        }

        /// <summary>Writes the file. False (and nothing written) when the file on disk did not parse.</summary>
        internal bool Write(string path)
        {
            if (!Writable) return false;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, ToJson());
            return true;
        }

        /// <summary>Two-space indentation for a file people read and commit.</summary>
        internal static string Pretty(string json)
        {
            var sb = new StringBuilder();
            var depth = 0;
            var inString = false;
            for (var i = 0; i < json.Length; i++)
            {
                var c = json[i];
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < json.Length) sb.Append(json[++i]);
                    else if (c == '"') inString = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inString = true; sb.Append(c); break;
                    case '{': case '[':
                        var close = c == '{' ? '}' : ']';
                        if (i + 1 < json.Length && json[i + 1] == close) { sb.Append(c).Append(close); i++; break; }
                        depth++; sb.Append(c).Append('\n').Append(' ', depth * 2); break;
                    case '}': case ']':
                        depth--; sb.Append('\n').Append(' ', depth * 2).Append(c); break;
                    case ',': sb.Append(",\n").Append(' ', depth * 2); break;
                    case ':': sb.Append(": "); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
