#if UNITY_IOS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.iOS.Xcode;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Copies the vendored static XCFrameworks from Native~ (invisible to Unity's importer: Unity 2022.3
    /// would otherwise link each slice as a separate framework and "succeed" under -undefined
    /// dynamic_lookup, failing at runtime) into the export and links them. Never embedded: static.
    /// </summary>
    internal static class OpenPushNativeFrameworks
    {
        internal const string ExportFolder = "Frameworks/OpenPushNative";
        internal static readonly string[] UnityFrameworkModules = { "OpenPushCore", "OpenPushLiveActivities", "OpenPush" };
        internal static readonly string[] ServiceExtensionModules = { "OpenPushCore", "OpenPushNSE" };
        internal static readonly string[] WidgetModules = { "OpenPushLiveActivities" };

        /// <summary>Tests point this at a fake Native~ folder.</summary>
        internal static string SourceOverride = null;

        internal static string ExportPath(string module) => ExportFolder + "/" + module + ".xcframework";

        internal static string SourceFolder()
        {
            if (!string.IsNullOrEmpty(SourceOverride)) return SourceOverride;
            // Packages/<name> resolves to the physical folder (embedded or Library/PackageCache@hash).
            return Path.GetFullPath(Path.Combine("Packages", "ai.openpush.unity", "Runtime", "Plugins", "iOS", "Native~"));
        }

        internal static IReadOnlyList<string> CopyInto(string exportRoot, IEnumerable<string> modules)
        {
            var paths = new List<string>();
            foreach (var module in modules)
            {
                var source = Path.Combine(SourceFolder(), module + ".xcframework");
                if (!Directory.Exists(source)) throw new DirectoryNotFoundException("OpenPush: missing vendored " + source);
                var relative = ExportPath(module);
                var target = Path.Combine(exportRoot, relative);
                if (Directory.Exists(target)) Directory.Delete(target, true);
                CopyDirectory(source, target);
                paths.Add(relative);
            }
            return paths;
        }

        /// <summary>
        /// Adds each framework to the target's Frameworks phase once. AddFileToBuildSection would add a
        /// duplicate on re-export, so any existing build entry for the file is removed first: the
        /// phase itself is the record, with no custom build setting a host tool could drop.
        /// </summary>
        internal static void Link(PBXProject project, string targetGuid, IEnumerable<string> projectPaths)
        {
            var phase = project.GetFrameworksBuildPhaseByTarget(targetGuid);
            foreach (var path in projectPaths)
            {
                var existing = project.FindFileGuidByProjectPath(path);
                if (existing != null) project.RemoveFileFromBuild(targetGuid, existing);
                project.AddFileToBuildSection(targetGuid, phase, existing ?? project.AddFile(path, path, PBXSourceTree.Source));
            }
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
                if (!file.EndsWith(".meta")) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }
}
#endif
