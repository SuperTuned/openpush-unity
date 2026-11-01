using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenPushSDK.Editor
{
    /// <summary>An app extension in an export, classified by its NSExtensionPointIdentifier.</summary>
    internal sealed class OpenPushXcodeExtension
    {
        internal const string ServicePoint = "com.apple.usernotifications.service";
        internal const string ContentPoint = "com.apple.usernotifications.content-extension";
        internal const string WidgetPoint = "com.apple.widgetkit-extension";

        public string Target, Kind, BundleId, InfoPlist, PrincipalClass, PrincipalFile, Language, Entitlements, Vendor;
        public List<string> Groups = new List<string>(), KeychainGroups = new List<string>(), Sources = new List<string>();
        public OpenPushPbxproj.Target Raw;

        /// <summary>OpenPush's own targets, which are never "existing plumbing".</summary>
        public bool IsOpenPush => Target == OpenPushIOSIdentifiers.ServiceExtensionTarget || Target == OpenPushIOSIdentifiers.WidgetTarget;

        /// <summary>
        /// The provisioning profile the target signs with (Release first): its specifier, else the legacy
        /// PROVISIONING_PROFILE UUID; null when it has none or only a $(VARIABLE).
        /// </summary>
        public string Profile
        {
            get
            {
                if (Raw == null) return null;
                foreach (var config in Raw.Configurations.OrderBy(c => c.Name == "Release" ? 0 : 1))
                    foreach (var key in new[] { "PROVISIONING_PROFILE_SPECIFIER", "PROVISIONING_PROFILE" })
                        if (config.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) && !value.Contains("$("))
                            return value.Trim();
                return null;
            }
        }

        /// <summary>The class name the Info.plist names, without its module prefix.</summary>
        public string ClassName => PrincipalClass == null ? null : PrincipalClass.Substring(PrincipalClass.LastIndexOf('.') + 1);
    }

    /// <summary>
    /// What an exported Xcode project already has: the app's App Groups and Keychain groups, and every
    /// notification service, content and widget extension with its bundle id, signing, entitlements,
    /// principal class and sources. Read from the files themselves, so it works for any vendor or a
    /// studio's own extension; a fingerprint only adds the vendor's name.
    /// </summary>
    internal sealed class OpenPushXcodeExport
    {
        public string Root, AppBundleId, AppEntitlements;
        public List<string> AppGroups = new List<string>(), AppKeychainGroups = new List<string>();
        public readonly List<OpenPushXcodeExtension> Extensions = new List<OpenPushXcodeExtension>();
        public OpenPushPbxproj Project;

        internal IEnumerable<OpenPushXcodeExtension> HostServiceExtensions
            => Extensions.Where(e => e.Kind == OpenPushSetupFinding.ServiceExtension && !e.IsOpenPush);

        internal OpenPushXcodeExtension Extension(string target) => Extensions.FirstOrDefault(e => e.Target == target);

        internal static string ProjectPath(string root) => Path.Combine(root, "Unity-iPhone.xcodeproj", "project.pbxproj");

        /// <summary>The export at <paramref name="root"/>, or null when it has no readable Unity-iPhone project.</summary>
        internal static OpenPushXcodeExport Scan(string root)
        {
            var path = ProjectPath(root);
            if (!File.Exists(path)) return null;
            OpenPushPbxproj project;
            try { project = OpenPushPbxproj.Parse(File.ReadAllText(path)); }
            catch (FormatException) { return null; }

            var export = new OpenPushXcodeExport { Root = root, Project = project };
            var app = project.App;
            if (app != null)
            {
                export.AppBundleId = app.Expanded("PRODUCT_BUNDLE_IDENTIFIER");
                export.AppEntitlements = app.Expanded("CODE_SIGN_ENTITLEMENTS");
                var entitlements = OpenPushPlist.Read(Full(root, export.AppEntitlements));
                export.AppGroups = OpenPushPlist.Strings(entitlements, "com.apple.security.application-groups");
                export.AppKeychainGroups = OpenPushPlist.Strings(entitlements, "keychain-access-groups");
            }
            foreach (var target in project.AppExtensions)
            {
                var infoPlist = target.Expanded("INFOPLIST_FILE");
                var info = OpenPushPlist.Read(Full(root, infoPlist));
                var kind = Kind(OpenPushPlist.String(info, "NSExtension", "NSExtensionPointIdentifier"));
                if (kind == null) continue;
                var extension = new OpenPushXcodeExtension
                {
                    Target = target.Name, Kind = kind, Raw = target, InfoPlist = infoPlist,
                    BundleId = target.Expanded("PRODUCT_BUNDLE_IDENTIFIER"),
                    PrincipalClass = OpenPushPlist.String(info, "NSExtension", "NSExtensionPrincipalClass"),
                    Entitlements = target.Expanded("CODE_SIGN_ENTITLEMENTS"),
                };
                var entitlements = OpenPushPlist.Read(Full(root, extension.Entitlements));
                extension.Groups = OpenPushPlist.Strings(entitlements, "com.apple.security.application-groups");
                extension.KeychainGroups = OpenPushPlist.Strings(entitlements, "keychain-access-groups");
                FindSources(root, extension);
                if (kind == OpenPushSetupFinding.ServiceExtension && !extension.IsOpenPush)
                    extension.Vendor = OpenPushVendorFingerprints.ServiceExtensionVendor(
                        extension.Sources.Select(s => SafeRead(Full(root, s))));
                export.Extensions.Add(extension);
            }
            return export;
        }

        private static string Kind(string point)
        {
            switch (point)
            {
                case OpenPushXcodeExtension.ServicePoint: return OpenPushSetupFinding.ServiceExtension;
                case OpenPushXcodeExtension.ContentPoint: return OpenPushSetupFinding.ContentExtension;
                case OpenPushXcodeExtension.WidgetPoint: return OpenPushSetupFinding.WidgetExtension;
                default: return null;
            }
        }

        /// <summary>
        /// Source files in the folder that holds the extension's Info.plist (where Unity editor scripts and
        /// Xcode both put an extension's files), and the one declaring the principal class. Backups OpenPush
        /// made are skipped.
        /// </summary>
        private static void FindSources(string root, OpenPushXcodeExtension extension)
        {
            var folder = string.IsNullOrEmpty(extension.InfoPlist) ? null : Path.GetDirectoryName(extension.InfoPlist);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(Full(root, folder))) return;
            var files = Directory.GetFiles(Full(root, folder), "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".swift") || f.EndsWith(".m") || f.EndsWith(".mm"))
                .Select(f => Relative(root, f))
                .Where(f => !f.Split('/').Contains(OpenPushExistingExtensionFiles.BackupFolder))
                .OrderBy(f => f, StringComparer.Ordinal).ToList();
            extension.Sources = files;
            var name = extension.ClassName;
            if (name == null) return;
            var swift = new Regex(@"(^|\s)class\s+" + Regex.Escape(name) + @"\b", RegexOptions.Multiline);
            var objc = new Regex(@"@implementation\s+" + Regex.Escape(name) + @"\b");
            foreach (var file in files)
            {
                var text = SafeRead(Full(root, file));
                if (file.EndsWith(".swift") ? swift.IsMatch(text) : objc.IsMatch(text))
                {
                    extension.PrincipalFile = file;
                    extension.Language = file.EndsWith(".swift") ? "swift" : "objc";
                    return;
                }
            }
        }

        /// <summary>The <c>found</c> list for OpenPushSetup.json: host extensions and groups, never OpenPush's own.</summary>
        internal List<OpenPushSetupFinding> Findings()
        {
            var findings = new List<OpenPushSetupFinding>();
            foreach (var extension in Extensions.Where(e => !e.IsOpenPush))
            {
                var finding = new OpenPushSetupFinding(extension.Kind, extension.Target, extension.InfoPlist) { Vendor = extension.Vendor };
                finding.Details["target"] = extension.Target;
                finding.Details["bundle_id"] = extension.BundleId;
                finding.Details["info_plist"] = extension.InfoPlist;
                finding.Details["principal_class"] = extension.PrincipalClass;
                finding.Details["principal_class_file"] = extension.PrincipalFile;
                finding.Details["language"] = extension.Language;
                finding.Details["entitlements"] = extension.Entitlements;
                finding.Details["app_groups"] = extension.Groups.Cast<object>().ToList();
                finding.Details["sources"] = extension.Sources.Cast<object>().ToList();
                finding.Details["deployment_target"] = extension.Raw.Setting("IPHONEOS_DEPLOYMENT_TARGET");
                finding.Details["profile"] = extension.Profile;
                // One profile and style, not every configuration's signing settings: the file is committed, and
                // per-configuration values would churn it on every signing tweak. Reuse needs only these.
                finding.Details["code_sign_style"] = extension.Raw.Setting("CODE_SIGN_STYLE");
                findings.Add(finding);
            }
            var openPushGroup = "group." + AppBundleId + ".openpush";
            var groups = AppGroups.Concat(Extensions.Where(e => !e.IsOpenPush).SelectMany(e => e.Groups)).Distinct().Where(g => g != openPushGroup);
            foreach (var group in groups)
            {
                var finding = new OpenPushSetupFinding(OpenPushSetupFinding.AppGroup, group,
                    AppGroups.Contains(group) ? AppEntitlements : Extensions.First(e => e.Groups.Contains(group)).Entitlements)
                {
                    Vendor = OpenPushVendorFingerprints.AppGroupVendor(group, AppBundleId),
                };
                finding.Details["on_app"] = AppGroups.Contains(group);
                finding.Details["on_extensions"] = Extensions.Where(e => !e.IsOpenPush && e.Groups.Contains(group)).Select(e => (object)e.Target).ToList();
                findings.Add(finding);
            }
            foreach (var group in AppKeychainGroups.Concat(Extensions.Where(e => !e.IsOpenPush).SelectMany(e => e.KeychainGroups)).Distinct())
                findings.Add(new OpenPushSetupFinding(OpenPushSetupFinding.KeychainGroup, group, AppEntitlements));
            return findings;
        }

        internal static string Full(string root, string relative)
            => string.IsNullOrEmpty(relative) ? null : Path.IsPathRooted(relative) ? relative : Path.Combine(root, relative);

        private static string Relative(string root, string full)
        {
            var prefix = Path.GetFullPath(root).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(full);
            return (path.StartsWith(prefix) ? path.Substring(prefix.Length) : path).Replace('\\', '/');
        }

        private static string SafeRead(string path)
        {
            try { return path != null && File.Exists(path) ? File.ReadAllText(path) : ""; }
            catch (IOException) { return ""; }
        }
    }
}
