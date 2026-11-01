using System;
using System.Collections.Generic;
using OpenPushSDK;
using OpenPushSDK.Core;
using UnityEngine;

namespace OpenPushFeatureLab
{
    internal interface IFeatureLabRuntime : IDisposable
    {
        event Action<LabEvent> EventRaised;
        event Action SnapshotChanged;
        LabSnapshot Snapshot { get; }
        void Initialize(string appId, string sdkKey);
        void RequestPermission();
        void SetPushEnabled(bool enabled);
        void Login(string externalId);
        void Logout();
        void AddTag(string key, string value);
        void RemoveTag(string key);
        void AddAlias(string label, string value);
        void RemoveAlias(string label);
        void PreviewNotification(NotificationDraft draft, bool suppressPresentation);
        void PreviewClick(NotificationDraft draft);
        void PreviewPermissionEvent();
        void PreviewSubscriptionEvent();
        void HandleAndroidLiveUpdate(IReadOnlyDictionary<string, string> payload);
        void OpenAndroidLiveUpdateSettings();
        void FetchInAppMessages();
        void ResetInAppDisplayState();
        void AddInAppTrigger(string key, string value);
        void Refresh();
    }

    internal sealed class DemoFeatureLabRuntime : IFeatureLabRuntime
    {
        private readonly LabSnapshot _snapshot = new LabSnapshot
        {
            IsInitialized = true,
            Permission = false,
            NotificationTypes = 0,
            OptedIn = false,
            SubscriptionId = "sub_demo_8F2A",
            Token = "demo-provider-token-8f2a4c",
            SdkVersion = OpenPush.SdkVersion,
            WrapperVersion = OpenPush.WrapperVersion,
            AndroidLiveUpdateCapability = "Demo preview"
        };

        public event Action<LabEvent> EventRaised;
        public event Action SnapshotChanged;
        public LabSnapshot Snapshot => _snapshot;

        public void Initialize(string appId, string sdkKey)
        {
            // In the Editor and desktop players the SDK runs its in-memory stub: no network, and
            // Fetch renders the bundled in-app fixture.
            if (Application.isEditor || (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer))
                OpenPush.Initialize("feature-lab-demo", "demo-key");
            Raise("Demo runtime ready", "SDK writes are simulated; no network request was made.", LabEventKind.Success);
        }

        public void RequestPermission()
        {
            _snapshot.Permission = true;
            _snapshot.PermissionKnown = true;
            _snapshot.NotificationTypes = 1;
            _snapshot.OptedIn = true;
            Raise("Permission observer", "Allowed · notification_types 1", LabEventKind.Success);
            SnapshotChanged?.Invoke();
        }

        public void SetPushEnabled(bool enabled)
        {
            _snapshot.OptedIn = enabled && _snapshot.Permission;
            Raise("Subscription observer", enabled ? "Push enabled" : "Push opted out", enabled ? LabEventKind.Success : LabEventKind.Warning);
            SnapshotChanged?.Invoke();
        }

        public void Login(string externalId)
        {
            _snapshot.ExternalId = externalId;
            Raise("Identity linked", externalId, LabEventKind.Success);
            SnapshotChanged?.Invoke();
        }

        public void Logout()
        {
            _snapshot.ExternalId = null;
            Raise("Identity cleared", "This install is anonymous again.");
            SnapshotChanged?.Invoke();
        }

        public void AddTag(string key, string value) => SetValue(_snapshot.Tags, key, value, "Tag added");
        public void RemoveTag(string key) => RemoveValue(_snapshot.Tags, key, "Tag removed");
        public void AddAlias(string label, string value) => SetValue(_snapshot.Aliases, label, value, "Alias added");
        public void RemoveAlias(string label) => RemoveValue(_snapshot.Aliases, label, "Alias removed");

        public void PreviewNotification(NotificationDraft draft, bool suppressPresentation)
        {
            Raise("Foreground listener", $"{draft.Kind} · {draft.Title}" + (suppressPresentation ? " · presentation suppressed" : " · host would present"), LabEventKind.Success);
        }

        public void PreviewClick(NotificationDraft draft)
            => Raise("Click listener", $"Opened {draft.Kind.ToLowerInvariant()} fixture · feature={draft.DataValue} · launch URL = {draft.LaunchUrl}", LabEventKind.Success);

        public void PreviewPermissionEvent()
            => Raise("Permission observer", _snapshot.Permission ? "Allowed" : "Not determined", LabEventKind.Info);

        public void PreviewSubscriptionEvent()
            => Raise("Subscription observer", _snapshot.OptedIn ? "Opted in" : "Opted out", LabEventKind.Info);

        public void HandleAndroidLiveUpdate(IReadOnlyDictionary<string, string> payload)
            => Raise("Android Live Update", "Demo fixture displayed · order-1234", LabEventKind.Success);

        public void OpenAndroidLiveUpdateSettings()
            => Raise("Promotion settings", "Would open Android notification promotion settings.");

