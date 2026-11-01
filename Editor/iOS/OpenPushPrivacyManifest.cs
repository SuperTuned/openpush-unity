#if UNITY_IOS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenPushSDK.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Puts the native SDKs' privacy manifests into the built app. The XCFrameworks are static and
    /// never embedded, so the <c>PrivacyInfo.xcprivacy</c> inside each framework never reaches the app
    /// bundle, and the app's privacy report would leave OpenPush out. Each manifest is copied, unchanged,
    /// from the vendored framework into a resource bundle (<c>OpenPushPrivacy_&lt;module&gt;.bundle</c>) in
    /// the Copy Bundle Resources phase of the target that links that framework: the app for OpenPush,
    /// OpenPushCore and OpenPushLiveActivities, the notification service extension for OpenPushCore and
    /// OpenPushNSE, and the Live Activity widget for OpenPushLiveActivities.
    ///
    /// Append-only and idempotent: it adds its own bundles and nothing else, and a re-export replaces
    /// them in place. A manifest the app or another SDK already has is not touched.
    /// </summary>
    public sealed class OpenPushPrivacyManifest : IPostprocessBuildWithReport
    {
        // After the extension injectors (1-3) and OpenPushIOSBuildSettings (45), so every target exists.
        public int callbackOrder => 46;

        internal const string ExportFolder = OpenPushNativeFrameworks.ExportFolder + "/Privacy";
        internal const string FileName = "PrivacyInfo.xcprivacy";

        internal static readonly string[] ServiceExtensionModules = OpenPushNativeFrameworks.ServiceExtensionModules;

        internal static string BundleName(string module) => "OpenPushPrivacy_" + module + ".bundle";

        internal static string BundlePath(string module) => ExportFolder + "/" + BundleName(module);

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var root = report.summary.outputPath;
            var projectPath = PBXProject.GetPBXProjectPath(root);
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            var settings = Resources.Load<OpenPushSettings>("OpenPushSettings");

            Add(project, root, project.GetUnityMainTargetGuid(), OpenPushNativeFrameworks.UnityFrameworkModules);
            var service = ServiceExtensionTarget(project, root, settings);
            if (service != null) Add(project, root, service, ServiceExtensionModules);
            var widget = project.TargetGuidByName(OpenPushIOSIdentifiers.WidgetTarget);
            if (widget != null) Add(project, root, widget, OpenPushNativeFrameworks.WidgetModules);
            project.WriteToFile(projectPath);
        }

        /// <summary>
        /// The target that links OpenPushNSE: OpenPush's own extension, or the existing one TakeOver or Share
        /// made OpenPush's. Null when the export has neither.
        /// </summary>
        private static string ServiceExtensionTarget(PBXProject project, string root, OpenPushSettings settings)
        {
            var own = project.TargetGuidByName(OpenPushIOSIdentifiers.ServiceExtensionTarget);
            if (own != null) return own;
            if (settings == null || !settings.InjectServiceExtension) return null;
            if (settings.IosExtensionMode != OpenPushIosExtensionMode.TakeOver
                && settings.IosExtensionMode != OpenPushIosExtensionMode.Share) return null;
            var name = string.IsNullOrWhiteSpace(settings.IosExtensionTarget) ? null : settings.IosExtensionTarget.Trim();
            if (name == null)
            {
                var hosts = OpenPushXcodeExport.Scan(root)?.HostServiceExtensions.ToList();
                if (hosts == null || hosts.Count != 1) return null;
                name = hosts[0].Target;
            }
            return project.TargetGuidByName(name);
        }

        /// <summary>Copies each module's manifest into its bundle and adds the bundle to the target's resources once.</summary>
        internal static void Add(PBXProject project, string root, string target, IEnumerable<string> modules)
        {
            if (string.IsNullOrEmpty(target)) return;
            var phase = project.AddResourcesBuildPhase(target);
            foreach (var module in modules)
            {
                var manifest = VendoredManifest(module);
                if (manifest == null) continue;
                WriteBundle(root, module, manifest);
                var path = BundlePath(module);
                var existing = project.FindFileGuidByProjectPath(path);
                if (existing != null) project.RemoveFileFromBuild(target, existing);
                project.AddFileToBuildSection(target, phase, existing ?? project.AddFile(path, path, PBXSourceTree.Source));
            }
        }

        /// <summary>The device slice's manifest, the one the framework ships; null for a module without one.</summary>
        internal static string VendoredManifest(string module)
        {
            var path = Path.Combine(OpenPushNativeFrameworks.SourceFolder(), module + ".xcframework", "ios-arm64",
                                    module + ".framework", FileName);
            return File.Exists(path) ? path : null;
        }

        private static void WriteBundle(string root, string module, string manifest)
        {
            var bundle = Path.Combine(root, BundlePath(module));
            Directory.CreateDirectory(bundle);
            File.Copy(manifest, Path.Combine(bundle, FileName), true);
            File.WriteAllText(Path.Combine(bundle, "Info.plist"), InfoPlist(module));
        }

        /// <summary>A resource bundle's Info.plist: no executable, so only identity and type.</summary>
        internal static string InfoPlist(string module)
            => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
             + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
             + "<plist version=\"1.0\"><dict>\n"
             + "<key>CFBundleIdentifier</key><string>ai.openpush.privacy." + module + "</string>\n"
             + "<key>CFBundleName</key><string>OpenPushPrivacy_" + module + "</string>\n"
             + "<key>CFBundlePackageType</key><string>BNDL</string>\n"
             + "<key>CFBundleInfoDictionaryVersion</key><string>6.0</string>\n"
             + "<key>CFBundleShortVersionString</key><string>" + OpenPushVersion.Current + "</string>\n"
             + "<key>CFBundleVersion</key><string>1</string>\n"
             + "</dict></plist>\n";
    }
}
#endif
