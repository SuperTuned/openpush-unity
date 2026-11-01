using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Window → OpenPush → Setup. The same checklist as the OpenPush page in
    /// Project Settings, in a window a host can leave open beside the inspector
    /// while fixing things.
    /// </summary>
    public sealed class OpenPushSetupWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Window/OpenPush/Setup")]
        public static void Open()
        {
            var window = GetWindow<OpenPushSetupWindow>(false, "OpenPush Setup", true);
            window.minSize = new Vector2(420f, 280f);
            window.Show();
        }

        // Coming back to the window is the moment a host has just changed
        // something elsewhere, so re-ask every row rather than showing the
        // cached answer. Project scans keep theirs: an asset change already
        // moved their stamp, and Re-check runs them again on request.
        private void OnFocus()
        {
            OpenPushSetupRows.Reevaluate();
            Repaint();
        }

        // Run all works one fix per editor tick; keep the window showing its progress.
        // Otherwise repaint only when something a row reads moved (a settings edit in
        // the inspector, an import), which costs a few field reads per poll.
        private void OnInspectorUpdate()
        {
            if (OpenPushRunAll.Running || OpenPushSetupRows.Stale) Repaint();
        }

        private void OnGUI()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                OpenPushSetupRows.Draw(OpenPushSetupCheck.Discover());
            }
        }
    }
}
