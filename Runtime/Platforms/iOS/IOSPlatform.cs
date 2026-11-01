using System;
using System.Collections.Generic;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;
using N = OpenPushSDK.iOS.IOSNative;

namespace OpenPushSDK.iOS
{
    /// <summary>
    /// The iOS platform over the Swift bridge. Synchronous calls return at once; async ones carry a
    /// correlation id that comes back as a "result" event on the main thread.
    /// </summary>
    internal sealed class IOSPlatform : IOpenPushPlatform
    {
        internal readonly OpenPushNativeEventRouter Router;
        internal readonly OpenPushPendingCalls Pending = new OpenPushPendingCalls();

        internal IOSPlatform()
        {
            Router = new OpenPushNativeEventRouter((id, display) => N.openpush_unity_complete_foreground(id, display),
                                                   id => N.openpush_unity_display_foreground(id));
            Router.Result += (id, payload) => Pending.Complete(id, payload);
            Core = new IOSCore(this); User = new IOSUser(); PushSubscription = new IOSPushSubscription(this);
            Notifications = new IOSNotifications(this); InAppMessages = new IOSInAppMessages(this);
            LiveActivities = new IOSLiveActivities(this); LiveUpdates = new IOSLiveUpdates();
            Verify = new IOSVerify(this);
        }

        public string Name => "ios";
        public IOpenPushCore Core { get; }
        public IOpenPushUser User { get; }
        public IOpenPushPushSubscription PushSubscription { get; }
        public IOpenPushNotifications Notifications { get; }
        public IOpenPushInAppMessages InAppMessages { get; }
        public IOpenPushLiveActivities LiveActivities { get; }
        public IOpenPushLiveUpdates LiveUpdates { get; }
        public IOpenPushVerify Verify { get; }

        internal long Expect(Action<Dictionary<string, object>> onResult) => Pending.Register(onResult);
        private static bool Ok(Dictionary<string, object> p, string key) => OpenPushWire.Bool(p, key);

        /// <summary>The bridge names the wrapper "unity" itself and takes only the version.</summary>
        internal static string WrapperVersion(string wrapper)
        {
            if (string.IsNullOrEmpty(wrapper)) return OpenPushVersion.Current;
            var slash = wrapper.IndexOf('/');
            return slash >= 0 ? wrapper.Substring(slash + 1) : wrapper;
        }

        private sealed class IOSCore : IOpenPushCore
        {
            private readonly IOSPlatform _p;
            internal IOSCore(IOSPlatform p) { _p = p; }
            public bool IsInitialized => N.openpush_unity_is_initialized();
            public string CurrentToken => N.openpush_unity_current_token();
            public string SdkVersion => N.openpush_unity_sdk_version();

            public void Initialize(OpenPushInitOptions o)
            {
                // The callback is registered first so events the bridge queued (a cold-start tap, an early
                // token) replay into the router before initialize emits anything new.
                // A re-Initialize starts a new native session; results still pending from the previous
                // one can never arrive, so fail them rather than leak their callbacks.
                _p.Pending.FailAll(OpenPushPendingCalls.Superseded());
                IOSEventCallback.Router = _p.Router;
                N.openpush_unity_set_event_callback(IOSEventCallback.Callback);
                N.openpush_unity_initialize(o.AppId, o.SdkKey, o.ServerUrl, o.AppGroup ?? "", WrapperVersion(o.Wrapper),
                                            o.Credential?.Invoke(), o.Provisional);
                if (!string.IsNullOrEmpty(o.AppGroup)) N.openpush_unity_remove_legacy_group_keys(o.AppGroup);
            }

            public void UpdateCredential(string credential) => N.openpush_unity_set_credential(credential);
            public void Register(string token, string externalId) => N.openpush_unity_register(token, externalId);
            public void Receipt(string type, string messageId, Action then)
                => N.openpush_unity_receipt(type, messageId, _p.Expect(_ => then?.Invoke()));
            public void TrackEvent(string name, string propertiesJson) => N.openpush_unity_track_event(name, propertiesJson);
            public void Login(string externalId, string authHash, Action<OpenPushIdentityResult> done)
                => N.openpush_unity_login(externalId, authHash, _p.Expect(p => done?.Invoke(IdentityOf(p))));
            public void Logout(Action<OpenPushIdentityResult> done)
                => N.openpush_unity_logout(_p.Expect(p => done?.Invoke(IdentityOf(p))));
            public void OnAppForegrounded() => N.openpush_unity_on_app_foregrounded();
            public void ClearToken() => _p.Router.RaiseLog(OpenPushLogLevel.Warning, "⚠ ClearToken is not supported by the iOS SDK; ignored.");