        public void FetchInAppMessages()
        {
            if (OpenPush.IsInitialized) OpenPush.InAppMessages.Fetch();
            Raise("In-app fetch", "Demo fetch · the bundled fixture is shown in the Editor.", LabEventKind.Success);
            SimulateLifecycle("demo-welcome");
        }

        public void ResetInAppDisplayState()
            => Raise("In-app display state reset", "Dismissals and redisplay counters forgotten.", LabEventKind.Warning);

        public void AddInAppTrigger(string key, string value)
        {
            Raise("In-app trigger added", $"{key} = {value}", LabEventKind.Success);
            SimulateLifecycle("demo-trigger-" + key);
        }

        public void Refresh() => SnapshotChanged?.Invoke();
        public void Dispose() { }

        private void SetValue(IDictionary<string, string> values, string key, string value, string title)
        {
            values[key] = value;
            Raise(title, $"{key} = {value}", LabEventKind.Success);
            SnapshotChanged?.Invoke();
        }

        private void RemoveValue(IDictionary<string, string> values, string key, string title)
        {
            values.Remove(key);
            Raise(title, key, LabEventKind.Warning);
            SnapshotChanged?.Invoke();
        }

        // The demo runtime has no presenter, so it reports the four phases a real
        // timed message walks through. The event list is what a host integrating
        // Lifecycle actually sees, so the simulation has to produce the same lines.
        private void SimulateLifecycle(string messageId)
        {
            foreach (var phase in new[] { "willDisplay", "didDisplay", "willDismiss", "didDismiss" })
                Raise("In-app lifecycle", $"{messageId} · {phase}");
        }

        private void Raise(string title, string detail, LabEventKind kind = LabEventKind.Info)
            => EventRaised?.Invoke(new LabEvent(title, detail, kind));
    }

    internal sealed class OpenPushFeatureLabRuntime : IFeatureLabRuntime
    {
        private readonly LabSnapshot _snapshot = new LabSnapshot();

        public OpenPushFeatureLabRuntime()
        {
            OpenPush.OnLog = note => Raise("SDK log", note);
            OpenPush.OnRegistered = () => { Raise("Device registered", "OpenPush registered this device with its push token.", LabEventKind.Success); Refresh(); };
            OpenPush.OnRegistrationFailed = error => Raise("Registration failed", error, LabEventKind.Error);
            OpenPush.Notifications.Clicked += OnClicked;
            OpenPush.Notifications.ForegroundWillDisplay += OnForeground;
            OpenPush.Notifications.PermissionChanged += OnPermissionChanged;
            OpenPush.PushSubscription.Changed += OnSubscriptionChanged;
            OpenPush.InAppMessages.Lifecycle += OnInAppLifecycle;
            Refresh();
        }

        public event Action<LabEvent> EventRaised;
        public event Action SnapshotChanged;
        public LabSnapshot Snapshot => _snapshot;

        public void Initialize(string appId, string sdkKey)
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(sdkKey))
            {
                Raise("Live mode unavailable", "Add an App ID and public SDK key in Feature Lab settings.", LabEventKind.Warning);
                return;
            }

