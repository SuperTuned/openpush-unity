using System;
using System.Collections.Generic;
using OpenPushSDK;
using UnityEngine;

namespace OpenPushFeatureLab
{
    internal sealed class FeatureLabController : IDisposable
    {
        private const string ModeKey = "openpush.featureLab.mode";
        private const string AppearanceKey = "openpush.featureLab.appearance";
        private const string ConsentRequiredKey = "openpush.featureLab.consentRequired";
        private const string ConsentGrantedKey = "openpush.featureLab.consentGranted";
        private const string AppIdKey = "openpush.featureLab.appId";
        private const string SdkKeyKey = "openpush.featureLab.sdkKey";
        private const int EventLimit = 40;

        private IFeatureLabRuntime _runtime;

        public FeatureLabController()
        {
            var settings = Resources.Load<OpenPushSettings>("OpenPushSettings");
            var bundledAppId = settings == null ? string.Empty : settings.AppId;
            var bundledSdkKey = settings == null ? string.Empty : settings.SdkKey;
            AppId = PlayerPrefs.GetString(AppIdKey, bundledAppId ?? string.Empty).Trim();
            SdkKey = PlayerPrefs.GetString(SdkKeyKey, bundledSdkKey ?? string.Empty).Trim();
            Mode = (LabRuntimeMode)PlayerPrefs.GetInt(
                ModeKey, (int)FeatureLabRuntimeDefaults.Mode(AppId, SdkKey));
            Appearance = (LabAppearance)PlayerPrefs.GetInt(AppearanceKey, (int)LabAppearance.System);
            ConsentRequired = PlayerPrefs.GetInt(ConsentRequiredKey, 0) == 1;
            ConsentGranted = PlayerPrefs.GetInt(ConsentGrantedKey, 0) == 1;
            SwitchRuntime(Mode);
        }

        public event Action Changed;
        public LabRuntimeMode Mode { get; private set; }
        public LabAppearance Appearance { get; private set; }
        public bool ConsentRequired { get; private set; }
        public bool ConsentGranted { get; private set; }
        public bool SuppressForegroundPresentation { get; set; }
        public string AppId { get; private set; }
        public string SdkKey { get; private set; }
        public LabSnapshot Snapshot => _runtime.Snapshot;
        public readonly List<LabEvent> Events = new List<LabEvent>();

        public bool LiveConfigured => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(SdkKey);
        public bool ConsentBlocksStartup => Mode == LabRuntimeMode.Live && ConsentRequired && !ConsentGranted;
        public bool LiveActionsBlocked => Mode == LabRuntimeMode.Live &&
                                          (ConsentBlocksStartup || !LiveConfigured || !Snapshot.IsInitialized);

        public void SetMode(LabRuntimeMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            SwitchRuntime(mode);
            Changed?.Invoke();
        }

        public void SetAppearance(LabAppearance appearance)
        {
            Appearance = appearance;
            PlayerPrefs.SetInt(AppearanceKey, (int)appearance);
            Changed?.Invoke();
        }

        public void SaveConfiguration(string appId, string sdkKey)
        {
            AppId = (appId ?? string.Empty).Trim();
            SdkKey = (sdkKey ?? string.Empty).Trim();
            PlayerPrefs.SetString(AppIdKey, AppId);
            PlayerPrefs.SetString(SdkKeyKey, SdkKey);
            if (Mode == LabRuntimeMode.Live && !ConsentBlocksStartup && LiveConfigured) _runtime.Initialize(AppId, SdkKey);
            Changed?.Invoke();
        }

        public void SetConsentRequired(bool required)
        {
            ConsentRequired = required;
            PlayerPrefs.SetInt(ConsentRequiredKey, required ? 1 : 0);
            if (!required && Mode == LabRuntimeMode.Live && LiveConfigured) _runtime.Initialize(AppId, SdkKey);
            Changed?.Invoke();
        }

        public void GrantConsent()
        {
            ConsentGranted = true;
            PlayerPrefs.SetInt(ConsentGrantedKey, 1);
            AddEvent(new LabEvent("Host consent granted", "The host may now initialize OpenPush.", LabEventKind.Success));
            if (Mode == LabRuntimeMode.Live && LiveConfigured) _runtime.Initialize(AppId, SdkKey);
            Changed?.Invoke();
        }

        public void ResetConsent()
        {
            ConsentGranted = false;
            PlayerPrefs.SetInt(ConsentGrantedKey, 0);
            AddEvent(new LabEvent("Saved consent reset", "Relaunch to test pre-initialization waiting state.", LabEventKind.Warning));
            Changed?.Invoke();
        }

