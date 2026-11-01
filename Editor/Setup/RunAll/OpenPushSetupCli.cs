using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Batch entry points, for <c>-batchmode -quit -executeMethod</c>:
    /// <list type="bullet">
    /// <item><c>OpenPushSDK.Editor.OpenPushSetupCli.RunAll</c> runs every fix that can fix itself, using the stored
    /// answers or Reuse for questions never answered, and never prompts. The app id and SDK key come from the
    /// OPENPUSH_SETUP environment variable (the console's link, JSON or two values). It fails (exit code 1)
    /// at the first row only a person can fix, naming it. After it adds EDM4U it stops and asks for a second
    /// run: Unity loads the new package on its next start.</item>
    /// <item><c>OpenPushSDK.Editor.OpenPushSetupCli.Report</c> writes ProjectSettings/OpenPushSetup.json: what the
    /// project and its last iOS export already have (<c>found</c>) and every checklist row (<c>checks</c>).</item>
    /// </list>
    /// </summary>
    public static class OpenPushSetupCli
    {
        public static void RunAll()
        {
            var fixedRows = new List<string>();
            var decision = OpenPushRunAllPlanner.RunToEnd(() =>
            {
                OpenPushExistingPlumbing.Invalidate();
                return OpenPushSetupRows.Collect(OpenPushSetupCheck.Discover());
            }, fixedRows);
            AssetDatabase.SaveAssets();
            var summary = OpenPushRunAll.Summary(decision, fixedRows);
            Debug.Log("OpenPush: " + summary);
            Report();
            if (decision.Step != OpenPushRunStep.Done && Application.isBatchMode)
                throw new InvalidOperationException("OpenPush setup is not complete. " + summary);
        }

        public static void Report()
        {
            OpenPushExistingPlumbing.Invalidate();
            var file = OpenPushPlumbingAnswers.File();
            if (!file.Writable)
            {
                Debug.LogError("OpenPush: " + OpenPushPlumbingAnswers.SetupFilePath + " is not valid JSON; the report was not written.");
                return;
            }
            file.SetFound(OpenPushExistingPlumbing.Detect());
            file.SetChecks(OpenPushSetupRows.Collect(OpenPushSetupCheck.Discover())
                .Select(r => new KeyValuePair<string, (bool, string)>(r.Title, (r.Passed, r.Detail))));
            file.Write(OpenPushPlumbingAnswers.SetupFilePath);
            Debug.Log("OpenPush: wrote " + OpenPushPlumbingAnswers.SetupFilePath + ".");
        }
    }
}
