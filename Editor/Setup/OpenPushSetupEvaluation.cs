using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// When the checklist evaluates its rows again. Two strengths: <see cref="Reevaluate"/> re-asks every row
    /// but keeps the stamped project scans (focus, a settings edit), and <see cref="Rescan"/> also moves the
    /// change stamp so every scan runs again (Re-check, after a fix, Run all). Between those, rows are
    /// evaluated again only when the caller's key changed: the change stamp, the settings asset's dirty
    /// count, the active build target and the few files and Player Settings the rows read. Nothing is
    /// re-evaluated on a timer. Free of UnityEditor types, so the tests drive it directly.
    /// </summary>
    internal sealed class OpenPushSetupRefresh
    {
        private bool _requested = true;
        private object _key;

        /// The next evaluation re-asks every row; stamped scans keep their answer.
        internal void Reevaluate() => _requested = true;

        /// The next evaluation re-asks every row and every stamped scan runs again.
        internal void Rescan()
        {
            OpenPushProjectChangeStamp.Bump();
            _requested = true;
        }

        /// True when an evaluation was asked for or the key moved since the last one.
        internal bool Due(object key) => _requested || !Equals(_key, key);

        internal void Evaluated(object key)
        {
            _requested = false;
            _key = key;
        }
    }

    /// <summary>The checklist evaluated into rows, shared by the window, Run all and the batch entry points.</summary>
    internal static class OpenPushSetupEvaluation
    {
        /// <summary>
        /// Every row, evaluated now: a check's own row, or the rows of its <see cref="OpenPushSetupCheck.Items"/>.
        /// Shared by the window and Run all, so both see exactly the same checklist.
        /// </summary>
        internal static List<OpenPushRunRow> Collect(IReadOnlyList<OpenPushSetupCheck> checks)
        {
            var rows = new List<OpenPushRunRow>(checks.Count);

            // One asset-database query for the whole pass: a row asks three
            // separate questions, and an import landing between two of them
            // would otherwise produce a ✓ next to a detail that says ✗.
            OpenPushSetupAssets.BeginEvaluation();
            try
            {
                foreach (var check in checks)
                {
                    var row = new OpenPushRunRow();
                    try
                    {
                        var items = check.Items();
                        if (items != null)
                        {
                            foreach (var item in items)
                                rows.Add(new OpenPushRunRow { Title = item.Title, Detail = item.Detail, Passed = item.Passed,
                                                              Buttons = item.Actions.ToArray(), Auto = item.RunAll });
                            continue;
                        }
                        row.Title = check.Title;
                        row.Passed = check.Passes();
                        row.Detail = check.Detail;
                        if (check.CanFix)
                            row.Buttons = new[] { new KeyValuePair<string, Action>(check.FixLabel, check.Fix) };
                        if (check.CanFix && check.FixesItself) row.Auto = check.Fix;
                        row.Pending = check.FixPending;
                    }
                    catch (Exception e)
                    {
                        // A check that throws reports itself as failing rather
                        // than taking every row after it down with it.
                        row.Passed = false;
                        row.Buttons = new KeyValuePair<string, Action>[0];
                        row.Auto = null;
                        row.Detail = check.GetType().Name + " threw: " + e.Message;
                    }
                    rows.Add(row);
                }
            }
            finally
            {
                OpenPushSetupAssets.EndEvaluation();
            }
            return rows;
        }
    }
}
