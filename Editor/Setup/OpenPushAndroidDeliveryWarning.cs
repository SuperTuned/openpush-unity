using System.Linq;
using OpenPushSDK.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Warns an Android build in Automatic delivery when the project has another messaging service and the
    /// Setup window never got an answer for it: both services then receive FCM pushes and they are split
    /// between them. Reuse (Forward) is the default answer there; the build does not switch on its own,
    /// because a project may rely on Automatic today.
    /// </summary>
    internal sealed class OpenPushAndroidDeliveryWarning : IPreprocessBuildWithReport
    {
        public int callbackOrder => -99;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var settings = OpenPushSetupAssets.Loadable();
            if (settings == null || settings.AndroidPushDelivery != OpenPushAndroidPushDelivery.Automatic) return;
            if (OpenPushPlumbingAnswers.Answered(OpenPushPlumbingAnswers.AndroidDeliveryChoice)) return;
            var others = OpenPushExistingPlumbing.Detect().Where(f => f.Kind == OpenPushSetupFinding.FcmService).Select(f => f.Id).ToList();
            if (others.Count == 0) return;
            Debug.LogWarning("OpenPush: " + string.Join(", ", others) + " also receives FCM pushes, so in Automatic delivery pushes are "
                + "split between it and OpenPush's service. Choose Reuse (Forward) or Create new in Window > OpenPush > Setup.");
        }
    }
}
