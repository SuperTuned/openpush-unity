using UnityEngine;

namespace OpenPushSDK.Android
{
    /// <summary>The Java adapter class and the Unity activity, created once.</summary>
    internal static class AndroidBridgeClass
    {
        internal const string Name = "ai.openpush.unity.OpenPushUnityBridge";
        private static AndroidJavaClass _bridge;
        internal static AndroidJavaClass Bridge => _bridge ??= new AndroidJavaClass(Name);

        internal static AndroidJavaObject Activity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return player.GetStatic<AndroidJavaObject>("currentActivity");
        }

        internal static int SdkInt()
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                return version.GetStatic<int>("SDK_INT");
        }
    }
}
