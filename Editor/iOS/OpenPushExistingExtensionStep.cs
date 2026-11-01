#if UNITY_IOS
using System.IO;
using UnityEditor.Build;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// TakeOver and Share on an extension the export already has. Its source is edited (Swift only, backed
    /// up first), OpenPushCore and OpenPushNSE are linked to it, and its deployment target is raised to 16.0.
    /// Its bundle id, team, entitlements and signing are kept, so its App ID and provisioning profile still
    /// match; the profile setting only fills a value the target does not have. The App Group is added to its
    /// entitlements when it does not carry it yet.
    /// </summary>
    internal static class OpenPushExistingExtensionStep
    {
        internal sealed class Input
        {
            public PBXProject Project;
            public string ProjectPath, Root, Main, Group, Profile;
            public OpenPushXcodeExtension Extension;
            public bool TakeOver, HostNamedGroup, AppSignsManually;
            public OpenPushSigningContext Signing;
        }

        internal static void Apply(Input input)
        {
            var extension = input.Extension;
            var guid = input.Project.TargetGuidByName(extension.Target);
            if (guid == null) throw new BuildFailedException("OpenPush: the project has no target named " + extension.Target + ".");

            OpenPushExtensionSigning.Enforce(OpenPushExtensionSigning.MissingProfileProblem(input.Project, guid, extension.Target,
                OpenPushIOSIdentifiers.ServiceExtensionProfileField, input.Profile, extension.BundleId, input.AppSignsManually), input.Signing);

            var outcome = input.TakeOver
                ? OpenPushExistingExtensionFiles.TakeOver(input.Root, extension, input.Group)
                : OpenPushExistingExtensionFiles.Share(input.Root, extension, input.Group);
            if (outcome.Problem != null) throw new BuildFailedException("OpenPush: " + outcome.Problem);
            if (outcome.Note != null) Debug.Log("OpenPush: " + outcome.Note);

            OpenPushNativeFrameworks.Link(input.Project, guid,
                OpenPushNativeFrameworks.CopyInto(input.Root, OpenPushNativeFrameworks.ServiceExtensionModules));
            OpenPushIOSBuildSettings.RaiseDeploymentTarget(input.Project, guid, "16.0");
            OpenPushExtensionSigning.LogKept(OpenPushExtensionSigning.Apply(input.Project, input.Main, guid, extension.Target,
                input.Profile, input.AppSignsManually, targetIsNew: false));
            input.Project.WriteToFile(input.ProjectPath);

            if (!extension.Groups.Contains(input.Group) || !input.HostNamedGroup)
                OpenPushServiceExtensionInjector.ApplyAppGroups(input.ProjectPath, input.Root, input.Main, guid,
                    extension.Entitlements ?? extension.Target + "/" + extension.Target + ".entitlements", input.Group, input.HostNamedGroup);

            if (input.TakeOver && extension.Vendor != null) ReportVendorLeftovers(input.Root, extension);
        }

        /// <summary>The previous SDK's pods for the taken-over target are reported, never edited.</summary>
        private static void ReportVendorLeftovers(string root, OpenPushXcodeExtension extension)
        {
            var podfile = Path.Combine(root, "Podfile");
            var block = File.Exists(podfile) ? OpenPushExistingExtensionFiles.PodfileTargetBlock(File.ReadAllText(podfile), extension.Target) : null;
            if (block != null)
                Debug.LogWarning("OpenPush: the Podfile still adds pods to " + extension.Target + ", which now runs OpenPush's "
                               + "handler. Remove your previous push SDK's package (Window > OpenPush > Setup), or delete these "
                               + "lines before pod install:\n" + block);
        }
    }
}
#endif
