using UnityEditor;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Opens the Setup window once, after the package is first imported: never twice in one editor session
    /// (SessionState), never again once the project's ProjectSettings/OpenPushSetup.json carries the marker,
    /// and never in batch mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class OpenPushSetupFirstRun
    {
        private const string SessionKey = "OpenPush.SetupWindowShown";
        internal const string Marker = "setup_window_shown";

        static OpenPushSetupFirstRun()
        {
            if (UnityEngine.Application.isBatchMode || SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += Show;
        }

        private static void Show()
        {
            SessionState.SetBool(SessionKey, true);
            var file = OpenPushPlumbingAnswers.File();
            if (!file.Writable || file.Flag(Marker)) return;
            file.SetFlag(Marker, true);
            file.Write(OpenPushPlumbingAnswers.SetupFilePath);
            OpenPushSetupWindow.Open();
        }
    }
}
