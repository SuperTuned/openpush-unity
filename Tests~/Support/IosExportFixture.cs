using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace OpenPushSDK.Tests
{
    /// <summary>
    /// An exported Xcode project for the iOS post-build steps: a temp export root, the in-memory
    /// PBXProject at its pbxproj path (main target "main"), a pbxproj text file for the steps that read
    /// it, the package's templates and fake vendored frameworks. Resets every stand-in it touches.
    /// </summary>
    internal sealed class IosExportFixture : System.IDisposable
    {
        public readonly string Root;
        public readonly OpenPushSettings Settings = new OpenPushSettings { InjectServiceExtension = true, InjectLiveActivityWidget = false };
        private readonly string _native;

        /// <summary>The previous push SDK whose export the vendor fixtures reproduce (migration detection).</summary>
        internal const string Vendor = "OneSignal";
        internal const string VendorLower = "onesignal";

        public IosExportFixture(string appBundleId, string fixture = null)
        {
            Root = Path.Combine(Path.GetTempPath(), "op-ios-export-" + Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(Root, "Unity-iPhone.xcodeproj"));
            Directory.CreateDirectory(Path.Combine(Root, "Unity-iPhone"));
            if (fixture != null) CopyFixture(Path.Combine(PackageRoot(), "Tests~", "Fixtures", "ios", fixture), Root);
            else File.WriteAllText(ProjectPath, "// stand-in pbxproj\n");
            _native = Path.Combine(Path.GetTempPath(), "op-ios-native-" + Path.GetRandomFileName());
            foreach (var m in new[] { "OpenPushCore", "OpenPushNSE", "OpenPush", "OpenPushLiveActivities" })
                Directory.CreateDirectory(Path.Combine(_native, m + ".xcframework"));
            OpenPushNativeFrameworks.SourceOverride = _native;
            OpenPushEditorTemplates.EditorFolderOverride = Path.Combine(PackageRoot(), "Editor");
            UnityEngine.Resources.StandIn = Settings;
            UnityEngine.Application.isBatchMode = false;
            UnityEngine.Debug.Logged.Clear();
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.iOSManualProvisioningProfileID = "";
            ProjectCapabilityManager.Reset();
            PBXProject.Recorded.Clear();
            PBXProject.Store.Remove(ProjectPath);
            OpenPushPlumbingAnswers.SetupFilePath = Path.Combine(Root, "ProjectSettings", "OpenPushSetup.json");
            EditorUtility.DialogAnswer = -1;
            EditorUtility.Dialogs.Clear();
            if (fixture != null) LoadTargets();
            else
            {
                Project.State.Main = "main";
                Project.SetBuildProperty("main", "PRODUCT_BUNDLE_IDENTIFIER", appBundleId);
            }
            PBXProject.Recorded.Clear();
        }

        /// <summary>Copies a fixture export (everything but its XcodeGen spec).</summary>
        private static void CopyFixture(string source, string target)
        {
            foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, dir)));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith("project.yml")) continue;
                File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)));
            }
        }

        /// <summary>Mirrors the fixture's pbxproj into the in-memory PBXProject the post-build steps write to.</summary>
        private void LoadTargets()
        {
            var parsed = OpenPushPbxproj.Parse(File.ReadAllText(ProjectPath));
            var project = Project;
            foreach (var target in parsed.Targets)
            {
                project.Targets[target.Name] = target.Guid;
                foreach (var config in target.Configurations)
                    foreach (var setting in config.Settings)
                        project.State.Properties[project.BuildConfigByName(target.Guid, config.Name) + ":" + setting.Key] = setting.Value;
            }
            project.State.Main = parsed.App.Guid;
            project.State.Configs.Clear();
            project.State.Configs.AddRange(parsed.App.Configurations.Select(c => c.Name));
        }

        internal string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

        public string ProjectPath => PBXProject.GetPBXProjectPath(Root);

        /// <summary>The project as the post-build steps see it (shared with them).</summary>
        public PBXProject Project
        {
            get { var p = new PBXProject(); p.ReadFromFile(ProjectPath); return p; }
        }

        public static string PackageRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "package.json"))) dir = dir.Parent;
            Assert.IsNotNull(dir, "package root not found above the test directory");
            return dir.FullName;
        }

        public BuildReport Report()
        {
            var report = new BuildReport();
            report.summary.platform = BuildTarget.iOS;
            report.summary.outputPath = Root;
            return report;
        }

        /// <summary>Runs the extension and capability steps and the recorder in callback order (1, 2, 3, 1000).</summary>
        public void Export()
        {
            var report = Report();
            foreach (var step in new UnityEditor.Build.IPostprocessBuildWithReport[]
                     { new OpenPushLiveActivityWidgetInjector(), new OpenPushIOSCapabilities(), new OpenPushServiceExtensionInjector(),
                       new OpenPushSetupRecorder() }
                     .OrderBy(s => s.callbackOrder))
                step.OnPostprocessBuild(report);
        }

        /// <summary>A build setting of <paramref name="target"/> in one configuration (null when unset).</summary>
        public string Setting(string target, string config, string key)
        {
            var project = Project;
            return project.GetBuildPropertyForConfig(project.BuildConfigByName(target, config), key);
        }

        /// <summary>The App Groups the entitlements file holds now (empty when there is no file).</summary>
        public string[] Groups(string entitlements)
            => OpenPushPlist.Strings(OpenPushPlist.Read(Path.Combine(Root, entitlements)), OpenPushAppGroupEntitlements.Key).ToArray();

        public void Dispose()
        {
            OpenPushNativeFrameworks.SourceOverride = null;
            OpenPushEditorTemplates.EditorFolderOverride = null;
            UnityEngine.Resources.StandIn = null;
            EditorUtility.DialogAnswer = -1;
            OpenPushPlumbingAnswers.SetupFilePath = OpenPushSetupFile.DefaultPath;
            UnityEngine.Application.isBatchMode = false;
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.iOSManualProvisioningProfileID = "";
            PBXProject.Store.Remove(ProjectPath);
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
            if (Directory.Exists(_native)) Directory.Delete(_native, true);
        }
    }
}
