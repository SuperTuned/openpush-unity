using System;
using UnityEditor;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Writes the app id and SDK key copied from the console into the settings asset: from the clipboard,
    /// or, in a batch run where there is none, from the OPENPUSH_SETUP environment variable (same formats).
    /// </summary>
    internal static class OpenPushPastedSetup
    {
        internal const string EnvironmentVariable = "OPENPUSH_SETUP";

        private static string Source()
        {
            var clipboard = Application.isBatchMode ? null : EditorGUIUtility.systemCopyBuffer;
            return string.IsNullOrWhiteSpace(clipboard) ? Environment.GetEnvironmentVariable(EnvironmentVariable) : clipboard;
        }

        internal static bool Available(out OpenPushSetupValues values)
            => OpenPushSetupPaste.TryParse(Source(), out values, out _);

        internal static bool Apply(OpenPushSettings settings, out string problem)
        {
            if (!OpenPushSetupPaste.TryParse(Source(), out var values, out problem)) return false;
            settings.AppId = values.AppId;
            settings.SdkKey = values.SdkKey;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("OpenPush: app id " + values.AppId + " and the SDK key were pasted into the settings asset.");
            if (values.Server != null && values.Server != OpenPushSDK.Core.OpenPushConfiguration.DefaultServerUrl)
                Debug.LogWarning("OpenPush: the copied setup names the server " + values.Server + ". The settings asset has no "
                               + "server field: pass it in code, OpenPush.Initialize(appId, sdkKey, \"" + values.Server + "\").");
            return true;
        }
    }
}
