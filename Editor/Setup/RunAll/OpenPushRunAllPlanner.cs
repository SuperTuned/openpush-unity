using System;
using System.Collections.Generic;

namespace OpenPushSDK.Editor
{
    /// <summary>One evaluated checklist row: what the Setup window draws and what Run all works through.</summary>
    internal sealed class OpenPushRunRow
    {
        public string Title = "(check failed)";
        public string Detail = string.Empty;
        public bool Passed;
        /// The row's buttons, left to right.
        public KeyValuePair<string, Action>[] Buttons = new KeyValuePair<string, Action>[0];
        /// What Run all may do for this row without asking anyone, or null when only a person can fix it.
        public Action Auto;
        /// The row's fix is still taking effect (a package loading): Run all waits.
        public bool Pending;
    }

    internal enum OpenPushRunStep { Done, Fix, Stop, Wait }

    internal sealed class OpenPushRunDecision
    {
        public OpenPushRunStep Step;
        public OpenPushRunRow Row;
        public string Reason;
    }

    /// <summary>
    /// Run all, one step at a time: the first failing row is fixed when it can fix itself and has not been
    /// tried yet in this run; otherwise the run stops there and says why. The caller re-evaluates every row
    /// between two steps, so a fix that changes what later rows see is always taken into account.
    /// </summary>
    internal static class OpenPushRunAllPlanner
    {
        internal static OpenPushRunDecision Next(IReadOnlyList<OpenPushRunRow> rows, ICollection<string> attempted)
        {
            foreach (var row in rows)
            {
                if (row.Pending)
                    return new OpenPushRunDecision { Step = OpenPushRunStep.Wait, Row = row,
                                                     Reason = row.Title + ": waiting for Unity to load the package it added." };
                if (row.Passed) continue;
                if (row.Auto != null && !attempted.Contains(row.Title))
                    return new OpenPushRunDecision { Step = OpenPushRunStep.Fix, Row = row };
                return new OpenPushRunDecision
                {
                    Step = OpenPushRunStep.Stop, Row = row,
                    Reason = row.Title + ": " + (row.Auto != null ? "its fix ran but the row still fails. " : "")
                           + (row.Detail ?? ""),
                };
            }
            return new OpenPushRunDecision { Step = OpenPushRunStep.Done };
        }

        /// <summary>
        /// Runs to the end synchronously (batch mode): <paramref name="evaluate"/> re-reads every row before
        /// each step. Returns the decision that ended the run and the titles of the rows it fixed.
        /// </summary>
        internal static OpenPushRunDecision RunToEnd(Func<IReadOnlyList<OpenPushRunRow>> evaluate, List<string> fixedRows, int maxSteps = 64)
        {
            var attempted = new HashSet<string>();
            for (var i = 0; i < maxSteps; i++)
            {
                var decision = Next(evaluate(), attempted);
                if (decision.Step == OpenPushRunStep.Wait)
                    // A batch run cannot wait for a package to load: Unity loads it when it starts again.
                    return new OpenPushRunDecision { Step = OpenPushRunStep.Stop, Row = decision.Row,
                        Reason = decision.Reason + " Run RunAll once more: Unity loads it on its next start." };
                if (decision.Step != OpenPushRunStep.Fix) return decision;
                attempted.Add(decision.Row.Title);
                fixedRows.Add(decision.Row.Title);
                decision.Row.Auto();
            }
            return new OpenPushRunDecision { Step = OpenPushRunStep.Stop, Reason = "Run all stopped after " + maxSteps + " steps." };
        }
    }
}