            private static OpenPushIdentityResult IdentityOf(Dictionary<string, object> p)
                => IdentityResultWire.From(p)?.ToPublic() ?? OpenPushIdentityResult.Failed(null);

            public event Action<OpenPushLogLevel, string> Log { add => _p.Router.Log += value; remove => _p.Router.Log -= value; }
            public event Action Registered { add => _p.Router.Registered += value; remove => _p.Router.Registered -= value; }
            public event Action<string> RegistrationFailed { add => _p.Router.RegistrationFailed += value; remove => _p.Router.RegistrationFailed -= value; }
            public event Action<string> IdentityVerificationRequired { add => _p.Router.IdentityVerificationRequired += value; remove => _p.Router.IdentityVerificationRequired -= value; }
        }

        private sealed class IOSUser : IOpenPushUser
        {
            public void AddTags(IReadOnlyDictionary<string, string> tags) => N.openpush_unity_add_tags(OpenPushWire.StringMapToJson(tags));
            public void RemoveTags(IReadOnlyList<string> keys) => N.openpush_unity_remove_tags(OpenPushWire.ToJson(keys));
            public Dictionary<string, string> GetTags()
                => new Dictionary<string, string>(OpenPushWire.StringMap(OpenPushWire.Parse("{\"t\":" + (N.openpush_unity_get_tags() ?? "{}") + "}"), "t"));
            public void AddAliases(IReadOnlyDictionary<string, string> aliases) => N.openpush_unity_add_aliases(OpenPushWire.StringMapToJson(aliases));
            public void RemoveAliases(IReadOnlyList<string> labels) => N.openpush_unity_remove_aliases(OpenPushWire.ToJson(labels));
            public string ExternalId => N.openpush_unity_external_id();
            public void AddSms(string number) => N.openpush_unity_add_sms(number);
            public void RemoveSms(string number) => N.openpush_unity_remove_sms(number);
            public void AddWhatsApp(string number) => N.openpush_unity_add_whatsapp(number);
            public void RemoveWhatsApp(string number) => N.openpush_unity_remove_whatsapp(number);
            public void AddEmail(string address) => N.openpush_unity_add_email(address);
            public void RemoveEmail(string address) => N.openpush_unity_remove_email(address);
        }

        private sealed class IOSPushSubscription : IOpenPushPushSubscription
        {
            private readonly IOSPlatform _p;
            internal IOSPushSubscription(IOSPlatform p) { _p = p; }
            private static PushSubscriptionWire Snapshot()
                => PushSubscriptionWire.From(OpenPushWire.Parse(N.openpush_unity_subscription())) ?? new PushSubscriptionWire();
            public string Id => Snapshot().id;
            public string Token => Snapshot().token;
            public bool OptedIn => Snapshot().opted_in;
            public int? Status => Snapshot().status;
            public void OptIn() => N.openpush_unity_opt_in();
            public void OptOut() => N.openpush_unity_opt_out();
            public event Action<PushSubscriptionState, PushSubscriptionState> Changed
            { add => _p.Router.SubscriptionChanged += value; remove => _p.Router.SubscriptionChanged -= value; }
        }

        private sealed class IOSNotifications : IOpenPushNotifications
        {
            private readonly IOSPlatform _p;
            internal IOSNotifications(IOSPlatform p) { _p = p; }
            public bool Permission => N.openpush_unity_permission();
            public bool CanRequestPermission => N.openpush_unity_can_request_permission();
            public void RequestPermission(bool fallbackToSettings, Action<bool> done)
                => N.openpush_unity_request_permission(fallbackToSettings, _p.Expect(p => done?.Invoke(Ok(p, "granted"))));
            public void RequestProvisionalPermission(Action<bool> done)
                => N.openpush_unity_request_provisional_permission(_p.Expect(p => done?.Invoke(Ok(p, "granted"))));
            public void ClearAll() => N.openpush_unity_clear_all();
            public void Remove(string messageId) => N._openpush_remove_notification(messageId);
            public void SetBadgeCount(int count) => N.openpush_unity_set_badge_count(count);
            // Host-forwarded payloads (see the IOpenPushNotifications docs): clicked receipt + one click
            // event; received receipt only; confirmed receipt only.
            public void ProcessClick(string dataJson) => N.openpush_unity_process_click_data(dataJson);
            public void ProcessForeground(string dataJson) => N.openpush_unity_process_foreground_data(dataJson);
            // iOS draws the alert itself and a resent copy collapses onto the first, so a forwarded
            // foreground payload is never claimed; repeat taps are dropped natively.
            public bool BeginReceived(string dataJson) => true;
            public void FinishReceived(string dataJson, bool handled) { }
            public void ProcessDisplayed(string dataJson) => N.openpush_unity_process_displayed_data(dataJson);
            public event Action<bool> PermissionChanged { add => _p.Router.PermissionChanged += value; remove => _p.Router.PermissionChanged -= value; }
            public event Action<OpenPushClickEvent> Clicked { add => _p.Router.Clicked += value; remove => _p.Router.Clicked -= value; }
            public event Action<OpenPushForegroundEvent> ForegroundWillDisplay
            { add => _p.Router.ForegroundWillDisplay += value; remove => _p.Router.ForegroundWillDisplay -= value; }
            // iOS background pushes use content-available and reach the native SDK only for their
            // receipt; the bridge does not surface them, so this never fires on iOS.
            public event Action<OpenPushBackgroundMessage> BackgroundMessage { add { } remove { } }
        }

