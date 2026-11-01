using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The vendor-fingerprint surface: every vendor name and string detection matches on lives here.
    /// Only fingerprints of known public releases are listed.
    /// </summary>
    internal static class OpenPushVendorFingerprints
    {
        internal static readonly IOpenPushVendorFingerprint[] All = { new OneSignalFingerprint() };

        /// <summary>The vendor whose template the sources are, or null.</summary>
        internal static string ServiceExtensionVendor(IEnumerable<string> sourceTexts)
        {
            var texts = sourceTexts.ToList();
            return All.FirstOrDefault(f => f.ClaimsServiceExtension(texts))?.Vendor;
        }

        internal static string AppGroupVendor(string group, string appBundleId)
            => All.FirstOrDefault(f => f.ClaimsAppGroup(group, appBundleId))?.Vendor;
    }

    /// <summary>
    /// OneSignal's Unity SDK 5.x: the com.onesignal.unity.ios package and the iOS extension target it
    /// adds to an Xcode export.
    /// </summary>
    internal sealed class OneSignalFingerprint : IOpenPushVendorFingerprint
    {
        public string Vendor => "onesignal";

        internal const string ExtensionTarget = "OneSignalNotificationServiceExtension";
        internal const string ExtensionModule = "OneSignalExtension";
        internal const string ExtensionCall = "OneSignalExtension.didReceiveNotificationExtensionRequest";
        internal const string IosPackage = "com.onesignal.unity.ios";
        internal const string AssetsFolder = "Assets/OneSignal";

        /// <summary>The module import and the call that hands the request to the SDK, together.</summary>
        public bool ClaimsServiceExtension(IEnumerable<string> sourceTexts)
        {
            var texts = sourceTexts.Where(t => t != null).ToList();
            return texts.Any(t => t.Contains("import " + ExtensionModule)) && texts.Any(t => t.Contains(ExtensionCall));
        }

        public bool ClaimsAppGroup(string group, string appBundleId)
            => !string.IsNullOrEmpty(appBundleId) && group == "group." + appBundleId + ".onesignal";

        public IEnumerable<OpenPushSetupFinding> InUnityProject(IOpenPushUnityProjectView project)
        {
            if (!project.HasPackage(IosPackage) && !project.HasAssetFolder(AssetsFolder)) yield break;
            var bundle = project.AppBundleId;
            var source = project.HasPackage(IosPackage) ? "Packages/" + IosPackage : AssetsFolder;
            var extension = new OpenPushSetupFinding(OpenPushSetupFinding.ServiceExtension, ExtensionTarget, source) { Vendor = Vendor, Editable = false };
            extension.Details["target"] = ExtensionTarget;
            extension.Details["bundle_id"] = string.IsNullOrEmpty(bundle) ? null : bundle + "." + ExtensionTarget;
            extension.Details["from_build_step"] = true;
            yield return extension;
            if (string.IsNullOrEmpty(bundle)) yield break;
            var group = new OpenPushSetupFinding(OpenPushSetupFinding.AppGroup, "group." + bundle + ".onesignal", source) { Vendor = Vendor, Editable = false };
            group.Details["on_app"] = true;
            group.Details["on_extensions"] = new List<object> { ExtensionTarget };
            group.Details["from_build_step"] = true;
            yield return group;
        }
    }
}

namespace OpenPushSDK.Editor
{
    /// <summary>Removal plans for the push SDKs OpenPush knows how to take out of a Unity project.</summary>
    internal static class OpenPushVendorRemovals
    {
        internal static readonly IOpenPushVendorRemoval[] All = { new OneSignalRemoval() };
    }

    /// <summary>
    /// What removal looks for in a project that has OneSignal's Unity SDK 5.x installed: the three
    /// com.onesignal.unity packages and their npm scope, Assets/OneSignal, the ProjectSettings
    /// files, the EDM4U dependency files and, unless the project reuses its icons, OneSignalConfig.androidlib.
    /// </summary>
    internal sealed class OneSignalRemoval : IOpenPushVendorRemoval
    {
        public string DisplayName => "OneSignal";
        public string CompatAssembly => "OpenPush.OneSignalCompat";

