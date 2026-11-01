using System.Collections.Generic;
using System.Linq;
using OpenPushSDK.Core;

namespace OpenPushSDK.Editor
{
    /// <summary>The three Firebase values the native FCM module reads, as google-services.json holds them.</summary>
    internal sealed class OpenPushFirebaseValues
    {
        public string ProjectId, ApplicationId, ApiKey;
    }

    /// <summary>
    /// Reads google-services.json: <c>project_info.project_id</c>, and from the client whose
    /// <c>client_info.android_client_info.package_name</c> is the app's Android package,
    /// <c>client_info.mobilesdk_app_id</c> and <c>api_key[0].current_key</c>. A file for several apps is
    /// never guessed from: no matching client, no values.
    /// </summary>
    internal static class OpenPushGoogleServices
    {
        internal static bool TryRead(string json, string packageName, out OpenPushFirebaseValues values, out string problem)
        {
            values = null;
            if (!OpenPushJson.TryParse(json, out var parsed) || !(parsed is Dictionary<string, object> root))
            {
                problem = "google-services.json is not valid JSON.";
                return false;
            }
            var projectId = (root.TryGetValue("project_info", out var p) ? p as Dictionary<string, object> : null) is Dictionary<string, object> info
                            && info.TryGetValue("project_id", out var id) ? id as string : null;
            var clients = root.TryGetValue("client", out var c) && c is List<object> list ? list.OfType<Dictionary<string, object>>().ToList()
                                                                                        : new List<Dictionary<string, object>>();
            var packages = new List<string>();
            foreach (var client in clients)
            {
                var clientInfo = client.TryGetValue("client_info", out var ci) ? ci as Dictionary<string, object> : null;
                var android = clientInfo != null && clientInfo.TryGetValue("android_client_info", out var a) ? a as Dictionary<string, object> : null;
                var package = android != null && android.TryGetValue("package_name", out var pn) ? pn as string : null;
                if (package != null) packages.Add(package);
                if (package != packageName) continue;
                var appId = clientInfo.TryGetValue("mobilesdk_app_id", out var m) ? m as string : null;
                var apiKey = client.TryGetValue("api_key", out var k) && k is List<object> keys
                             && keys.OfType<Dictionary<string, object>>().FirstOrDefault() is Dictionary<string, object> first
                             && first.TryGetValue("current_key", out var ck) ? ck as string : null;
                if (string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(apiKey))
                {
                    problem = "google-services.json has a client for " + packageName + " but not all three values (project id, app id, API key).";
                    return false;
                }
                values = new OpenPushFirebaseValues { ProjectId = projectId, ApplicationId = appId, ApiKey = apiKey };
                problem = null;
                return true;
            }
            problem = "google-services.json has no Android client for " + packageName
                    + (packages.Count > 0 ? " (it has " + string.Join(", ", packages) + ")" : "") + ".";
            return false;
        }
    }
}
