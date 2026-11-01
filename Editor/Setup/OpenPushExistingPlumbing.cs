using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Push plumbing the project already has, before any export exists, from three sources: what the last
    /// iOS export recorded in ProjectSettings/OpenPushSetup.json (which always wins), the project's own
    /// files (Assets/**/*.entitlements, hand-written extensions under Assets/Plugins/iOS, Android manifests,
    /// FirebaseMessagingService sources, google-services.json, icons), and what a known push SDK's own build
    /// step will add (its fingerprint's constants). Cached until an asset changes, a re-check is asked for, the
    /// answer file is rewritten (the iOS export records into it), Packages/manifest.json is written or the iOS
    /// bundle id changes: the Setup window re-asks its rows on every focus, and those must not rescan.
    /// </summary>
    internal static class OpenPushExistingPlumbing
    {
        private static readonly OpenPushStampedCache<List<OpenPushSetupFinding>> Cache = new OpenPushStampedCache<List<OpenPushSetupFinding>>();

        internal static void Invalidate() => OpenPushProjectChangeStamp.Bump();

        internal static IReadOnlyList<OpenPushSetupFinding> Detect()
            => Cache.Get(Scan, (OpenPushProjectChangeStamp.WriteTicks(OpenPushPlumbingAnswers.SetupFilePath),
                                OpenPushProjectChangeStamp.WriteTicks(OpenPushEdm4uManifest.ManifestPath),
                                PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS)));

        private sealed class ProjectView : IOpenPushUnityProjectView
        {
            private readonly string _manifest;
            public ProjectView()
            {
                _manifest = File.Exists("Packages/manifest.json") ? File.ReadAllText("Packages/manifest.json") : "";
            }
            public string AppBundleId => PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
            public bool HasPackage(string name) => _manifest.Contains("\"" + name + "\"") || Directory.Exists("Packages/" + name);
            public bool HasAssetFolder(string path) => AssetDatabase.IsValidFolder(path);
        }

        private static List<OpenPushSetupFinding> Scan()
        {
            var found = new List<OpenPushSetupFinding>(OpenPushPlumbingAnswers.File().Found);
            void Add(OpenPushSetupFinding finding)
            {
                if (!found.Any(f => f.Kind == finding.Kind && f.Id == finding.Id)) found.Add(finding);
            }

            var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/")).ToList();
            var view = new ProjectView();
            foreach (var fingerprint in OpenPushVendorFingerprints.All)
                foreach (var finding in fingerprint.InUnityProject(view)) Add(finding);

            foreach (var path in paths.Where(p => p.EndsWith(".entitlements")))
            {
                var entitlements = OpenPushPlist.Read(path);
                foreach (var group in OpenPushPlist.Strings(entitlements, "com.apple.security.application-groups"))
                {
                    var finding = new OpenPushSetupFinding(OpenPushSetupFinding.AppGroup, group, path)
                    {
                        Vendor = OpenPushVendorFingerprints.AppGroupVendor(group, view.AppBundleId),
                    };
                    finding.Details["on_app"] = true;
                    Add(finding);
                }
                foreach (var group in OpenPushPlist.Strings(entitlements, "keychain-access-groups"))
                    Add(new OpenPushSetupFinding(OpenPushSetupFinding.KeychainGroup, group, path));
            }

            foreach (var path in paths.Where(p => p.StartsWith("Assets/Plugins/iOS/") && Path.GetFileName(p).StartsWith("NotificationService")
                                                 && (p.EndsWith(".swift") || p.EndsWith(".m"))))
            {
                var finding = new OpenPushSetupFinding(OpenPushSetupFinding.ServiceExtension, Path.GetFileNameWithoutExtension(path), path)
                {
                    Vendor = OpenPushVendorFingerprints.ServiceExtensionVendor(new[] { File.ReadAllText(path) }),
                };
                finding.Details["hand_written"] = true;
                Add(finding);
            }

            var manifests = paths.Where(p => p.EndsWith("/AndroidManifest.xml") && p.StartsWith("Assets/Plugins/Android/"))
                .Select(p => (p, File.ReadAllText(p)));
            var sources = paths.Where(p => p.EndsWith(".java") || p.EndsWith(".kt")).Select(p => (p, File.ReadAllText(p)));
            var firebaseMessaging = paths.Any(p => p.EndsWith("/Firebase.Messaging.dll")) || view.HasPackage("com.google.firebase.messaging");
            foreach (var finding in OpenPushAndroidPlumbingScan.Findings(manifests, sources, paths, firebaseMessaging)) Add(finding);
            return found;
        }
    }
}
