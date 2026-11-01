using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenPushSDK.Core;

namespace OpenPushSDK.Compat
{
    /// <summary>
    /// Optional source-compatibility aliases for OpenPush. Supported members
    /// delegate to the main facade and keep no separate behavior or state.
    /// Unsupported members are compile errors with an OpenPush replacement at
    /// the call site.
    /// </summary>
    public static partial class OneSignal
    {
        public const string DefaultServerUrl = OpenPush.DefaultServerUrl;
        public const string Version = OpenPushVersion.Current;

        /// <summary>
        /// The SDK key comes from Resources/OpenPushSettings, allowing setup with
        /// only the app id.
        /// </summary>
        public static void Initialize(string appId) => OpenPush.Initialize(appId);

        public static void Initialize(string appId, string sdkKey,
                                      string serverUrl = DefaultServerUrl,
                                      Func<string> credential = null) =>
            OpenPush.Initialize(appId, sdkKey, serverUrl, credential);

        public static bool IsInitialized => OpenPush.IsInitialized;
        public static string SdkVersion => OpenPush.SdkVersion;
        public static string CurrentToken => OpenPush.CurrentToken;

        public static void Login(string externalId, string authHash = null,
                                 Action<OpenPushIdentityResult> done = null) =>
            OpenPush.Login(externalId, authHash, done);

        public static void Logout(Action<OpenPushIdentityResult> done = null) =>
            OpenPush.Logout(done);

        public static void Register(string token) => OpenPush.Register(token);

        [Obsolete("Call Login(externalId, authHash) for the identity and Register(token) for the token.")]
        public static void Register(string token, string externalId)
        {
#pragma warning disable CS0618 // Forwards to the deprecated OpenPush overload, which logs a new id in first.
            OpenPush.Register(token, externalId);
#pragma warning restore CS0618
        }

#pragma warning disable CS0618 // OpenPush.Flush is an obsolete no-op; the alias stays callable.
        public static void Flush() => OpenPush.Flush();
#pragma warning restore CS0618

        public static class Session
        {
            public static void AddOutcome(string name) => OpenPush.TrackEvent(name);

            public static void AddUniqueOutcome(string name) =>
                OpenPush.TrackEvent(name, new Dictionary<string, object> { ["unique"] = true });

            public static void AddOutcomeWithValue(string name, float value) =>
                OpenPush.TrackEvent(name, new Dictionary<string, object> { ["value"] = value });
        }

        public static partial class User
        {
            public static void AddTag(string key, string value) =>
                OpenPush.User.AddTag(key, value);

            /// v5 takes a mutable IDictionary; OpenPush takes IReadOnlyDictionary. The
            /// copy here is the only non-forwarding line in this file, and it exists so a
            /// caller's dictionary cannot mutate under the SDK after the call.
            public static void AddTags(IDictionary<string, string> tags) =>
                OpenPush.User.AddTags(tags == null
                    ? null
                    : new Dictionary<string, string>(tags));

            public static void RemoveTag(string key) => OpenPush.User.RemoveTag(key);
            public static void RemoveTags(params string[] keys) =>
                OpenPush.User.RemoveTags(keys);

            /// Returns a mutable copy of the tags.
            public static Dictionary<string, string> GetTags() =>
                new Dictionary<string, string>(OpenPush.User.GetTags());

            public static void AddAlias(string label, string id) =>
                OpenPush.User.AddAlias(label, id);
            public static void AddAliases(IDictionary<string, string> aliases) =>
                OpenPush.User.AddAliases(aliases == null
                    ? null
                    : new Dictionary<string, string>(aliases));
            public static void RemoveAlias(string label) => OpenPush.User.RemoveAlias(label);
            public static void RemoveAliases(params string[] labels) =>
                OpenPush.User.RemoveAliases(labels);

            public static string ExternalId => OpenPush.User.ExternalId;

            /// The number is added unverified: it receives nothing until a code sent with
            /// OpenPush.Verify.StartAsync is approved. Separators are removed as the server does; a number that is still not E.164 is refused with
            /// a log line and nothing is sent. v5 returns nothing, so OpenPush's bool is dropped.
            public static void AddSms(string number) => OpenPush.User.AddSms(number);
            public static void RemoveSms(string number) => OpenPush.User.RemoveSms(number);

