using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The native targets of an Xcode project file and their build settings per configuration, read from
    /// the pbxproj text. Unity's PBXProject cannot list targets, and detection has to find extensions by
    /// what they are (their extension point), never by name.
    /// </summary>
    internal sealed class OpenPushPbxproj
    {
        internal const string ApplicationType = "com.apple.product-type.application";
        internal const string AppExtensionType = "com.apple.product-type.app-extension";

        internal sealed class Configuration
        {
            public string Name;
            public Dictionary<string, string> Settings = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        internal sealed class Target
        {
            public string Guid, Name, ProductType;
            public readonly List<Configuration> Configurations = new List<Configuration>();

            /// <summary>The value in the first configuration that sets it (Debug before Release, as Xcode lists them).</summary>
            internal string Setting(string key)
                => Configurations.Select(c => c.Settings.TryGetValue(key, out var v) ? v : null).FirstOrDefault(v => !string.IsNullOrEmpty(v));

            internal string Setting(string configuration, string key)
                => Configurations.FirstOrDefault(c => c.Name == configuration)?.Settings.TryGetValue(key, out var v) == true ? v : null;

            /// <summary>
            /// A setting with $(VAR) / ${VAR} references to this target's other settings, TARGET_NAME and SRCROOT
            /// (the export root, written as nothing) expanded, and $(inherited) dropped.
            /// </summary>
            internal string Expanded(string key, string configuration = null)
            {
                var config = configuration == null ? Configurations.FirstOrDefault(c => c.Settings.ContainsKey(key))
                                                   : Configurations.FirstOrDefault(c => c.Name == configuration);
                if (config == null || !config.Settings.TryGetValue(key, out var raw)) return null;
                return Expand(raw, config, 0);
            }

            private string Expand(string value, Configuration config, int depth)
            {
                if (value == null || depth > 8) return value;
                var sb = new StringBuilder();
                for (var i = 0; i < value.Length; i++)
                {
                    if (value[i] == '$' && i + 1 < value.Length && (value[i + 1] == '(' || value[i + 1] == '{'))
                    {
                        var close = value[i + 1] == '(' ? ')' : '}';
                        var end = value.IndexOf(close, i + 2);
                        if (end > 0)
                        {
                            var name = value.Substring(i + 2, end - i - 2);
                            var colon = name.IndexOf(':');
                            var modifier = colon >= 0 ? name.Substring(colon + 1) : null;
                            if (colon >= 0) name = name.Substring(0, colon);
                            string resolved;
                            if (name == "SRCROOT" || name == "PROJECT_DIR") resolved = ".";
                            else if (name == "TARGET_NAME") resolved = Name;
                            else if (name == "inherited") resolved = "";
                            else if (config.Settings.TryGetValue(name, out var setting)) resolved = Expand(setting, config, depth + 1);
                            else if (name == "PRODUCT_NAME") resolved = Name;
                            else resolved = null;
                            if (resolved != null)
                            {
                                if (modifier == "rfc1034identifier" || modifier == "c99extidentifier")
                                    resolved = new string(resolved.Select(ch => char.IsLetterOrDigit(ch) || ch == '.' ? ch : modifier == "rfc1034identifier" ? '-' : '_').ToArray());
                                sb.Append(resolved);
                                i = end;
                                continue;
                            }
                        }
                    }
                    sb.Append(value[i]);
                }
                var text = sb.ToString().Trim();
                return text.StartsWith("./") ? text.Substring(2) : text;
            }
        }

        public readonly List<Target> Targets = new List<Target>();

        internal Target ByName(string name) => Targets.FirstOrDefault(t => t.Name == name);

        /// <summary>The app target: Unity-iPhone when present, else the first application target.</summary>
        internal Target App => Targets.FirstOrDefault(t => t.Name == "Unity-iPhone" && t.ProductType == ApplicationType)
                               ?? Targets.FirstOrDefault(t => t.ProductType == ApplicationType);

        internal IEnumerable<Target> AppExtensions => Targets.Where(t => t.ProductType == AppExtensionType);

        /// <summary>Parses pbxproj text. Throws <see cref="FormatException"/> when it is not one.</summary>
        internal static OpenPushPbxproj Parse(string text)
        {
            var reader = new Reader(text);
            if (!(reader.Value() is Dictionary<string, object> root)) throw new FormatException("not a project file");
            var objects = root.TryGetValue("objects", out var o) ? o as Dictionary<string, object> : null;
            if (objects == null) throw new FormatException("project file without objects");
            var project = new OpenPushPbxproj();
            foreach (var pair in objects)
            {
                if (!(pair.Value is Dictionary<string, object> obj) || Str(obj, "isa") != "PBXNativeTarget") continue;
                var target = new Target { Guid = pair.Key, Name = Str(obj, "name"), ProductType = Str(obj, "productType") };
                if (objects.TryGetValue(Str(obj, "buildConfigurationList") ?? "", out var listObj)
                    && listObj is Dictionary<string, object> list
                    && list.TryGetValue("buildConfigurations", out var configs) && configs is List<object> guids)
                {
                    foreach (var guid in guids.OfType<string>())
                    {
                        if (!(objects.TryGetValue(guid, out var c) && c is Dictionary<string, object> config)) continue;
                        var configuration = new Configuration { Name = Str(config, "name") };
                        if (config.TryGetValue("buildSettings", out var s) && s is Dictionary<string, object> settings)
                            foreach (var setting in settings)
                                configuration.Settings[setting.Key] = setting.Value is string v ? v
                                    : setting.Value is List<object> values ? string.Join(" ", values.OfType<string>()) : null;
                        target.Configurations.Add(configuration);
                    }
                }
                project.Targets.Add(target);
            }
            return project;
        }

        private static string Str(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var v) ? v as string : null;

        /// <summary>The OpenStep property-list syntax Xcode writes.</summary>
        private sealed class Reader
        {
            private readonly string _s;
            private int _i;

            public Reader(string s) { _s = s ?? ""; }

            public object Value()
            {
                Skip();
                if (_i >= _s.Length) throw new FormatException("unexpected end");
                var c = _s[_i];
                if (c == '{')
                {
                    _i++;
                    var map = new Dictionary<string, object>(StringComparer.Ordinal);
                    while (true)
                    {
                        Skip();
                        if (Peek() == '}') { _i++; return map; }
                        var key = Value() as string ?? throw new FormatException("key expected");
                        Expect('=');
                        map[key] = Value();
                        Expect(';');
                    }
                }
                if (c == '(')
                {
                    _i++;
                    var list = new List<object>();
                    while (true)
                    {
                        Skip();
                        if (Peek() == ')') { _i++; return list; }
                        list.Add(Value());
                        Skip();
                        if (Peek() == ',') _i++;
                    }
                }
                if (c == '"') return Quoted();
                var start = _i;
                while (_i < _s.Length && !char.IsWhiteSpace(_s[_i]) && "{}()=;,\"".IndexOf(_s[_i]) < 0
                       && !(_s[_i] == '/' && _i + 1 < _s.Length && (_s[_i + 1] == '*' || _s[_i + 1] == '/'))) _i++;
                if (_i == start) throw new FormatException("unexpected '" + c + "' at " + _i);
                return _s.Substring(start, _i - start);
            }

            private string Quoted()
            {
                _i++;
                var sb = new StringBuilder();
                while (_i < _s.Length && _s[_i] != '"')
                {
                    if (_s[_i] == '\\' && _i + 1 < _s.Length)
                    {
                        var e = _s[++_i];
                        sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                    }
                    else sb.Append(_s[_i]);
                    _i++;
                }
                _i++;
                return sb.ToString();
            }

            private char Peek() => _i < _s.Length ? _s[_i] : '\0';

            private void Expect(char c)
            {
                Skip();
                if (Peek() != c) throw new FormatException("'" + c + "' expected at " + _i);
                _i++;
            }

            private void Skip()
            {
                while (_i < _s.Length)
                {
                    if (char.IsWhiteSpace(_s[_i])) { _i++; continue; }
                    if (_s[_i] == '/' && _i + 1 < _s.Length && _s[_i + 1] == '*')
                    {
                        var end = _s.IndexOf("*/", _i + 2, StringComparison.Ordinal);
                        _i = end < 0 ? _s.Length : end + 2;
                        continue;
                    }
                    if (_s[_i] == '/' && _i + 1 < _s.Length && _s[_i + 1] == '/')
                    {
                        var end = _s.IndexOf('\n', _i);
                        _i = end < 0 ? _s.Length : end + 1;
                        continue;
                    }
                    break;
                }
            }
        }
    }
}
