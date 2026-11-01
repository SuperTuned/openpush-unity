using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The settings asset is somewhere the runtime can load it. The SDK reads it
    /// with <c>Resources.Load</c>, which sees none of: an asset outside a
    /// <c>Resources</c> folder, one that has been renamed, or one filed in a
    /// sub-folder of <c>Resources</c> (that would need a different load path).
    /// </summary>
    public sealed class SettingsInResources : OpenPushSetupCheck
    {
        public override int Order => 20;
        public override string Title => "Loadable from Resources";

        public override bool Passes() => OpenPushSetupAssets.LoadablePath() != null;

        public override string Detail
        {
            get
            {
                var loadable = OpenPushSetupAssets.LoadablePath();
                if (loadable != null) return "The runtime loads " + loadable + " at startup.";

                var paths = OpenPushSetupAssets.AllPaths();
                if (paths.Count == 0)
                    return "No settings asset yet. Create one on the row above, and it "
                         + "lands in the right place.";

                return "The asset at " + paths[0] + " is not where Resources.Load looks: it "
                     + "has to sit directly inside a Resources folder — not in a sub-folder "
                     + "of one — and be named " + OpenPushSetupAssets.FileName
                     + ". Fix moves it to " + OpenPushSetupAssets.DefaultPath + ".";
            }
        }

        public override bool FixesItself => true;

        public override bool CanFix => !Passes() && OpenPushSetupAssets.AllPaths().Count > 0;
        public override string FixLabel => "Move";

        public override void Fix()
        {
            if (Passes()) return;
            var paths = OpenPushSetupAssets.AllPaths();
            if (paths.Count == 0) return;
            if (!OpenPushSetupAssets.EnsureResourcesFolder()) return;

            // One move covers all three ways of being unloadable: outside a
            // Resources folder, in a sub-folder of one, or renamed. MoveAsset
            // renames as well as moves.
            var error = AssetDatabase.MoveAsset(paths[0], OpenPushSetupAssets.DefaultPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("OpenPush: could not move " + paths[0] + " to "
                             + OpenPushSetupAssets.DefaultPath + ": " + error);
                return;
            }
            AssetDatabase.SaveAssets();
            OpenPushSetupAssets.Reveal();
        }
    }
}