        private sealed class IOSInAppMessages : IOpenPushInAppMessages
        {
            private readonly IOSPlatform _p;
            internal IOSInAppMessages(IOSPlatform p) { _p = p; }
            public bool Paused { get => N.openpush_unity_iam_paused(); set => N.openpush_unity_iam_set_paused(value); }
            public void AddTriggers(string triggersJson) => N.openpush_unity_iam_add_triggers(triggersJson);
            public void RemoveTriggers(IReadOnlyList<string> keys) => N.openpush_unity_iam_remove_triggers(OpenPushWire.ToJson(keys));
            public void ClearTriggers() => N.openpush_unity_iam_clear_triggers();
            public void Fetch() => N.openpush_unity_iam_fetch();
            public event Action<string, string> Lifecycle { add => _p.Router.InAppLifecycle += value; remove => _p.Router.InAppLifecycle -= value; }
            public event Action<string, OpenPushInAppClick> Clicked { add => _p.Router.InAppClicked += value; remove => _p.Router.InAppClicked -= value; }
        }

        private sealed class IOSLiveActivities : IOpenPushLiveActivities
        {
            private readonly IOSPlatform _p;
            internal IOSLiveActivities(IOSPlatform p) { _p = p; }
            public void SetupDefault(LiveActivitySetupOptions options)
                => N.openpush_unity_la_setup_default(options?.EnablePushToStart ?? true, options?.EnablePushToUpdate ?? true);
            public void StartDefault(string activityId, string attributesJson, string contentJson) => N.openpush_unity_la_start_default(activityId, attributesJson, contentJson);
            public void Enter(string activityId, string token, string activityType, Action<bool> done)
                => N.openpush_unity_la_enter(activityId, token, activityType ?? "", _p.Expect(p => done?.Invoke(Ok(p, "ok"))));
            public void Exit(string activityId, Action<bool> done) => N.openpush_unity_la_exit(activityId, _p.Expect(p => done?.Invoke(Ok(p, "ok"))));
            public void SetPushToStartToken(string activityType, string token, Action<bool> done)
                => N.openpush_unity_la_set_push_to_start_token(activityType, token, _p.Expect(p => done?.Invoke(Ok(p, "ok"))));
            public void RemovePushToStartToken(string activityType, Action<bool> done)
                => N.openpush_unity_la_remove_push_to_start_token(activityType, _p.Expect(p => done?.Invoke(Ok(p, "ok"))));
        }

        private sealed class IOSVerify : IOpenPushVerify
        {
            private readonly IOSPlatform _p;
            internal IOSVerify(IOSPlatform p) { _p = p; }
            public void Start(string phone, OpenPushVerifyChannel channel, string locale, Action<OpenPushVerifyResult> done)
                => N.openpush_unity_verify_start(phone, OpenPushVerifyWire.Channel(channel), locale, Answer(done));
            public void Check(string verificationId, string code, Action<OpenPushVerifyResult> done)
                => N.openpush_unity_verify_check(verificationId, code, Answer(done));
            public void Resend(string verificationId, OpenPushVerifyChannel channel, Action<OpenPushVerifyResult> done)
                => N.openpush_unity_verify_resend(verificationId, OpenPushVerifyWire.Channel(channel), Answer(done));
            private long Answer(Action<OpenPushVerifyResult> done) => _p.Expect(p => done?.Invoke(OpenPushVerifyWire.Read(p)));
        }

        private sealed class IOSLiveUpdates : IOpenPushLiveUpdates
        {
            public OpenPushLiveUpdateResult HandleAndroidPayload(string dataJson, string collapseId) => OpenPushLiveUpdateResult.Unsupported;
            public OpenPushLiveUpdateCapability Capability => OpenPushLiveUpdateCapability.UnsupportedPlatform;
            public bool OpenPromotionSettings() => false;
        }
    }
}
