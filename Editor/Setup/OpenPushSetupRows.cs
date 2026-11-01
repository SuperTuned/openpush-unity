using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Draws the checklist. Both the <see cref="OpenPushSetupWindow"/> and the
    /// OpenPush page in Project Settings call this and nothing else, so the two
    /// surfaces cannot drift apart.
    ///
    /// Results are evaluated on layout events only, and only when something they
    /// read moved (see <see cref="OpenPushSetupRefresh"/>): every check queries
    /// the asset database, and IMGUI repaints on every mouse move over the
    /// window. Evaluating on layout also keeps the control count identical
    /// between a layout pass and the repaint that follows it, which is what
    /// IMGUI requires.
    /// </summary>
    public static class OpenPushSetupRows
    {
        private static readonly OpenPushSetupRefresh Refresh = new OpenPushSetupRefresh();

        private static OpenPushRunRow[] _rows;
        private static int _checkCount;
        private static GUIStyle _statusStyle;
        private static GUIStyle _detailStyle;
        private static bool _stylesArePro;

        /// Forces the next layout pass to re-run every check, project scans included.
        public static void Invalidate() => Refresh.Rescan();

        /// Forces the next layout pass to re-ask every row; project scans keep their answer until an asset changes.
        public static void Reevaluate() => Refresh.Reevaluate();

        /// True when the next layout pass would evaluate the rows again: for a cheap poll that repaints.
        internal static bool Stale => _rows == null || Refresh.Due(RefreshKey());

        /// <summary>
        /// Everything a row reads that no asset import reports: the settings asset's edits (its dirty count),
        /// the build target, the Player Settings the rows quote, and the files outside Assets they parse.
        /// Each part is a field read or a file's write time, so computing it on every layout event is cheap.
        /// </summary>
        private static object RefreshKey()
        {
            var settings = OpenPushSetupAssets.LoadableSinceLastChange();
            return (OpenPushProjectChangeStamp.Stamp,
                    settings != null ? settings.GetInstanceID() : 0,
                    settings != null ? EditorUtility.GetDirtyCount(settings) : -1,
                    EditorUserBuildSettings.activeBuildTarget,
                    string.Join("|", PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android),
                        PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS),
                        (int)PlayerSettings.Android.minSdkVersion, (int)PlayerSettings.Android.targetSdkVersion,
                        PlayerSettings.iOS.appleEnableAutomaticSigning, PlayerSettings.iOS.iOSManualProvisioningProfileID),
                    OpenPushProjectChangeStamp.WriteTicks(OpenPushPlumbingAnswers.SetupFilePath),
                    (OpenPushProjectChangeStamp.WriteTicks(OpenPushEdm4uManifest.ManifestPath), OpenPushProjectChangeStamp.WriteTicks(OpenPushResolveState.StatePath)));
        }

        public static void Draw(IReadOnlyList<OpenPushSetupCheck> checks)
        {
            if (checks == null) return;

            var layout = Event.current == null || Event.current.type == EventType.Layout;
            if (layout && (_rows == null || _checkCount != checks.Count || Stale))
                Evaluate(checks);

            if (_rows == null) return; // First event of the surface was not a layout.

            EnsureStyles();
            // GUILayout.Label, not EditorGUILayout.LabelField: only the former
            // asks the style for a height, which is what makes wordWrap work.
            GUILayout.Label(
                "Everything below is checked against this project. Nothing here is required "
                + "to compile — it is what has to be true before a build registers a device.",
                _detailStyle);
            EditorGUILayout.Space();

            Action fix = null;
            string fixTitle = null;
            for (var i = 0; i < _rows.Length; i++)
            {
                var pressed = DrawRow(_rows[i]);
                if (pressed != null) { fix = pressed; fixTitle = _rows[i].Title; }
                EditorGUILayout.Space();
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Re-check", GUILayout.Width(90))) Invalidate();
                using (new EditorGUI.DisabledScope(OpenPushRunAll.Running))
                    if (GUILayout.Button("Run all", GUILayout.Width(90))) { fix = OpenPushRunAll.Start; fixTitle = "Run all"; }
                GUILayout.FlexibleSpace();
            }
            if (!string.IsNullOrEmpty(OpenPushRunAll.Status)) GUILayout.Label(OpenPushRunAll.Status, _detailStyle);

            // Run the fix after the rows are drawn, so a throwing fix cannot
            // leave an IMGUI layout group open.
            if (fix == null) return;
            try
            {
                fix();
            }
            catch (Exception e)
            {
                Debug.LogError($"OpenPush: setup fix \"{fixTitle}\" failed: {e}");
            }
            Invalidate();
        }

        /// The action of the button pressed in this row, or null.
        private static Action DrawRow(OpenPushRunRow row)
        {
            Action pressed = null;
            using (new EditorGUILayout.HorizontalScope())
            {
                var status = _statusStyle;
                status.normal.textColor = row.Passed ? Passed : Failed;
                GUILayout.Label(row.Passed ? "✓" : "✗", status, GUILayout.Width(18));

                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(row.Title, EditorStyles.boldLabel);
                    GUILayout.Label(row.Detail, _detailStyle);
                }

                foreach (var button in row.Buttons)
                {
                    // 70 wide like every row's button; a longer label ("Create new") gets what it needs.
                    var width = Mathf.Max(70f, GUI.skin.button.CalcSize(new GUIContent(button.Key)).x);
                    if (GUILayout.Button(button.Key, GUILayout.Width(width), GUILayout.ExpandHeight(false)))
                        pressed = button.Value;
                }
            }
            return pressed;
        }

        private static Color Passed =>
            EditorGUIUtility.isProSkin ? new Color(0.44f, 0.80f, 0.44f) : new Color(0.13f, 0.47f, 0.13f);

        private static Color Failed =>
            EditorGUIUtility.isProSkin ? new Color(0.94f, 0.48f, 0.42f) : new Color(0.66f, 0.13f, 0.09f);

        private static void Evaluate(IReadOnlyList<OpenPushSetupCheck> checks)
        {
            // The key before the pass: a change landing during it is seen by the next layout.
            var key = RefreshKey();
            _rows = Collect(checks).ToArray();
            _checkCount = checks.Count;
            Refresh.Evaluated(key);
        }

        /// <summary>Every row, evaluated now (see <see cref="OpenPushSetupEvaluation.Collect"/>).</summary>
        internal static List<OpenPushRunRow> Collect(IReadOnlyList<OpenPushSetupCheck> checks)
            => OpenPushSetupEvaluation.Collect(checks);

        private static void EnsureStyles()
        {
            // EditorStyles is only valid inside OnGUI, so the styles are built on
            // first draw rather than in a static constructor — and rebuilt when
            // the editor skin changes underneath them.
            var pro = EditorGUIUtility.isProSkin;
            if (_detailStyle != null && _statusStyle != null && _stylesArePro == pro) return;

            _detailStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            _statusStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.UpperCenter };
            _stylesArePro = pro;
        }
    }
}
