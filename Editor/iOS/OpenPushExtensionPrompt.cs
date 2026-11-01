#if UNITY_IOS
using System.Collections.Generic;
using OpenPushSDK.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Settles an export that does not match the stored service-extension answer. An interactive build asks
    /// once and stores the answer, so the next build goes straight through. A batch build never prompts.
    /// When the question was never answered and the export already has its own service extension, a batch
    /// build adds none and goes on, as earlier OpenPush builds did, and says so in the build log; nothing is
    /// stored. Any other mismatch fails the batch build, naming what was stored and what the export has.
    /// </summary>
    internal static class OpenPushExtensionPrompt
    {
        internal static OpenPushExtensionDecision Resolve(OpenPushExtensionDecision decision, OpenPushSettings settings,
                                                          IReadOnlyList<OpenPushXcodeExtension> hosts, bool ownExists,
                                                          IReadOnlyList<string> appGroups)
        {
            if (Application.isBatchMode && KeepsTheProjectsOwnExtension(settings, hosts))
            {
                Debug.LogWarning("OpenPush: this export already has a notification service extension ("
                    + string.Join(", ", Names(hosts)) + "), so this batch build adds none, as earlier builds did, and "
                    + "stores nothing. Choose Reuse or Create new in Window > OpenPush > Setup to change this.");
                return new OpenPushExtensionDecision { Action = OpenPushExtensionAction.Off, Target = decision.Target };
            }
            var host = decision.Target;
            var reuse = host == null ? "Inject (OpenPush's own extension)" : decision.Suggested + " " + host.Target;
            if (Application.isBatchMode || host != null && hosts.Count > 1)
                throw new BuildFailedException(decision.Message + " Stored: IosExtensionMode = " + settings.IosExtensionMode
                    + (string.IsNullOrWhiteSpace(settings.IosExtensionTarget) ? "" : ", IosExtensionTarget = " + settings.IosExtensionTarget)
                    + ". Found: " + (hosts.Count == 0 ? "no service extension" : string.Join(", ", Names(hosts)))
                    + ". Choose in Window > OpenPush > Setup (Reuse means " + reuse + "), or set the fields in OpenPushSettings.");

            var message = decision.Message + "\n\n" + (host == null
                ? "Use OpenPush's own extension instead, or turn OpenPush's extension off?"
                : host.Target + (host.Vendor != null ? " was made by your previous push SDK. Reuse makes it OpenPush's extension "
                                                       + "and keeps its bundle id, App Group and provisioning profile."
                                                     : " has code that is not a known push SDK template. Reuse keeps that code and "
                                                       + "adds OpenPush's lines first in it (the original is backed up)."));
            var answer = EditorUtility.DisplayDialogComplex("OpenPush notification service extension", message,
                host == null ? "Use OpenPush's" : "Reuse " + host.Target, "Cancel build", "Turn off OpenPush's extension");
            if (answer == 1) throw new BuildFailedException(decision.Message + " The build was cancelled.");

            var mode = answer == 2 ? OpenPushIosExtensionMode.Off : host == null ? OpenPushIosExtensionMode.Inject : decision.Suggested;
            OpenPushPlumbingAnswers.SetServiceExtension(settings, mode, host);
            // Reuse is the default for the App Group too, when it was never answered: the group the app and
            // the reused extension already share.
            if (host != null && mode != OpenPushIosExtensionMode.Off && string.IsNullOrWhiteSpace(settings.AppGroupIdentifier)
                && !OpenPushPlumbingAnswers.Answered(OpenPushPlumbingAnswers.AppGroupChoice)
                && OpenPushAppGroupReuse.Resolve(appGroups, new[] { (IReadOnlyList<string>)host.Groups }, out _) is string group
                && host.Groups.Count > 0)
                OpenPushPlumbingAnswers.SetAppGroup(settings, OpenPushPlumbingSource.Reused, group);
            var next = OpenPushExtensionModeDecision.Decide(settings.InjectServiceExtension, settings.IosExtensionMode,
                                                           settings.IosExtensionTarget, hosts, ownExists);
            if (next.Action == OpenPushExtensionAction.Ask) throw new BuildFailedException(next.Message);
            return next;
        }

        /// <summary>
        /// The case earlier builds settled on their own: the default Inject, never answered in Setup, and an
        /// export that already has a service extension of its own. They skipped OpenPush's extension.
        /// </summary>
        internal static bool KeepsTheProjectsOwnExtension(OpenPushSettings settings, IReadOnlyList<OpenPushXcodeExtension> hosts)
            => hosts.Count > 0 && settings.IosExtensionMode == OpenPushIosExtensionMode.Inject
               && !OpenPushPlumbingAnswers.Answered(OpenPushPlumbingAnswers.ServiceExtensionChoice);

        private static IEnumerable<string> Names(IEnumerable<OpenPushXcodeExtension> hosts)
        {
            foreach (var h in hosts) yield return h.Target + (h.BundleId != null ? " (" + h.BundleId + ")" : "");
        }
    }
}
#endif
