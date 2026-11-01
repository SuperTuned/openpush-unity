using System.Collections.Generic;
using OpenPushSDK.Core;
using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The settings asset as the checklist sees it. Everything here goes through
    /// <c>AssetDatabase</c> rather than <c>Resources.Load</c>: the checklist has
    /// to describe assets the runtime cannot load as well as the one it can.
    /// </summary>
    internal static class OpenPushSetupAssets
    {
        /// The package name, for Package Manager deep links.
        internal const string PackageName = "ai.openpush.unity";

        /// The one folder the checklist creates and moves into.
        internal const string ResourcesFolder = "Assets/Resources";

        /// The file name <c>Resources.Load</c> looks for, plus its extension.
        internal static string FileName => OpenPushConfiguration.SettingsResourceName + ".asset";

        internal static string DefaultPath => ResourcesFolder + "/" + FileName;

        private static bool _capturing;
        private static List<string> _snapshot;

        /// <summary>
        /// Opens a window during which <see cref="AllPaths"/> answers from one
        /// query. A row asks three separate questions — Passes, Detail, CanFix —
        /// and they must not be able to disagree because an import landed
        /// between two of them.
        /// </summary>
        internal static void BeginEvaluation()
        {
            _capturing = true;
            _snapshot = null;
        }

        /// Closes the window and drops the snapshot, so the next caller — a Fix
        /// button, say — sees the project as it is now.
        internal static void EndEvaluation()
        {
            _capturing = false;
            _snapshot = null;
        }

        /// Every OpenPushSettings asset in the project, ordinally sorted so
        /// "the first one" is the same answer every time.
        internal static List<string> AllPaths()
        {
            if (!_capturing) return Query();
            return _snapshot ??= Query();
        }

        private static List<string> Query()
        {
            var guids = AssetDatabase.FindAssets("t:OpenPushSettings");
            var paths = new List<string>(guids.Length);
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
            paths.Sort(string.CompareOrdinal);
            return paths;
        }

        /// <summary>
        /// The path of the asset the runtime would actually load, or null.
        /// <c>Resources.Load&lt;OpenPushSettings&gt;("OpenPushSettings")</c> resolves
        /// that name against the root of each <c>Resources</c> folder, so the asset
        /// has to sit *directly* inside one and carry exactly that file name.
        /// A sub-folder — <c>Assets/Resources/OpenPush/OpenPushSettings.asset</c> —
        /// would have to be loaded as <c>"OpenPush/OpenPushSettings"</c>, which the
        /// runtime never asks for, so it does not count.
        /// </summary>
        internal static string LoadablePath()
        {
            foreach (var path in AllPaths())
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.EndsWith("/Resources/" + FileName)) return path;
            }
            return null;
        }

        /// The asset the runtime would load, or null.
        internal static OpenPushSettings Loadable()
        {
            var path = LoadablePath();
            return path == null ? null : AssetDatabase.LoadAssetAtPath<OpenPushSettings>(path);
        }

        private static readonly OpenPushStampedCache<OpenPushSettings> LoadableCache = new OpenPushStampedCache<OpenPushSettings>();

        /// <summary>
        /// <see cref="Loadable"/>, searched for again only when an asset changed (creating, moving or deleting
        /// the settings asset is an asset change). For callers that run on every IMGUI event; a row inside an
        /// evaluation uses <see cref="Loadable"/>.
        /// </summary>
        internal static OpenPushSettings LoadableSinceLastChange()
        {
            var settings = LoadableCache.Get(Loadable);
            return settings != null ? settings : null;
        }

        /// The loadable asset, or failing that any asset, or null.
        internal static OpenPushSettings AnyAsset()
        {
            var loadable = Loadable();
            if (loadable != null) return loadable;
            var paths = AllPaths();
            return paths.Count == 0 ? null : AssetDatabase.LoadAssetAtPath<OpenPushSettings>(paths[0]);
        }

        /// <summary>
        /// Reveals the settings asset in the Project window. The fallback for
        /// every "Select" button, and the tail of the create and move fixes.
        /// </summary>
        internal static void Reveal()
        {
            var asset = AnyAsset();
            if (asset == null)
            {
                Debug.LogWarning("OpenPush: no OpenPushSettings asset to select. "
                               + "Run the \"Settings asset\" fix first.");
                return;
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        /// Creates Assets/Resources when it is missing. Returns false on failure.
        internal static bool EnsureResourcesFolder()
        {
            if (AssetDatabase.IsValidFolder(ResourcesFolder)) return true;
            AssetDatabase.CreateFolder("Assets", "Resources");
            if (AssetDatabase.IsValidFolder(ResourcesFolder)) return true;
            Debug.LogError("OpenPush: could not create " + ResourcesFolder + ".");
            return false;
        }
    }
}
