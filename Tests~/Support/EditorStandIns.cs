namespace OpenPushSDK
{
    internal sealed class OpenPushSettings
    {
        public bool InjectLiveActivityWidget { get; set; }
        public bool InjectServiceExtension { get; set; }
        public string AppGroupIdentifier { get; set; }
        public string ServiceExtensionBundleIdentifier { get; set; }
        public string ServiceExtensionProvisioningProfile { get; set; }
        public string LiveActivityWidgetBundleIdentifier { get; set; }
        public string LiveActivityWidgetProvisioningProfile { get; set; }
        public OpenPushSDK.Core.OpenPushIosExtensionMode IosExtensionMode { get; set; }
        public string IosExtensionTarget { get; set; }
        public OpenPushSDK.Core.OpenPushPlumbingSource AppGroupSource { get; set; }
        public OpenPushSDK.Core.OpenPushPlumbingSource IconsSource { get; set; }
        public string AndroidNotificationIconName { get; set; }
        public OpenPushSDK.Core.OpenPushAndroidPushDelivery AndroidPushDelivery { get; set; }
        public bool OpenLaunchUrls { get; set; } = true;
    }
}

namespace OpenPushSDK.Editor
{
    /// <summary>The settings asset as the checklist loads it: none in these tests (Automatic) unless a test sets one.</summary>
    internal static class OpenPushSetupAssets
    {
        internal static OpenPushSettings StandIn;
        internal static int Evaluations;
        internal static OpenPushSettings Loadable() => StandIn;
        internal static void BeginEvaluation() => Evaluations++;
        internal static void EndEvaluation() { }
    }

    /// <summary>EDM4U's reflective resolver, recorded.</summary>
    internal static class OpenPushEdm4u
    {
        internal static bool StandInLoaded;
        internal static int StandInResolves;
        internal static bool Loaded => StandInLoaded;
        internal static bool ForceResolve() { StandInResolves++; return true; }
        internal static OpenPushResolveRecord Last() => null;
        internal static OpenPushResolveRecord Current() => new OpenPushResolveRecord();
    }
}

namespace UnityEngine
{
    internal static class Resources
    {
        /// <summary>What Resources.Load returns in a test: null unless a test sets it.</summary>
        public static object StandIn;
        public static T Load<T>(string path) where T : class => StandIn as T;
    }

    internal static class Debug
    {
        /// <summary>Every message, prefixed with its level ("log: ", "warning: ", "error: ").</summary>
        public static readonly System.Collections.Generic.List<string> Logged = new System.Collections.Generic.List<string>();
        public static void Log(object message) => Logged.Add("log: " + message);
        public static void LogWarning(object message) => Logged.Add("warning: " + message);
        public static void LogError(object message) => Logged.Add("error: " + message);
    }

    internal static class Application
    {
        public static bool isBatchMode { get; set; }
    }
}

namespace UnityEditor
{
    internal static class PlayerSettings
    {
        public static string bundleVersion => "1.0";
        public static string productName => "Stand-in Product";

        internal static class iOS
        {
            public static string buildNumber => "1";
            public static string appleDeveloperTeamID { get; set; } = "";
            public static bool appleEnableAutomaticSigning { get; set; } = true;
            public static string iOSManualProvisioningProfileID { get; set; } = "";
        }
    }

    public enum BuildTarget
    {
        iOS,
        Android
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    internal sealed class InitializeOnLoadAttribute : System.Attribute { }

    internal static class EditorApplication
    {
        public static System.Action delayCall;
    }

    internal static class AssetDatabase
    {
        /// <summary>Every FindAssets filter asked for, in order; FindAssets finds nothing.</summary>
        public static readonly System.Collections.Generic.List<string> Searches = new System.Collections.Generic.List<string>();
        public static void ImportAsset(string path) { }
        public static void SaveAssets() { }
        public static string[] FindAssets(string filter) { Searches.Add(filter); return new string[0]; }
        public static string GUIDToAssetPath(string guid) => guid;
    }

