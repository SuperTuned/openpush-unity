using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// One piece of push plumbing found in the project or its last export, in the shared
    /// <c>openpush-setup/1</c> shape the iOS and Android setup tools write too.
    /// </summary>
    internal sealed class OpenPushSetupFinding
    {
        internal const string AppGroup = "app_group";
        internal const string KeychainGroup = "keychain_group";
        internal const string ServiceExtension = "service_extension";
        internal const string WidgetExtension = "widget_extension";
        internal const string ContentExtension = "content_extension";
        internal const string FcmService = "fcm_service";
        internal const string FcmReceiver = "fcm_receiver";
        internal const string FirebaseApp = "firebase_app";
        internal const string NotificationIcon = "notification_icon";
        internal const string NotificationChannel = "notification_channel";
        internal const string PushSdk = "push_sdk";

        public string Kind, Id, Source, Vendor;
        public bool Editable = true;
        public readonly Dictionary<string, object> Details = new Dictionary<string, object>(StringComparer.Ordinal);

        public OpenPushSetupFinding(string kind, string id, string source)
        {
            Kind = kind; Id = id; Source = source;
        }

        internal string Detail(string key) => Details.TryGetValue(key, out var value) ? value as string : null;

        internal bool Flag(string key) => Details.TryGetValue(key, out var value) && value is bool b && b;

        internal IReadOnlyList<string> Strings(string key)
            => Details.TryGetValue(key, out var value) && value is IEnumerable<object> list
                ? list.OfType<string>().ToList()
                : value is IEnumerable<string> strings ? strings.ToList() : new List<string>();

        internal Dictionary<string, object> ToJson() => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["kind"] = Kind, ["id"] = Id, ["source"] = Source, ["vendor"] = Vendor, ["editable"] = Editable,
            ["details"] = new Dictionary<string, object>(Details, StringComparer.Ordinal),
        };

        internal static OpenPushSetupFinding FromJson(object json)
        {
            if (!(json is Dictionary<string, object> map) || !(map.TryGetValue("kind", out var kind) && kind is string k)) return null;
            var finding = new OpenPushSetupFinding(k, map.TryGetValue("id", out var id) ? id as string : null,
                                                   map.TryGetValue("source", out var source) ? source as string : null)
            {
                Vendor = map.TryGetValue("vendor", out var vendor) ? vendor as string : null,
                Editable = !map.TryGetValue("editable", out var editable) || !(editable is bool e) || e,
            };
            if (map.TryGetValue("details", out var details) && details is Dictionary<string, object> d)
                foreach (var pair in d) finding.Details[pair.Key] = pair.Value;
            return finding;
        }

        public override string ToString() => Kind + " " + Id + (Vendor != null ? " (" + Vendor + ")" : "");
    }
}
