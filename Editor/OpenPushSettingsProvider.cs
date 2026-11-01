using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Project Settings → OpenPush. Renders exactly the rows the setup window
    /// renders, because it is where a host looks for a package's project-level
    /// configuration before it thinks to look under Window.
    /// </summary>
    internal static class OpenPushSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            SettingsProvider provider = null;
            provider = new SettingsProvider("Project/OpenPush", SettingsScope.Project)
            {
                label = "OpenPush",
                // Opening the page is like focusing the Setup window: re-ask every row. While it
                // is open, repaint only when something a row reads moved.
                activateHandler = (_, __) => OpenPushSetupRows.Reevaluate(),
                inspectorUpdateHandler = () => { if (OpenPushSetupRows.Stale) provider.Repaint(); },
                guiHandler = _ =>
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(8f);
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUILayout.Space();
                            OpenPushSetupRows.Draw(OpenPushSetupCheck.Discover());
                            DrawAndroidDelivery();
                        }
                        GUILayout.Space(8f);
                    }
                },
                keywords = new HashSet<string>
                {
                    "OpenPush", "push", "notifications", "SDK key", "app id",
                    "Live Activity", "service extension", "App Group", "setup", "bundle id", "provisioning profile", "signing",
                    "FCM", "Firebase", "EDM4U", "Android", "launch URL"
                }
            };
            return provider;
        }

        private static void DrawAndroidDelivery()
        {
            // Runs on every IMGUI event: no asset search unless an asset changed.
            var settings = OpenPushSetupAssets.LoadableSinceLastChange();
            if (settings == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Android delivery", EditorStyles.boldLabel);
            var serialized = new SerializedObject(settings);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.AndroidPushDelivery)));
            var modeChanged = EditorGUI.EndChangeCheck();
            using (new EditorGUI.DisabledScope(settings.AndroidPushDelivery != OpenPushSDK.Core.OpenPushAndroidPushDelivery.Automatic))
            {
                EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.AndroidFirebaseProjectId)));
                EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.AndroidFirebaseApplicationId)));
                EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.AndroidFirebaseApiKey)));
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("iOS extension identifiers and signing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.AppGroupIdentifier)));
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.ServiceExtensionBundleIdentifier)));
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.ServiceExtensionProvisioningProfile)));
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.LiveActivityWidgetBundleIdentifier)));
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.LiveActivityWidgetProvisioningProfile)));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Notification taps", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.OpenLaunchUrls)));
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Upgrading from an earlier OpenPush SDK build", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty(nameof(OpenPushSettings.ReloginWithoutAuthHash)));
            // A settings edit re-asks the rows; the project scans that read these values key on them.
            var applied = serialized.ApplyModifiedProperties();
            if (applied && modeChanged) OpenPushAndroidDependencies.WriteNow();
            if (applied) OpenPushSetupRows.Reevaluate();
        }
    }
}
