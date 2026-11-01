using System;
using OpenPushSDK.Platform;
using UnityEngine;
using UnityEngine.Scripting;

namespace OpenPushSDK.Android
{
    /// <summary>Implements ai.openpush.unity.OpenPushUnityListener. Called on native threads.</summary>
    internal sealed class AndroidEventListenerProxy : AndroidJavaProxy
    {
        private readonly OpenPushNativeEventRouter _router;
        [Preserve]
        internal AndroidEventListenerProxy(OpenPushNativeEventRouter router) : base("ai.openpush.unity.OpenPushUnityListener") { _router = router; }
        [Preserve]
        public void onEvent(string kind, string json) => _router.Route(kind, json);
    }

    /// <summary>Implements ai.openpush.unity.OpenPushUnityCallback for one async call.</summary>
    internal sealed class AndroidCallbackProxy : AndroidJavaProxy
    {
        private readonly Action<string> _onResult;
        [Preserve]
        internal AndroidCallbackProxy(Action<string> onResult) : base("ai.openpush.unity.OpenPushUnityCallback") { _onResult = onResult; }
        [Preserve]
        public void onResult(string json)
        {
            try { _onResult?.Invoke(json); } catch (Exception) { /* never unwind into Java */ }
        }
    }
}