    internal static class TypeCache
    {
        public static System.Type[] GetTypesDerivedFrom<T>() => new System.Type[0];
    }

    internal static class EditorUtility
    {
        /// <summary>The button a test answers dialogs with (0 ok, 1 cancel, 2 alt); -1 fails the test.</summary>
        public static int DialogAnswer = -1;
        public static readonly System.Collections.Generic.List<string> Dialogs = new System.Collections.Generic.List<string>();
        public static int DisplayDialogComplex(string title, string message, string ok, string cancel, string alt)
        {
            Dialogs.Add(message);
            if (DialogAnswer < 0) throw new System.InvalidOperationException("unexpected dialog: " + message);
            return DialogAnswer;
        }
        public static void SetDirty(object target) { }
    }

    internal static class EditorUserBuildSettings
    {
        public static bool development => false;
    }
}

namespace UnityEditor.Build
{
    internal sealed class BuildFailedException : System.Exception
    {
        public BuildFailedException(string message) : base(message) { }
    }

    internal interface IPreprocessBuildWithReport
    {
        int callbackOrder { get; }
        void OnPreprocessBuild(Reporting.BuildReport report);
    }

    internal interface IPostprocessBuildWithReport
    {
        int callbackOrder { get; }
        void OnPostprocessBuild(Reporting.BuildReport report);
    }
}

namespace UnityEditor.Build.Reporting
{
    public sealed class BuildReport
    {
        public BuildSummary summary { get; } = new BuildSummary();
    }

    public sealed class BuildSummary
    {
        public UnityEditor.BuildTarget platform { get; set; }
        public string outputPath { get; set; }
    }
}

namespace UnityEditor.iOS.Xcode
{
    /// <summary>
    /// Unity's PBXProject, in memory. Projects read from the same path share one model, the way the real
    /// class shares the file on disk, so a post-build step and a test see each other's writes. A project
    /// that never reads a file has a private model. Build settings are kept per configuration.
    /// </summary>
    internal sealed class PBXProject
    {
        internal sealed class Model
        {
            public string Main = string.Empty, Framework = string.Empty;
            public readonly System.Collections.Generic.List<string> Configs =
                new System.Collections.Generic.List<string> { "Debug", "Release", "ReleaseForProfiling", "ReleaseForRunning" };
            public readonly System.Collections.Generic.Dictionary<string, string> Targets = new System.Collections.Generic.Dictionary<string, string>();
            public readonly System.Collections.Generic.Dictionary<string, string> Properties = new System.Collections.Generic.Dictionary<string, string>();
            public readonly System.Collections.Generic.Dictionary<string, string> Files = new System.Collections.Generic.Dictionary<string, string>();
            public readonly System.Collections.Generic.List<string> BuildSectionFiles = new System.Collections.Generic.List<string>();
            public readonly System.Collections.Generic.List<string> AddedFiles = new System.Collections.Generic.List<string>();
            /// <summary>Each build-phase entry as "phase:file", so a test can tell Resources from Frameworks.</summary>
            public readonly System.Collections.Generic.List<string> PhaseFiles = new System.Collections.Generic.List<string>();
            /// <summary>Extension targets added by name, in order.</summary>
            public readonly System.Collections.Generic.List<string> Extensions = new System.Collections.Generic.List<string>();
        }

        /// <summary>Every model a test or step read from a path.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, Model> Store = new System.Collections.Generic.Dictionary<string, Model>();
        /// <summary>Every SetBuildProperty ("target:NAME=value"), SetBuildPropertyForConfig ("target|config:NAME=value") and AddFrameworkToProject ("target:link NAME weak=W").</summary>
        public static readonly System.Collections.Generic.List<string> Recorded = new System.Collections.Generic.List<string>();

        private Model _m = new Model();
        internal Model State => _m;

        public static string GetPBXProjectPath(string root) => System.IO.Path.Combine(root ?? string.Empty, "Unity-iPhone.xcodeproj", "project.pbxproj");
        public void ReadFromFile(string path)
        {
            if (!Store.TryGetValue(path, out var model)) Store[path] = model = new Model();
            _m = model;
        }
        public void WriteToFile(string path) => Store[path] = _m;

