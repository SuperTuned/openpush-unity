#if UNITY_IOS
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>How the app itself is signed, read once per export.</summary>
    internal sealed class OpenPushSigningContext
    {
        public bool AutomaticSigning;
        public string AppProfile;
        public bool BatchMode;

        internal static OpenPushSigningContext Current() => new OpenPushSigningContext
        {
            AutomaticSigning = PlayerSettings.iOS.appleEnableAutomaticSigning,
            AppProfile = PlayerSettings.iOS.iOSManualProvisioningProfileID,
            BatchMode = Application.isBatchMode,
        };
    }

    /// <summary>
    /// Signing for the extension targets OpenPush creates, so an export signs under the host's own
    /// manual signing instead of failing in xcodebuild. Applied only when the app signs manually (Player
    /// Settings' automatic signing is off and the app has a profile) or a profile setting is filled;
    /// otherwise nothing is written. Then, per configuration: the main target's
    /// CODE_SIGN_STYLE (Manual when it has none, and always Manual with a profile) and CODE_SIGN_IDENTITY,
    /// and PROVISIONING_PROFILE_SPECIFIER only from the setting. A target that existed before this
    /// export keeps every signing value it already has; what was kept is logged.
    /// </summary>
    internal static class OpenPushExtensionSigning
    {
        internal const string Style = "CODE_SIGN_STYLE";
        internal const string Identity = "CODE_SIGN_IDENTITY";
        internal const string ProfileSpecifier = "PROVISIONING_PROFILE_SPECIFIER";
        internal const string Profile = "PROVISIONING_PROFILE";

        /// <summary>
        /// True when the app signs manually with a profile: Player Settings' automatic signing is off and
        /// a profile is named there or as a literal on the main target. Automatic signing off with no
        /// profile anywhere is an export nobody signs in Unity (CI with signing off, or signing set up
        /// later), which leaves signing untouched.
        /// </summary>
        internal static bool AppSignsManually(PBXProject project, string main, OpenPushSigningContext context)
        {
            if (context.AutomaticSigning) return false;
            if (!string.IsNullOrWhiteSpace(context.AppProfile)) return true;
            return Literal(project.GetBuildPropertyForAnyConfig(main, ProfileSpecifier)) != null
                || Literal(project.GetBuildPropertyForAnyConfig(main, Profile)) != null;
        }

        /// <summary>
        /// Writes the signing values to <paramref name="target"/> per configuration. Returns one line per
        /// value an existing target kept, for the build log.
        /// </summary>
        internal static IReadOnlyList<string> Apply(PBXProject project, string main, string target, string targetName,
                                                    string profile, bool appSignsManually, bool targetIsNew)
        {
            var kept = new List<string>();
            profile = OpenPushIOSIdentifiers.Optional(profile);
            if (!appSignsManually && profile == null) return kept;

            foreach (var config in project.BuildConfigNames())
            {
                var extension = project.BuildConfigByName(target, config);
                if (string.IsNullOrEmpty(extension)) continue;
                var app = project.BuildConfigByName(main, config);
                var appStyle = string.IsNullOrEmpty(app) ? null : Literal(project.GetBuildPropertyForConfig(app, Style));
                var appIdentity = string.IsNullOrEmpty(app) ? null : Literal(project.GetBuildPropertyForConfig(app, Identity));

                var hasProfile = !targetIsNew
                    && (Literal(project.GetBuildPropertyForConfig(extension, ProfileSpecifier)) != null
                        || Literal(project.GetBuildPropertyForConfig(extension, Profile)) != null);

                Write(project, extension, targetName, config, Style, profile != null ? "Manual" : appStyle ?? "Manual", targetIsNew, kept);
                if (appIdentity != null) Write(project, extension, targetName, config, Identity, appIdentity, targetIsNew, kept);
                if (profile == null) continue;
                if (hasProfile)
                {
                    kept.Add(targetName + " " + config + ": kept its provisioning profile "
                           + (Literal(project.GetBuildPropertyForConfig(extension, ProfileSpecifier))
                              ?? project.GetBuildPropertyForConfig(extension, Profile)));
                    continue;
                }
                Write(project, extension, targetName, config, ProfileSpecifier, profile, targetIsNew, kept);
            }
            return kept;
        }

        /// <summary>
        /// Null unless the app signs manually and <paramref name="target"/> (null when it is about to be
        /// created) would have no provisioning profile; then the sentence that names the setting to fill.
        /// </summary>
        internal static string MissingProfileProblem(PBXProject project, string target, string targetName, string field,
                                                     string profile, string bundleId, bool appSignsManually)
        {
            if (!appSignsManually || OpenPushIOSIdentifiers.Optional(profile) != null) return null;
            if (!string.IsNullOrEmpty(target) && project.BuildConfigNames().Any(config =>
            {
                var guid = project.BuildConfigByName(target, config);
                return !string.IsNullOrEmpty(guid)
                    && (Literal(project.GetBuildPropertyForConfig(guid, ProfileSpecifier)) != null
                        || Literal(project.GetBuildPropertyForConfig(guid, Profile)) != null);
            })) return null;
            return "OpenPush: the app signs manually, but OpenPushSettings." + field + " is blank, so the "
                 + targetName + " extension (" + bundleId + ") has no provisioning profile and cannot sign. "
                 + "Fill " + field + " with that App ID's profile name or UUID, or turn the extension off.";
        }

        private static void Write(PBXProject project, string config, string targetName, string configName,
                                  string key, string value, bool targetIsNew, List<string> kept)
        {
            if (!targetIsNew)
            {
                var existing = project.GetBuildPropertyForConfig(config, key);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    if (existing.Trim() != value)
                        kept.Add(targetName + " " + configName + ": kept " + key + " = " + existing.Trim());
                    return;
                }
            }
            project.SetBuildPropertyForConfig(config, key, value);
        }

        /// <summary>A literal value, or null for blank or a $(VARIABLE) reference.</summary>
        private static string Literal(string value)
            => string.IsNullOrWhiteSpace(value) || value.Contains("$(") || value.Contains("${") ? null : value.Trim();

        /// <summary>
        /// Fails a batch build, or warns an interactive one, when <see cref="MissingProfileProblem"/> found
        /// one: xcodebuild would otherwise fail later with an error that never names the setting.
        /// </summary>
        internal static void Enforce(string problem, OpenPushSigningContext context)
        {
            if (problem == null) return;
            if (context.BatchMode) throw new UnityEditor.Build.BuildFailedException(problem);
            Debug.LogWarning(problem);
        }

        internal static void LogKept(IReadOnlyList<string> kept)
        {
            if (kept.Count > 0) Debug.Log("OpenPush: kept existing signing values. " + string.Join("; ", kept));
        }
    }
}
#endif
