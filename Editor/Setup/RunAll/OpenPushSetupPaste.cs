using System;
using System.Collections.Generic;
using System.Linq;
using OpenPushSDK.Core;

namespace OpenPushSDK.Editor
{
    /// <summary>The app id, SDK key and (optional) server copied from the OpenPush console.</summary>
    internal sealed class OpenPushSetupValues
    {
        public string AppId, SdkKey, Server;
    }

    /// <summary>
    /// Reads what the console's copy buttons put on the clipboard, in any of three shapes:
    /// <list type="bullet">
    /// <item>a link, <c>openpush://setup?app=…&amp;sdk=…[&amp;server=…]</c>;</item>
    /// <item>JSON, <c>{"app_id": …, "sdk_key": …, "server": …}</c> (appId / sdkKey / app / sdk / server_url too);</item>
    /// <item>the two raw values, app id first: on two lines, or separated by whitespace or a comma, each
    /// optionally labelled ("App ID: …", "SDK key: …").</item>
    /// </list>
    /// Nothing is fetched; anything else is refused rather than guessed.
    /// </summary>
    internal static class OpenPushSetupPaste
    {
        internal static bool TryParse(string text, out OpenPushSetupValues values, out string problem)
        {
            values = null;
            problem = null;
            var input = (text ?? "").Trim();
            if (input.Length == 0) { problem = "The clipboard is empty."; return false; }
            if (input.StartsWith("openpush://", StringComparison.OrdinalIgnoreCase)) values = FromLink(input);
            else if (input.StartsWith("{")) values = FromJson(input);
            else values = FromRaw(input);
            if (values != null && !string.IsNullOrWhiteSpace(values.AppId) && !string.IsNullOrWhiteSpace(values.SdkKey)
                && Plain(values.AppId) && Plain(values.SdkKey))
            {
                values.AppId = values.AppId.Trim();
                values.SdkKey = values.SdkKey.Trim();
                values.Server = string.IsNullOrWhiteSpace(values.Server) ? null : values.Server.Trim().TrimEnd('/');
                return true;
            }
            values = null;
            problem = "The clipboard does not hold an OpenPush app id and SDK key. Copy them from the console "
                    + "(the two values, the openpush://setup link, or the JSON) and try again.";
            return false;
        }

        private static bool Plain(string value) => value.Trim().Length > 0 && !value.Trim().Any(char.IsWhiteSpace);

        private static OpenPushSetupValues FromLink(string link)
        {
            if (!link.StartsWith("openpush://setup", StringComparison.OrdinalIgnoreCase)) return null;
            var query = link.IndexOf('?') < 0 ? "" : link.Substring(link.IndexOf('?') + 1);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in query.Split('&'))
            {
                var eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                map[pair.Substring(0, eq)] = Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
            }
            return new OpenPushSetupValues { AppId = Get(map, "app", "app_id"), SdkKey = Get(map, "sdk", "sdk_key"), Server = Get(map, "server", "server_url") };
        }

        private static OpenPushSetupValues FromJson(string json)
        {
            if (!OpenPushJson.TryParse(json, out var parsed) || !(parsed is Dictionary<string, object> root)) return null;
            var map = root.Where(p => p.Value is string).ToDictionary(p => p.Key, p => (string)p.Value, StringComparer.OrdinalIgnoreCase);
            return new OpenPushSetupValues
            {
                AppId = Get(map, "app_id", "appId", "app"), SdkKey = Get(map, "sdk_key", "sdkKey", "sdk"),
                Server = Get(map, "server", "server_url", "serverUrl"),
            };
        }

        /// <summary>An app id as the server makes them: the app's lowercase slug.</summary>
        internal static readonly System.Text.RegularExpressions.Regex AppIdShape =
            new System.Text.RegularExpressions.Regex("^[a-z0-9][a-z0-9_-]{0,63}$");

        /// <summary>An SDK key as the server makes them: op_sdk_&lt;slug without dashes&gt;_&lt;16 hex&gt;.</summary>
        internal static readonly System.Text.RegularExpressions.Regex SdkKeyShape =
            new System.Text.RegularExpressions.Regex("^op_sdk_[a-z0-9_]+_[0-9a-f]{16}$");

        /// <summary>
        /// Raw text is only believed when it is exactly an app id and an SDK key of the server's shapes, in either
        /// order or labelled, and the key belongs to that app id. Anything else (a sentence, a URL, two random
        /// words) is refused, so a stray clipboard never lands in the settings.
        /// </summary>
        private static OpenPushSetupValues FromRaw(string text)
        {
            var tokens = new List<string>();
            foreach (var line in text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var value = line.Trim();
                var colon = value.IndexOf(':');
                if (colon > 0)
                {
                    var label = value.Substring(0, colon).Trim().ToLowerInvariant();
                    if (!(label.Contains("app") || label.Contains("sdk") || label.Contains("key"))) return null;
                    value = value.Substring(colon + 1);
                }
                tokens.AddRange(value.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries));
            }
            if (tokens.Count != 2) return null;
            var sdk = tokens.FirstOrDefault(t => SdkKeyShape.IsMatch(t));
            var app = tokens.FirstOrDefault(t => t != sdk && AppIdShape.IsMatch(t));
            if (sdk == null || app == null || !sdk.StartsWith("op_sdk_" + app.Replace("-", "") + "_")) return null;
            return new OpenPushSetupValues { AppId = app, SdkKey = sdk };
        }

        private static string Get(IDictionary<string, string> map, params string[] keys)
        {
            foreach (var key in keys) if (map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
            return null;
        }
    }
}
