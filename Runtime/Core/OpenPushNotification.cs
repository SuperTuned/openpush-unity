using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenPushSDK.Core
{
    /// <summary>
    /// One composer action button. <c>SystemIcon</c> marks <c>Icon</c> as a
    /// platform icon name rather than an asset the host ships.
    /// </summary>
    public sealed class OpenPushAction
    {
        public string Id;
        public string Label;
        public string Icon;
        public bool SystemIcon;

        /// <summary>
        /// The button's own link (its <c>url</c> in the composer), or null when the button has
        /// none. A tap on a button with a link reports it as <see cref="OpenPushClickEvent.ActionUrl"/>.
        /// </summary>
        public string Url;
    }

    /// <summary>
    /// The composed notification behind a click or foreground event, read out of
    /// the data map the server delivered. The raw map stays available as
    /// <see cref="Data"/>: this type names the keys the composer sends so a host
    /// does not have to spell them, and it never invents values the payload did
    /// not carry. iOS alert keys are folded into <c>aps</c> by the server and are
    /// not visible here.
    /// </summary>
    public sealed class OpenPushNotification
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyData =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly IReadOnlyList<OpenPushAction> NoActions =
            Array.Empty<OpenPushAction>();

        /// <summary>The key that marks a background (data-only) push.</summary>
        internal const string BackgroundKey = "op_background";

        /// <summary>Prefix of the composer's free-form Android overrides.</summary>
        private const string AndroidPrefix = "op_android_";

        public string MessageId;
        public string Title;
        public string Body;
        public string ImageUrl;
        /// <summary>The <c>deep_link</c> the message opens, when it carries one.</summary>
        public string LaunchUrl;
        public string CollapseId;
        /// <summary><c>high</c> or <c>normal</c>.</summary>
        public string Priority;
        public string ActionCategory;
        public string AppId;
        /// <summary>Time to live in seconds; null when absent or not a number.</summary>
        public int? TtlSeconds;
        public bool SystemMessage;

        /// <summary>
        /// True for a background (data-only) push: <c>op_background</c> is <c>"1"</c> and the push
        /// has no title and no body. Such a push is never displayed; Android delivers it to
        /// <c>OpenPush.Notifications.BackgroundMessageReceived</c>.
        /// </summary>
        public bool IsBackground;

        /// <summary>Empty when the payload carries no parseable actions.</summary>
        public IReadOnlyList<OpenPushAction> Actions = NoActions;

        /// <summary>
        /// Composer overrides for the Android renderer, keyed by the part after
        /// <c>op_android_</c> — <c>op_android_channel</c> reads as
        /// <c>channel</c>. Empty when the payload carries none.
        /// </summary>
        public IReadOnlyDictionary<string, string> AndroidOverrides = EmptyData;

        /// <summary>
        /// The data map this was read from — the same instance the caller
        /// passed, not a copy. Never null: an absent payload reads as empty.
        /// </summary>
        public IReadOnlyDictionary<string, string> Data = EmptyData;

        /// <summary>
        /// Reads a notification out of a push data payload. Missing keys read as
        /// null (or an empty collection); a null payload is safe.
        /// </summary>
        public static OpenPushNotification From(IReadOnlyDictionary<string, string> data)
        {
            var notification = new OpenPushNotification { Data = data ?? EmptyData };
            if (data == null) return notification;

            notification.MessageId = Text(data, "op_message_id");
            notification.Title = Text(data, "title");
            notification.Body = Text(data, "body");
            notification.ImageUrl = Text(data, "image_url");
            notification.LaunchUrl = Text(data, "deep_link");
            notification.CollapseId = Text(data, "op_collapse_id");
            notification.Priority = Text(data, "op_priority");
            notification.ActionCategory = Text(data, "op_action_category");
            notification.AppId = Text(data, "op_app");

            var ttl = Text(data, "op_ttl");
            if (ttl != null && int.TryParse(ttl, NumberStyles.Integer,
                                            CultureInfo.InvariantCulture, out var seconds))
                notification.TtlSeconds = seconds;

            // The server sets this key only on a system message, but a payload
            // that spells the absent case out explicitly must not read as true.
            var system = Text(data, "op_system_message");
            notification.SystemMessage = system != null &&
                !string.Equals(system, "false", StringComparison.OrdinalIgnoreCase) &&
                system != "0";

            notification.Actions = ParseActions(Text(data, "op_actions"));
            notification.IsBackground = Text(data, BackgroundKey) == "1" &&
                notification.Title == null && notification.Body == null;

            Dictionary<string, string> overrides = null;
            foreach (var pair in data)
            {
                if (pair.Key == null || !pair.Key.StartsWith(AndroidPrefix, StringComparison.Ordinal))
                    continue;
                var name = pair.Key.Substring(AndroidPrefix.Length);
                if (name.Length == 0) continue;
                overrides ??= new Dictionary<string, string>(StringComparer.Ordinal);
                overrides[name] = pair.Value;
            }
            if (overrides != null) notification.AndroidOverrides = overrides;

            return notification;
        }

        /// <summary>
        /// Parses the composer's action array. Anything the array cannot be read
        /// as, and any entry without both an id and a label, is dropped: a host
        /// renders what it can show rather than failing the whole notification.
        /// </summary>
        internal static IReadOnlyList<OpenPushAction> ParseActions(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return NoActions;
            if (!OpenPushJson.TryParse(json, out var parsed) ||
                !(parsed is List<object> entries)) return NoActions;

            List<OpenPushAction> actions = null;
            foreach (var entry in entries)
            {
                if (!(entry is Dictionary<string, object> fields)) continue;
                var id = Field(fields, "id");
                var label = Field(fields, "label");
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(label)) continue;
                actions ??= new List<OpenPushAction>(entries.Count);
                actions.Add(new OpenPushAction
                {
                    Id = id,
                    Label = label,
                    Icon = Field(fields, "icon"),
                    SystemIcon = Truthy(fields, "system_icon"),
                    Url = Field(fields, "url"),
                });
            }
            return actions ?? NoActions;
        }

        private static string Text(IReadOnlyDictionary<string, string> data, string key)
            => data.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

        private static string Field(Dictionary<string, object> fields, string key)
            => fields.TryGetValue(key, out var value) && value is string text && text.Length > 0
                ? text
                : null;

        private static bool Truthy(Dictionary<string, object> fields, string key)
        {
            if (!fields.TryGetValue(key, out var value)) return false;
            return value switch
            {
                bool flag => flag,
                double number => number != 0,
                string text => text.Length > 0 && text != "0" &&
                               !string.Equals(text, "false", StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
        }
    }
}
