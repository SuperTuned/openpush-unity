using System;
using System.Collections.Generic;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK
{
    public static partial class OpenPush
    {
        /// <summary>Android Live Updates, rendered by the native live-updates module.</summary>
        public static class LiveUpdates
        {
            public static OpenPushLiveUpdateResult HandleAndroidPayload(IReadOnlyDictionary<string, string> data, string collapseId = null)
            {
                RequireInitialized("LiveUpdates.HandleAndroidPayload");
                return Platform.LiveUpdates.HandleAndroidPayload(OpenPushWire.StringMapToJson(data), collapseId);
            }

            public static OpenPushLiveUpdateCapability Capability => Platform.LiveUpdates.Capability;

            public static bool OpenPromotionSettings()
            {
                RequireInitialized("LiveUpdates.OpenPromotionSettings");
                return Platform.LiveUpdates.OpenPromotionSettings();
            }
        }

        /// <summary>iOS ActivityKit Live Activities through the native module. No send surface.</summary>
        public static class LiveActivities
        {
            public static void SetupDefault(LiveActivitySetupOptions options = null)
            {
                RequireInitialized("LiveActivities.SetupDefault");
                Platform.LiveActivities.SetupDefault(options ?? new LiveActivitySetupOptions());
            }

            public static void StartDefault(string activityId, IDictionary<string, object> attributes, IDictionary<string, object> content)
            {
                RequireInitialized("LiveActivities.StartDefault");
                Platform.LiveActivities.StartDefault(activityId,
                    OpenPushJson.WriteValueMap(attributes ?? new Dictionary<string, object>()),
                    OpenPushJson.WriteValueMap(content ?? new Dictionary<string, object>()));
            }

            public static void Enter(string activityId, string token, Action<bool> done = null)
                => Enter(activityId, token, null, done);

            public static void Enter(string activityId, string token, string activityType, Action<bool> done = null)
            {
                RequireInitialized("LiveActivities.Enter");
                Platform.LiveActivities.Enter(activityId, token, activityType, OnMain(done));
            }

            public static void Exit(string activityId, Action<bool> done = null)
            {
                RequireInitialized("LiveActivities.Exit");
                Platform.LiveActivities.Exit(activityId, OnMain(done));
            }

            public static void SetPushToStartToken(string activityType, string token, Action<bool> done = null)
            {
                RequireInitialized("LiveActivities.SetPushToStartToken");
                Platform.LiveActivities.SetPushToStartToken(activityType, token, OnMain(done));
            }

            /// <summary>The token argument is accepted for source compatibility; the native call needs only the type.</summary>
            public static void RemovePushToStartToken(string activityType, string token = null, Action<bool> done = null)
            {
                RequireInitialized("LiveActivities.RemovePushToStartToken");
                Platform.LiveActivities.RemovePushToStartToken(activityType, OnMain(done));
            }

            private static Action<bool> OnMain(Action<bool> done)
                => done == null ? (Action<bool>)(_ => { }) : ok => OpenPushHost.Dispatcher.Post(() => done(ok));
        }
    }
}
