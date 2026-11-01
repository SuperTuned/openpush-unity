#if UNITY_IOS
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Links the vendored OpenPush XCFrameworks into UnityFramework (static, never embedded) and sets
    /// the build settings the Swift bridge and the natives need. Idempotent on re-export.
    /// </summary>
    public sealed class OpenPushIOSBuildSettings : IPostprocessBuildWithReport
    {
        // The extension injectors (1-3) copy and link their own frameworks through the same
        // idempotent helper, so this step can run after them.
        public int callbackOrder => 45;

        // Each injected extension target and its Info.plist, relative to the export root.
        private static readonly (string Target, string Plist)[] Extensions =
        {
            ("OpenPushNotificationService", "OpenPushNotificationService/Info.plist"),
            ("OpenPushLiveActivityWidget", "OpenPushLiveActivityWidget/Info.plist"),
        };

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var root = report.summary.outputPath;
            var path = PBXProject.GetPBXProjectPath(root);
            var project = new PBXProject(); project.ReadFromFile(path);
            var framework = project.GetUnityFrameworkTargetGuid();
            var main = project.GetUnityMainTargetGuid();
            OpenPushNativeFrameworks.Link(project, framework,
                OpenPushNativeFrameworks.CopyInto(root, OpenPushNativeFrameworks.UnityFrameworkModules));
            Apply(project, framework, main);
            SyncExtensions(project, root, main, OpenPushExtensionValues.From(project, main, root));
            project.WriteToFile(path);
            var settings = Resources.Load<OpenPushSettings>("OpenPushSettings");
            ApplyLaunchUrlSetting(root, settings == null || settings.OpenLaunchUrls);
        }

        internal const string SuppressLaunchUrlsKey = "OpenPushSuppressLaunchURLs";

        /// <summary>
        /// The bridge opens a tapped push's launch URL unless the app's Info.plist sets
        /// <see cref="SuppressLaunchUrlsKey"/>. Written only when the settings turn launch URLs off,
        /// and removed otherwise, so an append export follows the current setting.
        /// </summary>
        internal static void ApplyLaunchUrlSetting(string root, bool openLaunchUrls)
        {
            var plistPath = Path.Combine(root, "Info.plist");
            if (!File.Exists(plistPath)) return;
            var document = LoadPlist(plistPath);
            var dict = document.Root.Element("dict");
            var entry = dict.Elements("key").FirstOrDefault(k => k.Value == SuppressLaunchUrlsKey);
            if (openLaunchUrls)
            {
                if (entry == null) return;
                entry.ElementsAfterSelf().FirstOrDefault()?.Remove();
                entry.Remove();
            }
            else if (entry?.ElementsAfterSelf().FirstOrDefault() is XElement existing)
            {
                existing.ReplaceWith(new XElement("true"));
            }
            else
            {
                dict.AddFirst(new XElement("key", SuppressLaunchUrlsKey), new XElement("true"));
            }
            SavePlist(document, plistPath);
        }

        /// <summary>
        /// Writes each injected extension's Info.plist values as literals, the way the extension
        /// templates of other SDKs and Unity's own app Info.plist do: CFBundleDisplayName (the
        /// target's name), and the app's CFBundleShortVersionString and CFBundleVersion, which
        /// Apple requires the extension to match. Not build variables: a variable resolves to empty
        /// whenever the setting does not reach the extension target (a host build step or CI that
        /// rewrites build settings), and App Store Connect rejects the upload (90360 and friends).
        /// Also gives each extension without a team the app's development team, which automatic signing needs,
        /// and sets the target's MARKETING_VERSION and CURRENT_PROJECT_VERSION to the same versions.
        /// Rewrites what an append export of an older template kept. Idempotent.
        /// </summary>
        internal static void SyncExtensions(PBXProject project, string root, string main, OpenPushExtensionValues values)
        {
            foreach (var (target, plist) in Extensions)
            {
                var extension = project.TargetGuidByName(target);
                if (extension == null) continue;
                // Only a target without a team gets the app's: a team set on an append export (by the host or
                // another build step) is signing the host chose, and is kept.
                if (!string.IsNullOrEmpty(values.DevelopmentTeam)
                    && string.IsNullOrWhiteSpace(project.GetBuildPropertyForAnyConfig(extension, "DEVELOPMENT_TEAM")))
                    project.SetBuildProperty(extension, "DEVELOPMENT_TEAM", values.DevelopmentTeam);
                // The same versions as build settings too, as the injected targets have always carried
                // them: a tool that reads or bumps the target's versions sees the app's.
                SetIfDifferent(project, extension, "MARKETING_VERSION", values.MarketingVersion);
                SetIfDifferent(project, extension, "CURRENT_PROJECT_VERSION", values.BuildNumber);
                var plistPath = Path.Combine(root, plist);
                if (!File.Exists(plistPath)) continue;
                SetPlistStrings(plistPath, new[]
                {
                    ("CFBundleDisplayName", target),
                    ("CFBundleShortVersionString", values.MarketingVersion),
                    ("CFBundleVersion", values.BuildNumber),
                });
            }
        }

        private static void SetIfDifferent(PBXProject project, string target, string property, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || project.GetBuildPropertyForAnyConfig(target, property) == value) return;
            project.SetBuildProperty(target, property, value);
        }

        internal const string MarketingVersionPlaceholder = "__OPENPUSH_MARKETING_VERSION__";
        internal const string BuildNumberPlaceholder = "__OPENPUSH_BUILD_NUMBER__";

        /// <summary>
        /// An extension Info.plist template with the app's versions written in, so an injected extension
        /// carries them from the moment it is created (the order-45 sync then keeps them current).
        /// </summary>
        internal static string WithAppVersions(string template, OpenPushExtensionValues values)
            => template.Replace(MarketingVersionPlaceholder, values.MarketingVersion)
                       .Replace(BuildNumberPlaceholder, values.BuildNumber);

        // Plists are edited as XML so the same code runs in the Editor and under the tests' stand-ins.
        private static XDocument LoadPlist(string path)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, IgnoreWhitespace = true };
            using (var reader = XmlReader.Create(path, settings)) return XDocument.Load(reader);
        }

        // XDocument writes a DOCTYPE it parsed as `"...dtd"[]>`: valid XML, but Xcode's plist parser
        // stops at the empty internal subset and the Info.plist build step fails.
        private static void SavePlist(XDocument document, string path)
        {
            if (document.DocumentType != null) document.DocumentType.InternalSubset = null;
            document.Save(path);
        }

        internal static string ReadPlistString(string path, string key)
        {
            var entry = LoadPlist(path).Root?.Element("dict")?.Elements("key").FirstOrDefault(k => k.Value == key);
            return entry?.ElementsAfterSelf().FirstOrDefault()?.Value;
        }

        private static void SetPlistStrings(string path, (string Key, string Value)[] entries)
        {
            var document = LoadPlist(path);
            var dict = document.Root.Element("dict");
            var changed = false;
            foreach (var (key, value) in entries)
            {
                var entry = dict.Elements("key").FirstOrDefault(k => k.Value == key);
                var existing = entry?.ElementsAfterSelf().FirstOrDefault();
                if (existing != null && existing.Name == "string" && existing.Value == value) continue;
                if (existing != null) existing.ReplaceWith(new XElement("string", value));
                else dict.AddFirst(new XElement("key", key), new XElement("string", value));
                changed = true;
            }
            if (changed) SavePlist(document, path);
        }

        /// <summary>
        /// Sets IPHONEOS_DEPLOYMENT_TARGET to <paramref name="minimum"/> only when the target's current
        /// value is lower, missing or unreadable: a host on iOS 17+ keeps its own floor.
        /// </summary>
        internal static void RaiseDeploymentTarget(PBXProject project, string target, string minimum)
        {
            var current = project.GetBuildPropertyForAnyConfig(target, "IPHONEOS_DEPLOYMENT_TARGET");
            if (ParseOsVersion(current) is System.Version have && have >= ParseOsVersion(minimum)) return;
            project.SetBuildProperty(target, "IPHONEOS_DEPLOYMENT_TARGET", minimum);
        }

        private static System.Version ParseOsVersion(string value)
        {
            var text = (value ?? "").Trim();
            if (text.Length > 0 && !text.Contains(".")) text += ".0";
            return System.Version.TryParse(text, out var version) ? version : null;
        }

        internal static void Apply(PBXProject project, string framework, string main)
        {
            foreach (var target in new[] { framework, main })
                RaiseDeploymentTarget(project, target, "16.0");
            project.SetBuildProperty(framework, "SWIFT_VERSION", "5.0");
            project.SetBuildProperty(framework, "DEFINES_MODULE", "YES");
            project.SetBuildProperty(framework, "CLANG_ENABLE_MODULES", "YES");
            project.SetBuildProperty(framework, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");
            project.SetBuildProperty(main, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            // Static libraries carry no load commands, so the system frameworks the natives use are
            // linked here: WebKit/SafariServices (in-app messages), weak ActivityKit/WidgetKit (16.1+).
            project.AddFrameworkToProject(framework, "WebKit.framework", false);
            project.AddFrameworkToProject(framework, "SafariServices.framework", false);
            project.AddFrameworkToProject(framework, "UserNotifications.framework", false);
            project.AddFrameworkToProject(framework, "ActivityKit.framework", true);
            project.AddFrameworkToProject(framework, "WidgetKit.framework", true);
        }
    }

    /// <summary>
    /// What the injected extensions copy from the app: its versions (Apple requires them to match)
    /// and its development team. The app's literal Info.plist values win, since that is what the
    /// extension is validated against; Player Settings stand in for a missing or variable one.
    /// </summary>
    internal sealed class OpenPushExtensionValues
    {
        public readonly string MarketingVersion, BuildNumber, DevelopmentTeam;

        public OpenPushExtensionValues(string marketingVersion, string buildNumber, string developmentTeam)
        {
            MarketingVersion = marketingVersion;
            BuildNumber = buildNumber;
            DevelopmentTeam = developmentTeam;
        }

        internal static OpenPushExtensionValues From(PBXProject project, string main, string root)
        {
            var appPlist = Path.Combine(root, "Info.plist");
            string Literal(string key, string fallback)
            {
                var value = File.Exists(appPlist) ? OpenPushIOSBuildSettings.ReadPlistString(appPlist, key) : null;
                return string.IsNullOrWhiteSpace(value) || value.Contains("$") ? fallback : value.Trim();
            }
            var team = project.GetBuildPropertyForAnyConfig(main, "DEVELOPMENT_TEAM");
            return new OpenPushExtensionValues(
                Literal("CFBundleShortVersionString", PlayerSettings.bundleVersion),
                Literal("CFBundleVersion", PlayerSettings.iOS.buildNumber),
                string.IsNullOrWhiteSpace(team) ? PlayerSettings.iOS.appleDeveloperTeamID ?? "" : team.Trim());
        }
    }
}
#endif