        public void RequestPermission() { if (CanRun()) _runtime.RequestPermission(); }
        public void SetPushEnabled(bool enabled) { if (CanRun()) _runtime.SetPushEnabled(enabled); }
        public void Login(string externalId) { if (CanRun() && !string.IsNullOrWhiteSpace(externalId)) _runtime.Login(externalId.Trim()); }
        public void Logout() { if (CanRun()) _runtime.Logout(); }
        public void AddTag(string key, string value) { if (CanRun() && Valid(key, value)) _runtime.AddTag(key.Trim(), value.Trim()); }
        public void RemoveTag(string key) { if (CanRun()) _runtime.RemoveTag(key); }
        public void AddAlias(string key, string value) { if (CanRun() && Valid(key, value)) _runtime.AddAlias(key.Trim(), value.Trim()); }
        public void RemoveAlias(string key) { if (CanRun()) _runtime.RemoveAlias(key); }
        public void PreviewNotification(NotificationDraft draft) { if (CanRun()) _runtime.PreviewNotification(draft, SuppressForegroundPresentation); }
        public void PreviewClick(NotificationDraft draft) { if (CanRun()) _runtime.PreviewClick(draft); }
        public void PreviewPermissionEvent() { if (CanRun()) _runtime.PreviewPermissionEvent(); }
        public void PreviewSubscriptionEvent() { if (CanRun()) _runtime.PreviewSubscriptionEvent(); }
        public void Refresh() { if (CanRun()) _runtime.Refresh(); }
        public void FetchInAppMessages() { if (CanRun()) _runtime.FetchInAppMessages(); }
        public void ResetInAppDisplayState() { if (CanRun()) _runtime.ResetInAppDisplayState(); }
        public void AddInAppTrigger(string key, string value)
        { if (CanRun() && Valid(key, value)) _runtime.AddInAppTrigger(key.Trim(), value.Trim()); }

        public void PreviewPlatformActivity()
        {
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                PreviewIosLiveActivity();
                return;
            }
            PreviewAndroidLiveUpdate();
        }

        public void OpenPlatformActivitySettings()
        {
            if (Application.platform != RuntimePlatform.IPhonePlayer)
                OpenAndroidLiveUpdateSettings();
        }

        private void PreviewAndroidLiveUpdate()
        {
            if (!CanRun()) return;
            _runtime.HandleAndroidLiveUpdate(new Dictionary<string, string>
            {
                ["op_live_update"] = "true",
                ["op_message_id"] = "feature-lab-live-update",
                ["title"] = "Order on the way",
                ["body"] = "Arrives in 12 minutes",
                ["progress"] = "0.72"
            });
        }

        private void PreviewIosLiveActivity()
        {
            if (!CanRun()) return;
            OpenPush.LiveActivities.SetupDefault();
            OpenPush.LiveActivities.StartDefault(
                "feature-lab-order-1234",
                new Dictionary<string, object> { ["title"] = "Order on the way" },
                new Dictionary<string, object>
                {
                    ["status"] = "Arrives in 12 minutes",
                    ["value"] = "12 min"
                });
            AddEvent(new LabEvent(
                "iOS Live Activity",
                "Requested the order fixture from ActivityKit.",
                LabEventKind.Success));
        }

        private void OpenAndroidLiveUpdateSettings() { if (CanRun()) _runtime.OpenAndroidLiveUpdateSettings(); }

        public void AddSyntheticEvent(string kind)
        {
            switch (kind)
            {
                case "foreground": PreviewNotification(DefaultDraft("Simple")); break;
                case "click": PreviewClick(DefaultDraft("Button")); break;
                case "permission": PreviewPermissionEvent(); break;
                default: PreviewSubscriptionEvent(); break;
            }
        }

        public void ClearEvents()
        {
            Events.Clear();
            Changed?.Invoke();
        }

        public static NotificationDraft DefaultDraft(string kind)
        {
            var draft = new NotificationDraft { Kind = kind };
            switch (kind)
            {
                case "Simple":
                    draft.Title = "Welcome to OpenPush";
                    draft.Description = "A simple foreground notification fixture.";
                    break;
                case "Image":
                    draft.Title = "A new feature just landed";
                    draft.Description = "Rich media is rendered by your platform notification integration.";
                    break;
                case "Button":
                    draft.Title = "Your order is ready";
                    draft.Description = "Action buttons are declared by the native host.";
                    draft.DataKey = "action";
                    draft.DataValue = "view_order";
                    break;
                case "Sound":
                    draft.Title = "New message";
                    draft.Description = "Custom sounds are configured in the native notification payload.";
                    draft.DataKey = "sound";
                    draft.DataValue = "openpush_ping";
                    break;
            }
            return draft;
        }

        public void Dispose()
        {
            if (_runtime == null) return;
            _runtime.EventRaised -= AddEvent;
            _runtime.SnapshotChanged -= OnSnapshotChanged;
            _runtime.Dispose();
        }

        private void SwitchRuntime(LabRuntimeMode mode)
        {
            Dispose();
            _runtime = mode == LabRuntimeMode.Live
                ? (IFeatureLabRuntime)new OpenPushFeatureLabRuntime()
                : new DemoFeatureLabRuntime();
            _runtime.EventRaised += AddEvent;
            _runtime.SnapshotChanged += OnSnapshotChanged;
            if (mode == LabRuntimeMode.Demo || (!ConsentBlocksStartup && LiveConfigured)) _runtime.Initialize(AppId, SdkKey);
        }

        private bool CanRun()
        {
            if (!LiveActionsBlocked) return true;
            var reason = !LiveConfigured
                ? "Add an App ID and public SDK key in Feature Lab settings."
                : ConsentBlocksStartup
                    ? "Grant host consent before running Live SDK actions."
                    : "OpenPush did not initialize. Check the event stream and configuration.";
            AddEvent(new LabEvent("SDK waiting", reason, LabEventKind.Warning));
            return false;
        }

        private static bool Valid(string key, string value)
            => !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value);

        private void AddEvent(LabEvent item)
        {
            Events.Insert(0, item);
            if (Events.Count > EventLimit) Events.RemoveRange(EventLimit, Events.Count - EventLimit);
            Changed?.Invoke();
        }

        private void OnSnapshotChanged() => Changed?.Invoke();
    }
}
