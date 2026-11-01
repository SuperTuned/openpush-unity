using System;
using System.Collections.Generic;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK
{
    public static partial class OpenPush
    {
        /// <summary>
        /// In-app messages, fetched, triggered and presented by the native SDK. The Editor shows a
        /// bundled Demo fixture instead (Feature Lab Demo mode).
        /// </summary>
        public static class InAppMessages
        {
            public static bool Paused
            {
                get => Platform.InAppMessages.Paused;
                set { RequireInitialized("InAppMessages.Paused"); Platform.InAppMessages.Paused = value; }
            }

            public static void AddTrigger(string key, object value)
            {
                RequireInitialized("InAppMessages.AddTrigger");
                if (string.IsNullOrEmpty(key)) return;
                AddTriggers(new Dictionary<string, object> { [key] = value });
            }

            public static void AddTriggers(IReadOnlyDictionary<string, object> values)
            {
                RequireInitialized("InAppMessages.AddTriggers");
                if (values == null || values.Count == 0) return;
                Platform.InAppMessages.AddTriggers(OpenPushJson.WriteValueMap(values));
            }

            public static void RemoveTrigger(string key) => RemoveTriggers(key);

            public static void RemoveTriggers(params string[] keys)
            {
                RequireInitialized("InAppMessages.RemoveTriggers");
                if (keys == null || keys.Length == 0) return;
                Platform.InAppMessages.RemoveTriggers(keys);
            }

            public static void ClearTriggers() { RequireInitialized("InAppMessages.ClearTriggers"); Platform.InAppMessages.ClearTriggers(); }

            /// <summary>Asks the native SDK for a fresh eligibility check (it throttles itself).</summary>
            public static void Fetch() { RequireInitialized("InAppMessages.Fetch"); Platform.InAppMessages.Fetch(); }

            /// <summary>Each presentation phase: message id, then willDisplay/didDisplay/willDismiss/didDismiss.</summary>
            public static event Action<string, string> Lifecycle
            {
                add
                {
                    if (value == null) return;
                    var wrapper = InAppLifecycleHandlers.Add(value, e => value(e.MessageId, e.Phase));
                    if (wrapper != null) Hub.InAppLifecycle.Subscribe(wrapper);
                }
                remove
                {
                    var wrapper = InAppLifecycleHandlers.Remove(value);
                    if (wrapper != null) Hub.InAppLifecycle.Unsubscribe(wrapper);
                }
            }

            /// <summary>An action taken inside a message: message id, then the action.</summary>
            public static event Action<string, OpenPushInAppClick> Clicked
            {
                add
                {
                    if (value == null) return;
                    var wrapper = InAppClickHandlers.Add(value, e => value(e.MessageId, e.Click));
                    if (wrapper != null) Hub.InAppClicked.Subscribe(wrapper);
                }
                remove
                {
                    var wrapper = InAppClickHandlers.Remove(value);
                    if (wrapper != null) Hub.InAppClicked.Unsubscribe(wrapper);
                }
            }

            [Obsolete("ResetDisplayState is a no-op: in-app display state lives in the native SDK, which has no reset yet.")]
            public static void ResetDisplayState()
                => Log(OpenPushLogLevel.Warning, "⚠ InAppMessages.ResetDisplayState() is a no-op.");
        }
    }
}