        /// <summary>Targets a test declares present (name to guid); every other name reads as absent.</summary>
        public System.Collections.Generic.Dictionary<string, string> Targets => _m.Targets;
        public System.Collections.Generic.List<string> BuildSectionFiles => _m.BuildSectionFiles;
        /// <summary>Every AddFile call as "path|projectPath|tree".</summary>
        public System.Collections.Generic.List<string> AddedFiles => _m.AddedFiles;
        public System.Collections.Generic.List<string> PhaseFiles => _m.PhaseFiles;

        public string TargetGuidByName(string name) => _m.Targets.TryGetValue(name, out var guid) ? guid : null;
        public string GetUnityFrameworkTargetGuid() => _m.Framework;
        public string GetUnityMainTargetGuid() => _m.Main;

        public System.Collections.Generic.IEnumerable<string> BuildConfigNames() => _m.Configs;
        public string BuildConfigByName(string target, string name) => target + "|" + name;
        public string GetBuildPropertyForConfig(string config, string property)
            => _m.Properties.TryGetValue(config + ":" + property, out var value) ? value : null;
        public void SetBuildPropertyForConfig(string config, string property, string value)
        {
            _m.Properties[config + ":" + property] = value;
            Recorded.Add(config + ":" + property + "=" + value);
        }

        public string GetBuildPropertyForAnyConfig(string target, string property)
        {
            foreach (var config in _m.Configs)
                if (GetBuildPropertyForConfig(BuildConfigByName(target, config), property) is string value) return value;
            return string.Empty;
        }
        public void SetBuildProperty(string target, string property, string value)
        {
            foreach (var config in _m.Configs) _m.Properties[BuildConfigByName(target, config) + ":" + property] = value;
            Recorded.Add(target + ":" + property + "=" + value);
        }

        public string AddFile(string path, string projectPath) => AddFile(path, projectPath, PBXSourceTree.Source);
        public string AddFile(string path, string projectPath, PBXSourceTree tree)
        {
            _m.AddedFiles.Add(path + "|" + projectPath + "|" + tree);
            var guid = "file-" + projectPath;
            _m.Files[projectPath] = guid;
            return guid;
        }
        public string FindFileGuidByProjectPath(string projectPath) => _m.Files.TryGetValue(projectPath, out var guid) ? guid : null;
        public string GetFrameworksBuildPhaseByTarget(string target) => "frameworks-" + target;
        public void AddFileToBuildSection(string target, string phase, string file)
        {
            _m.BuildSectionFiles.Add(target + ":" + file);
            _m.PhaseFiles.Add(phase + ":" + file);
        }
        public string AddResourcesBuildPhase(string target) => "resources-" + target;
        public void RemoveFileFromBuild(string target, string file)
        {
            _m.BuildSectionFiles.RemoveAll(f => f == target + ":" + file);
            _m.PhaseFiles.RemoveAll(f => f.EndsWith("-" + target + ":" + file));
        }
        public void AddFileToBuild(string target, string file) { }
        public void AddFrameworkToProject(string target, string framework, bool weak)
            => Recorded.Add(target + ":link " + framework + " weak=" + weak);

        /// <summary>What Unity's AddAppExtension writes: the target, its bundle id and Info.plist per configuration.</summary>
        internal string StandInAddAppExtension(string name, string bundleId, string infoPlistPath)
        {
            var guid = "guid-" + name;
            _m.Targets[name] = guid;
            _m.Extensions.Add(name);
            foreach (var config in _m.Configs)
            {
                SetBuildPropertyForConfig(BuildConfigByName(guid, config), "PRODUCT_BUNDLE_IDENTIFIER", bundleId);
                SetBuildPropertyForConfig(BuildConfigByName(guid, config), "INFOPLIST_FILE", infoPlistPath);
            }
            return guid;
        }
    }

    internal enum PBXSourceTree { Absolute, Group, Source, Build, Developer, Sdk }

