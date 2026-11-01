#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Applies Push Notifications and the remote-notification background mode to the
    /// exported project, so a fresh export builds and registers for push without anyone
    /// opening the Signing &amp; Capabilities tab by hand.
    ///
    /// An App Group is added here only when the host opts in by filling
    /// <see cref="OpenPushSettings.AppGroupIdentifier"/> or by reusing the group already in the
    /// export (<see cref="OpenPushSettings.AppGroupSource"/>); a group that is missing from the
    /// provisioning profile fails the build outright under manual signing, so none is synthesized.
    /// </summary>
    public sealed class OpenPushIOSCapabilities : IPostprocessBuildWithReport
    {
        // After the widget injector (1), which writes the project file itself, and before
        // OpenPushIOSBuildSettings (45); the capability manager reads the file here.
        public int callbackOrder => 2;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var settings = Resources.Load<OpenPushSettings>("OpenPushSettings");
            var root = report.summary.outputPath;
            var projectPath = PBXProject.GetPBXProjectPath(root);
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            var main = project.GetUnityMainTargetGuid();

            // Written before the capability manager runs: it reads the project from disk.
            if (MigrateLegacyEntitlements(project, main, root)) project.WriteToFile(projectPath);
            var entitlements = MainTargetEntitlements(project, main, root);
            var manager = new ProjectCapabilityManager(projectPath, entitlements, null, main);
            manager.AddPushNotifications(EditorUserBuildSettings.development);
            manager.AddBackgroundModes(BackgroundModesOptions.RemoteNotifications);
            // The named group, or with AppGroupSource Reused the one the app and its existing extension
            // share (see OpenPushExportAppGroup). Whitespace-only is the same non-answer as empty.
            // AddAppGroups replaces the array: keep the file's own groups beside it.
            var group = OpenPushExportAppGroup.Configured(settings, root);
            var groups = group == null ? null : OpenPushAppGroupEntitlements.Merged(root, entitlements, group);
            if (groups != null)
                manager.AddAppGroups(groups);
            manager.WriteToFile();
        }

        /// <summary>The main target's default entitlements file, relative to the export root.</summary>
        internal const string DefaultMainEntitlements = "Unity-iPhone/Unity-iPhone.entitlements";

        /// <summary>The separate file earlier OpenPush SDK builds wrote; an append export may still point at it.</summary>
        internal const string LegacyMainEntitlements = "Unity-iPhone/OpenPush.entitlements";

        /// <summary>
        /// Moves an append export from an earlier OpenPush SDK build off <see cref="LegacyMainEntitlements"/> and onto
        /// the target-named file, copying its keys when that file does not exist yet (an existing one
        /// is the host's and is kept; the push keys are re-added to it right after). Otherwise a host
        /// post-processor that adds the target-named path would leave two paths in the setting again.
        /// True when the project changed.
        /// </summary>
        internal static bool MigrateLegacyEntitlements(PBXProject project, string main, string exportRoot)
        {
            var configured = project.GetBuildPropertyForAnyConfig(main, "CODE_SIGN_ENTITLEMENTS")?.Trim() ?? "";
            // Also a value an earlier failed export already doubled ("OpenPush... Unity-iPhone...").
            var doubled = configured.Contains(LegacyMainEntitlements) && configured.IndexOfAny(new[] { ' ', '\t' }) >= 0;
            if (configured != LegacyMainEntitlements && !doubled) return false;
            var legacy = Path.Combine(exportRoot, LegacyMainEntitlements);
            var target = Path.Combine(exportRoot, DefaultMainEntitlements);
            if (File.Exists(legacy) && !File.Exists(target)) File.Copy(legacy, target);
            project.SetBuildProperty(main, "CODE_SIGN_ENTITLEMENTS", DefaultMainEntitlements);
            return true;
        }

        /// <summary>
        /// The entitlements file the main target signs with, relative to the export root and
        /// always with forward slashes (the pbxproj takes it verbatim). Push keys merge into
        /// the file the target already uses, because a target signs with exactly one file:
        /// a second one would either drop the other's keys or, once a host post-processor
        /// adds its own path with AddBuildProperty, leave CODE_SIGN_ENTITLEMENTS holding two
        /// space-separated paths that Xcode reads as one missing file. With nothing usable
        /// configured, the target-named file is used, which is the name Unity and host
        /// build steps use too, so a later AddBuildProperty of it stays one value.
        /// ProjectCapabilityManager reads the file before writing, so other keys survive.
        /// </summary>
        internal static string MainTargetEntitlements(PBXProject project, string main, string exportRoot)
        {
            var configured = project.GetBuildPropertyForAnyConfig(main, "CODE_SIGN_ENTITLEMENTS");
            if (string.IsNullOrWhiteSpace(configured)) return DefaultMainEntitlements;
            var relative = configured.Trim().Replace('\\', '/');
            const string srcRoot = "$(SRCROOT)/";
            if (relative.StartsWith(srcRoot)) relative = relative.Substring(srcRoot.Length);
            // A value with whitespace is already two paths run together; never build on it.
            if (relative.IndexOfAny(new[] { ' ', '\t' }) >= 0) return DefaultMainEntitlements;
            return File.Exists(Path.Combine(exportRoot, relative)) ? relative : DefaultMainEntitlements;
        }
    }
}
#endif
