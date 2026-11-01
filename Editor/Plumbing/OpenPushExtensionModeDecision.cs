using System.Collections.Generic;
using System.Linq;
using OpenPushSDK.Core;

namespace OpenPushSDK.Editor
{
    /// <summary>What the service-extension step does with one export.</summary>
    internal enum OpenPushExtensionAction
    {
        /// <summary>Add OpenPush's own extension.</summary>
        Inject,
        /// <summary>OpenPush's own extension is already there (an append export): refresh it.</summary>
        RefreshOwn,
        TakeOver,
        Share,
        /// <summary>Change nothing.</summary>
        Off,
        /// <summary>The export does not match the stored answer: ask once (interactive) or fail (batch).</summary>
        Ask,
        /// <summary>Two service extensions; no answer fixes it. The build stops.</summary>
        Conflict,
    }

    internal sealed class OpenPushExtensionDecision
    {
        public OpenPushExtensionAction Action;
        /// <summary>The existing extension TakeOver, Share or Ask is about.</summary>
        public OpenPushXcodeExtension Target;
        /// <summary>For Ask: the mode Reuse means for that extension (TakeOver for a known vendor's, else Share).</summary>
        public OpenPushIosExtensionMode Suggested;
        public string Message;
    }

    /// <summary>
    /// The mode switch that replaced the early return on an existing service extension. The stored answer
    /// (<see cref="OpenPushSettings.IosExtensionMode"/> and <see cref="OpenPushSettings.IosExtensionTarget"/>)
    /// is checked against the real export, which always wins: when they differ, nothing is guessed.
    /// </summary>
    internal static class OpenPushExtensionModeDecision
    {
        internal static OpenPushExtensionDecision Decide(bool injectServiceExtension, OpenPushIosExtensionMode mode,
                                                         string storedTarget, IReadOnlyList<OpenPushXcodeExtension> hostExtensions,
                                                         bool ownExists)
        {
            if (!injectServiceExtension || mode == OpenPushIosExtensionMode.Off)
                return new OpenPushExtensionDecision { Action = OpenPushExtensionAction.Off };
            var target = string.IsNullOrWhiteSpace(storedTarget) ? null : storedTarget.Trim();

            if (hostExtensions.Count == 0)
            {
                if (ownExists) return new OpenPushExtensionDecision { Action = OpenPushExtensionAction.RefreshOwn };
                if (mode == OpenPushIosExtensionMode.Share)
                    return Ask(null, "the stored answer is Share " + (target ?? "the existing extension")
                             + ", but this export has no notification service extension");
                // TakeOver of an extension a clean export no longer has (its SDK was removed): OpenPush's own,
                // under the identifiers the Reuse answer copied into the settings.
                return new OpenPushExtensionDecision { Action = OpenPushExtensionAction.Inject };
            }

            var names = string.Join(", ", hostExtensions.Select(e => e.Target));
            if (ownExists)
                return new OpenPushExtensionDecision
                {
                    Action = OpenPushExtensionAction.Conflict,
                    Message = "This export has two notification service extensions, OpenPush's " + OpenPushIOSIdentifiers.ServiceExtensionTarget
                            + " and " + names + ", and iOS runs only one. Something added " + names + " after OpenPush's step "
                            + "(usually your previous push SDK's build step): remove that SDK first (Previous push SDK row in "
                            + "Window > OpenPush > Setup).",
                };
            if (hostExtensions.Count > 1)
                return Ask(hostExtensions[0], "this export has several notification service extensions (" + names + ")");

            var host = hostExtensions[0];
            if (mode == OpenPushIosExtensionMode.Inject)
                return Ask(host, "this export already has a notification service extension, " + host.Target
                             + ", and the stored answer is Inject (iOS runs only one)");
            if (target != null && target != host.Target)
                return Ask(host, "the stored answer is " + mode + " " + target + ", but this export's service extension is " + host.Target);
            return new OpenPushExtensionDecision
            {
                Action = mode == OpenPushIosExtensionMode.TakeOver ? OpenPushExtensionAction.TakeOver : OpenPushExtensionAction.Share,
                Target = host,
            };
        }

        /// <summary>Reuse means TakeOver for a known vendor's template and Share for anything else.</summary>
        internal static OpenPushIosExtensionMode ReuseMode(OpenPushXcodeExtension extension)
            => extension?.Vendor != null ? OpenPushIosExtensionMode.TakeOver : OpenPushIosExtensionMode.Share;

        private static OpenPushExtensionDecision Ask(OpenPushXcodeExtension host, string message)
            => new OpenPushExtensionDecision
            {
                Action = OpenPushExtensionAction.Ask, Target = host, Suggested = ReuseMode(host),
                Message = "OpenPush: " + message + ".",
            };
    }

    /// <summary>
    /// The App Group to reuse when <see cref="OpenPushSettings.AppGroupSource"/> is Reused and no group is
    /// named: the one the app and its existing extension share; with no extension, the app's only group.
    /// </summary>
    internal static class OpenPushAppGroupReuse
    {
        internal static string Resolve(IReadOnlyList<string> appGroups, IEnumerable<IReadOnlyList<string>> extensionGroups,
                                       out string problem)
        {
            problem = null;
            var extensions = extensionGroups.Where(g => g != null && g.Count > 0).ToList();
            var shared = extensions.Count == 0 ? appGroups.Distinct().ToList()
                : appGroups.Where(g => extensions.Any(e => e.Contains(g))).Distinct().ToList();
            if (shared.Count == 1) return shared[0];
            problem = shared.Count == 0
                ? (appGroups.Count == 0
                    ? "AppGroupSource is Reused, but the app has no App Group to reuse."
                    : "AppGroupSource is Reused, but no App Group is on both the app and its existing extension (app: "
                      + string.Join(", ", appGroups) + ").")
                : "AppGroupSource is Reused, but more than one App Group could be the one in use (" + string.Join(", ", shared)
                  + "). Name it in OpenPushSettings.AppGroupIdentifier.";
            return null;
        }
    }
}
