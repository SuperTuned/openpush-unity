using OpenPushSDK.Platform;
using UnityEngine;
using UnityEngine.Scripting;

namespace OpenPushSDK.iOS
{
    internal static class IOSPlatformRegistration
    {
        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() => OpenPushPlatformRegistry.Set(new IOSPlatform());
    }
}
