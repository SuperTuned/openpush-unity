using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Existing Android push plumbing in a Unity project's own files: services on
    /// <c>com.google.firebase.MESSAGING_EVENT</c> in manifests and .androidlib folders, classes extending
    /// <c>FirebaseMessagingService</c>, Firebase Unity Messaging, google-services.json and <c>ic_stat_*</c>
    /// icons. OpenPush's own service is never reported.
    /// </summary>
    internal static class OpenPushAndroidPlumbingScan
    {
        internal const string MessagingEvent = "com.google.firebase.MESSAGING_EVENT";
        /// <summary>The service Firebase Unity Messaging merges into the app.</summary>
        internal const string FirebaseUnityService = "com.google.firebase.messaging.cpp.ListenerService";

        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        private static readonly Regex JavaService = new Regex(@"class\s+(\w+)\s+extends\s+(?:[\w.]+\.)?FirebaseMessagingService\b");
        private static readonly Regex KotlinService = new Regex(@"class\s+(\w+)\s*(?:\([^)]*\))?\s*:\s*(?:[\w.]+\.)?FirebaseMessagingService\s*\(");
        private static readonly Regex Package = new Regex(@"^\s*package\s+([\w.]+)", RegexOptions.Multiline);
        private static readonly Regex StatusIcon = new Regex(@"(^|/)res/drawable[^/]*/(?<name>ic_stat_[^/.]+)\.(png|webp|xml)$", RegexOptions.IgnoreCase);

        /// <summary>Services declared on MESSAGING_EVENT in one manifest, with the manifest's package applied to ".Name".</summary>
        internal static List<string> MessagingServices(string manifestXml)
        {
            var services = new List<string>();
            XDocument document;
            try { document = XDocument.Parse(manifestXml ?? ""); }
            catch (Exception) { return services; }
            var package = (string)document.Root?.Attribute("package") ?? "";
            foreach (var service in document.Descendants("service"))
            {
                var listens = service.Elements("intent-filter").Elements("action")
                    .Any(a => (string)a.Attribute(Android + "name") == MessagingEvent);
                if (!listens) continue;
                var name = (string)service.Attribute(Android + "name");
                if (string.IsNullOrEmpty(name)) continue;
                if (name.StartsWith(".")) name = package + name;
                services.Add(name);
            }
            return services;
        }

        /// <summary>The fully qualified class a Java or Kotlin source declares as a FirebaseMessagingService, or null.</summary>
        internal static string MessagingServiceClass(string source)
        {
            var match = JavaService.Match(source ?? "");
            if (!match.Success) match = KotlinService.Match(source ?? "");
            if (!match.Success) return null;
            var package = Package.Match(source);
            return package.Success ? package.Groups[1].Value + "." + match.Groups[1].Value : match.Groups[1].Value;
        }

        internal static bool IsOpenPush(string service) => service != null && service.StartsWith("ai.openpush.");

        /// <summary>
        /// Findings from the project's files. <paramref name="manifests"/> and <paramref name="sources"/> are
        /// (asset path, text) pairs; <paramref name="assetPaths"/> is every asset path.
        /// </summary>
        internal static List<OpenPushSetupFinding> Findings(IEnumerable<(string Path, string Text)> manifests,
                                                            IEnumerable<(string Path, string Text)> sources,
                                                            IEnumerable<string> assetPaths, bool firebaseUnityMessaging)
        {
            var findings = new List<OpenPushSetupFinding>();
            var paths = (assetPaths ?? Enumerable.Empty<string>()).Select(p => p.Replace('\\', '/')).ToList();
            var editable = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (path, text) in sources)
            {
                var name = MessagingServiceClass(text);
                if (name != null && !IsOpenPush(name)) editable[name] = path;
            }
            void AddService(string name, string source, bool canEdit)
            {
                if (IsOpenPush(name) || findings.Any(f => f.Kind == OpenPushSetupFinding.FcmService && f.Id == name)) return;
                var finding = new OpenPushSetupFinding(OpenPushSetupFinding.FcmService, name, source) { Editable = canEdit };
                if (editable.TryGetValue(name, out var file)) finding.Details["source_file"] = file;
                findings.Add(finding);
            }
            foreach (var (path, text) in manifests)
                foreach (var service in MessagingServices(text))
                    AddService(service, path, editable.ContainsKey(service));
            foreach (var pair in editable) AddService(pair.Key, pair.Value, true);
            if (firebaseUnityMessaging) AddService(FirebaseUnityService, "Firebase Unity Messaging", false);

            foreach (var json in paths.Where(p => p.EndsWith("/google-services.json") || p == "google-services.json"))
                findings.Add(new OpenPushSetupFinding(OpenPushSetupFinding.FirebaseApp, json, json));

            foreach (var group in paths.Select(p => StatusIcon.Match(p)).Where(m => m.Success).GroupBy(m => m.Groups["name"].Value))
            {
                if (group.Key.StartsWith("ic_stat_openpush")) continue;
                var icon = new OpenPushSetupFinding(OpenPushSetupFinding.NotificationIcon, group.Key, group.First().Value.TrimStart('/'));
                icon.Details["files"] = group.Select(m => (object)m.Value.TrimStart('/')).ToList();
                findings.Add(icon);
            }
            return findings;
        }
    }
}