            /// AddEmail subscribes the address at once, unless this app's suppression list has it. RemoveEmail removes
            /// the subscription; it is not an unsubscribe. An address that fails the local check is refused with a log
            /// line and nothing is sent. v5 returns nothing, so OpenPush's bool is dropped.
            public static void AddEmail(string email) => OpenPush.User.AddEmail(email);
            public static void RemoveEmail(string email) => OpenPush.User.RemoveEmail(email);

            public static event Action<OpenPushUserState> Changed
            {
                add => OpenPush.User.Changed += value;
                remove => OpenPush.User.Changed -= value;
            }

            public static void TrackEvent(string name,
                                          Dictionary<string, object> properties = null) =>
                OpenPush.TrackEvent(name, properties);

            /// v5 puts Id/Token/OptedIn/OptIn/OptOut/Changed on one object; the accessor
            /// now carries Changed too, so this is a straight forward.
            public static PushSubscriptionAccessor PushSubscription =>
                OpenPush.User.PushSubscription;
        }

        public static partial class Notifications
        {
            public static bool Permission => OpenPush.Notifications.Permission;
            public static bool CanRequestPermission =>
                OpenPush.Notifications.CanRequestPermission;
            public static int NotificationTypes => OpenPush.Notifications.NotificationTypes;

            public static event Action<bool> PermissionChanged
            {
                add => OpenPush.Notifications.PermissionChanged += value;
                remove => OpenPush.Notifications.PermissionChanged -= value;
            }

            public static event Action<OpenPushClickEvent> Clicked
            {
                add => OpenPush.Notifications.Clicked += value;
                remove => OpenPush.Notifications.Clicked -= value;
            }

            public static event Action<OpenPushForegroundEvent> ForegroundWillDisplay
            {
                add => OpenPush.Notifications.ForegroundWillDisplay += value;
                remove => OpenPush.Notifications.ForegroundWillDisplay -= value;
            }

            public static Task<bool> RequestPermissionAsync(bool fallbackToSettings = false) =>
                OpenPush.Notifications.RequestPermissionAsync(fallbackToSettings);

            public static void RequestPermission(bool fallbackToSettings = false,
                                                 Action<bool> done = null) =>
                OpenPush.Notifications.RequestPermission(fallbackToSettings, done);

            public static bool ProcessForeground(IReadOnlyDictionary<string, string> data) =>
                OpenPush.Notifications.ProcessForeground(data);

            public static void ProcessDisplayed(IReadOnlyDictionary<string, string> data) =>
                OpenPush.Notifications.ProcessDisplayed(data);

            public static void ProcessClick(IReadOnlyDictionary<string, string> data) =>
                OpenPush.Notifications.ProcessClick(data);

            public static void OnAppForegrounded() =>
                OpenPush.Notifications.OnAppForegrounded();

            public static void ClearAllNotifications() =>
                OpenPush.Notifications.ClearAll();
        }

        public static class LiveActivities
        {
            public static Task<bool> EnterAsync(string activityId, string token)
            {
                var completion = new TaskCompletionSource<bool>();
                OpenPush.LiveActivities.Enter(activityId, token,
                    ok => completion.TrySetResult(ok));
                return completion.Task;
            }

            [Obsolete("Kept for source compatibility; this unregisters the update token server-side.", false)]
            public static Task<bool> ExitAsync(string activityId)
            {
                var completion = new TaskCompletionSource<bool>();
                OpenPush.LiveActivities.Exit(activityId,
                    ok => completion.TrySetResult(ok));
                return completion.Task;
            }

            public static void SetupDefault(LiveActivitySetupOptions options = null)
                => OpenPush.LiveActivities.SetupDefault(options);
            public static void StartDefault(string activityId,
                                            IDictionary<string, object> attributes,
                                            IDictionary<string, object> content)
                => OpenPush.LiveActivities.StartDefault(activityId, attributes, content);
            public static void SetPushToStartToken(string activityType, string token)
                => OpenPush.LiveActivities.SetPushToStartToken(activityType, token);
            public static void RemovePushToStartToken(string activityType)
                => OpenPush.LiveActivities.RemovePushToStartToken(activityType);
        }
    }
}
