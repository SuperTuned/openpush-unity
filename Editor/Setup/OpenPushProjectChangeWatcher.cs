using UnityEditor;
using UnityEditor.PackageManager;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Moves the <see cref="OpenPushProjectChangeStamp"/> when the project changes underneath the Setup rows:
    /// any asset import, delete or move, and Package Manager registering packages (a resolve after
    /// Packages/manifest.json changed, which imports no asset when only a registry moved). A Build Settings
    /// scene-list change only re-evaluates the rows: the one row that reads it keys its cache on the list.
    /// </summary>
    internal sealed class OpenPushProjectChangeWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            => OpenPushProjectChangeStamp.Bump();

        [InitializeOnLoadMethod]
        private static void Watch()
        {
            Events.registeredPackages += _ => OpenPushProjectChangeStamp.Bump();
            EditorBuildSettings.sceneListChanged += OpenPushSetupRows.Reevaluate;
        }
    }
}