            OpenPush.Initialize(appId.Trim(), sdkKey.Trim());
            Refresh();
            Raise("SDK initialized", "Live calls now use the configured OpenPush app.", LabEventKind.Success);
        }

        public void RequestPermission()
            => OpenPush.Notifications.RequestPermission(true, granted =>
            {
                _snapshot.PermissionKnown = true;
                Raise("Permission request", granted ? "Allowed" : "Denied", granted ? LabEventKind.Success : LabEventKind.Warning);
                Refresh();
            });

        public void SetPushEnabled(bool enabled)
        {
            if (enabled) OpenPush.PushSubscription.OptIn();
            else OpenPush.PushSubscription.OptOut();
            Refresh();
        }

        public void Login(string externalId)
            => OpenPush.Login(externalId, done: result =>
            {
                Raise("Login result", IdentityDetail(result), result.Success ? LabEventKind.Success : LabEventKind.Warning);
                Refresh();
            });

        public void Logout()
            => OpenPush.Logout(result =>
            {
                Raise("Logout result", IdentityDetail(result), result.Success ? LabEventKind.Success : LabEventKind.Warning);
                Refresh();
            });

        public void AddTag(string key, string value) { OpenPush.User.AddTag(key, value); Refresh(); }
        public void RemoveTag(string key) { OpenPush.User.RemoveTag(key); Refresh(); }

        public void AddAlias(string label, string value)
        {
            OpenPush.User.AddAlias(label, value);
            _snapshot.Aliases[label] = value;
            Raise("Alias write sent", $"{label} = {value}");
            SnapshotChanged?.Invoke();
        }

        public void RemoveAlias(string label)
        {
            OpenPush.User.RemoveAlias(label);
            _snapshot.Aliases.Remove(label);
            Raise("Alias removal sent", label);
            SnapshotChanged?.Invoke();
        }

        public void PreviewNotification(NotificationDraft draft, bool suppressPresentation)
        {
            var data = Payload(draft);
            if (suppressPresentation) data["op_silent"] = "true";
            var prevented = OpenPush.Notifications.ProcessForeground(data);
            Raise("Host fixture forwarded", prevented ? "SDK listener requested suppression." : "Host remains responsible for native presentation.", LabEventKind.Success);
        }

        public void PreviewClick(NotificationDraft draft) => OpenPush.Notifications.ProcessClick(Payload(draft));
        public void PreviewPermissionEvent() => OnPermissionChanged(OpenPush.Notifications.Permission);
        public void PreviewSubscriptionEvent() => OnSubscriptionChanged(OpenPush.User.PushSubscription.Snapshot());

        public void HandleAndroidLiveUpdate(IReadOnlyDictionary<string, string> payload)
        {
            var result = OpenPush.LiveUpdates.HandleAndroidPayload(payload, "feature-lab");
            Raise("Android Live Update", result.ToString(), result == OpenPushLiveUpdateResult.Displayed ? LabEventKind.Success : LabEventKind.Warning);
            Refresh();
        }

        public void FetchInAppMessages()
        {
            OpenPush.InAppMessages.Fetch();
            Raise("In-app fetch requested", "The SDK fetches at most once every 30 seconds.");
        }

        public void ResetInAppDisplayState()
            => Raise("In-app display state", "Reset is not available: display state lives in the native SDK.", LabEventKind.Warning);

        public void AddInAppTrigger(string key, string value)
        {
            OpenPush.InAppMessages.AddTrigger(key, value);
            Raise("In-app trigger added", $"{key} = {value}", LabEventKind.Success);
        }

        public void OpenAndroidLiveUpdateSettings()
        {
            var opened = OpenPush.LiveUpdates.OpenPromotionSettings();
            Raise("Promotion settings", opened ? "Opened" : "Unavailable on this platform", opened ? LabEventKind.Success : LabEventKind.Warning);
        }

        public void Refresh()
        {
            var subscription = OpenPush.User.PushSubscription.Snapshot();
            _snapshot.IsInitialized = OpenPush.IsInitialized;
            _snapshot.Permission = OpenPush.Notifications.Permission;
            _snapshot.NotificationTypes = OpenPush.Notifications.NotificationTypes;
            _snapshot.OptedIn = subscription.OptedIn;
            _snapshot.SubscriptionId = subscription.Id;
            _snapshot.Token = subscription.Token;
            _snapshot.ExternalId = OpenPush.User.ExternalId;
            _snapshot.SdkVersion = OpenPush.SdkVersion;
            _snapshot.WrapperVersion = OpenPush.WrapperVersion;
            _snapshot.Tags.Clear();
            foreach (var tag in OpenPush.User.GetTags()) _snapshot.Tags[tag.Key] = tag.Value;
            _snapshot.AndroidLiveUpdateCapability = OpenPush.LiveUpdates.Capability.ToString();
            SnapshotChanged?.Invoke();
        }

        public void Dispose()
        {
            OpenPush.Notifications.Clicked -= OnClicked;
            OpenPush.Notifications.ForegroundWillDisplay -= OnForeground;
            OpenPush.Notifications.PermissionChanged -= OnPermissionChanged;
            OpenPush.PushSubscription.Changed -= OnSubscriptionChanged;
            OpenPush.InAppMessages.Lifecycle -= OnInAppLifecycle;
        }

        private void OnClicked(OpenPushClickEvent click)
            => Raise("Click listener", $"Message {click.MessageId ?? "unknown"} opened the app. · launch URL = {click.Url ?? "none"}", LabEventKind.Success);

        private void OnForeground(OpenPushForegroundEvent notification)
        {
            if (notification.Data != null && notification.Data.ContainsKey("op_silent")) notification.PreventDefault();
            Raise("Foreground listener", notification.DefaultPrevented ? "Presentation suppressed" : "Host may present", LabEventKind.Success);
        }

        private void OnPermissionChanged(bool permitted)
        {
            _snapshot.PermissionKnown = true;
            Raise("Permission observer", $"Allowed {permitted} · notification_types {OpenPush.Notifications.NotificationTypes}");
            Refresh();
        }

        private void OnInAppLifecycle(string messageId, string phase)
            => Raise("In-app lifecycle", $"{messageId ?? "unknown"} · {phase}");

        private void OnSubscriptionChanged(PushSubscriptionState state)
        {
            Raise("Subscription observer", $"{state.Id ?? "pending"} · opted in {state.OptedIn}");
            Refresh();
        }

        private static Dictionary<string, string> Payload(NotificationDraft draft)
            => new Dictionary<string, string>
            {
                ["op_message_id"] = "feature-lab-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ["title"] = draft.Title,
                ["description"] = draft.Description,
                [draft.DataKey] = draft.DataValue,
                ["deep_link"] = draft.LaunchUrl
            };

        private static string IdentityDetail(OpenPushIdentityResult result)
            => result.Success ? "Success" : result.IdentityRejectedNote ?? result.Error ?? "Unknown result";

        private void Raise(string title, string detail, LabEventKind kind = LabEventKind.Info)
            => EventRaised?.Invoke(new LabEvent(title, detail, kind));
    }
}
