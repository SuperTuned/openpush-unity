#if UNITY_IOS
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Runs after every other post-build step, including other packages' (a vendor's runs at 45): records
    /// what the real export holds into ProjectSettings/OpenPushSetup.json <c>found</c>, so the next editor
    /// session knows the existing extension's names, and stops a build that ended up with two notification
    /// service extensions (iOS runs one; another package's step added one after OpenPush's).
    /// </summary>
    public sealed class OpenPushSetupRecorder : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var export = OpenPushXcodeExport.Scan(report.summary.outputPath);
            if (export == null) return;
            var file = OpenPushPlumbingAnswers.File();
            file.SetFound(export.Findings());
            file.WriteIfChanged(OpenPushPlumbingAnswers.SetupFilePath);
            if (!file.Writable)
                Debug.LogWarning("OpenPush: " + OpenPushPlumbingAnswers.SetupFilePath + " is not valid JSON, so what this export holds was not recorded.");

            var services = export.Extensions.Where(e => e.Kind == OpenPushSetupFinding.ServiceExtension).Select(e => e.Target).ToList();
            // Only a pair that includes OpenPush's own is ours to stop; anything else is the host's own setup.
            if (services.Count > 1 && services.Contains(OpenPushIOSIdentifiers.ServiceExtensionTarget))
                throw new BuildFailedException("OpenPush: this export has " + services.Count + " notification service extensions ("
                    + string.Join(", ", services) + "), and iOS runs only one. A build step that runs after OpenPush's added one, "
                    + "usually your previous push SDK's: remove that SDK first (Previous push SDK row in Window > OpenPush > Setup).");
        }
    }
}
#endif
