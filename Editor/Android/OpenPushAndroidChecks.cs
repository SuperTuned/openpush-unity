using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OpenPushSDK.Core;

namespace OpenPushSDK.Editor
{
    internal static class OpenPushAndroidChecks
    {
        internal const int MinSdk = 24;
        internal const int TargetSdk = 34;

        internal static bool UnityVersionSupported(string unityVersion)
        {
            var m = Regex.Match(unityVersion ?? "", @"^(\d+)\.(\d+)");
            if (!m.Success) return false;
            var major = int.Parse(m.Groups[1].Value); var minor = int.Parse(m.Groups[2].Value);
            return major > 2022 || (major == 2022 && minor >= 3);
        }

        /// <summary>target 0 is "Automatic (highest installed)", which Unity 2022.3 resolves to 34+ with a current SDK.</summary>
        internal static bool SdkLevelsOk(int minSdk, int targetSdk)
            => minSdk >= MinSdk && (targetSdk == 0 || targetSdk >= TargetSdk);

        internal static bool FirebaseConfigOk(OpenPushAndroidPushDelivery delivery, IEnumerable<string> assetPaths,
                                              string projectId, string applicationId, string apiKey)
            => FirebaseConfigProblem(delivery, assetPaths, projectId, applicationId, apiKey) == null;

        /// <summary>
        /// Null when Automatic delivery can get an FCM token. The native FCM module reads the three
        /// ai.openpush.fcm.* meta-data values, or reuses a default FirebaseApp. A google-services.json in
        /// Assets produces that app only when Firebase Unity (FirebaseApp.androidlib) or the
        /// google-services Gradle plugin (google-services.xml) turns it into resources; a plain Unity
        /// export applies neither, so the JSON alone does not count.
        /// </summary>
        internal static string FirebaseConfigProblem(OpenPushAndroidPushDelivery delivery, IEnumerable<string> assetPaths,
                                                     string projectId, string applicationId, string apiKey)
        {
            if (delivery == OpenPushAndroidPushDelivery.Forward) return null;
            if (!string.IsNullOrWhiteSpace(projectId) && !string.IsNullOrWhiteSpace(applicationId) && !string.IsNullOrWhiteSpace(apiKey))
                return null;
            var paths = (assetPaths ?? Enumerable.Empty<string>()).Select(p => (p ?? "").Replace('\\', '/').TrimEnd('/')).ToList();
            var hasJson = paths.Any(p => p.EndsWith("/google-services.json") || p == "google-services.json");
            var hasDefaultApp = paths.Any(p => p.EndsWith("/FirebaseApp.androidlib") || p.EndsWith("/google-services.xml"));
            if (hasJson && hasDefaultApp) return null;
            const string Values = "Fill the Firebase project id, application id and API key in OpenPushSettings (the meta-data path).";
            return hasJson
                ? "google-services.json is in Assets, but nothing turns it into a Firebase app: a plain Unity export applies no google-services Gradle plugin. "
                  + Values + " The JSON only works alongside Firebase Unity (FirebaseApp.androidlib) or the google-services Gradle plugin."
                : "Automatic delivery needs the Firebase configuration. " + Values
                  + " google-services.json only works alongside Firebase Unity or the google-services Gradle plugin. Without it the device never gets an FCM token (the SDK only logs it).";
        }
    }
}