    /// <summary>
    /// Unity's ProjectCapabilityManager as it behaves: WriteToFile reads the entitlements file, sets the keys
    /// this manager touched and writes it back. AddAppGroups REPLACES the application-groups array with the
    /// groups it is given (verified against UnityEditor.iOS.Extensions.Xcode 2022.3.14f1 and 6000.3.22f1).
    /// </summary>
    internal sealed class ProjectCapabilityManager
    {
        /// <summary>Every AddAppGroups call, as "entitlements path: g1,g2".</summary>
        public static readonly System.Collections.Generic.List<string> GroupWrites = new System.Collections.Generic.List<string>();

        public static void Reset() => GroupWrites.Clear();

        private readonly string _project, _entitlements, _target;
        private readonly System.Collections.Generic.Dictionary<string, System.Xml.Linq.XElement> _set =
            new System.Collections.Generic.Dictionary<string, System.Xml.Linq.XElement>();

        public ProjectCapabilityManager(string pbxProjectPath, string entitlementFilePath,
                                        string targetName = null, string targetGuid = null)
        {
            _project = pbxProjectPath; _entitlements = entitlementFilePath; _target = targetGuid;
        }
        public void AddPushNotifications(bool development)
            => _set["aps-environment"] = new System.Xml.Linq.XElement("string", development ? "development" : "production");
        public void AddBackgroundModes(BackgroundModesOptions options) { }
        public void AddAppGroups(string[] groups)
        {
            GroupWrites.Add(_entitlements + ": " + string.Join(",", groups));
            _set["com.apple.security.application-groups"] =
                new System.Xml.Linq.XElement("array", System.Linq.Enumerable.Select(groups, g => new System.Xml.Linq.XElement("string", g)));
        }
        public void WriteToFile()
        {
            if (string.IsNullOrEmpty(_project)) return;
            var root = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(_project));
            var path = System.IO.Path.Combine(root, _entitlements);
            var document = System.IO.File.Exists(path)
                ? System.Xml.Linq.XDocument.Load(path, System.Xml.Linq.LoadOptions.None)
                : new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("plist", new System.Xml.Linq.XAttribute("version", "1.0"), new System.Xml.Linq.XElement("dict")));
            var dict = document.Root.Element("dict");
            foreach (var pair in _set)
            {
                var key = System.Linq.Enumerable.FirstOrDefault(dict.Elements("key"), k => k.Value == pair.Key);
                if (key != null) { System.Linq.Enumerable.First(key.ElementsAfterSelf()).Remove(); key.Remove(); }
                dict.Add(new System.Xml.Linq.XElement("key", pair.Key), new System.Xml.Linq.XElement(pair.Value));
            }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            if (document.DocumentType != null) document.DocumentType.InternalSubset = null;
            document.Save(path);
            // Like Unity's, it points the target at its entitlements file.
            if (_target == null) return;
            var project = new PBXProject(); project.ReadFromFile(_project);
            if (string.IsNullOrEmpty(project.GetBuildPropertyForAnyConfig(_target, "CODE_SIGN_ENTITLEMENTS")))
                project.SetBuildProperty(_target, "CODE_SIGN_ENTITLEMENTS", _entitlements);
        }
    }

    [System.Flags]
    internal enum BackgroundModesOptions
    {
        None = 0,
        // Unity's real value; 1 << 4 is ExternalAccessoryCommunication.
        RemoteNotifications = 0x100
    }

    internal sealed class PlistDocument
    {
        public PlistElementDict root { get; } = new PlistElementDict();
        public void ReadFromFile(string path) { }
        public void WriteToFile(string path) { }
    }

    internal sealed class PlistElementDict
    {
        public void SetBoolean(string key, bool value) { }
    }
}

namespace UnityEditor.iOS.Xcode.Extensions
{
    internal static class PBXProjectExtensions
    {
        public static string AddAppExtension(
            this UnityEditor.iOS.Xcode.PBXProject project,
            string mainTarget,
            string name,
            string bundleId,
            string infoPlistPath) => project.StandInAddAppExtension(name, bundleId, infoPlistPath);
    }
}
