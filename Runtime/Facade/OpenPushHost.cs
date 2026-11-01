using System;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK
{
    /// <summary>The project settings the facade needs, read once per call from the Resources asset.</summary>
    internal sealed class OpenPushSettingsSnapshot
    {
        public string SdkKey;
        public string AppId;
        /// <summary>Already resolved (host group or group.&lt;bundle&gt;.openpush); null when no extension is injected.</summary>
        public string AppGroup;
        public OpenPushAndroidPushDelivery AndroidPushDelivery;
        public bool ReloginWithoutAuthHash = true;
    }

    /// <summary>
    /// Seams the Unity host (Runtime/Unity/OpenPushUnityHost) fills in at startup, so the facade
    /// itself has no Unity engine dependency and runs under dotnet test.
    /// </summary>
    internal static class OpenPushHost
    {
        internal static IOpenPushDispatcher Dispatcher = ImmediateDispatcher.Instance;
        internal static Func<OpenPushSettingsSnapshot> Settings = () => null;
        internal static Action ShowDemoInAppFixture;
        /// <summary>The Unity console (Debug.LogError) in a player; null under dotnet test.</summary>
        internal static Action<string> ReportError;
        /// <summary>PlayerPrefs in iOS/Android players, where an earlier build's state can exist; null elsewhere.</summary>
        internal static IOpenPushLegacyPrefs LegacyPrefs;
        /// <summary>
        /// In an iOS/Android device player, the platform assembly that must be registered
        /// ("OpenPush.iOS" / "OpenPush.Android"); null in the Editor and desktop players, where the stub is expected.
        /// </summary>
        internal static string DevicePlatformAssembly;

        internal static void Reset()
        {
            Dispatcher = ImmediateDispatcher.Instance;
            Settings = () => null;
            ShowDemoInAppFixture = null;
            ReportError = null;
            LegacyPrefs = null;
            DevicePlatformAssembly = null;
        }
    }
}
