using System.IO;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Finds the folder of extension templates the iOS injectors copy into an export: the package's, or
    /// a copy under Assets/OpenPush for hosts that vendor the package. Null when neither exists.
    /// </summary>
    internal static class OpenPushEditorTemplates
    {
        /// <summary>Tests point this at the package's Editor folder.</summary>
        internal static string EditorFolderOverride = null;

        internal static string Find(string folder)
        {
            if (!string.IsNullOrEmpty(EditorFolderOverride)) return Path.Combine(EditorFolderOverride, folder);
            var source = Path.Combine("Packages", "ai.openpush.unity", "Editor", folder);
            if (Directory.Exists(source)) return source;
            source = Path.Combine("Assets", "OpenPush", "Editor", folder);
            return Directory.Exists(source) ? source : null;
        }
    }
}
