using System;
using UnityEngine.Android;

namespace OpenPushSDK.Android
{
    /// <summary>
    /// POST_NOTIFICATIONS through Unity's own permission API. The native SDK's prompt completes only
    /// through Activity.onRequestPermissionsResult, which UnityPlayerActivity does not forward, so the
    /// answer is handed to the adapter, which updates native state and re-syncs the subscription.
    /// </summary>
    internal static class AndroidPermissionRequester
    {
        private const string PostNotifications = "android.permission.POST_NOTIFICATIONS";

        internal static void Request(bool fallbackToSettings, Action<bool> done)
        {
            var bridge = AndroidBridgeClass.Bridge;
            if (AndroidBridgeClass.SdkInt() < 33)
            {
                done?.Invoke(bridge.CallStatic<bool>("permission"));
                return;
            }
            if (Permission.HasUserAuthorizedPermission(PostNotifications))
            {
                bridge.CallStatic("onPermissionResult", true);
                done?.Invoke(bridge.CallStatic<bool>("permission"));
                return;
            }
            var finished = false;
            void Finish(bool granted)
            {
                if (finished) return;
                finished = true;
                bridge.CallStatic("onPermissionResult", granted);
                done?.Invoke(granted && bridge.CallStatic<bool>("permission"));
            }
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => Finish(true);
            callbacks.PermissionDenied += _ => Finish(false);
#pragma warning disable CS0618 // PermissionDeniedAndDontAskAgain is the only permanent-denial signal on Unity 2022.3
            callbacks.PermissionDeniedAndDontAskAgain += _ =>
            {
                Finish(false);
                if (!fallbackToSettings) return;
                using (var activity = AndroidBridgeClass.Activity()) bridge.CallStatic("openNotificationSettings", activity);
            };
#pragma warning restore CS0618
            Permission.RequestUserPermission(PostNotifications, callbacks);
        }
    }
}
