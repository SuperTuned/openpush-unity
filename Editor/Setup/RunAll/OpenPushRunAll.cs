using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The Setup window's Run all: a queue over the checklist, one fix per editor tick with every row
    /// re-evaluated in between. It waits while Unity compiles or resolves packages (adding EDM4U reloads the
    /// domain), and resumes after a reload from SessionState. Rows with a stored answer use it; unanswered
    /// existing-setup rows take Reuse. It stops at the first row that cannot fix itself and says why.
    /// </summary>
    [InitializeOnLoad]
    internal static class OpenPushRunAll
    {
        private const string ActiveKey = "OpenPush.RunAll.Active";
        private const string AttemptedKey = "OpenPush.RunAll.Attempted";
        private const string FixedKey = "OpenPush.RunAll.Fixed";
        private const string StatusKey = "OpenPush.RunAll.Status";
        private const string WaitKey = "OpenPush.RunAll.WaitingSince";
        private const double MaxWaitSeconds = 300;

        static OpenPushRunAll()
        {
            if (Running) EditorApplication.update += Tick;
        }

        internal static bool Running => SessionState.GetBool(ActiveKey, false);

        /// The last run's outcome, for the window.
        internal static string Status => SessionState.GetString(StatusKey, "");

        internal static void Start()
        {
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(AttemptedKey, "");
            SessionState.SetString(FixedKey, "");
            SessionState.SetString(StatusKey, "Run all: working…");
            // The first step sees the project as it is now; every later one follows a fix, which rescans.
            OpenPushSetupRows.Invalidate();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static List<string> Read(string key)
            => SessionState.GetString(key, "").Split('\n').Where(s => s.Length > 0).ToList();

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var attempted = Read(AttemptedKey);
            var fixedRows = Read(FixedKey);
            // No rescan here: a waiting run ticks on every editor update, and the stamped scans already moved
            // after the last fix (Invalidate below) or after an import. A domain reload starts them afresh.
            var decision = OpenPushRunAllPlanner.Next(OpenPushSetupRows.Collect(OpenPushSetupCheck.Discover()), attempted);
            if (decision.Step == OpenPushRunStep.Wait)
            {
                // Adding EDM4U resolves packages and reloads the domain; keep checking until it has loaded.
                var since = SessionState.GetFloat(WaitKey, 0f);
                if (since <= 0f) SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup);
                else if (EditorApplication.timeSinceStartup - since > MaxWaitSeconds)
                {
                    Finish("Run all stopped: " + decision.Reason + " It did not load within " + MaxWaitSeconds + " seconds; press Run all again.");
                    return;
                }
                SessionState.SetString(StatusKey, "Run all: " + decision.Reason);
                return;
            }
            SessionState.EraseFloat(WaitKey);
            if (decision.Step == OpenPushRunStep.Fix)
            {
                attempted.Add(decision.Row.Title);
                SessionState.SetString(AttemptedKey, string.Join("\n", attempted));
                try
                {
                    decision.Row.Auto();
                    fixedRows.Add(decision.Row.Title);
                    SessionState.SetString(FixedKey, string.Join("\n", fixedRows));
                }
                catch (Exception e)
                {
                    Debug.LogError("OpenPush: Run all fix \"" + decision.Row.Title + "\" failed: " + e);
                }
                OpenPushSetupRows.Invalidate();
                return;
            }
            Finish(Summary(decision, fixedRows));
        }

        internal static string Summary(OpenPushRunDecision decision, IReadOnlyCollection<string> fixedRows)
        {
            var done = fixedRows.Count == 0 ? "nothing to fix" : "fixed " + string.Join(", ", fixedRows);
            return decision.Step == OpenPushRunStep.Done
                ? "Run all: every row passes (" + done + ")."
                : "Run all stopped at \"" + decision.Row?.Title + "\" (" + done + "). " + decision.Reason;
        }

        private static void Finish(string status)
        {
            SessionState.EraseFloat(WaitKey);
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetString(StatusKey, status);
            Debug.Log("OpenPush: " + status);
            OpenPushSetupRows.Invalidate();
        }
    }
}
