using System.Linq;
using UnityEditor;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The bundle ids and provisioning profiles of the extensions an iOS export gets. Fails on an id that
    /// is not under the app's, and, when the app signs manually, on an injected extension without a
    /// profile: a batch build stops on exactly that.
    /// </summary>
    public sealed class IosExtensionIdentifiers : OpenPushSetupCheck
    {
        public override int Order => 51;
        public override string Title => "iOS extension identifiers and signing";

        private static string AppBundleId => PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);

        /// The app signs manually with a profile from Player Settings (see OpenPushExtensionSigning).
        private static bool AppSignsManually => !PlayerSettings.iOS.appleEnableAutomaticSigning
            && !string.IsNullOrWhiteSpace(PlayerSettings.iOS.iOSManualProvisioningProfileID);

        private static System.Collections.Generic.List<string> Problems()
        {
            var s = OpenPushSetupAssets.Loadable();
            if (s == null) return new System.Collections.Generic.List<string>();
            return OpenPushIOSIdentifiers.Problems(AppBundleId, AppSignsManually,
                s.InjectServiceExtension, s.ServiceExtensionBundleIdentifier, s.ServiceExtensionProvisioningProfile,
                s.InjectLiveActivityWidget, s.LiveActivityWidgetBundleIdentifier, s.LiveActivityWidgetProvisioningProfile,
                s.IosExtensionMode, ReusedTargetProfile(s));
        }

        /// The profile the last export recorded for the extension TakeOver or Share reuses.
        private static string ReusedTargetProfile(OpenPushSettings s)
        {
            var extensions = OpenPushPlumbingAnswers.File().Found.Where(f => f.Kind == OpenPushSetupFinding.ServiceExtension).ToList();
            var target = string.IsNullOrWhiteSpace(s.IosExtensionTarget)
                ? (extensions.Count == 1 ? extensions[0] : null)
                : extensions.FirstOrDefault(f => f.Id == s.IosExtensionTarget.Trim());
            return target?.Detail("profile");
        }

        public override bool Passes() => Problems().Count == 0;

        public override string Detail
        {
            get
            {
                var problems = Problems();
                if (problems.Count > 0) return string.Join(" ", problems);
                var s = OpenPushSetupAssets.Loadable();
                if (s == null) return "No settings asset yet; extensions use the default ids.";
                string Describe(bool on, string id, string profile)
                    => !on ? "off" : id + (OpenPushIOSIdentifiers.Optional(profile) is string p ? " (profile " + p + ")" : "");
                var mode = s.IosExtensionMode;
                var service = !s.InjectServiceExtension || mode == OpenPushSDK.Core.OpenPushIosExtensionMode.Off ? "off"
                    : mode == OpenPushSDK.Core.OpenPushIosExtensionMode.Inject
                        ? Describe(true, OpenPushIOSIdentifiers.ServiceExtension(s.ServiceExtensionBundleIdentifier, AppBundleId), s.ServiceExtensionProvisioningProfile)
                        : mode + " " + (OpenPushIOSIdentifiers.Optional(s.IosExtensionTarget) ?? "the existing extension")
                          + (ReusedTargetProfile(s) is string kept ? " (keeps its profile " + kept + ")" : "");
                return "Service extension: " + service
                     + ". Live Activity widget: "
                     + Describe(s.InjectLiveActivityWidget, OpenPushIOSIdentifiers.Widget(s.LiveActivityWidgetBundleIdentifier, AppBundleId), s.LiveActivityWidgetProvisioningProfile)
                     + ". Signing: " + (AppSignsManually ? "manual" : "automatic or set up after export")
                     + ". Blank ids use the defaults; existing signing values in the export are kept.";
            }
        }

        public override bool CanFix => true;
        public override string FixLabel => "Select";
        public override void Fix() => OpenPushSetupAssets.Reveal();
    }
}
