using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Writes OpenPushDependencies.xml, and resolves when it changed or the package moved, before an
    /// Android build. The [InitializeOnLoad] writer runs from a delayCall, which a single-shot batch
    /// build (-batchmode -quit -executeMethod) never reaches, so CI builds need this step. Ordered
    /// before EDM4U's own pre-build resolution.
    /// </summary>
    internal sealed class OpenPushAndroidPreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var changed = OpenPushAndroidDependencies.WriteNow();
            if (!OpenPushEdm4u.Loaded)
            {
                Debug.LogError("OpenPush: EDM4U (com.google.external-dependency-manager) is not loaded, so "
                    + OpenPushAndroidDependencies.OutputPath + " cannot be resolved and the Gradle build will not find the OpenPush natives.");
                return;
            }
            if (changed || OpenPushResolveState.NeedsForcedResolve(OpenPushEdm4u.Last(), OpenPushEdm4u.Current()))
                OpenPushEdm4u.ForceResolve();
        }
    }
}
