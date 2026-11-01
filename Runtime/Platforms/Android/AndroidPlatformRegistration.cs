using OpenPushSDK.Platform;
using UnityEngine;
using UnityEngine.Scripting;

namespace OpenPushSDK.Android
{
    internal static class AndroidPlatformRegistration
    {
        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => OpenPushPlatformRegistry.Set(new AndroidPlatform());
    }
}
