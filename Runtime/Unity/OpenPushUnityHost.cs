using OpenPushSDK.Core;
using UnityEngine;

namespace OpenPushSDK
{
    /// <summary>
    /// Wires the Unity-free facade to Unity: settings from Resources, the Demo in-app preview in the
    /// Editor and desktop players, and credential refresh on focus. Runs after every platform
    /// assembly registered itself (SubsystemRegistration), and touches OpenPush.Platform so native
    /// events are wired before the first scene can miss a cold-start click.
    /// </summary>
    internal static class OpenPushUnityHost
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            OpenPushHost.Settings = () => OpenPushSettings.Load()?.Snapshot();
            OpenPushHost.ReportError = Debug.LogError;
#if UNITY_EDITOR || !(UNITY_ANDROID || UNITY_IOS)
            OpenPushHost.ShowDemoInAppFixture = OpenPushDemoInApp.Show;
#endif
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            OpenPushHost.LegacyPrefs = new PlayerPrefsLegacyPrefs();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            OpenPushHost.DevicePlatformAssembly = "OpenPush.Android";
#elif UNITY_IOS && !UNITY_EDITOR
            OpenPushHost.DevicePlatformAssembly = "OpenPush.iOS";
#endif
            var _ = OpenPush.Platform;
            OpenPush.ReportStrippedPlatform();
            var host = new GameObject("OpenPush") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(host);
            host.AddComponent<OpenPushFocusHook>();
        }
    }

    internal sealed class OpenPushFocusHook : MonoBehaviour
    {
        private void OnApplicationFocus(bool focused)
        {
            if (focused) OpenPush.RefreshCredential();
        }
    }
}