        internal static readonly string[] PackageNames = { "com.onesignal.unity.core", "com.onesignal.unity.ios", "com.onesignal.unity.android" };
        internal const string Scope = "com.onesignal";
        internal const string ApiNamespace = "OneSignalSDK";
        private static readonly string[] IconLibraries = { "Assets/Plugins/Android/OneSignalConfig.androidlib", "Assets/Plugins/Android/OneSignalConfig.plugin" };

        public OpenPushRemovalPlan Plan(string root, bool reuseIcons)
        {
            var plan = new OpenPushRemovalPlan { DisplayName = DisplayName };
            var manifest = System.IO.Path.Combine(root, "Packages", "manifest.json");
            var manifestText = System.IO.File.Exists(manifest) ? System.IO.File.ReadAllText(manifest) : "";
            foreach (var package in PackageNames)
                if (manifestText.Contains("\"" + package + "\"")) plan.Packages.Add(package);
            if (manifestText.Contains("\"" + Scope + "\"")) plan.Scopes.Add(Scope);

            void Add(string path)
            {
                if ((System.IO.Directory.Exists(System.IO.Path.Combine(root, path)) || System.IO.File.Exists(System.IO.Path.Combine(root, path)))
                    && !plan.Paths.Any(p => p == path || path.StartsWith(p + "/"))) plan.Paths.Add(path);
            }
            Add("Assets/OneSignal");
            Add("ProjectSettings/OneSignalSettings.json");
            Add("ProjectSettings/GoogleDependencyOneSignal.xml");
            var assets = System.IO.Path.Combine(root, "Assets");
            if (System.IO.Directory.Exists(assets))
                foreach (var file in System.IO.Directory.GetFiles(assets, "OneSignal*Dependencies.xml", System.IO.SearchOption.AllDirectories))
                    Add(Relative(root, file));
            foreach (var library in IconLibraries)
            {
                if (!System.IO.Directory.Exists(System.IO.Path.Combine(root, library))) continue;
                if (reuseIcons) plan.Kept.Add(library + " (your notification icons are reused)");
                else Add(library);
            }

            // Game code is only read while the SDK is still installed: it is what removing it would break.
            if (!plan.Empty && System.IO.Directory.Exists(assets))
                foreach (var script in System.IO.Directory.GetFiles(assets, "*.cs", System.IO.SearchOption.AllDirectories))
                {
                    var relative = Relative(root, script);
                    if (plan.Paths.Any(p => relative.StartsWith(p + "/"))) continue;
                    // OpenPush's own files (a vendored copy, or the imported sample) are not game code.
                    if (relative.StartsWith("Assets/OpenPush/") || relative.StartsWith("Assets/Samples/OpenPush/")) continue;
                    if (!System.IO.File.ReadAllText(script).Contains(ApiNamespace)) continue;
                    plan.Scripts.Add(relative);
                    var asmdef = OwningAsmdef(root, System.IO.Path.GetDirectoryName(script));
                    if (asmdef != null && !plan.Asmdefs.Contains(asmdef)) plan.Asmdefs.Add(asmdef);
                }
            plan.Paths.Sort(StringComparer.Ordinal);
            plan.Scripts.Sort(StringComparer.Ordinal);
            return plan;
        }

        /// <summary>
        /// The asmdef (or the .asmref pointing at one) that compiles scripts in <paramref name="folder"/>, or null
        /// (Assembly-CSharp, which sees the compatibility assembly already).
        /// </summary>
        private static string OwningAsmdef(string root, string folder)
        {
            var assets = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, "Assets"));
            for (var dir = System.IO.Path.GetFullPath(folder); dir != null && dir.Length >= assets.Length; dir = System.IO.Path.GetDirectoryName(dir))
            {
                var asmdef = System.IO.Directory.GetFiles(dir, "*.asmdef").FirstOrDefault()
                          ?? System.IO.Directory.GetFiles(dir, "*.asmref").FirstOrDefault();
                if (asmdef != null) return Relative(root, asmdef);
            }
            return null;
        }

        private static string Relative(string root, string full)
            => System.IO.Path.GetRelativePath(System.IO.Path.GetFullPath(root), System.IO.Path.GetFullPath(full)).Replace('\\', '/');
    }
}
