#if UNITY_IOS
using UnityEditor.iOS.Xcode;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Registers a file the injectors copied into the export. The path handed to PBXProject.AddFile is
    /// the project-relative one, never the absolute path it was written to: Unity derives the Xcode
    /// reference from that argument, and an absolute Windows path comes out mangled
    /// (OpenPushNotificationService.swift became OpenPushNotificationService.s), so the build fails.
    /// </summary>
    internal static class OpenPushXcodeSources
    {
        internal static string Add(PBXProject project, string projectPath)
            => project.FindFileGuidByProjectPath(projectPath)
               ?? project.AddFile(projectPath, projectPath, PBXSourceTree.Source);
    }
}
#endif
