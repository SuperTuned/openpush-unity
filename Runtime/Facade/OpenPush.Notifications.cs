using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK
{
    public static partial class OpenPush
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyData = new Dictionary<string, string>();

        /// <summary>
        /// Notification permission and delivery. The native SDK captures taps and foreground
        /// arrivals itself (Android with automatic FCM; iOS through the package's delegate capture).
        /// The Process* calls are for hosts that own delivery (Android HostOwned mode).
        /// </summary>
        public static class Notifications
        {
            public static bool Permission => Platform.Notifications.Permission;

            /// <summary>notification_types before the explicit opt-out is applied; positive means permitted.</summary>
            public static int NotificationTypes => OpenPushNotificationTypesCalculator.Compute(
                Platform.Notifications.Permission, Platform.Notifications.CanRequestPermission, Platform.PushSubscription.Status);

            public static bool CanRequestPermission => Platform.Notifications.CanRequestPermission;

            public static event Action<bool> PermissionChanged
            { add => Hub.PermissionChanged.Subscribe(value); remove => Hub.PermissionChanged.Unsubscribe(value); }

            public static event Action<OpenPushClickEvent> Clicked
            { add => Hub.Clicked.Subscribe(value); remove => Hub.Clicked.Unsubscribe(value); }

            /// <summary>
            /// Unlike the other events this one is not buffered: with no handler the notification is
            /// displayed at once, and a handler added later never sees it.
            /// </summary>
            public static event Action<OpenPushForegroundEvent> ForegroundWillDisplay
            { add => Hub.Foreground.Subscribe(value); remove => Hub.Foreground.Unsubscribe(value); }

            /// <summary>
            /// Background (data-only) pushes, Android only: a push sent with <c>op_background</c> =
            /// <c>"1"</c> and no title or body is never displayed and arrives here instead, on the
            /// Unity main thread, after the SDK has posted its <c>received</c> receipt. While the game
            /// is paused in the background, handlers run when it resumes. A push that arrives before
            /// <see cref="OpenPush.Initialize"/> has run in this process (for example one that starts
            /// a killed app) is logged and dropped by the native SDK. Values raised before the first
            /// handler is added are buffered (newest ten) and replayed to it. iOS background pushes
            /// (<c>content-available</c>) are not raised here.
            /// </summary>
            public static event Action<OpenPushBackgroundMessage> BackgroundMessageReceived
            { add => Hub.BackgroundMessage.Subscribe(value); remove => Hub.BackgroundMessage.Unsubscribe(value); }

            /// <summary>
            /// Forward a payload received while foregrounded (host-owned delivery). Handlers run now,
            /// on this (main) thread, exactly once; the platform then posts only the received
            /// receipt. Returns true when a handler called PreventDefault, or when this push was
            /// already handled on this device (a copy the server re-sent): either way, do not display it.
            /// A background (data-only) push skips the foreground handlers, goes to
            /// <see cref="BackgroundMessageReceived"/> (Android), and returns true: never display it.
            /// </summary>
            public static bool ProcessForeground(IReadOnlyDictionary<string, string> data)
            {
                RequireInitialized("Notifications.ProcessForeground");
                var payload = data ?? EmptyData;
                var json = OpenPushWire.StringMapToJson(payload);
                if (!Platform.Notifications.BeginReceived(json)) return true;
                var handled = false;
                try
                {
                    var notification = OpenPushNotification.From(payload);
                    if (notification.IsBackground)
                    {
                        Platform.Notifications.ProcessForeground(json);
                        handled = true;
                        return true;
                    }
                    var local = new OpenPushForegroundEvent
                    { MessageId = MessageId(payload), Data = payload, Notification = notification };
                    Hub.Foreground.DeliverNow(local);
                    Platform.Notifications.ProcessForeground(json);
                    handled = true;
                    return local.DefaultPrevented;
                }
                finally
                {
                    Platform.Notifications.FinishReceived(json, handled);
                }
            }

            /// <summary>Records that a host renderer posted this notification (Android host-owned delivery).</summary>
            public static void ProcessDisplayed(IReadOnlyDictionary<string, string> data)
            {
                RequireInitialized("Notifications.ProcessDisplayed");
                Platform.Notifications.ProcessDisplayed(OpenPushWire.StringMapToJson(data ?? EmptyData));
            }

            /// <summary>
            /// Forward a tapped payload from a host-owned integration. The platform posts the
            /// clicked receipt and raises <see cref="Clicked"/> once; this call raises nothing itself.
            /// </summary>
            public static void ProcessClick(IReadOnlyDictionary<string, string> data)
            {
                RequireInitialized("Notifications.ProcessClick");
                Platform.Notifications.ProcessClick(OpenPushWire.StringMapToJson(data ?? EmptyData));
            }

            public static void RequestPermission(bool fallbackToSettings = false, Action<bool> done = null)
            {
                RequireInitialized("Notifications.RequestPermission");
                Platform.Notifications.RequestPermission(fallbackToSettings,
                    granted => OpenPushHost.Dispatcher.Post(() => done?.Invoke(granted)));
            }

            public static Task<bool> RequestPermissionAsync(bool fallbackToSettings = false)
            {
                var completion = new TaskCompletionSource<bool>();
                RequestPermission(fallbackToSettings, granted => completion.TrySetResult(granted));
                return completion.Task;
            }

            /// <summary>iOS provisional (quiet) authorization; reports false, changing nothing, elsewhere.</summary>
            public static void RequestProvisionalPermission(Action<bool> done = null)
            {
                RequireInitialized("Notifications.RequestProvisionalPermission");
                Platform.Notifications.RequestProvisionalPermission(
                    granted => OpenPushHost.Dispatcher.Post(() => done?.Invoke(granted)));
            }

            public static void OnAppForegrounded()
            {
                RequireInitialized("Notifications.OnAppForegrounded");
                Platform.Core.OnAppForegrounded();
            }

            /// <summary>Removes this app's delivered notifications from the shade/centre.</summary>
            public static void ClearAll() { RequireInitialized("Notifications.ClearAll"); Platform.Notifications.ClearAll(); }

            /// <summary>
            /// Removes delivered notifications for an OpenPush message. Android removes what the
            /// OpenPush renderer posted (automatic FCM); iOS matches op_message_id in the centre.
            /// </summary>
            public static void Remove(string messageId)
            {
                RequireInitialized("Notifications.Remove");
                if (string.IsNullOrEmpty(messageId)) return;
                Platform.Notifications.Remove(messageId);
            }

            /// <summary>Sets the app icon badge (iOS). A no-op on other platforms.</summary>
            public static void SetBadgeCount(int count)
            {
                RequireInitialized("Notifications.SetBadgeCount");
                Platform.Notifications.SetBadgeCount(Math.Max(0, count));
            }
        }
    }
}
