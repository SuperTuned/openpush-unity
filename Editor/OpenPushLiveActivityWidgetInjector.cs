#if UNITY_IOS
using System.IO;
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
    /// Adds the default Live Activity widget extension to the exported project, unless the host owns a
    /// widget extension. Its bundle id and provisioning profile come from OpenPushSettings when set.
    ///
    /// The settings asset decides: InjectLiveActivityWidget, whatever value an existing asset holds.
    /// A project with no settings asset gets no widget, unless the export already has OpenPush's
    /// widget target from an earlier build (an append export), which is kept up to date as before.
    /// </summary>
    public sealed class OpenPushLiveActivityWidgetInjector : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var settings = Resources.Load<OpenPushSettings>("OpenPushSettings");
            if (settings != null && !settings.InjectLiveActivityWidget) return;

            var root = report.summary.outputPath;
            var projectPath = PBXProject.GetPBXProjectPath(root);
            var projectText = File.ReadAllText(projectPath);
            // A host widget extension; our own target (an append export) is handled below.
            if (Regex.IsMatch(projectText,
                "isa = PBXNativeTarget;(?:(?!\\n\\s*};).)*\\bname = (?!\\\"?OpenPushLiveActivityWidget\\\"?;)[^;\\n]*Widget[^;\\n]*;(?:(?!\\n\\s*};).)*\\bproductType = \\\"com\\.apple\\.product-type\\.app-extension\\\";",
                RegexOptions.Singleline | RegexOptions.IgnoreCase)) return;
            var project = new PBXProject(); project.ReadFromFile(projectPath);
            var existing = project.TargetGuidByName(OpenPushIOSIdentifiers.WidgetTarget);
            if (settings == null && existing == null)
            {
                Debug.Log("OpenPush: no OpenPushSettings asset, so no Live Activity widget was added. "
                        + "Turn it on in Window > OpenPush > Setup (Live Activity widget row).");
                return;
            }
            var main = project.GetUnityMainTargetGuid();
            var appBundleId = project.GetBuildPropertyForAnyConfig(main, "PRODUCT_BUNDLE_IDENTIFIER");
            var configuredId = settings?.LiveActivityWidgetBundleIdentifier;
            var bundleProblem = OpenPushIOSIdentifiers.BundleIdProblem(
                OpenPushIOSIdentifiers.WidgetBundleField, configuredId, appBundleId);
            if (bundleProblem != null) throw new BuildFailedException(bundleProblem);
            var bundleId = OpenPushIOSIdentifiers.Widget(configuredId, appBundleId);
            var profile = settings?.LiveActivityWidgetProvisioningProfile;
            var signing = OpenPushSigningContext.Current();
            var appSignsManually = OpenPushExtensionSigning.AppSignsManually(project, main, signing);

            OpenPushExtensionSigning.Enforce(OpenPushExtensionSigning.MissingProfileProblem(project, existing,
                OpenPushIOSIdentifiers.WidgetTarget, OpenPushIOSIdentifiers.WidgetProfileField, profile, bundleId,
                appSignsManually), signing);
            if (existing != null)
            {
                // An append export: the target is ours from an earlier run. Only an explicit setting moves
                // it to another App ID, and signing values it already has are kept.
                if (!string.IsNullOrWhiteSpace(configuredId))
                    project.SetBuildProperty(existing, "PRODUCT_BUNDLE_IDENTIFIER", bundleId);
                OpenPushExtensionSigning.LogKept(OpenPushExtensionSigning.Apply(project, main, existing,
                    OpenPushIOSIdentifiers.WidgetTarget, profile, appSignsManually, targetIsNew: false));
                project.WriteToFile(projectPath);
                return;
            }

            var plistPath = Path.Combine(root, "Info.plist");
            var plist = new PlistDocument(); plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("NSSupportsLiveActivities", true); plist.WriteToFile(plistPath);

            var output = Path.Combine(root, "OpenPushLiveActivityWidget");
            Directory.CreateDirectory(output);
            var source = OpenPushEditorTemplates.Find("WidgetTemplate");
            if (source == null || !Directory.Exists(source))
                throw new BuildFailedException("OpenPush Live Activity widget templates were not found.");
            var swift = Path.Combine(output, "OpenPushDefaultWidget.swift");
            var info = Path.Combine(output, "Info.plist");
            if (!File.Exists(swift)) File.Copy(Path.Combine(source, "OpenPushDefaultWidget.swift.txt"), swift);
            if (!File.Exists(info))
                File.WriteAllText(info, OpenPushIOSBuildSettings.WithAppVersions(
                    File.ReadAllText(Path.Combine(source, "Widget-Info.plist.txt")), OpenPushExtensionValues.From(project, main, root)));

            var widget = project.AddAppExtension(main, OpenPushIOSIdentifiers.WidgetTarget,
                bundleId, "OpenPushLiveActivityWidget/Info.plist");
            project.AddFileToBuild(widget,
                OpenPushXcodeSources.Add(project, "OpenPushLiveActivityWidget/OpenPushDefaultWidget.swift"));
            // DefaultLiveActivityAttributes comes from the native module; push-to-start matches on
            // that type name, so the widget must use the module's type, never a copy.
            OpenPushNativeFrameworks.Link(project, widget,
                OpenPushNativeFrameworks.CopyInto(root, OpenPushNativeFrameworks.WidgetModules));
            project.AddFrameworkToProject(widget, "WidgetKit.framework", false);
            project.AddFrameworkToProject(widget, "SwiftUI.framework", false);
            project.SetBuildProperty(widget, "SWIFT_VERSION", "5.0");
            OpenPushIOSBuildSettings.RaiseDeploymentTarget(project, widget, "16.1");
            project.SetBuildProperty(widget, "APPLICATION_EXTENSION_API_ONLY", "YES");
            project.SetBuildProperty(widget, "SKIP_INSTALL", "YES");
            project.SetBuildProperty(widget, "GENERATE_INFOPLIST_FILE", "NO");
            OpenPushExtensionSigning.Apply(project, main, widget, OpenPushIOSIdentifiers.WidgetTarget,
                profile, appSignsManually, targetIsNew: true);
            project.WriteToFile(projectPath);
        }
    }
}
#endif
