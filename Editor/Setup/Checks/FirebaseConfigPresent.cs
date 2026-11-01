using System.Collections.Generic;
using System.Linq;
using OpenPushSDK.Core;
using UnityEditor;
namespace OpenPushSDK.Editor
{
    public sealed class FirebaseConfigPresent : OpenPushSetupCheck
    {
        public override int Order => 63;
        public override string Title => "Firebase configuration (Automatic delivery)";

        /// The assets that decide whether a google-services.json counts: the JSON itself and what
        /// Firebase Unity or the google-services plugin generates from it.
        private static IEnumerable<string> FirebaseAssetPaths()
            => AssetDatabase.FindAssets("google-services").Concat(AssetDatabase.FindAssets("FirebaseApp"))
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.StartsWith("Assets/")).Distinct();

        private sealed class State
        {
            public string Problem;
            public bool FromJson;
        }

        private static readonly OpenPushStampedCache<State> Cache = new OpenPushStampedCache<State>();

        /// <summary>
        /// The row's answer, cached until an asset changes or a value it reads does (the delivery mode, the
        /// three Firebase values, the Android package): it searches the asset database twice and parses
        /// google-services.json. Passes, Detail, FixLabel and FixesItself share one answer.
        /// </summary>
        private static State Current()
        {
            var s = OpenPushSetupAssets.Loadable();
            if (s == null) return new State();
            var package = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            return Cache.Get(() =>
            {
                var paths = FirebaseAssetPaths().ToList();
                return new State
                {
                    Problem = OpenPushAndroidChecks.FirebaseConfigProblem(s.AndroidPushDelivery, paths, s.AndroidFirebaseProjectId, s.AndroidFirebaseApplicationId, s.AndroidFirebaseApiKey),
                    FromJson = FromJson(paths, out _, out _),
                };
            }, (s.GetInstanceID(), s.AndroidPushDelivery, s.AndroidFirebaseProjectId, s.AndroidFirebaseApplicationId, s.AndroidFirebaseApiKey, package));
        }

        private static string Problem() => Current().Problem;

        public override bool Passes() => Problem() == null;
        public override string Detail => Problem()
            ?? "The native FCM module can create the Firebase app (the three Firebase values in OpenPushSettings, or google-services.json alongside Firebase Unity).";
        public override bool CanFix => true;
        public override string FixLabel => Problem() != null && Current().FromJson ? "Fill" : "Select";
        public override bool FixesItself => Problem() != null && Current().FromJson;

        /// <summary>The three values from Assets/**/google-services.json, for the app's Android package.</summary>
        private static bool FromJson(IEnumerable<string> paths, out OpenPushFirebaseValues values, out string problem)
        {
            values = null;
            problem = "No google-services.json in Assets.";
            var package = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            foreach (var path in paths.Where(p => p.EndsWith("/google-services.json")))
                if (OpenPushGoogleServices.TryRead(System.IO.File.ReadAllText(path), package, out values, out problem)) return true;
            return false;
        }

        /// Fill: the three Firebase values from google-services.json; otherwise reveal the asset.
        public override void Fix()
        {
            var s = OpenPushSetupAssets.Loadable();
            var found = FromJson(FirebaseAssetPaths(), out var values, out var problem);
            if (s == null || !found)
            {
                if (s != null && Problem() != null) UnityEngine.Debug.LogWarning("OpenPush: " + problem);
                OpenPushSetupAssets.Reveal();
                return;
            }
            s.AndroidFirebaseProjectId = values.ProjectId;
            s.AndroidFirebaseApplicationId = values.ApplicationId;
            s.AndroidFirebaseApiKey = values.ApiKey;
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
        }
    }
}
