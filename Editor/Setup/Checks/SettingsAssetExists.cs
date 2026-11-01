using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The project has an OpenPushSettings asset at all. Everything else on the
    /// checklist reads that asset, so this row comes first.
    /// </summary>
    public sealed class SettingsAssetExists : OpenPushSetupCheck
    {
        public override int Order => 10;
        public override string Title => "Settings asset";

        public override bool Passes() => OpenPushSetupAssets.AllPaths().Count > 0;

        public override string Detail
        {
            get
            {
                var paths = OpenPushSetupAssets.AllPaths();
                if (paths.Count == 0)
                    return "No OpenPushSettings asset in this project. Fix creates "
                         + OpenPushSetupAssets.DefaultPath + ", which is also what "
                         + "Assets → Create → OpenPush → Settings makes.";
                if (paths.Count == 1) return "Found at " + paths[0] + ".";
                return paths.Count + " settings assets found; the first one in a Resources "
                     + "folder wins at runtime. The rest are dead weight.";
            }
        }

        // Nothing to offer once the asset exists — the rows below act on it.
        public override bool FixesItself => true;
        public override bool CanFix => !Passes();
        public override string FixLabel => "Create";

        public override void Fix()
        {
            if (Passes()) return;
            if (!OpenPushSetupAssets.EnsureResourcesFolder()) return;

            // FindAssets("t:OpenPushSettings") does not see an asset whose script
            // is missing or broken, so "no settings asset" does not prove the
            // canonical path is free. CreateAsset would silently replace it.
            if (AssetDatabase.LoadMainAssetAtPath(OpenPushSetupAssets.DefaultPath) != null)
            {
                Debug.LogError("OpenPush: " + OpenPushSetupAssets.DefaultPath + " already holds "
                             + "an asset the settings search does not recognise — most likely "
                             + "an OpenPushSettings whose script reference is broken. Nothing was "
                             + "created; inspect or delete that file first.");
                return;
            }

            var asset = OpenPushSettings.CreateWithCurrentDefaults();
            AssetDatabase.CreateAsset(asset, OpenPushSetupAssets.DefaultPath);
            AssetDatabase.SaveAssets();
            OpenPushSetupAssets.Reveal();
        }
    }
}
