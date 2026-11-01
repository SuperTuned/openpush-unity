using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Something starts OpenPush: an OpenPushBootstrap component in a build scene, or a script that calls
    /// OpenPush.Initialize. Optional, so the row always passes; the Add Bootstrap button is opt-in and Run
    /// all never presses it, because a game that initialises after consent must not get one.
    /// </summary>
    public sealed class BootstrapInScene : OpenPushSetupCheck
    {
        public override int Order => 45;
        public override string Title => "Startup (optional)";
        public override bool Passes() => true;

        private static string BootstrapGuid()
            => AssetDatabase.FindAssets("OpenPushBootstrap t:MonoScript").Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p) == "OpenPushBootstrap.cs").Select(AssetDatabase.AssetPathToGUID).FirstOrDefault();

        private static string FirstScene() => EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled && File.Exists(s.path))?.path;

        private static readonly OpenPushStampedCache<string> StarterCache = new OpenPushStampedCache<string>();

        /// The build scene that has the component, or the script that initialises, or null. Reads every script,
        /// so it is cached until an asset changes or the scene list does.
        private static string Starter()
            => StarterCache.Get(FindStarter, string.Join("|", EditorBuildSettings.scenes.Select(s => s.path + s.enabled)));

        private static string FindStarter()
        {
            var guid = BootstrapGuid();
            if (guid != null)
                foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)))
                    if (File.ReadAllText(scene.path).Contains("guid: " + guid)) return scene.path;
            return AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/") && p.EndsWith(".cs") && !p.Contains("/Samples/"))
                .FirstOrDefault(p => File.ReadAllText(p).Contains("OpenPush.Initialize("));
        }

        public override string Detail
        {
            get
            {
                var starter = Starter();
                if (starter != null) return "OpenPush is started by " + starter + ".";
                return FirstScene() == null
                    ? "Nothing starts OpenPush yet, and Build Settings has no scene. Call OpenPush.Initialize(appId) "
                      + "in code, or add the OpenPush Bootstrap component to your first scene."
                    : "Nothing starts OpenPush yet. Call OpenPush.Initialize(appId) in code (after consent, if your game "
                      + "asks for it), or press Add Bootstrap to put the OpenPush Bootstrap component in " + FirstScene() + ".";
            }
        }

        public override bool CanFix => Starter() == null && FirstScene() != null;
        public override string FixLabel => "Add Bootstrap";

        public override void Fix()
        {
            var path = FirstScene();
            if (path == null || Starter() != null) return;
            var open = EditorSceneManager.GetSceneByPath(path);
            var wasOpen = open.IsValid() && open.isLoaded;
            // Saving the scene would also save edits the user has not chosen to keep: never do that.
            if (wasOpen && open.isDirty)
            {
                Debug.LogWarning("OpenPush: " + path + " has unsaved changes. Save or revert them, then press Add Bootstrap again.");
                return;
            }
            var scene = wasOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var go = new GameObject("OpenPush");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<OpenPushBootstrap>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("OpenPush: added the OpenPush Bootstrap component to " + path + ".");
        }
    }
}
