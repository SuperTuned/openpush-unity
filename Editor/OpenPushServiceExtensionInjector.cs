#if UNITY_IOS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Adds a UNNotificationServiceExtension to the exported project. It is the
    /// only OpenPush code iOS runs while the app is not, so without it a push
    /// that is never tapped reports neither receipt and a campaign image never renders.
    ///
    /// The extension is the native OpenPushNSE handler, linked against the vendored
    /// OpenPushCore and OpenPushNSE frameworks. It reads the app's state from the
    /// native openpush.shared.* mirror that initialize(appGroup:) writes into the
    /// App Group named by the OpenPushAppGroup key in its Info.plist.
    ///
    /// Enabled by default on newly created settings assets. Injection adds an App
    /// Group capability, so the group must exist in the provisioning profile under
    /// manual signing. Hosts that already own a service extension turn it off.
    /// The extension's bundle id and provisioning profile come from
    /// OpenPushSettings when set (see <see cref="OpenPushExtensionSigning"/>).
    ///
    /// iOS runs exactly one service extension per app. When the export already
    /// carries one (found by its NSExtensionPointIdentifier, whoever made it),
    /// <see cref="OpenPushSettings.IosExtensionMode"/> decides: TakeOver makes a
    /// known vendor's extension OpenPush's, Share adds OpenPush's lines to the
    /// host's own code, Off leaves it alone. An export that does not match the
    /// stored answer asks once in an interactive build and fails a batch build
    /// (<see cref="OpenPushExtensionModeDecision"/>).
    /// </summary>
    public sealed class OpenPushServiceExtensionInjector : IPostprocessBuildWithReport
    {
        private const string TargetName = "OpenPushNotificationService";
        private const string GroupPlaceholder = "__OPENPUSH_APP_GROUP__";

        // After the widget injector (1) and the capability manager (2), and before
        // OpenPushIOSBuildSettings (45), which re-syncs this target's versions with
        // the app's. This one both writes the project file and re-opens it through
        // ProjectCapabilityManager.
        public int callbackOrder => 3;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var settings = Resources.Load<OpenPushSettings>("OpenPushSettings");
            // No asset means there is no project-level decision to inject.
            if (settings == null || !settings.InjectServiceExtension) return;
            Run(settings, report.summary.outputPath);
        }

        private static void Run(OpenPushSettings settings, string root)
        {
            var projectPath = PBXProject.GetPBXProjectPath(root);
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            // Our own target is looked for BEFORE the host-extension check
            // below, because that check cannot tell the two apart from the
            // outside: an append export into a folder we already injected into
            // reads exactly like a project that owns a service extension, and
            // the injector would decline to touch its own work — leaving a
            // stale App Group in the plist with no way to ever correct it.
            var existing = project.TargetGuidByName(TargetName);

            var main = project.GetUnityMainTargetGuid();
            var bundleId = project.GetBuildPropertyForAnyConfig(main, "PRODUCT_BUNDLE_IDENTIFIER");
            if (string.IsNullOrEmpty(bundleId))
                throw new BuildFailedException(
                    "OpenPush could not read PRODUCT_BUNDLE_IDENTIFIER from the exported "
                  + "project, and both the notification service extension's bundle id and "
                  + "its App Group are derived from it.");

            // The extension's identity and signing: a bundle id and profile the host already
            // has an App ID and profile for, so a manual-signing export signs as it comes out of Unity.
            var bundleProblem = OpenPushIOSIdentifiers.BundleIdProblem(
                OpenPushIOSIdentifiers.ServiceExtensionBundleField, settings.ServiceExtensionBundleIdentifier, bundleId);
            if (bundleProblem != null) throw new BuildFailedException(bundleProblem);
            var extensionBundleId = OpenPushIOSIdentifiers.ServiceExtension(settings.ServiceExtensionBundleIdentifier, bundleId);
            var profile = settings.ServiceExtensionProvisioningProfile;
            var signing = OpenPushSigningContext.Current();
            var appSignsManually = OpenPushExtensionSigning.AppSignsManually(project, main, signing);

            var source = OpenPushEditorTemplates.Find("ServiceTemplate");
            if (source == null || !Directory.Exists(source))
                throw new BuildFailedException(
                    "OpenPush notification service extension templates were not found.");

            // What the export already has, read from its files: the real export always wins over what
            // the settings remember.
            var export = OpenPushXcodeExport.Scan(root);
            var hosts = export?.HostServiceExtensions.ToList() ?? new List<OpenPushXcodeExtension>();
            var decision = OpenPushExtensionModeDecision.Decide(settings.InjectServiceExtension, settings.IosExtensionMode,
                                                                settings.IosExtensionTarget, hosts, existing != null);
            if (decision.Action == OpenPushExtensionAction.Ask)
                decision = OpenPushExtensionPrompt.Resolve(decision, settings, hosts, existing != null,
                                                           export?.AppGroups ?? new List<string>());

            // Read after the decision: answering the build dialog can also choose to reuse the group.
            // The host's own group wins when it named one, because that is the
            // group the capabilities step already put on the main target and the one their
            // provisioning profile carries. Otherwise the package picks a name
            // that cannot collide with anything else the app owns.
            var configured = OpenPushExportAppGroup.Configured(settings, root);
            var hostNamedGroup = configured != null;
            var group = hostNamedGroup ? configured : "group." + bundleId + ".openpush";
            switch (decision.Action)
            {
                case OpenPushExtensionAction.Conflict:
                    throw new BuildFailedException("OpenPush: " + decision.Message);
                case OpenPushExtensionAction.Off:
                    Debug.Log("OpenPush: service extension Off" + (hosts.Count > 0 ? " (" + hosts[0].Target + " left as it is)" : "")
                            + ". OpenPush pushes show no images or action buttons and report no delivery receipts unless "
                            + "your extension calls OpenPush's handler.");
                    return;
                case OpenPushExtensionAction.TakeOver:
                case OpenPushExtensionAction.Share:
                    (group, hostNamedGroup) = ExistingExtensionGroup(decision.Target, export, group, hostNamedGroup);
                    OpenPushExistingExtensionStep.Apply(new OpenPushExistingExtensionStep.Input
                    {
                        Project = project, ProjectPath = projectPath, Root = root, Main = main, Extension = decision.Target,
                        TakeOver = decision.Action == OpenPushExtensionAction.TakeOver, Group = group, HostNamedGroup = hostNamedGroup,
                        Profile = profile, AppSignsManually = appSignsManually, Signing = signing,
                    });
                    return;
            }

            var output = Path.Combine(root, TargetName);
            var swift = Path.Combine(output, TargetName + ".swift");
            var info = Path.Combine(output, "Info.plist");

            // An append export into a folder that already has the target. The
            // target itself needs nothing, but the App Group may have changed
            // since, and so may the app's version — so every value this injector
            // derives from the project is refreshed rather than left stale until
            // someone wipes the export folder.
            if (existing != null)
            {
                OpenPushExtensionSigning.Enforce(OpenPushExtensionSigning.MissingProfileProblem(project, existing,
                    TargetName, OpenPushIOSIdentifiers.ServiceExtensionProfileField, profile, extensionBundleId,
                    appSignsManually), signing);
                if (Directory.Exists(output)) WriteInfoPlist(source, info, group, OpenPushExtensionValues.From(project, main, root));
                // Only an explicit setting moves an existing target to another App ID.
                if (!string.IsNullOrWhiteSpace(settings.ServiceExtensionBundleIdentifier))
                    project.SetBuildProperty(existing, "PRODUCT_BUNDLE_IDENTIFIER", extensionBundleId);
                OpenPushExtensionSigning.LogKept(OpenPushExtensionSigning.Apply(
                    project, main, existing, TargetName, profile, appSignsManually, targetIsNew: false));
                // Idempotent: refreshes the vendored frameworks and links each one once.
                OpenPushNativeFrameworks.Link(project, existing,
                    OpenPushNativeFrameworks.CopyInto(root, OpenPushNativeFrameworks.ServiceExtensionModules));
                OpenPushIOSBuildSettings.RaiseDeploymentTarget(project, existing, "16.0");
                project.WriteToFile(projectPath);
                ApplyAppGroups(projectPath, root, main, existing, OwnEntitlements, group, hostNamedGroup);
                return;
            }

            // Only now is it worth asking whether the HOST owns one: everything
            // above this line is about a target we put there ourselves.
            // The scan found none. The project-text checks below stay as a backstop for an extension whose
            // Info.plist the scan could not read: a project that declares the extension point in build
            // settings, or a native app-extension target named for what it does (our own name excluded).
            var projectText = File.ReadAllText(projectPath);
            if (projectText.Contains("com.apple.usernotifications.service") || Regex.IsMatch(projectText,
                "isa = PBXNativeTarget;(?:(?!\\n\\s*};).)*\\bname = (?!\\\"?OpenPushNotificationService\\\"?;)[^;\\n]*NotificationService[^;\\n]*;(?:(?!\\n\\s*};).)*\\bproductType = \\\"com\\.apple\\.product-type\\.app-extension\\\";",
                RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                Debug.LogWarning("OpenPush: this export seems to have a notification service extension whose Info.plist "
                               + "could not be read, so OpenPush added none.");
                return;
            }

            OpenPushExtensionSigning.Enforce(OpenPushExtensionSigning.MissingProfileProblem(project, null,
                TargetName, OpenPushIOSIdentifiers.ServiceExtensionProfileField, profile, extensionBundleId,
                appSignsManually), signing);

            Directory.CreateDirectory(output);
            // The Swift is copied only when it is absent: a host that edited the
            // extension in the exported project keeps its edits, the same
            // bargain the widget template makes. The plist is generated rather
            // than edited — it carries the substituted App Group — so it is
            // rewritten on every run that reaches this file.
            if (!File.Exists(swift))
                File.Copy(Path.Combine(source, TargetName + ".swift.txt"), swift);
            WriteInfoPlist(source, info, group, OpenPushExtensionValues.From(project, main, root));

            var extension = project.AddAppExtension(main, TargetName,
                extensionBundleId, TargetName + "/Info.plist");
            project.AddFileToBuild(extension,
                OpenPushXcodeSources.Add(project, TargetName + "/" + TargetName + ".swift"));
            project.AddFrameworkToProject(extension, "UserNotifications.framework", false);
            // The template is the native handler: OpenPushNSE and the OpenPushCore it builds on.
            OpenPushNativeFrameworks.Link(project, extension,
                OpenPushNativeFrameworks.CopyInto(root, OpenPushNativeFrameworks.ServiceExtensionModules));
            project.SetBuildProperty(extension, "SWIFT_VERSION", "5.0");
            OpenPushIOSBuildSettings.RaiseDeploymentTarget(project, extension, "16.0");
            project.SetBuildProperty(extension, "APPLICATION_EXTENSION_API_ONLY", "YES");
            project.SetBuildProperty(extension, "SKIP_INSTALL", "YES");
            project.SetBuildProperty(extension, "GENERATE_INFOPLIST_FILE", "NO");
            project.SetBuildProperty(extension, "TARGETED_DEVICE_FAMILY", "1,2");
            OpenPushExtensionSigning.Apply(project, main, extension, TargetName, profile, appSignsManually, targetIsNew: true);
            // Written before ApplyAppGroups: its capability managers each read
            // this file from disk, and the extension target has to exist in it.
            project.WriteToFile(projectPath);
            ApplyAppGroups(projectPath, root, main, extension, OwnEntitlements, group, hostNamedGroup);
        }

        private const string OwnEntitlements = TargetName + "/" + TargetName + ".entitlements";

        /// <summary>
        /// The group for an extension the export already has. A new group on an existing extension needs its
        /// App ID and provisioning profile changed, so when the App Group question was never answered and no
        /// group is named, the extension's own group is used: the one it shares with the app, or its only one
        /// (then added to the app). Otherwise the chosen group is added, with a warning when the extension did
        /// not carry it.
        /// </summary>
        private static (string Group, bool HostNamed) ExistingExtensionGroup(OpenPushXcodeExtension extension, OpenPushXcodeExport export,
                                                                           string group, bool hostNamed)
        {
            if (!hostNamed && !OpenPushPlumbingAnswers.Answered(OpenPushPlumbingAnswers.AppGroupChoice) && extension.Groups.Count > 0)
            {
                var shared = OpenPushAppGroupReuse.Resolve(export.AppGroups, new[] { (IReadOnlyList<string>)extension.Groups }, out _);
                if (shared != null) return (shared, true);
                if (extension.Groups.Count == 1) return (extension.Groups[0], false);
            }
            if (!extension.Groups.Contains(group))
                Debug.LogWarning("OpenPush: " + extension.Target + " gets the App Group " + group + ", which its entitlements did not "
                               + "carry: its App ID and provisioning profile need that group too (manual signing fails without it). "
                               + "Reuse the extension's own group in Window > OpenPush > Setup to avoid that.");
            return (group, hostNamed);
        }

        /// <summary>
        /// Both ends of the App Group have to hold the entitlement, or the
        /// shared defaults are two separate, empty containers.
        ///
        /// The main target's file is the one OpenPushIOSCapabilities (order 2)
        /// wrote, resolved the same way so both passes share one file, and the
        /// manager reads it back before writing, so push notifications and
        /// background modes survive. That pass adds a group
        /// only when the host filled in AppGroupIdentifier — so when it did not,
        /// the group added here is new to the main target and this is where it
        /// lands. When it did, the group is already there and the array would
        /// only be rewritten with the same value, so the file is left alone.
        /// </summary>
        internal static void ApplyAppGroups(string projectPath, string root, string main, string extension,
                                            string extensionEntitlements, string group, bool hostNamedGroup)
        {
            // AddAppGroups replaces the whole array, so each file gets its own groups plus OpenPush's
            // (OpenPushAppGroupEntitlements), and a file that already has the group is not rewritten.
            if (!hostNamedGroup)
            {
                var written = new PBXProject(); written.ReadFromFile(projectPath);
                var appEntitlements = OpenPushIOSCapabilities.MainTargetEntitlements(written, main, root);
                var appGroups = OpenPushAppGroupEntitlements.Merged(root, appEntitlements, group);
                if (appGroups != null)
                {
                    var app = new ProjectCapabilityManager(projectPath, appEntitlements, null, main);
                    app.AddAppGroups(appGroups);
                    app.WriteToFile();
                }
            }

            var extGroups = OpenPushAppGroupEntitlements.Merged(root, extensionEntitlements, group);
            if (extGroups == null) return;
            var ext = new ProjectCapabilityManager(projectPath, extensionEntitlements, null, extension);
            ext.AddAppGroups(extGroups);
            ext.WriteToFile();
        }

        private static void WriteInfoPlist(string source, string info, string group, OpenPushExtensionValues versions)
        {
            var template = File.ReadAllText(Path.Combine(source, "Service-Info.plist.txt")).Replace(GroupPlaceholder, group);
            File.WriteAllText(info, OpenPushIOSBuildSettings.WithAppVersions(template, versions));
        }
    }
}
#endif
