using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenPushSDK.Core
{
    /// <summary>
    /// Minimal JSON helpers for the SDK's small, fixed payloads. Not a general
    /// JSON library: TryGetStringField is a bounded scan used only to detect
    /// structured error codes (e.g. scope_conflict) in short server responses.
    /// </summary>
    public static class OpenPushJson
    {
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? "";
            var sb = new StringBuilder(value.Length + 8);
            foreach (var ch in value)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }

        public static string WriteStringMap(IEnumerable<KeyValuePair<string, string>> entries)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            var first = true;
            foreach (var pair in entries)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(pair.Key)).Append("\":\"")
                  .Append(Escape(pair.Value ?? "")).Append('"');
            }
            return sb.Append('}').ToString();
        }

        public static string WriteValueMap(IEnumerable<KeyValuePair<string, object>> entries)
        {
            var sb = new StringBuilder("{");
            var first = true;
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry.Key == null) continue;
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(Escape(entry.Key)).Append("\":");
                    WriteValue(sb, entry.Value);
                }
            }
            return sb.Append('}').ToString();
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null: sb.Append("null"); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case string s: sb.Append('"').Append(Escape(s)).Append('"'); return;
                case sbyte _: case byte _: case short _: case ushort _:
                case int _: case uint _: case long _: case ulong _:
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return;
                case float f:
                    if (float.IsNaN(f) || float.IsInfinity(f))
                        throw new ArgumentException("JSON numbers must be finite");
                    sb.Append(f.ToString("R", CultureInfo.InvariantCulture)); return;
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d))
                        throw new ArgumentException("JSON numbers must be finite");
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); return;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case IEnumerable<KeyValuePair<string, object>> map:
                    sb.Append(WriteValueMap(map)); return;
                case System.Collections.IEnumerable list:
                    sb.Append('[');
                    var first = true;
                    foreach (var item in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteValue(sb, item);
                    }
                    sb.Append(']'); return;
                default:
                    sb.Append('"').Append(Escape(Convert.ToString(
                        value, CultureInfo.InvariantCulture))).Append('"'); return;
            }
        }

        public static bool TryParseStringMap(string json, out Dictionary<string, string> map)
        {
            map = null;
            if (string.IsNullOrEmpty(json)) return false;
            var i = 0;
            SkipWhitespace(json, ref i);
            if (i >= json.Length || json[i++] != '{') return false;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            SkipWhitespace(json, ref i);
            if (i < json.Length && json[i] == '}')
            {
                i++;
                SkipWhitespace(json, ref i);
                if (i != json.Length) return false;
                map = result;
                return true;
            }
            while (true)
            {
                SkipWhitespace(json, ref i);
                if (!TryReadString(json, ref i, out var key)) return false;
                SkipWhitespace(json, ref i);
                if (i >= json.Length || json[i++] != ':') return false;
                SkipWhitespace(json, ref i);
                if (!TryReadString(json, ref i, out var value)) return false;
                result[key] = value;
                SkipWhitespace(json, ref i);
                if (i >= json.Length) return false;
                if (json[i] == ',') { i++; continue; }
                if (json[i++] != '}') return false;
                SkipWhitespace(json, ref i);
                if (i != json.Length) return false;
                map = result;
                return true;
            }
        }

        /// <summary>Parses the nested object/array shape used by IAM definitions.</summary>
        public static bool TryParse(string json, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            var i = 0;
            if (!TryReadValue(json, ref i, out value)) return false;
            SkipWhitespace(json, ref i);
            return i == json.Length;
        }

        private static bool TryReadValue(string json, ref int i, out object value)
        {
            value = null; SkipWhitespace(json, ref i);
            if (i >= json.Length) return false;
            if (json[i] == '"')
            {
                if (!TryReadString(json, ref i, out var text)) return false;
                value = text; return true;
            }
            if (json[i] == '{')
            {
                i++; var map = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace(json, ref i); if (i < json.Length && json[i] == '}') { i++; value = map; return true; }
                while (true)
                {
                    SkipWhitespace(json, ref i); if (!TryReadString(json, ref i, out var key)) return false;
                    SkipWhitespace(json, ref i); if (i >= json.Length || json[i++] != ':') return false;
                    if (!TryReadValue(json, ref i, out var item)) return false; map[key] = item;
                    SkipWhitespace(json, ref i); if (i >= json.Length) return false;
                    if (json[i] == ',') { i++; continue; }
                    if (json[i++] != '}') return false; value = map; return true;
                }
            }
            if (json[i] == '[')
            {
                i++; var list = new List<object>(); SkipWhitespace(json, ref i);
                if (i < json.Length && json[i] == ']') { i++; value = list; return true; }
                while (true)
                {
                    if (!TryReadValue(json, ref i, out var item)) return false; list.Add(item);
                    SkipWhitespace(json, ref i); if (i >= json.Length) return false;
                    if (json[i] == ',') { i++; continue; }
                    if (json[i++] != ']') return false; value = list; return true;
                }
            }
            foreach (var literal in new[] { "true", "false", "null" })
            {
                if (!StartsWithLiteral(json, i, literal)) continue;
                i += literal.Length; value = literal == "true" ? (object)true : literal == "false" ? false : null; return true;
            }
            var start = i;
            if (json[i] == '-') i++;
            while (i < json.Length && char.IsDigit(json[i])) i++;
            if (i < json.Length && json[i] == '.') { i++; while (i < json.Length && char.IsDigit(json[i])) i++; }
            if (i < json.Length && (json[i] == 'e' || json[i] == 'E'))
            { i++; if (i < json.Length && (json[i] == '+' || json[i] == '-')) i++; while (i < json.Length && char.IsDigit(json[i])) i++; }
            if (i == start || !double.TryParse(json.Substring(start, i - start), NumberStyles.Float,
                                               CultureInfo.InvariantCulture, out var number)) return false;
            value = number; return true;
        }

        private static void SkipWhitespace(string value, ref int i)
        {
            while (i < value.Length && char.IsWhiteSpace(value[i])) i++;
        }

        private static bool TryReadString(string json, ref int i, out string value)
        {
            value = null;
            if (i >= json.Length || json[i++] != '"') return false;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                var ch = json[i++];
                if (ch == '"') { value = sb.ToString(); return true; }
                if (ch < 0x20) return false;
                if (ch != '\\') { sb.Append(ch); continue; }
                if (i >= json.Length) return false;
                var escaped = json[i++];
                switch (escaped)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > json.Length ||
                            !int.TryParse(json.Substring(i, 4), NumberStyles.HexNumber,
                                          CultureInfo.InvariantCulture, out var code)) return false;
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: return false;
                }
            }
            return false;
        }

        public static string TryGetStringField(string json, string field)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field)) return null;
            var needle = "\"" + field + "\"";
            var at = json.IndexOf(needle, StringComparison.Ordinal);
            if (at < 0) return null;
            var i = at + needle.Length;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != ':') return null;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < json.Length && json[i] != '"')
            {
                if (json[i] == '\\' && i + 1 < json.Length) i++;
                sb.Append(json[i]);
                i++;
            }
            return i < json.Length ? sb.ToString() : null;
        }

        /// <summary>
        /// Reads a whole number out of a response body without parsing it.
        /// A bounded scan like <see cref="TryGetStringField"/>: /v1/ingest
        /// reports how many events it accepted and how many it dropped, and
        /// those two counts are all the SDK reads back from a receipt.
        /// </summary>
        public static bool TryGetIntField(string json, string field, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field)) return false;
            var needle = "\"" + field + "\"";
            var at = json.IndexOf(needle, StringComparison.Ordinal);
            if (at < 0) return false;
            var i = at + needle.Length;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != ':') return false;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            var start = i;
            if (i < json.Length && json[i] == '-') i++;
            while (i < json.Length && char.IsDigit(json[i])) i++;
            return i > start && int.TryParse(json.Substring(start, i - start), NumberStyles.Integer,
                                             CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// True when the field is present with a value that is neither
        /// <c>null</c> nor <c>false</c>. The server marks a refused identity with
        /// <c>identity_rejected</c>: a bare presence check would also fire on an
        /// explicit <c>:false</c>, and a string-only check would miss a
        /// <c>:true</c> that carries no note. Both mistakes are silent, and one
        /// of them reports a working login as broken.
        /// </summary>
        public static bool HasTruthyField(string json, string field)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(field)) return false;
            var needle = "\"" + field + "\"";
            var at = json.IndexOf(needle, StringComparison.Ordinal);
            if (at < 0) return false;
            var i = at + needle.Length;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != ':') return false;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return false;
            return !StartsWithLiteral(json, i, "null") && !StartsWithLiteral(json, i, "false");
        }

        private static bool StartsWithLiteral(string json, int at, string literal)
            => at + literal.Length <= json.Length &&
               string.CompareOrdinal(json, at, literal, 0, literal.Length) == 0;
    }
}
