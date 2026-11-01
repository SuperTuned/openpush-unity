using System;
using System.Collections.Generic;
using OpenPushSDK.Core;
using UnityEditor;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Writes a Reuse / Create new answer: the settings fields a build reads, and the matching
    /// <c>choices</c> entry in ProjectSettings/OpenPushSetup.json, which is how the Setup window knows the
    /// question was answered. The Setup rows and the build dialog both answer through here.
    /// </summary>
    internal static class OpenPushPlumbingAnswers
    {
        /// <summary>Tests point this at a temp file.</summary>
        internal static string SetupFilePath = OpenPushSetupFile.DefaultPath;

        internal const string ServiceExtensionChoice = "service_extension";
        internal const string AppGroupChoice = "app_group";
        internal const string AndroidDeliveryChoice = "android_delivery";
        internal const string IconsChoice = "icons";

        internal static OpenPushSetupFile File() => OpenPushSetupFile.Read(SetupFilePath);

        internal static bool Answered(string choice) => File().HasChoice(choice);

        /// <summary>
        /// The service extension answer. Reuse (TakeOver or Share) also copies the extension's bundle id and
        /// profile into the empty identifier fields, so a later clean export without that extension signs
        /// OpenPush's own with the same App ID.
        /// </summary>
        internal static void SetServiceExtension(OpenPushSettings settings, OpenPushIosExtensionMode mode, OpenPushXcodeExtension host)
            => SetServiceExtension(settings, mode, host?.Target, host?.BundleId, host?.Profile);

        internal static void SetServiceExtension(OpenPushSettings settings, OpenPushIosExtensionMode mode, string target,
                                                 string bundleId, string profile)
        {
            settings.IosExtensionMode = mode;
            settings.IosExtensionTarget = mode == OpenPushIosExtensionMode.TakeOver || mode == OpenPushIosExtensionMode.Share ? target : null;
            if (mode == OpenPushIosExtensionMode.TakeOver || mode == OpenPushIosExtensionMode.Share)
            {
                if (string.IsNullOrWhiteSpace(settings.ServiceExtensionBundleIdentifier) && !string.IsNullOrWhiteSpace(bundleId)
                    && !bundleId.Contains("$("))
                    settings.ServiceExtensionBundleIdentifier = bundleId;
                if (string.IsNullOrWhiteSpace(settings.ServiceExtensionProvisioningProfile) && !string.IsNullOrWhiteSpace(profile)
                    && !profile.Contains("$("))
                    settings.ServiceExtensionProvisioningProfile = profile;
            }
            Save(settings, ServiceExtensionChoice, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["mode"] = ModeName(mode), ["target"] = settings.IosExtensionTarget,
                ["bundle_id"] = OpenPushIOSIdentifiers.Optional(settings.ServiceExtensionBundleIdentifier),
                ["profile"] = OpenPushIOSIdentifiers.Optional(settings.ServiceExtensionProvisioningProfile),
            });
        }

        /// <summary>Reuse: the given group (null leaves it to the export: the one the app and extension share).</summary>
        internal static void SetAppGroup(OpenPushSettings settings, OpenPushPlumbingSource source, string group)
        {
            settings.AppGroupSource = source;
            if (source == OpenPushPlumbingSource.Reused) settings.AppGroupIdentifier = group;
            else if (settings.AppGroupIdentifier == group) settings.AppGroupIdentifier = null;
            Save(settings, AppGroupChoice, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["mode"] = source == OpenPushPlumbingSource.Reused ? "reuse" : "new",
                ["value"] = OpenPushIOSIdentifiers.Optional(settings.AppGroupIdentifier),
            });
        }

        internal static void SetAndroidDelivery(OpenPushSettings settings, OpenPushAndroidPushDelivery delivery, string service)
        {
            settings.AndroidPushDelivery = delivery;
            Save(settings, AndroidDeliveryChoice, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["mode"] = delivery == OpenPushAndroidPushDelivery.Automatic ? "automatic" : "forward",
                ["service"] = service,
            });
        }

        internal static void SetIcons(OpenPushSettings settings, OpenPushPlumbingSource source, string small)
        {
            settings.IconsSource = source;
            settings.AndroidNotificationIconName = source == OpenPushPlumbingSource.Reused ? small : null;
            Save(settings, IconsChoice, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["mode"] = source == OpenPushPlumbingSource.Reused ? "reuse" : "new",
                ["small"] = source == OpenPushPlumbingSource.Reused ? small : null,
                ["large"] = null,
            });
        }

        /// <summary>Forgets an answer, so the Setup row asks again (the Change button).</summary>
        internal static void Forget(string choice)
        {
            var file = File();
            file.RemoveChoice(choice);
            file.Write(SetupFilePath);
        }

        internal static string ModeName(OpenPushIosExtensionMode mode)
        {
            switch (mode)
            {
                case OpenPushIosExtensionMode.TakeOver: return "take_over";
                case OpenPushIosExtensionMode.Share: return "share";
                case OpenPushIosExtensionMode.Off: return "off";
                default: return "inject";
            }
        }

        private static void Save(OpenPushSettings settings, string choice, Dictionary<string, object> value)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            var file = File();
            file.SetChoice(choice, value);
            file.Write(SetupFilePath);
        }
    }
}
