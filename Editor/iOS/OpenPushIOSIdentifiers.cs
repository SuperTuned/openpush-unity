using System.Text.RegularExpressions;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The bundle ids of the extensions OpenPush adds to an iOS export. A blank setting keeps the
    /// default name; a filled one must sit under the app's bundle id, which is what Xcode requires of
    /// an embedded extension. Shared by the injectors and the Setup row, so both say the same thing.
    /// </summary>
    internal static class OpenPushIOSIdentifiers
    {
        internal const string ServiceExtensionTarget = "OpenPushNotificationService";
        internal const string WidgetTarget = "OpenPushLiveActivityWidget";
        internal const string WidgetSuffix = "LiveActivityWidget";

        internal const string ServiceExtensionBundleField = "ServiceExtensionBundleIdentifier";
        internal const string ServiceExtensionProfileField = "ServiceExtensionProvisioningProfile";
        internal const string WidgetBundleField = "LiveActivityWidgetBundleIdentifier";
        internal const string WidgetProfileField = "LiveActivityWidgetProvisioningProfile";

        private static readonly Regex BundleIdCharacters = new Regex("^[A-Za-z0-9.-]+$");

        /// <summary>The configured id, trimmed, or the app's id plus <paramref name="defaultSuffix"/>.</summary>
        internal static string Resolve(string configured, string appBundleId, string defaultSuffix)
            => string.IsNullOrWhiteSpace(configured) ? appBundleId + "." + defaultSuffix : configured.Trim();

        internal static string ServiceExtension(string configured, string appBundleId)
            => Resolve(configured, appBundleId, ServiceExtensionTarget);

        internal static string Widget(string configured, string appBundleId)
            => Resolve(configured, appBundleId, WidgetSuffix);

        /// <summary>A trimmed value, or null for blank.</summary>
        internal static string Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>
        /// Null when <paramref name="configured"/> is blank (the default is used) or is a valid id under
        /// <paramref name="appBundleId"/>; otherwise a sentence naming <paramref name="field"/>.
        /// </summary>
        internal static string BundleIdProblem(string field, string configured, string appBundleId)
        {
            if (string.IsNullOrWhiteSpace(configured)) return null;
            var value = configured.Trim();
            var prefix = (appBundleId ?? "").Trim() + ".";
            if (!BundleIdCharacters.IsMatch(value) || value.EndsWith(".") || value.Contains(".."))
                return "OpenPushSettings." + field + " \"" + value + "\" is not a bundle id (letters, digits, '-' and '.' only).";
            if (prefix.Length == 1)
                return "OpenPushSettings." + field + " is set, but the app has no bundle id to check it against.";
            if (!value.StartsWith(prefix) || value.Length == prefix.Length)
                return "OpenPushSettings." + field + " \"" + value + "\" must start with the app's bundle id and a dot ("
                     + prefix + "…): Xcode only embeds an extension whose id sits under the app's.";
            return null;
        }

        /// <summary>
        /// What the Setup row reports: every bundle id problem, and, when the app signs manually, each
        /// injected extension that would have no profile (a batch build fails on it). Empty when fine.
        /// </summary>
        internal static System.Collections.Generic.List<string> Problems(
            string appBundleId, bool appSignsManually,
            bool injectService, string serviceBundleId, string serviceProfile,
            bool injectWidget, string widgetBundleId, string widgetProfile,
            OpenPushSDK.Core.OpenPushIosExtensionMode serviceMode = OpenPushSDK.Core.OpenPushIosExtensionMode.Inject,
            string reusedTargetProfile = null)
        {
            var problems = new System.Collections.Generic.List<string>();
            void Add(string problem) { if (problem != null) problems.Add(problem); }
            // Off changes nothing, so nothing has to sign; TakeOver and Share keep the existing target's own
            // profile (as the last export recorded it), so the setting is only needed when it had none.
            if (injectService && serviceMode != OpenPushSDK.Core.OpenPushIosExtensionMode.Off)
            {
                var reused = serviceMode == OpenPushSDK.Core.OpenPushIosExtensionMode.TakeOver
                          || serviceMode == OpenPushSDK.Core.OpenPushIosExtensionMode.Share;
                Add(BundleIdProblem(ServiceExtensionBundleField, serviceBundleId, appBundleId));
                if (appSignsManually && Optional(serviceProfile) == null && !(reused && Optional(reusedTargetProfile) != null))
                    problems.Add("The app signs manually, so the service extension needs OpenPushSettings."
                               + ServiceExtensionProfileField + " (the profile for " + ServiceExtension(serviceBundleId, appBundleId) + ").");
            }
            if (injectWidget)
            {
                Add(BundleIdProblem(WidgetBundleField, widgetBundleId, appBundleId));
                if (appSignsManually && Optional(widgetProfile) == null)
                    problems.Add("The app signs manually, so the Live Activity widget needs OpenPushSettings."
                               + WidgetProfileField + " (the profile for " + Widget(widgetBundleId, appBundleId) + ").");
            }
            return problems;
        }
    }
}
