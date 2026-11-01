using System;

namespace OpenPushSDK.Compat
{
    // Every compatibility symbol OpenPush does not implement is declared here with
    // [Obsolete(error: true)], so a migrating host learns the whole gap from one
    // compile with the replacement named at the call site. Nothing here runs.
    //
    // These are `partial` continuations of the real compatibility classes, not a
    // separate bucket: an unsupported member is declared so the
    // compiler refuses (CS0619) while quoting the replacement, exactly as the iOS
    // and Android facades do. A host never has to look a symbol up somewhere else
    // to find out why it is gone.
    //
    // Every member also throws. The attribute is the real mechanism, but a
    // reflective or warning-suppressed caller must not get a silent no-op that
    // reports success while doing nothing.

    // `Initialize(string appId)` is implemented by loading the SDK key from
    // Resources/OpenPushSettings and does not belong among these refusals.

    public static partial class OneSignal
    {
        [Obsolete("OpenPush has no consent gate. Gate your own Initialize(...) call instead; before initialization nothing is collected and no request is made.", true)]
        public static bool ConsentRequired { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        [Obsolete("OpenPush has no consent gate. Call Initialize(...) once the user has consented.", true)]
        public static bool ConsentGiven { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public static class Debug
        {
            [Obsolete("OpenPush has one diagnostic sink, gated by one level: set OpenPush.OnLog to receive lines and OpenPush.LogLevel to choose how many.", true)]
            public static int LogLevel { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            [Obsolete("OpenPush does not display diagnostic alerts. Use OpenPush.OnLog for diagnostics.", true)]
            public static int AlertLevel { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        }

        public static class InAppMessages
        {
            public static bool Paused { get => OpenPushSDK.OpenPush.InAppMessages.Paused; set => OpenPushSDK.OpenPush.InAppMessages.Paused = value; }
            public static void AddTrigger(string key, string value) => OpenPushSDK.OpenPush.InAppMessages.AddTrigger(key, value);
            public static void AddTriggers(System.Collections.Generic.Dictionary<string, string> values)
            {
                if (values == null)
                {
                    OpenPushSDK.OpenPush.InAppMessages.AddTriggers(null);
                    return;
                }
                var converted = new System.Collections.Generic.Dictionary<string, object>(values.Count);
                foreach (var pair in values) converted[pair.Key] = pair.Value;
                OpenPushSDK.OpenPush.InAppMessages.AddTriggers(converted);
            }
            public static void RemoveTrigger(string key) => OpenPushSDK.OpenPush.InAppMessages.RemoveTrigger(key);
            public static void RemoveTriggers(params string[] keys) => OpenPushSDK.OpenPush.InAppMessages.RemoveTriggers(keys);
            public static void ClearTriggers() => OpenPushSDK.OpenPush.InAppMessages.ClearTriggers();
            public static event Action<string, string> Lifecycle
            {
                add => OpenPushSDK.OpenPush.InAppMessages.Lifecycle += value;
                remove => OpenPushSDK.OpenPush.InAppMessages.Lifecycle -= value;
            }
            public static event Action<string, OpenPushSDK.Core.OpenPushInAppClick> Clicked
            {
                add => OpenPushSDK.OpenPush.InAppMessages.Clicked += value;
                remove => OpenPushSDK.OpenPush.InAppMessages.Clicked -= value;
            }

            [Obsolete("Use OpenPush.InAppMessages.Lifecycle and handle the willDisplay phase.", true)]
            public static event Action<string> WillDisplay
            {
                add => throw new NotSupportedException();
                remove => throw new NotSupportedException();
            }

            [Obsolete("Use OpenPush.InAppMessages.Lifecycle and handle the didDisplay phase.", true)]
            public static event Action<string> DidDisplay
            {
                add => throw new NotSupportedException();
                remove => throw new NotSupportedException();
            }

            [Obsolete("Use OpenPush.InAppMessages.Lifecycle and handle the willDismiss phase.", true)]
            public static event Action<string> WillDismiss
            {
                add => throw new NotSupportedException();
                remove => throw new NotSupportedException();
            }

            [Obsolete("Use OpenPush.InAppMessages.Lifecycle and handle the didDismiss phase.", true)]
            public static event Action<string> DidDismiss
            {
                add => throw new NotSupportedException();
                remove => throw new NotSupportedException();
            }
        }

        public static class Location
        {
            [Obsolete("OpenPush does not collect or share location. Store location-derived targeting as a tag or alias when needed.", true)]
            public static bool IsShared { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            [Obsolete("OpenPush never collects location. Send a tag or alias you compute yourself if you need geo targeting.", true)]
            public static void RequestPermission() => throw new NotSupportedException();
        }

        public static partial class User
        {
            [Obsolete("OpenPush has no separate user id. Use OpenPush.User.ExternalId or OpenPush.User.PushSubscription.Id.", true)]
            public static string OneSignalId => throw new NotSupportedException();

            [Obsolete("OpenPush reads the device language and sends it with every registration. To override it, send a language tag with OpenPush.User.AddTag.", true)]
            public static string Language { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            // AddSms, RemoveSms, AddEmail and RemoveEmail are NOT refused: they forward to
            // OpenPush's phone numbers and email addresses and live in OneSignal.cs. Do not add refusals for them here.
        }

        public static partial class Notifications
        {
            [Obsolete("OpenPush exposes the permission grant and notification_types bitmask. A separate native permission enum is unavailable.", true)]
            public static int PermissionNative => throw new NotSupportedException();

            [Obsolete("Use OpenPush.Notifications.ClearAll() / OpenPush.Notifications.Remove(messageId). The OpenPush API takes the op_message_id, not a platform notification id.", true)]
            public static void RemoveNotification(int id) => throw new NotSupportedException();
        }

        // LiveActivities is fully forwarded by the compatibility facade; no
        // compile-time refusal belongs in this file.
    }
}
